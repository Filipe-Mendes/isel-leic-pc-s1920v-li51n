# Aula 23 - _Tasks_(II)

___

## Sumário

- Revisão sobre as relações entre _tasks_ abordadas na aula anterior.

- Tipo `TaskCompletionSource<T>` e respectiva utilização ("_everything can be a task_ !"); exemplo de utilização na implementação de um método para executar assincronamente um _delay_.

- Modelo de invocação assíncrona _Task-based Asynchronous Pattern_ (TAP). Implementação de interfaces assíncrons segundo o modelo TAP com base em interfaces segundo o modelo APM.
	
### _Everything is a Task_
	
- Atrás dissemos que uma `Task` representa uma peça de actividade assíncrona. Esta actividade assíncrona pode ser computação mas também pode ser de I/O. Um exemplo de uma _task_ que não é de computação é o resultado da transformação de uma implementação da interface `IAsyncResult` e do respectivo _end method_ de uma operação assíncrona APM numa _task_ (estilo TAP) utilizando o método `TaskFactory.FromAsync`.

#### `TaskCompletionSource<TResult>`
	
- O tipo `TaskCompletionSource<T>` tem duas responsabilidades: uma é produzir um objecto do tipo `Task<T>`; a outra é providenciar um conjunto de métodos para controlar o resultado daquela _task_. Na aula anterior dissemos que uma _task_ pode terminar num de três estados (`RanToCompletion`, `Canceled`, `Faulted`). A seguir mostramos um subconjunto dos métodos a classe `TaskCompletionSource<TResult>`.
	
```C#
public class TaskCompletionSource<TResult> {
	...
	
	public Task<T> Task { get; }
	
	// unconditional set methods
	public void SetResult(TResult result);
	public void SetException(Exception exception);
	public void SetCanceled();
	
	// conditional set methods - do nothing if the TCS is already in a terminated state
	public bool TrySetResult(TResult result);
	public bool TrySetException(Exception exception);
	public bool TrySetException(IEnmerable<Exception> exceptions);
	public bool TrySetCancelled();
	public bool TrySetCancelled(CancellationToken ctoken);
	...
}

```

- Atrás vimos uma API semelhante a esta: `CancellationTokenSource` para controlar o processo de cancelamento. Uma instância de `TaskCompletionSource` é usada em código que que pretende controlar o resultado da instância do tipo `Task` sob controlo de uma instância de `TaskCompletionSource`. Um objecto `TaskCompletionSource` expõe um objecto `Task` por intermédio da sua propriedade `Task`; essa `Task` será passada para o código que pretende observar o respectivo resultado. O argumento tipo de `TaskCompletionSource<TResult>` é usado para indicar o tipo do resultado da `Task`. Se deseja produzir uma `Task` em vez de uma `Task<TResult>`, a _Microsoft_ aconselha usar uma `TaskCompletionSource<object>`, uma vez que `Task<object>` estende `Task` pelo que pode ser sempre tratado como apenas `Task`.

- O próximo excerto de código mostra um exemplo simples de produzir uma `Task<int>` via a `TaskCompletionSource<int>`. Considera-se que a _task_ não será concluída até que seja pressionada a tecla `Enter` e o resultado da _task_ será definido com a chamada ao método `SetResult`.
	
```C#
var tcs = new TaskCompletionSource<int>();

Task<int> syntheticTask = tcs.Task; 

syntheticTask.ContinueWith(ascendent => Console.WriteLine($"Result: {ascendent.Result}"));

Console.Write("-- press <enter> to complete the synthetic task");
Console.ReadLine();

tcs.SetResult(42);
Console.ReadLine();
```

- Outro exemplo de utilização de utilização do tipo `TaskCompletionSource` é a implementação do método `DelayAsync`, cujo propósito é implementar a funcionalidade _delay_ que possa ser invocada de forma assíncrona. Usando a classe `System.Threading.Timer`, apresenta-se a seguir uma implementação simplificada de um método que implemente esta funcionalidade.

```C#
public static Task DelayAsync(int millisDelay) {
	TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
	new System.Threading.Timer((_) => tcs.SetResult(null), null, millisDelay, Timeout.Infinite);
	return tcs.Task;
}
```

- Associa-se ao _delay_ assíncrono uma instância de `TaskCompletionSource<object>`. Depois lança-se um _timer_ para disparar quando terminar o _delay_; no _callback_ do _timer_ usa-se o método `SetResult` para terminar a `Task` que fica associada à operação de _delay_ assíncrono que é obtida com a propriedade `TaskCompletionSource<TResult>.Task`.  

- Em cenários onde um _delay_ assíncrona seja usado para definir um _timeout_ numa operação assíncrona pode ser interessante suportar o cancelamento do _delay_ o que pode ser feito usando o método cuja implementação se mostra a seguir.
  
```C#
public static Task DelayAsync(int millisDelay, CancellationToken ctoken) {
	TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();	
	Timer timer = null;
	CancellationTokenRegistration ctokenRegistration;
		
	// if the cancellation token can be canceled, register a cancellation handler with it
	if (ctoken.CanBeCanceled) {
		ctokenRegistration = ctoken.Register(() => {
			if (tcs.TrySetCanceled(ctoken))
				timer?.Dispose();
		});
	} else
		ctokenRegistration = default(CancellationTokenRegistration);
		
	// start the timer
	timer = new Timer((_) => {
				if (tcs.TrySetResult(null)) {
					if (ctoken.CanBeCanceled)
						ctokenRegistration.Dispose();
				}
			}, null, millisDelay, Timeout.Infinite);
	return tcs.Task;
}
```

- Se quisermos fazer um _cleanup_ adequeado, na difinição do _timer callback_ e do _cancellation handler_ temos dependências mútuas, isto é: o _timer callback_ depende da `CancellationTokenRegistration` patra desactivar o _cancellation handler_ e o _cancellation handler_ necessita da referência para o `Timer`para desactivar o _timer_. A solução é providenciar uma iniciação por omissão para as duas variáveis antes da respectiva utilização. Se o _cancellation token_ já estiver no estado cancelado quando se chama o método `CancellationToken.Register` o _callback_ especificado é executado sincronamente, o que não levanta nenhum problema uma vez que o _timer_ ainda não foi criado e portanto é ignorado. Quando o _cancellation token_ transita para o estado cancelado após o registo do _cancellation handler_, este é executado no contexto da _thread_ que invoca o método `Cancel` na respectiva instância de `CancellationTokenSource`. Nesta situação existe uma _race condition_ entre a afectação da variável `timer` no método `DelayAsync` e o teste da mesma variável no _cancellation handler_. Esta _race condition_ é benigna, na medida em que pior que pode acontecer é que o _cancellation handler_ ser executado quando o _timer_ já foi activado mas a variável `timer` ainda não foi afectada; nesta situação, o _timer_ não será cancelado e acabará por executar o respectivo _callback_ que verificará que a `Task` associada à instância de `TaskCompletionSource` já foi concluída, pois o método `TrySetCanceled` retornará `false`.

#### Demo

- No ficheiro [delay.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/delay.cs) consta a definição dos dois _overloads_ do método `DelayAsync` e a respectiva utilização.

### _Task-based Asynchronous Pattern_ (TAP)

- Em aulas anteriores abordámos um dos modelos de invocacação assíncrona definidos pelo .NET _Framework_, designado _Asynchronous Programming Model_ (APM). Neste modelo a uma operação cuja API síncrona seja:

```C#
T Xxx(U u, ..., V v);
```

- Corresponde uma API assícrona definida pelos seguintes dois métodos:

```C#
IAsyncResult BeginXxx(U u, ..., V v, AsyncCallback completionCallback, object callbackState);

T EndXxx(IAsyncResult asyncResult);
```

- O método `BeginXxx` lança a operação assícrona e devolve um objecto que implementa a interface `IAsyncResult` que representa a operação assíncrona. O método `EndXxx` permite obter o resultado da operação assíncrona representada pelo valor do argumento `asyncResult`.

- O _rendezvous_ com a conclusão da operação assíncrona pode ser feito usando técnicas de _polling_ (usando a interface `IAsyncResult`) ou usando a técnica de _callback_ especificando um `completionCallback` na chamada ao método `BeginXxx`.

- Com a introdução da _Task Parallel Library_ (TPL), o .NET _Framework_ definiu um novo modelo de invocação assíncrona designado por _Task-based Asynchronous Pattern_ (TAP), que como o nome indica usa _tasks_ para representar as operações assíncronas. A API segundo o modelo TAP para a API síncrona anterior é:

```C#
Task<T> XxxAsync(U u, ..., V v);
```

- Existe apenas um método que recebe o nome do método síncrono correspondente com o sufixo `Async`. Este método em vez de devolver o resultado da operação assíncrona devolve uma instância de `Task<TResult>` que representa a operção assícrona em curso.

- O _rendezvous_ com a conclusão da operação assíncrona pode ser feita usando técnicas de _polling_ (usando a propriedade `Task.IsCompleted` ou `Task.Result` ou os métodos `Task.Wait`, `Task.WaitAll` ou `Task.WaitAny`) ou usando a técnica de _callback_ através do agendamento de continuação na _task_ que representa a operação assíncrona (usando os métodos `Task.ContinueWith`, `TaskFactory.ContinueWhenAny` ou `TaskFactory.ContinueWhenAny`).

- O modelo TAP é mais simples de usar do que o modelo APM pois não tem o problema da chamada síncrona ao _completion callback_ que pode ocorrer aquando da chamada ao método `BeginXxx`.

- Usando o tipo `TaskCompletionSource<TResult>` é simples implementar uma interface ao modelo TAP quando se dispões de uma interface segundo o modelo APM. Usando os método `BeginXxx` e `EndXxx`, definidos anteriormente, a implementação do método `XxxAsync` será:

```C#

// TAP asynchronous API
Task<T> XxxAsync(U u, ..., V v) {
	TaskCompletionSource<T> tcs = new TaskCompletionSource<T>;
		
	BeginXxx(u, ...,v, (asyncResult) => {
		try {
			tcs.SetResult(EndXxx(asyncResult));
		} catch (Exception ex) {
			tcs.SetException(ex);
		}
	}, null);
	
	return tcs.Task;
}
```

- O tipo `TaskFactory` define o método `TaskFactory.FromAsync` que permite implementar interfaces segundo o estilo TAP com base em interfaces segundo o estilo APM.

___
	
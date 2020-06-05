# Aula 20 - _Tasks_ (I)

___

## Sumário

- Conceito de _task_  como infraestrutura adequada para a concepção de aplicações baseadas em programação assíncrona.

- Suporte para o conceito de _task_ no .NET _Framework_ e no _Java_: _Task Parallel Library_ (TPL) e classe `CompletableFuture`, respectivamente.

- Utilização de _tasks_ usando a TPL: criação de _tasks_; passagem de argumentos para as tasks; retorno de valores das _tasks_ (`Task<T>`); estados de uma _task_ e tratamento de erros.

- .NET _Cancellation Framework_: classes `CancellationTokenSource`, `CancellationToken` e `OperationCanceledException`. Funcionalidades de TPL relativas a: cancelamento de _tasks_; Reportar progresso a partir das _tasks_ (interface `IProgress<T>` e classe `Progress<T>`).
	
- Relações entre _tasks_: continuações e relação parental entre _tasks_ (_parent/child_); padrão _fork/join_ usando continuações.

## Conceito de _Task_

- Uma **_task_ representa uma unidade assíncrona discreta de trabalho**, ou seja, representa uma actividade autónoma em curso ou já terminada.

###.NET _Framework Task Parallel Library_ (TPL)

- O suporte para o conceito de _Task_ no .NET _Framework_ faz parte do componente que se designa por _Task Parallel Library_ (TPL) cuja funcionalidade está defina no _namespace_ `System.Threading.Tasks`.

- Os principais tipos definidos pela TPL são:

	- `Task`, `Task<TResult>`, `TaskCompletionSource<TResult>`, `TaskCreationOptions` e `TaskStatus`

	- `TaskFactory` e `TaskFactory<TResult>`
	
	- `TaskCanceledException`

	- `TaskScheduler`, ConcurrentExclusiveSchedulerPair e `TaskSchedulerException`

### Estados de uma _Task_

- Uma _task_ pode estar num dos seguintes estados (defindos pelo tipo enumerado `TaskStatus`):

	- `Created`: a _task_ foi iniciada mas ainda não foi submetida ao _scheduler_ para execução;
	
	- `WaitingForActivation`: a _task_ aguarda ser activada e submetida ao _scheduler_ para execução pela infraestrutura interna do .NET _Framework_ (estado em que ficam as _tasks_ que representam as continuações associdas a uma _task_ que ainda não terminou);
	
	- `WaitingToRun`: a _task_ foi submetida ao _scheduler_ para execução, mas a sua execução ainda não se iniciou;
	
	- `Running`: a _tasks_ está em execução, mas ainda não terminou;
	
	- `WaitingForChildrenToComplete`: a _task_ terminou a sua execução e está implicitamente a aguardar a conclusão de todas as  _tasks_ filhas que lhe foram associadas (craidas com a opção `TaskCreationOptions.AttatchedToParent`);
	
	- `RanToCompletion`: a _task_ completou a execução com sucesso;
	
	- `Canceled`: a _task_ reconheceu o cancelamento lançando uma `OperationCanceledException` com o `CancellationToken` especificado na crição quando o _token_ ficou no estado sinalizado, ou então o `CancellationToken` já estava sinalizado antes da _task_ começar a ser executada;
	
	- `Faulted`: a _task_ terminou devido ao lançamento de uma excepção não-tratada.
	
	
### Criação de _Tasks_ e Passagem de Dados para as _Tasks_

- Instâncias do tipo `Task` ou `Task<TResult>` podem ser criadas com o método `TaskFactory.StartNew` ou com o método `Task.Run`. A classe `Task` define a propriedade estática `Factory` que devolve uma instância do tipo `TaskFactory` com a configuração por omissão.
	
- Para passar dados directamente para uma nova _task_ tem que se utilizar o método de fabrico `TaskFactory.StartNew`, pois não existe nenhum _overload_ do método `Task.Run` que o permita fazer. Também é obviamente possível passar dados para uma _task_ através da captura de estado via _closures_.

- É necessário ter em atenção que a captura de estado através de _closures_ pode não funcionar correctamente quando se usa invocação assíncrona. Por exemplo, considere o seguinte código:

```C#
for (int i = 0; i < 10; i++) {
	Task.Run(() => Console.WriteLine(i));
}
Console.ReadLine();
```

- A intenção deste código seria imprimir os número de 0 a 9, não necessariamente por ordem crescente porque não existe controlo sobre a ordem com que as _tasks_ são submetidas para execução pelo _Default Task Scheduler_. Contudo se executarmos este código possivelmente veremos a impressão de dez 10s na consola.

- A causa para este resultado está relacionada com a _closure_; o compilador terá que capturar a variável local `i` e colocá-la num objecto gerado pelo compilador que fica armazenado no _heap_, de modo a que possa ser referenciado dentro de cada um dos dez _lambdas_. A questão importante aqui é saber quando é que esse objecto é criado? Como a variável `i` é declarada fora do corpo do ciclo, o ponto de captura é, por isso, também fora do corpo do ciclo. Isto resulta em que, neste caso, é apenas criado um único objecto para armazenar o valor de `i`, e este único objecto é usado para armazenar cada incremento do `i`. Como cada _task_ irá partilhar o  mesmo objecto _closure_, no momento em que a primeira _task_ executa a _thread_ principal já completou o ciclo, e assim `i` tem o valor 10. Por isso, todas as dez _tasks_ que foram criadas imprimirão o mesmo valor de `i`, ou seja 10.

- Para resolver este problema, necessário alterar o ponto de captura. O compilador apenas captura as variáveis que são utilizadas pelo _lambda_ e faz captura com base no âmbito da declaração da variável. Por isso, em vez de capturar `i`, que é definida uma única vez durante o tempo de vida do ciclo, necessitamos de introduzir uma nova variável local, definida dentro do corpo do ciclo e usar esta variável dentro da _lamdba_. Em consequência, a variável é capturada separadamente por cada iteração do ciclo. O código será:

```C#
for (int i = 0; i < 10; i++) {
	int capturedI = i;
	Task.Run(() => Console.WriteLine(capturedI));
}
Console.ReadLine();
```

- Executando este código verificamos que agora são mostrados na consola os número 0 a 9, por uma qualquer ordem que depende da forma como as _tasks_ são submetidas ao _scheduler_ para execução.

#### Demo

- No ficheiro [tasks.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/tree/master/src/tasks) está um programa de demonstração que ilustra as várias formas de criar _tasks_ assim como as duas forma de passar dados a uma nova _task_ (através de uma argumento do método `TaskFactory.StartNew` e através da captura de estado pela _closure_).


### Retornando Dados de uma _Task_

- Já se perguntou quais são as chances de ganhar na lotaria com 49.000 números possíveis, dos quais precisa de escolher os 600 selecionados numa noite? Seguramente já saberá que as suas hipóteses de ganhar são reduzidas, mas a seguir apresenta-se a implementação parcial do código que permite calcular as chances.

```C#
BigInteger n = 49000;
BigInteger r = 600;

BigInteger part1 = Factorial(n);
BigInteger part2 = Factorial(n - r);
BigInteger part3 = Factorial(r);

BigInteger chances = part1 / (part2 * part3);
Console.WriteLine(chances);
```

- Executando este código sequencialmente será utilizado apenas um _core_ de processamento; contudo,considerando que os cálculos de `part1`, `part2` e `part3` são independentes uns dos outros, poderemos potencialmente acelerar o cálculo se calcularmos as diferentes partes em _tasks_ separadas. Quando os resultados estiverem disponíveis, fazemos a as operações de multiplicação e divisão - a TPL é adequada para este tipo de problema, como se ilustra com o código mostrado a seguir.

```C#
BigInteger n = 49000;
BigInteger r = 600;

Task<BigInteger> part1 = Task.Run(() => Factorial(n));
Task<BigInteger> part2 = Task.Run(() => Factorial(n - r));
Task<BigInteger> part3 = Task.Run(() => Factorial(r));

BigInteger chances = part1.Result / (part2.Result * part3.Result);
Console.WriteLine(chances);
````

- Este código uma uma forma diferente do método `Task.Run`:

```C#
public Task<TResult> Run<TResult>(Func<TResult> function);
```

- O argumento genérico `TResult` identifica o tipo de resultado que a _task_ irá retornar. Para que a _task_ seja capaz de de retornar um resultado deste tipo, a assinatura do _delegate_ que representa o corpo da _task_ tem que ser do tipo `Func<TResult`. Além disso, o método `Task.Run` agora retorna não uma instância do tipo `Task` mas uma instância do tipos `Task<TResult>`. Este tipo tem uma propriedade adicional chamada `Result`, que é usada para obter o resultado da operação assíncrona. Esta propriedade pode apenas dar o resultado depois da operação assíncrona ter sido concluída; assim, se a propriedade `Result` for acedida antes da operação assíncrona estar concluída, a _thread_ invocante é bloqueada até que o resultado esteja disponível.

#### Demo	

- No ficheiro [returning-data.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/returning-data.cs) está um programa de demonstração que faz o cálculo das chances da lotaria de forma síncrona e de forma assíncrona medindo o tempo de cálculo nos dois casos.
	 
### Tratamento dos Erros 

- As chamadas aos métodos .NET regulares são chamadas síncronas. Uma chamada a um método pode produzir um resultado válido ou uma excepção e isso também deve o comportamento quando usam chamadas assíncronas com base em _tasks_. Quando uma _task_ é concluída, ela pode completar num de três estados:
	
	- `RanToCompletion`;
	
	- `Canceled`;
	
	- `Faulted`.

- `RunToCompletion`, como esperado, significa que o método raiz da _task_ terminou graciosamente. `Faulted` implica que a execução da _task_ terminou devido ao lançamento de uma excepção não tratada. O cancelamento será discutido a seguir em secção própria.

- A forma mais lógica de entregar a excepção lançada por uma _task_ é quando outra _thread_ espera pelo resultado de uma dada _task_ - por outras palavras quando é feita a chamada a `Task.Wait`, `Task.WaitAll` ou `Task.Result`.

- As excepções lançadas pelas _tasks_ são agregadas numa instância do tipo `AggregateException` o que, A princípio, pode parece um pouco estranho, uma vez que `AggregateException` implica múltiplas excepções e a terminação das _tasks_ no estado `Faulted` é sempre determinada pela ocorrência de **uma** excepção. Como veremos adiante, as _tasks_ podem ser organizadas segundo uma relação parental, onde uma _task_ com _tasks_ filhas não termina antes todas as _tasks_ filhas terem terminado. Se um ou várias _tasks_ filhas terminarem no estado `Faulted`, essa informação necessita de ser propogada e, por essa razão, a TPL agrupará sempre as excepções relacionadas com as _tasks_ numa instância de `AggregateException`. Outra situação em que pode ser necessário propagar múltiplas excepções é quando se utiliza o método `Task.WaitAll` para aguardar a terminação de um grupo de _tasks_. As excepções lançadas pelas _tasks_ em apreço, se ocorrerem, são agrupados numa instância de `AggregateException`. Assim, o código para tratar excepções lançadas pelas _tasks_ deverá ter a estrutura que se mostra a seguir: 

```C#
Task task = Task.Run(() => DoSomething());

// do something else

// synchronize with task outcome 
try {
	task.Wait();
} catch (AggregateException errors) {
	foreach (Exception error in errors.InnerExceptions)
		Console.WriteLine($"{error.getType().Name}: {error.Message}");
}
```

- Isto é um pouco complicado, e será mesmo pior na medida em que é possível que uma dada excepção interna possa também ser uma excepção agregada, requerendo outro nível de iteração. Por isso, o tipo `AggregateException` define o método `Flatten` que produz uma nova instância de `AggregateException` que contém um único conjunto de excepções internas. Usando este método, o tratamento de excepção será:

```C#
catch (AggregateException errors) {
	foreach (Exception error in errors.Flatten().InnerExceptions)
		Console.WriteLine($"{error.getType().Name}: {error.Message}");
}
```

- O papel de um _handler_ de excepção e ver o tipo da excepção e decidir como deve recuperar do erro. No caso dos erros representados por `AggregateException` isso significaria iterar através de todas as excepções internas, examinar o tipo de cada uma delas decidir ser pode ser tratada e, se não for esse o caso, voltar a lançar a excepção para dar oporturnidade a outros _exception hanlers_ acima no _stack_.

- O tipo `AggregateException` também define o método `Handle` para auxiliar no tratamento de excepções reduzindo a quantidade de código que é necessário escrever. O método `Handle` aceita um _delegate_ predicado que é aplicado a cada uma das excepções agrupadas na `AggregateException`. O predicado deve devolver `true` se a excepção é pode ser tratada e `false` no contrário. No fim de processar todas as excepções, qualquer excepção não tratada será relançada de novo como parte de uma nova instância de `AggregateException` contendo apenas as excepções que foram consideradas não tratadas.

- Por exemplo, numa situação em que queiramos ignorar a `TaskCanceledException` mas considerar as outra excepções eventualmente agrupadas na `AggregateException` ...

```C#
...
var loopTask = ...;
try {
	long result = loopTask.Result;
		Console.WriteLine($"\n-- Successful execution of {result} loop iterations");
} catch (AggregateException ae) {
	try {
		ae.Handle((ex) => {
			if (ex is TaskCanceledException) {
				Console.WriteLine($"\n** The task was cancelled by user with: \"{ex.Message}\"");
				return true;
			}
			return false;
		});
	} catch (AggregateException ae2) {
		foreach (Exception ex in ae2.Flatten().InnerExceptions)
			Console.WriteLine($"\n** Exception type: {ex.GetType().Name}: ; Message: {ex.Message}");
	}
}
...
```

#### Demo

- Os ficheiros [error-handling.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/error-handling.cs) e [cancellation.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/cancellation.cs) contêm código que faz o tratamento das excepções lançadas por _tasks_.

## Ignorando Erros

- Existem muitos cenários onde esperar pela conclusão de uma _task_ não é adequado. O principal objectivo da programação assíncrona, afinal, é lançar operações assíncronas libertando a _thread_ que lança a operação para que possa ser utilizada para realizar qualquer outro processamento útil. Este levanta a seguinte pergunta: E se eu não esperar pela conclusão de uma _tasks_? E se eu simplesmente quero fazer _fire and forget_? Por exemplo, uma aplicação pode lançar uma _task_ a intervalos regulares para tentar actualizar uma _cache_ em _background_. A aplicação pode não se importar se a _task_ ocoasionalmente falha, o que terá como consequência um _cache miss_ quando o utilizador tentar obter os dados cujo carregamento em _cache_ falhou. Enquanto o projectista de aplicações pode achar que não tem problemas ignorar simplesmente quaisquer falhas da _task_ que faz a actualização da _cache_, será realmente seguro fazê-lo? Acontece que isso dependerá do estado em que a _task_ termina e da versão do .NET _Framework_ onde a applicação está a executar.

### .NET 4.0

- No .NET 4.0, se uma _task_ termina no estado `Faulted` temos a obrigação de observar o erro. Se não o fizermos, a aplicação fará  _shutdown_ num momento aleatório no futuro. Este comportamento resulta do facto de que a equipa da TPL decidiu que seria uma péssima prática ignorar simplesmente os erros. De facto, as excepções indicam uma qualquer falha não expectável. Estas falhas não expectáveis podem levar a que a aplicação transite para um estado inválido, e posterior pode conduzir a outras falhas e à corrupção do estado da aplicação. O objectivo de capturar as excepções é garantir, antes de que o processamento continue, que o processo permaneça num estado válido. Assim, considera-se que simplesmente ignorar as excepções não é uma boa ideia.

- A pergunta pertinente é: como sabe a TPL que decidimos ignorar o erro? Enquanto temos uma referência activa para a _task_ temos a possibilidade de observar o erro. Mas, a partir do momento que deixamos de ter acesso à referência, não poderemos observar a excepção associada a uma _task_ particular. Assim que deixem de existir referências activas para o objecto _task_, ele é um candidato a ser colectado pelo _garbage collector_. O objecto task_ contém um finalizador; quando o objecto _task_ é criado, ele é registado na _finalization queue_. Quando o _garbage collector_ decide descartar o objecto _tasks_, ele constata que o mesmo está registado na _finalization queue_ e, por isso, não poderá já remover o objecto da memória; em alternativa, o objecto _task_ e colocado na _reachable queue_. Quando a _finalizer thread_ executa o finalizador da _task_ e, constata que não foi feita nenhuma tentativa para observar a excepção, lança a excepção na contexto da _finalizer thread_, terminando o processo.

- O facto de que este comportamento é baseado numa acção desencadead pelo _garbage-collector_ significa que pode haver um longo período de tempo entre a a terminação da task no estado `Faulted` e a terminação da aplicação, tornando muito difícil fazer _debug_ e por vezes essa situação não será detectada até o código estar em produção.

- Isto não é a imagem completa. Existe uma última oportunidade disponível para tratar a excepção: registando um _handler_ no evento `UnobservedTaskException` declarado na classe `TaskScheduler`. Antes do finalizador da _tasK_ relançar a excepçaõ, o evento é disparado, e quisquer subscritores terão uma última oportunidade de dizer que observaram a excepção e indicar que é seguro manter o processo em execução. Isto é indicado chamando o método `SetObserved` no argumento que descreve o evento. Se nenhum _event hanlder_ der a excepção como "observada", ela será relançada na _finalizer thread_, o que implica a terminação do processo.

- O seguinte código mostra o registo no `TaskScheduler.UnobservedTaskException:

```C#
TaskScheduler.UnobservedTaskException +=
	 (sender, e) {
		 Console.WriteLine($"***exception sender type: {sender.GetType()}");
		 foreach (Exception ex in e.Exception.InnerExceptions)
		 	Console.WriteLine($"**unobserved exception {ex.GetType().Name}: {ex.Message}");
		e.SetObserved();
	 };
```

- Embora isto satisfaça a necessidade de não termos que esperar pela terminação das _tasks_, ainda significa que ou erro pode ser ou não visto até um mommento aleatório no futuro, ou em alguns casos nem sequer ser visto. Adicionalmemnte, entretanto o código da aplicação pode estar a executar-se com um estado que possivelmente será inválido. As excepções são geralmente melhor tratadas o mais próximo possível da sua fonte. Este _event handler_ é realmente útil para _logging_ e possivelmente para alertar o projectistas de que devem observar as _tasks_ para determinar se ocorreram erros.

### .NET 4.5

- Houve uma reacção na comunidade de desenvolvedores sobre esse mecanismo geral de "notificação aleatória do error". Em resposta a esta crítica, a Microsoft decidiu alterar este comportamento no .NET _Framework_ 4.5. A correcção foi simples: não relançar a excepção na _finalizer thread_. As excepções são oferecidas as todos os subscritores do evento `TaskScheduler.UnobservedTaskException`, mas nunca são relançadas na _finalizer thread_. Por outras palavras, se o programador não empreende nenhuma acção para tratar as excepções, a TPL irá simplemente engoli-las.

- Se tiver o .NET 4.5 instalado, não poderá executar a aplicação no .NET 4.0. Ainda é possível especificar que o .NET 4.5 deve assumir o comportamento do .NET 4.0, definindo um ficheiro de configuração da aplicação que contenha o seguinte:

```
<configuration>
	<runtime>
		<ThrowUnobservedExceptions enable = "true" />
	</runtime>
</configuration>
```

#### Demo

- Os ficheiros [ignoring-errors.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/ignoring-errors.cs) e [ignoring-errors.exe.config](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/ignoring-errors.exe.config) contêm o código que permite demostrar as várias possibilidades de ignorar os erros segundo o comportamento do .NET _Framework_ 4.0 ou 4.5.

### Cancelamento das _Tasks_

- Qualquer método de cancelamento requer cooperação da própria operação assíncrona. A operação definirá pontos onde da sua execução onde o cancelamento é seguro. Assim, o cancelamento será educadamente solicitado e a operação assíncrona reagirá assim que puder. Por isso, é possível que uma operação assíncrona cujo cancelamento foi solicitado pode completar com sucesso.

- Para implementar um protocolo de cancelamento, o .NET _Framework_ 4.0 introduziu dois novos tipos: `CancellationTokenSource` e `CancellationToken`. Estes dois tipos coordenam o cancelamento. O _cancellation token source_ é usado pela parte que pretende solicitar o cancelamento; o _cancellation token_ é passado a cada operação assíncrona que se pretende poder cancelar.

- Para chamar um método com um _cancellation token_, é nececcário ter um. O _cancellation token_ vem de uma _cancellation token source_; como dissemos atrás, a _cancellation token source_ é um objecto que é usado pela parte do código que pretende iniciar o processo de cancelamento. O código que se mostra a seguir mostra código que tira partido desta API, primeiro criando uma _cancellation token source_, depois extraindo o _token_ da _source_ e passando-o para o método assíncrono.

```C#
public static void Main() {
	// the source of cancellation
	CancellationTokenSource cts = new CancellationTokenSource();
	// task receives the underlying CancellationToken
	CancellationToken ctoken = cts.Token;
				
	var loopTask = LoopRandomAsync(ctoken);
		
	while (!loopTask.IsCompleted) {
		if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
			// cancel through CancellationTokenSource
			cts.Cancel();
		}
		Thread.Sleep(50);
	}
	...
}
```

- Se o utilizador deseja cancelar todas as operações que tenham acesso ao _cancellation token_, invocam o método `Cancel` na instância de `CancellationTokenSource`. Isto tem apenas como efeito alterar o estado do `CancellationToken` para cancelado.

- A segunda parte deste protocolo é responder ao pedido educado da _cancellation token source_ para cancelar. Existem duas oportunidades para fazer isto: antes ou durante a execução da operação assíncrona. Recorde que criar e lançar uma _task_ não significa execução imediata; a _task_ pode estar na fila do _Task Scheduler_ esperando execução. Por isso, para que a TPL não execute uma _task_ à qual foi solicitado o cancelamento, ela também precisa de conhecer o _cancellation token_, que é passado para o método que cria e lança a _task_, isto é, `TaskFactory.StartNew` ou `Task.Run`.

- Quando a operação assíncrona está em execução, é da responsabilidade da _task_ decidir quando é seguro responder a um pedido de cancelamento. A propriedade `CancellationToken.IsCancellationRequested` que é afectada com `true` quando é invocado o método `Cancel` na respectiva instância de `CancellationTokenSource`.

- Para informar a TPL que uma operação assincrona responde ao cancelamento, a operação deve terminar lançando uma `OperationCancelledException` especificando o _cancellation token_ que através do qual foi comunicado o cancelamento. Isto pode ser feito excplicitamente testando directamente a propriedade `CancellationToken.IsCancellationRequested` ou invocando  o método `CancellationToken.ThrowIfCancellationRequested` que lança a `OperationCancelledException` no caso se ter sido solicitado o cancelamento. O seguinte código exemplifica:

```C#
private static Task<int> LoopRandomAsync(CancellationToken ctoken) {
		
	return Task<int>.Run(() => {
		Random rnd = new Random(Environment.TickCount);
		int loopCount = rnd.Next(100);
			
		// 25% failures!
		if (loopCount > 75)
			throw new InvalidOperationException(loopCount.ToString() + " are too much loops!");
			
		Console.Write($"[{loopCount}]");
			
		for (int i = 0; i < loopCount; i++) {
				
			// ctoken.ThrowIfCancellationRequested();
			// or
			if (ctoken.IsCancellationRequested) {
				// do some necessary cleanup!
				throw new OperationCanceledException("LoopRandom task cancelled!", ctoken);
			}
			// show progress
			Console.Write('.');
			// yield processor for a random time between 10 and 100 ms 				
			Thread.Sleep(rnd.Next(10, 100));
		}
		return loopCount;
	}, ctoken);		// specify cancellation token
}
```

- Além do cancelamento conduzido pelo utilizador, é razoável que as operações assíncronas possa ser canceladas porque estão a demorar muito tempo a concluir. Em alternativa a especificar-se um valor de _timeout_ adicional, o _timeout_ pode ser implementado com cancelamento. Quando criamos uma instância de `CancellationTokenSource` é possível especificar um período de tempo depois do qual o cancelamento é disparado automaticaente. Existe também um método, `CancelAfter`, que pode ser usado numa instância de `CancellationTokenSource` para definir um intervalo de tempo para solicitar o cancelamento após a criação. No seguinte excerto de código ilustra-se a definição de um _timeout_ que desadeia o cancelamento automático enquanto mantém a hipótese de continuar a dispor de cancelamento manual.

```C#
public static void Main() {
	// the source of cancellation
	CancellationTokenSource cts = new CancellationTokenSource(2500);	// cancel automatically after 2500 ms
	// task receives the underlying CancellationToken
	CancellationToken ctoken = cts.Token;
				
	var loopTask = LoopRandomAsync(ctoken);
		
	while (!loopTask.IsCompleted) {
		if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
			// cancel through CancellationTokenSource
			cts.Cancel();
		}
		Thread.Sleep(50);
	}
	...
}
```

- Se a API que definirmos contém dois métodos assíncronos, um que pode providenciar cancelamento e outro que não. Não queremos ter que escrever dois métodos separados só para omitir a lógica de cancelamento; em alternativa, podemos tirar partido do _dummy cancellation token_ providenciado pela TPL através da propriedade `CancellationToken.None`.
  
### Reportar Progresso a Partir de uma _Task_

- Uma necessidade comum em APIs assíncronas é a necessidade de suporte para reportar progresso ou conclusão. O progresso é tipicamente representado por uma percentagem, mas isso nem sempre é adequado; durante uma instalação, pode ser interessante ver que componentes estão a ser instaladas. O .NET _Framework_ 4.5 introduziu uma forma _standard_ de representar o progresso, por via da interface `IProgess<T>, cuja definição é a seguinte:
	
```C#
public interface IProgress<in T> {
	void Report(T value);
}
```

- Trata-se de uma interface muito simples. A ideia é que alguém que que ver o progresso deverá definir um tipo que implemente esta interface; as instâncias desse objecto serão passadas ao método assíncrono que espera um objecto com o tipo `IProgress<T>`. Depois, o método assíncrono chama o método `Report` de cada vez que quiser reportar progresso.

- Para consumir os reportes de progresso é necessário fornecer um objecto que implemente `IReport<T>`. Para simplificar, a TPL providencia uma implementação de `IProgress<T>` na classe `Progress<T>`. Este tipo é um adapatdor para a interface `IProgress<T>` permitindo consumir os reportes de progresso ou por via de um simples _delegate_ passado ao construtor ou através da subscrição tradicional do evento `Progress<T>.ProgressChanged`.

#### Demo

- No ficheiro [progress-report.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/progress-report.cs) encontra-se um programa que ilusta a utilização da interface `IProgress<T>` e da classe `Progress<T>`. 

## Relações entre Tasks

- Até agora, geralmente considerámos cada _task_ como a sua própria ilha de actividade. A seguir veremos como podemos encadear _tasks_ ou organizá-las com relacionamento pai-filho (ou mãe-filha?).

### Encadeamento de _Tasks_ (_Continuations_)

- Além de criar _tasks_ que são imediatamente prontas para execução, a TPL permite definir uma _task_ que não seja imediatamente submetida ao _scheduler_ para execução, mas que fica no estado `WaitingForActivation`. A _task_ transita para o estado `WaitingToRun` assim que ou mais _tasks_ antecedentes tenham completado. O seguinte excerto de código cria duas _tasks_ uma com o método normal `Task.Run`, a segunda é criada com o método `Task.ContinueWith`:

```C#
Task<int> firstTask = Task.Run<int>(() => { Console.WriteLine("First Task"); return 42; });

Task secondTask = firstTask.ContinueWith(ascendent => Console.WriteLine($"Second Task, First task returned {ascendent.Result}"));

secondTask.Wait();
```

- O método `Task.ContninueWith` cria uma segunda _task_, que será activada assim que a primeira _task_ tenha terminado. O _delegate_ passada ao método `ContinueWith` representa o corpo da `secondTask` na mesma forma que o _delegate_ passado ao método `Task.Run`, com uma diferença que é o facto de ser passado ao método um parâmetro que respresenta a _task_ que esta _tasks_ está a continuar. Isto permite que os resultados de uma _task_ fluam para outra. As continuções definidas até agora são incondicionais - por outras palavras, não interessa o estado em que a _task_ anterior completa; a segunda _task_ será sempre activada.  Podem acontecer situações onde apenas desejemos executar uma _task_ subsequente se a _task_ anterior completou com sucesso. As continuações condicionais são obtidas especificando uma das `TaskContinuatonOptions` na chamada ao método `Task.ContinueWith`, como se ilustra no seguinte excerto de código:

```C#
Task secondTask = firstTask.ContinueWith(ProcessResult, TaskContinuationOptions.OnlyOnRanToCompletion);

Task errorHandler = firstTask.ContinueWith(ProcessResult, TaskContinuationOptions.OnlyOnFaulted);

secondTask.Wait();
```

- Este código coloca uma questão importante: o que acontece se outro trecho de código continuasse a partir de `secondTask` ou simplesmente agardasse se `secondTask` terminaria com sucesso? Se `secondTask` não completar com sucesso, então a _task_ que a continuaria nunca executaria. A forma como a TPL trata esta situação é fazendo com que `secondTask` transite para o estado `Canceled`, evitando assim um  _deadlock_.

- Uma utilização comum para continuações `OnlyOnFaulted` é utilizá-las para tratar excepções não tratadas da _task_ antecedente para evitar que existam excepções não observadas. 
  
#### Porquê Usar Continuações

- À primeira vista, encadear _task_ parece bastante estranho. Porquê ter a _task_ A e, depois, quando esta completar, executar a _task_ B? Afinal, poderíamos apenas combinar a funcionalidade das _tasks_ A e B numa única _task_. Isto pode ser certamente verdadeiro para _computer-based tasks_, mas o que a _task_ A for uma _I/O-based task_ e a for uma _computer-based task_ desenhada para processar os dados retornados pela _I/O-based task_. Não podemos simplesmente combinar esta funcionalidade numa única _task_, mas o que podemos fazer é usar uma continuação. O exerto de código mostrado a seguir mosta esta abordagem.

```C#
private static Task<string> DownloadWebPageAsync(string url) {
	WebRequest request = WebRequest.Create(url);
	Task<WebResponse> response = request.GetResponseAsync();
		
	return response.ContinueWith<string>(antecedent => {
		using (var reader = new StreamReader(antecedent.Result.GetResponseStream())) {
			return reader.ReadToEnd();
		}
		
	});
}
```

-  Esta abordagem tem a vantagem de que não existe nunhuma _worker thread_ a ser utilizada enquanto se espera a resposta do servidor _web_, mas apenas quando a resposta é recebida a continuação está pronta para execução numa _worker thread_ do _pool_, e uma vez executando prossegue para fazer o _download_ do conteúdo.

- Anteriormente aludimos ao facto de que as continuações poder-se-iam basear não apenas numa _task_ mas em vàrias. Considere o seguinte cenário: um vasto conjunto de dados tem um algoritmo aplicado a ele, e depois uma vez completado, será produzido o sumário dos resultados. Assumindo que o vasto conjunto de dados pode ser dividido em partes isoladas, podemos criar múltiplas _tasks_ para executar o algoritmo sobre uma pequena porção do conjunto global, com uma _task_ que faça o sumário que é definida como continuação que executarão após a conclusão de todas as _tasks_ algorítmicas. O seguinte excerto de código ilustra esta situação.

```C#
Task[] algoritmTasks = new Task[4];
for (int nTask = 0; nTask < algorithmTasks.Length; nTask++) {
	int partToProcess = nTask;
	algotithmTasks[nTask] = Task.Run(() => ProcessPart(partToProcess));
}
Task.Factory.ContinueWhenAll(algothmTasks, antecedentTasks = > ProduceSummary());
```

- Outra continuação baseada em múltiplas _tasks_ pode ser definida com o método `TaskFactory.ContinueWhenAny`. Como o próprio nome sugere, isto pode ser usado para continuar quando se completa **uma** das _tasks_ de um _array_. Isto pode ser interessante em situações onde, por exemplo, interrogamos três servidores para obter um resultado e o primeiro que responder ganha. Contudo, este método torna-se complicado. A continuação será sempre accionada independentemente da forma com uma _task_ é concluída, e não poderemos usar a opção `OnXxxTaskContinuationOptions` nos métodos `ContinueWhenAll/Any` para resolver este problema. Isto obviamente significa que se a primeira _task_ a completar o faz no estado `Faulted`, então os posteriores sucessos não serão observados. Mesmo que a continuação dispare com sucesso no .NET 4.0, ainda deveremos tratar os erros das restantes _tasks_, para não entrar em conflito com as excepções não observadas das _tasks_. Veremos adiante técnicas simples de obter este comportamento que usam  `async` e `await`.

- As continuações são uma técnica muito poderosa para manter o número de _worker threads_ activas no mínimo e, mais importante, para permitir que operações assíncronas executando em diferentes contextos possam ser encadeadas.

### _Tasks_ Aninhadas e _Tasks_ Filhas

- Durante a execução de uma _task_, a TPL permite que uma _task_ crie outras _tasks_. Estas outras _tasks_ são designadas por aninhadas ou filhas, consoante a forma as mesmas são criadas. As _tasks_ aninhadas não têm nenhum impacto na _task_ que as criou; a única coisa interessante aqui é que as _tasks_ aninhadas serão agendadas na _work-stealing queue_ da _worker thread_ que a criou, em vez do agendamento ser feito na fila partilhada.

- Se executar o seguinte excerto de código, existe pouca probabilidade da mensagem `Nested` aparecer, uma vez que a _task_ exterior irá terminar imediatamente depois de criar a _task_ aninhada.

```C#
Task.Run(() => {
	Task nested = Task.Factory.StartNew(() => Console.WriteLine("Nested..."));
}).Wait();
```

- Modificando o código ligeiramente para determinar que a _task_ aninhada é uma _task_ filha resultará que a _task_ mãe não terminará enquanto a _task_ filha não terminar, o que garante que a mensagem `Nested` será mostrada na consola. Para tornar uma _task_ aninhada numa _task_ filha, especifica-se a opção `TaskCreationOptions.AttachToParent` quando a _task_ aninhada é criada, como se mostra no seguinte excerto de código.

```C#
Task.Run(() => {
	Task child = Task.Factory.StartNew(() => Console.WriteLine("Nested..."), TaskCreationOptions.AttachToParent);
}).Wait();
```

- O outro efeito de criar _tasks_ filhas em oposição a criar _tasks_ aninhadas está relacionado com o tratamento de excepções. Qualquer excepção não tratada com origem numa _task_ filha é propagada para a _task_ mãe. Qualquer código que processe o reultado da _task_ mãe ira ver todas as excepções das _tasks_ filhas como parte da excepção agregada.

- Além de ser possível criar uma _task_ como filha, é também possível evitar que as _tasks_ de se tornarem filhas, especificando a opção `TaskCreationOptions.DenyChildAttach`. Se for feita uma tentativa para criar uma _task_ filha, isso é simplesmente ignorado e a _task_ é criada como _task_ aninhada. Uma utilização possível da utilização desta opção e permitir que uma biblioteca possa expor _tasks_ sem receio que seja necessário tratar excepções lançadas por código sobre o qual não têm conhecimento.

#### Porquê Utilizar _Tasks_ Filhas

- Consideremos o seguinte excerto de código.

```C#
public Task ImportXmlFilesAsync(string dataDir, CancellationToken, ctoken) {
	return Tsk.Run(() => {
		foreach (FileInfo file in new DirectoryInfo(dataDir).GetFiles("*.xml")) {
			XElement doc = XElement.Load(file.FullName);
			InternalProcessXml(doc);
		}
	}, ctoken);
}
```

- Podemos melhorar o desempenho desta peça de código executando o carregamento e o processamento em _tasks_ separadas. Contudo, não pretedemos acrescentar complexidade ao método `ImportXmlFilesAsync` retornando múltipls _tasks_. Podemos simplemente fazer com o corpo do ciclo `foreach` crie uma _task_ filha. A complexidade de usar múltiplas _tasks_, de baixa granulosidade, e assim oculta ao consumidor, continuando a haver uma única _task_ para representar todo o processo. O código é apresentado a seguir.

```C#
public Task ImportXmlFilesAsync(string dataDir, CancellationToken, ctoken) {
	return Tsk.Run(() => {
		foreach (FileInfo file in new DirectoryInfo(dataDir).GetFiles("*.xml")) {
			string fileToProcess = file.FullName;	// captured state..
			Task.Factory.StartNew(() => {
				// convenient point to check cancellation
			
				XElement doc = XElement.Load(fileToProcess);
				InternalProcessXml(doc, ctoken);
			}, ctoken, TaskCreationOptions.AttachedToParent);
		}
	}, ctoken);
}
```

#### Demo

- NO ficheiro [why-child-tasks-cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tasks/why-child-tasks.cs) encontra-se um programa completo que usa esta técnica.


## Conclusão

- A introdução da _tasks_ TPL pela primeira vez no .NET _Framework_ providencia uma forma consistente de representar actividades assíncronas. Esta consistência não se limita apenas às _tasks_ mas também inclui a TPL como um todo com a criação de primitivas de cancelamento e progresso, resultando em APIs assíncronas consistentes em toda a plataforma.

- Quando desenhar e implementar o seu código de agora em diante, considere quanto tempo cada método pode demorar a executar. Os métodos que utilizam recursos de I/O devem ser escritos como assíncronos e todas as operações de I/O realizadas dentro do método devem ser assíncronas. Os métodos assíncronos são identificados pelo facto de retornarem uma instância dos tipos `Task` ou `Task<T>` e terem o sufixo `Async`; seguindo este padrão permite aos outros projectistas identificarem facilmente os métodos assíncronos. Se possivel, providencia suporte para reportar progresso ou cancelamento das operações assíncronas usando os tipos `CancellationToken` e `IProgress<T>`. Deixe os métodos que realizam processamento puro síncronos e permita que o chamador os decida invocar de forma assincrona.

- Por último, que escrever código que nunca bloqueia é mais provável que produza soluções mais escaláveis. 

- Acontece que criar _tasks_ é a parte mais fácil da programação assíncrona. A parte difícil da programação assíncrona que requer habilidade é conseguir que as _tasks_ cooperem entre si e as soluções sejam escaláveis.

___  

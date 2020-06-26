# Aula 25 - Sincronização em Programação Assíncrona e Sincronizadores com Interface Assíncrona (I)

___

## Sumário

- Sincronização em programação assíncrona: sincronização no acesso a dados partilhados mutáveis e sincronização de controlo.

- Mecanismos usados nos dois tipos de sincronização: _locks_ e instruções atómicas no acesso a dados partilhados mutáveis; sincronizadores com interface assíncrona na sincronização de controlo ou em mecanismos de comunicação.

- Necessidade de suportar cancelamento em sincronizadores com interface assíncrona; problemas que o cancelamento assíncrona levanta na implementação, comparando com as implementações baseadas em monitor onde as acções de cancelamento (_timeout_ e interrupção).

- Padrão de desenho para implementar sincronizadores com interface assíncrona no .NET _Framework_ e no _Java_.


## Sincronização em Programação Assíncrona

- A ideia de sincronizar a execução de _threads_ cooperantes está associada à programação síncrona. O seu objectivo é o definir pontos de sincronização em que as _threads_ se bloqueiam sempre que não estejam reunidas as condições para que a sua execução possa prosseguir; por exemplo, o processamento dos dados obtidos com uma operação de I/O deve bloquear a _thread_ corrente após o lançamento da operação de I/O até que a mesma esteja concluída. A necessidade de sincronização é determinada pela necessidade de garantir as pré-condições necessárias para executar determinados processamentos, não pela necessidade de bloquear a _thread_ que executa uma determinada sequência de acções num fio de processamento autónomo; o bloqueio das _threads_ é um **meio** para condicionar a execução de cada átomo de processamento às necessárias pré-condições.  

- Em programação assíncrona não existem _threads_ associadas a cada uma das sequências de processamento autónomo que executam em paralelo nas aplicações. Contudo, as acções em cada sequência de processamento também podem estar condicionadas por pré-condições. Assim, ainda que **não seja relevante saber qual a _thread_ que executa o quê** é necessario que sejam asseguradas as pré-condiçoes que condicionam as sucessivas acções de uma determinada sequência de processamento.

- Na sincronização no acesso a dados partilhados mutáveis, onde o bloqueio de _threads_ só ocorre quando existe simultaneidade no acesso aos dados partilhados, consideramos que a utilização de _locks_ com interface síncrona (bloqueante) em programação assíncrona é razoável mesmo em programação assícrona, dado que os períodos de bloqueio são, em regra, pouco frequentes e breves, pelo que o desempenho é globalmente melhor do recorrer a _locks_ com uma interface assíncrona para evitar, de todo, o bloqueio das _worker threads_. 

- Na sincronização de controlo, não é possível, à partida, prever ou limitar o tempo que as _threads_ poderão ficar bloqueadas quando são utilizados sincronizadores com interface síncrona. Por isso, a alternativa é **utilizar sincronizadores com interfaces assíncronas**, onde a **espera para que estejam reunidas as condições de sincronização se faz sem o bloqueio das _worker threads_**. 


## Implementação de Sincronizadores com Interface Assíncrona no .NET _Framework_

- Na implementação se sincronizadores com interface assíncrona vamos usar o modelo _Task-based Asynchronous Pattern_ (TAP). As operações de sincronização com semântica _acquire_ retornam de imediato devolvendo uma _task_ que representa a operação assíncrona em curso. Depois, o código invocante utiliza uma das técnicas da programação assíncrona - agendamento de continuações ou a suspensão da execução de métodos métodos assíncronos - para prosseguir o processamento após estarem reunidas as condições da sincronização.

- Nas aplicações assíncronas, não existe afinidade entre cada atómo de processamentoe as _threads_ que os executam, é considerado conveniente que as operações de sincronização com semântica _acquire_ suportem cancelamento. Assim, será sempre possível agir sobre as operações assíncronas em curso, não só para observar o seu progresso ou resultado (o que é feita usando a _task_ subjacente), mas também para accionar o respectivo cancelamento, sempre que a lógica das aplicações assim o determinar. Quando implementarmos sincronizadores com interface assíncrona no .NET _Framework_, vamos suportar o cancelamento das operações assíncronas por _timeout_ e, directamente, usando o .NET _cancellation framework_.

- Em versões recentes do NET _Framework_, existe um sincronizador que suporta interface assíncrona na operação _acquire_ que é implementado pela classe `System.Threading.SemaphoreSlim`. Esta implementação do semáforo define uma operação _acquire_ com interface síncrona (método `SemaphoreSlim.Wait`) e outra com interface assíncrona (método `SemaphoreSlim.WaitAsync`). No padrão de desenho que abordaremos adiante, vamos também suportar os dois tipos de interfaces; contudo, a interface síncrona sera implementada usando a interface assíncrona, o que não acontece na implementação do `SemaphoreSlim` do .NET _Framework_. 

- A implementação de sincronizadores com interface assíncrona segue um estilo semelhante ao que anteriormente designámos por "estilo _kernel_". A principal diferença é o facto de não existir uma _thread_ bloqueada por cada operação pendente com semântica _acquire_. Em vez de haver uma _thread_ bloqueada numa variável condição do monitor que será notificada quando a operação for concluída ou cancelada, vamos ter uma _task_ (controlada por uma instância do tipo `TaskCompletionSource`) que será completada quando a operação for concluída ou cancelada.

- O padrão de desenho que vamos apresentar a seguir tem, como dissemos acima, uma estrutura semelhante à implementação de sincronizadores baseados em monitor ao "estilo _kernel_", nomeadamente:

	- Toda a acesso ao estado partilhado mutável é protegido por um _lock_.
	
	- Cada operação assíncrona pendente é representada por um objecto _asynchronous request_ que é inserido na respectiva fila de espera; estes objectos têm associada uma instância do tipo `TaskCompletionSource<TResult>` que controla a _task_ (obtida com a propriedade `TaskCompletion<TResult>.Task) que representa a operação assíncrona em curso; este tipo de objecto deverá ter campo(s) para armazenar o(s) argumento(s) da operação assícrona se existirem (e.g., no sémaforo haverá um campo com o número de autorizações solicitadas); o resultado da operação será naturalmente obtido com a propriedade `Task.Result`.
	
	- O cancelamento por _timeout_ ou por via de um _cancellation token_, poderá ocorrer em simultâneo com a conclusão normal do pedido assíncrono ou mesmo o cancelamento por uma razão pode ocorrer em simultâneo com o cancelamento pela outra razão. Assim, antes de proceder ao cancelamento, removendo o _asynchronous request_ da fila de espera é necessário testar se o pedido já foi satisfeito ou cancelado. Na implementação de sincronizadores com base em monitor segundo o "estilo kernel", também podia haver uma _race condition_ entre a conclusão normal da operação _acquire_ e a detecção por parte da _thread_ invocante de que teria sido accionando o cancelamento (através do retorno do método `Monitor.Wait` por _timeout_ ou por interrupção). Assim, qualquer que fosse a condição de saída do método `Monitor.Wait`, foi sempre necessário testar o campo `done` do objecto _request_ para determinar se a operação tinha sido concluída normalmente e, em caso afirmativo, optámos por ignorar o cancelamento.

	- Enquanto que na implementação de interfaces síncronas há uma _thread_ associada a cada operação _acquire_ pendente, a indicação de conclusão da operação ou o accionamento do seu cancelamento é sempre comunicada à _thread_ quando esta se encontra bloqueada numa variável condição do monitor; a própria implementação do monitor garante que o retorno do método `Monitor.Wait` já feito na posse do _lock_ do monitor, portanto podemos dizer que o processamento do cancelamento é centralizado e síncrono. Na implementação de interfaces assíncronas, não existe nenhuma _thread_ bloqueda por cada operação _acquire_ pendente, pelo que os eventuais canceladores (_timer_ e _callback_ registado na instância de `CancellationToken`) executam assíncronamente no contexto de _threads_ arbitrárias e pode ser executado mais do que um _cancellation handler_ simultaneamente e ainda em simultâneo com a respectiva operação _release_. Assim, será necessário adquirir a posse do _lock_ em todos os caminhos de código que vão aceder ao objecto que decreve a operação _acquire_ assíncrona.
	
	- Um aspecto importante a ter em consideração, na implementação destes sincronizadores, é saber em que _thread_ são executados os _cancellation handlers_. Este aspecto é importante para se determinar que garantias de visibilidade e atomicidade são dadas pelo _lock_ ou mesmo determinar se será possível a ocorrência de situações de _deadlock_.
	
	- No .NET _Framework_, sabemos que o _callback_ associado a uma instância do tipo `System.Threading.Timer` executa numa _worker thread_ do _thread pool_. Assim, mesmo que o _timer_ seja criado por uma _thread_ que tem a posse do _lock_ (como veremos adiante é o caso), o _lock_ garante que o _cancellation hanlder_ bloqueia a _worker thread_ que o invoca enquanto o _lock_ não for libertado por parte da _thread_ que lançou o _timer_. Assim, existe a garantia de que o estado partilhado mutável fixado antes de libertar o _lock_ é visto pela _thread_ que executa o _callback_ do _timer_ depois desta adqurir a posse do _lock_.
		
	- Relativamente aos _cancellation handlers_ registados nas instâncias do tipo `CancellationToken`, sabemos que o .NET _Framework_ os executa: (i) se o cancelamento ainda não foi accionado, o _callback_ executará, mais tarde, na _thread_ que invocar o método `CancellationTokenSource.Cancel`, ou; (ii) quando o cancelamento já foi accionado, o _cancellation handler_ executa sincronamente na _thread_ que invoca o método `CancellationToken.Register`. Esta execução síncrona será analisada com cuidado, na discussão da implementação do semáforo que apresentamos adiante, dado que os _locks_ que vamos utilizar no .NET _Framework_ e em _Java_ suportam a acquisição recursiva por parte da _thread_ que já detém a posse do _lock_.

### Implementação de um Semáforo com Interface Assíncrona
	
- Neste ponto, vamos explicar a implementação segundo este padrão de desenho com base no conteúdo do ficheiro [SemaphoreAsync.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/synchs-async/SemaphoreAsync.cs) que vamos analisar a seguir por excertos.

- Começamos com o início da definição da classe `SemaphoreAsync`, a definição do tipo de dados que armazena cada _asynchronous request_, assim como os restantes elementos do estado partilhado mutável.

```C#
public class SemaphoreAsync {
			
	// The type used to hold each async acquire request
	private class AsyncAcquire: TaskCompletionSource<bool> {
		internal readonly int acquires;					// the number of requested permits
		internal readonly CancellationToken cToken;		// cancellation token
		internal CancellationTokenRegistration cTokenRegistration;	// used to dispose the cancellation handler 
		internal Timer timer;
		internal bool done;		// true when the async request is completed or canceled
		
		internal AsyncAcquire(int acquires, CancellationToken cToken) : base() {
			this.acquires = acquires;
			this.cToken = cToken;
		}

		/**
		 * Disposes resources associated with this async acquire.
		 *
		 * Note: when this method is called we are sure that the field "timer" is correctly affected,
		 *		 but we are not sure if the "cTokenRegistration" field is.
		 * 		 However, this does not cause any damage, because when this method is called by
		 *	     cancellation handler this field is not used, as the resources mobilized to register
		 *		 the handler are released after its invocation.
		 */
		internal void Dispose(bool canceling = false) {
			if (!canceling && cToken.CanBeCanceled)
				cTokenRegistration.Dispose();
			timer?.Dispose();
		}
	}

	// The lock - we do not use the monitor functionality
	private readonly object theLock = new object();
	
	// available and maximum number of permits	
	private int permits;
	private readonly int maxPermits;

	// The queue of pending asynchronous requests.
	private readonly LinkedList<AsyncAcquire> asyncAcquires;
	
```

- O tipo `AsyncAcquire` tem um campo para armazenar o número de autorizações solicitadas (`acquires`), dois campos para armazenar o _cancellation token_ (´cToken´) e a _cancellation token registratio_ (`cTokenRegistration`), um campo que armazena uma instância do tipo `Timer` quando é especificado um _timeout_ e, por último, um campo que indica se o pedido assíncrono já foi completado ou cancelado (`done`). As instâncias deste tipo são inseridas numa _asynchronoues request queue_ (`asyncAcquires`) e são acedidas na posse do _lock_ global. 

- O acesso ao estado partilhado mutável é protegido pelo _lock_ do monitor implícito associado ao objecto `theLock`. O número de autorizações disponíveis está armazenado no campo `permits` e o número máximo de autorizações que podem estar sob custódia do semaforo está armazenado no campo imutável `maxPermits`.

- A seguir, declaramos os _delegates_ usados para armazenar os _cancellation handlers_ e duas instâncias do tipo `Task<bool>` já concluídas para devolver os resultados `true` e `false`. 

```C#
	/**
	 * Delegates used as cancellation handlers for asynchrounous requests 
	 */
	private readonly Action<object> cancellationHandler;
	private readonly TimerCallback timeoutHandler;

	/**
	 *  Completed tasks use to return constant results from the AcquireAsync method
	 */
	private static readonly Task<bool> trueTask = Task.FromResult<bool>(true);
	private static readonly Task<bool> falseTask = Task.FromResult<bool>(false);
	private static readonly Task<bool> argExceptionTask = Task.FromException<bool>(new ArgumentException("acquires"));
```

- O código do construtor do semáforo é o seguinte:

```C#
	/**
	 * Constructor
	 */
	public SemaphoreAsync(int initial = 0, int maximum = Int32.MaxValue) {
		// Validate arguments
		if (initial < 0 || initial > maximum)
			throw new ArgumentOutOfRangeException("initial");
		if (maximum <= 0)
			throw new ArgumentOutOfRangeException("maximum");
        
		// Construct delegates used to describe the two cancellation handlers.
		cancellationHandler = new Action<object>((acquireNode) => AcquireCancellationHandler(acquireNode, true));
		timeoutHandler = new TimerCallback((acquireNode) => AcquireCancellationHandler(acquireNode, false));

		// Initialize the maximum number of permits - immutable
		maxPermits = maximum;
        
		// Initialize the shared mutable state
		asyncAcquires = new LinkedList<AsyncAcquire>();
		permits = initial;
	}
```

- Para além da validação dos argumentos, o construtor inicia os _delegates_ que implementam os _cancellation handlers_ para o _timeout_ e para o cancelamento explícito. Estes _delegates_ aceitam como argumento uma instância do tipo `LinkedLisNode<AsyncAcquire>` e invocam o mesmo método `AcquireCancellatioHandler` passando para além da referência para nó da lista um _boolean_ que indica se o cancelamento é directo ou por _timeout_. A seguir, armazena o número máximo de autorizações num campo imutável (`maxPermits`). Por último inicializa a fila de pedidos de _acquire_ assícronos pendentes (`asyncAcquire`) e o número inicial de autorizações disponíveis (`permits`).
	
- O método auxiliar `SatisfyPendingAsyncAcquires`, chamado sempre que existam condições para satisfazer _async acquires_ pendentes, complete todos os _async acquires_ em fila de espera que possam ser satisfeitos com o número de autorizações sob custódia do semáforo. Este método não completa as _tasks_ associadas ao _async acquires_ que satisfaz pelo facto da _thread_ invovante ter a posse do _lock_; assim, é devolvida uma lista com as instâncias do tipo `AsyncAcquire` satisfeitos (o campo `done` destes objectos é afectado com `true`, pelo que os _async acquires_ subjacentes já não podem ser cancelados).

```C#
	...
	/**
	 * Returns the list of all pending async acquires that can be satisfied with
	 * the number of permits currently owned by the semaphore.
	 *
	 * Note: This method is called when the current thread owns the lock.
	 */
	private List<AsyncAcquire> SatisfyPendingAsyncAcquires() {
		List<AsyncAcquire> satisfied = null;
		while (asyncAcquires.Count > 0) {
			AsyncAcquire acquire = asyncAcquires.First.Value;
			// Check if available permits allow satisfy this async acquire
			if (acquire.acquires > permits)
				break;
			// Remove the async acquire from the queue
			asyncAcquires.RemoveFirst();
			
			// Update permits and mark the async acquire as done
			permits -= acquire.acquires;
			acquire.done = true;
			// Add the async acquire to the result list
			if (satisfied == null)
					satisfied = new List<AsyncAcquire>(1);
			satisfied.Add(acquire);
		}
		return satisfied;
	}
```

- Este método é chamado quando a _thread_ corrente tem a posse do _lock_. Um aspecto importante a reter neste método é o facto do mesmo não completar de imediato as _tasks_ associadas aos _async acquires_ que são satisfeitos, devolvendo antes uma lista com as respectivas instâncias de `AsyncAcquire`. Isto deve-se ao facto de **não ser boa prática conpletar as _tasks_ associadas às instâncias do tipo `TaskCompletionSource<TResult>` quando a _thread_ corrente está na posse de um ou mais _locks_**. A principal razão para esta regra e o facto das _tasks_ a completar poderem ter agendadas continuações para executar sincromamente (o que acontece quando é especifcada a opção `TaskContinuationOptions.ExecuteSynchronously` no agendamento da continuação). Se isso acontecer, **teremos código desconhecido a executar numa _thread_ que tem a posse do _lock_ que estamos a usar para proteger o estado partilhado mutável do nosso semáforo, o que pode consequências desastrosas. Por exemplo, se o método `SatisfyPendingAsyncAcquires` completasse as _tasks_ no corpo do ciclo quando satisfaz cada _async acquire_, se uma continuação executada sincronamente invocasse os métodos `AcquireAsync` ou `Release` sobre o mesmo semáforo, essas operações seriam executadas com o semáforo num estado indeterminado, uma vez que o _lock_, por admitir acquisições recursivas, não bloqueria a _thread_ que executava a continuação. (Este problema também não pode ser resolvido usando um _lock_ não admitisse aquisição recursiva, pois, nesse caso, ocorreria uma situação de **_deadlock_**, pelo facto da _thread_ tentar adquirir um _lock_ que está na sua posse.)

- Neste método não é necessário testar o campo `AsyncAcquire.done`, porque a posse do _lock_ garante que todos os _async acquires_ que estão na fila de espera não podem ter sido completados ou cancelados. 

- O método auxiliar `CompleteSatisfiedAsyncAcquires` completa as _tasks_ associadas a cada um dos _async acquires_ da lista passada como argumento. Pelas razões que explicámos anteriormente, este método executa quando a _thread_ invocante **não tem a posse do _lock_**.

```C#
	/**
	 * Complete the tasks associated to the satisfied async acquire requests.
	 *
	 *  Note: This method is called when the current thread **does not own the lock**.
	 */
	private void CompleteSatisfiedAsyncAcquires(List<AsyncAcquire> toComplete) {
		if (toComplete != null) {
			foreach (AsyncAcquire acquire in toComplete) {
				// Dispose the resources associated with the async acquirer and
				// complete its task with success.
				acquire.Dispose();
				acquire.SetResult(true);	// complete the associated request's task
			}
		}
	}
```

- O método `AcquireAsync` permite às _threads_ solicitarem autorizações ao semáforo usando uma interface assíncrona, segundo o modelo _Task-based Asynchronous Pattern_ (TAP).

```C#
	/**
	 * Acquires one or more permits asynchronously enabling, optionally,
	 * a timeout and/or cancellation.
	*/
	public Task<bool> AcquireAsync(int acquires = 1, int timeout = Timeout.Infinite,
								   CancellationToken cToken = default(CancellationToken)) {
		// Validate the argument "acquires"
		if (acquires < 1 || acquires > maxPermits)
			return argExceptionTask;			 
	
		lock(theLock) {
			// If the queue is empty ans sufficiente authorizations are available,
			// the acquire can be satisfied immediatelly; so, the field permits is
			// updated and a completed task is returned with a result of true.
			if (asyncAcquires.Count == 0 && permits >= acquires) {
				permits -= acquires;
				return trueTask;
			}
			// If the acquire was specified as immediate, return completed task with
			// a result of false, which means timeout.
			if (timeout == 0)
				return falseTask;
			
			// If the cancellation was already requested return a task in the Canceled state
			if (cToken.IsCancellationRequested)
				return Task.FromCanceled<bool>(cToken);
						
			// Create a request node and insert it in requests queue
			AsyncAcquire acquire = new AsyncAcquire(acquires, cToken);
			LinkedListNode<AsyncAcquire> acquireNode = asyncAcquires.AddLast(acquire);

			/**
			 * Activate the specified cancelers when owning the lock.
			 */
			
			/**
			 * Since the timeout handler, that runs on a thread pool's worker thread,
			 * that acquires the lock before access the fields "acquirer.timer" and
			 * "acquirer.cTokenRegistration" these assignements will be visible to the
			 * timeout handler.
			 */
			if (timeout != Timeout.Infinite)
				acquire.timer = new Timer(timeoutHandler, acquireNode, timeout, Timeout.Infinite);
			
			/**
			 * If the cancellation token is already in the canceled state, the cancellation
			 * handler will run immediately and synchronously, which *causes no damage* because
			 * this processing is terminal and the implicit locks can be acquired recursively.
			 */
			if (cToken.CanBeCanceled)
            	acquire.cTokenRegistration = cToken.Register(cancellationHandler, acquireNode);

			// Return the Task<bool> that represents the async acquire
			return acquire.Task;
		}
    }

```

- Este método começa por adquirir a posse do _lock_. Depois, se a fila de _async acquires_ estiver vazia e o semáforo tiver o número de autorizações suficientes, o _acquire_ pode ser satisfeito de imediato, pelo que é actualizado o valor do campo `permits` e devolvida uma _task_ já concluída com o valor `true`; nesta situação, a operação assíncrona completou-se sincronamente. Se existirem _async acquires_ em fila de espera ou se não existem autorizações suficientes, o _async acquire_ poderá ser cancelado de imediato (`timeout` igual a zero ou `cToken.IsCancellationRequested` igual a `true`) ou vai ficar em fila de espera representado por uma instância do tipo `AsyncAcquire`.

- É boa prática testar se foi solicitado o cancelamento imediato (_timeout_ zero ou um _cancellation token_ já cancelado) antes de criar o objecto que descreve o _async aquire_ que será posteriormente inserido na fila de espera, evitando-se assim a criação do objecto quando o cancelamento é imediato. 

- A activação dos canceladores, como já referimos, anteriormente pode ser delicada, pois os _cancellation handlers_ pode ser executados antes do retorno do método que os registam. É necessário garantir que os campos `AsyncAcquire.timer` e `AsyncAcquire.cTokenRegistration` que são utilizados nos _cancellation handlers_ já formam correctamente afectados. Para analisar se temos essa garantia temos que saber em que _threads_ e quando são executados os _cancellation handlers_.

- Os _callbacks_ associados a instância do tipo `Timer` executam sempre numa _worker thread_ do _thread pool_, pelo que a acquisição do _lock_ por parte do _timeout cancellation handler_, garante que, se o _timer_ disparar antes do retorno do método que o lança, a _worker thread_ não poderá adquirir o _lock_ enquanto este estiver na posse da _thread_ que está a executar o método `AcquireAsync`; Assim, no _cancellation handler_ associado ao _timeout_, após a aquisição do _lock_, temos a garantia de que o semáforo está num estado coerente, isto é, todos os compomentes do estado acedidos pelo _cancellation handler_ estão correctamente afectados e visíveis.

- O _callback_ associado a um _cancellation token_ pode ser executado numa das duas seguintes formas: (i) se o _cancellation token_ não estiver no estado cancelado quando é invocado o método `CancellationToken.Register`, a situação mais provável, o _cancellation handler_ é executado pela _thread_ que invoca o método `CancellationTokenSource.Cancel` para accionar o cancelamento, ou; (ii) se o _cancellation token_ já estiver no estado cancelado quando é invocado o método `CancellationToken.Register`, situação pouco frequente, mas possível, o _cancellation handler_ e executado sincronamente na _thread_ que chama aquele método. No caso do _cancellation handler_ ser executado por uma _thread_ diferente daquela que fez o respectivo registo, não existe qualquer problema - situação igual ao que acontece no _timeout cancellation handler_ - dado que a posse do _lock_ garante a visibilidade e a atomicidade no acesso ao estado partilhado mutável do semáforo. Na situação em que o _cancellation handler_ é executado sincronamente, a execução do _cancellation handler_ ocorre na _thread_ que o registou e antes do retorno do método `CancellationToken.Register`. A posse do _lock_ garante que nenhuma outra _thread_ acede ao semáforo. Qualquer modelo de memória, garante que é respeitada a na visibilidade da acções sobre a memória feitas por uma _thread_. Por isso, sabemos que a respectiva instância do tipo `AsyncAcquire` já foi inserido na fila de espera, que o campo `AsyncAcquire.timer` está correctamente afectado e que ainda não foi executada a afectação do campo `AsyncAcquire.cTokenRegistration`. Como não é possível que alguma outra _thread_ possa ter modificado o campo `AsyncAcquire.done`, o _cancellation handler_ remove o nó contendo o objecto `AsyncAcquire` da fila de espera e, no caso de ter sido especificado um _timeout_, cancela o _timer_ subjacente usando o campo `AsyncAcquire.timer`. Tendo em consideração que os recursos, reservados aquando do registo de um _cancellation handler_ num _cancellation token_, são libertados automaticamente depois de ter sido invocado o respectivo _callback_, não é necessário que o _cancellatiom handler_ utilize a informação armazenada no campo `AsynRequire.cTokenRegistration`. Assim, temos, nas duas situações possíveis, a garantia que o _cancellation handler_ executa com os requisitos de visibilidade e atomicidade no acesso ao estado partilhado mutável do semáforo.

- Por último o método `AcquireAsync` devolve uma instância do tipo `Task<bool>` que representa o _async acquire_ em curso.

- O método `Release` devolve à custódia do semáforo o número de autorizações especificadas.

```C#
	/**
	 * Releases the specified number of permits
	 */
	public void Release(int releases = 1) {
		// A list to hold temporarily the already satisfied asynchronous acquires 
		List<AsyncAcquire> satisfied = null;
		lock(theLock) {
			// Validate argument
			if (permits + releases < permits || permits + releases > maxPermits)
				throw new InvalidOperationException("Exceeded the maximum number of permits");	
			permits += releases;
			// Satisfy the pending async acquires that the current value of permits allows.
			satisfied = SatisfyPendingAsyncAcquires();
		}
		// After release the lock, complete the tasks underlying all satisfied async acquires
		if (satisfied != null)
			CompleteSatisfiedAsyncAcquires(satisfied);
	}
```

- O código deste método reflete a preocupação, já referida atrás, de não completar as _tasks_ subjacentes às operações assíncronas concluídas quando a _thread_ tem a posse do _lock_, de modo a evitar **surpresas** se ocorrer a execução síncrona de continuações. Assim o método `SatisfyPendingAsyncAcquire` processa os _async aquires_ pendentes considerando o novo valor do campo `permits` e devolve uma lista com as instâncias do tipo `AsyncAcquire` correspondes aos _async acquires_ que foram satisfeitos. Após ser libertado o _lock_, é invocado o método `CompleteSatisfiedAsyncAcquires` para completar as _tasks_ em apreço.

- O método `AcquireCancellationHandler` tenta cancelar o _async acquire_ cuja respectiva instância de `LinkedListNode<AsyncAcquire>` é passado como argumento. Este método recebe ainda um segundo argumento que indica se o cancelamento foi accionadado pelo _cancellation token_ ou devido à ocorrência de _timeout_.

```C#
	/**
	 * Try to cancel an async acquire request
	 */
	private void AcquireCancellationHandler(object _acquireNode, bool canceling) {
		LinkedListNode<AsyncAcquire> acquireNode = (LinkedListNode<AsyncAcquire>)_acquireNode;
		AsyncAcquire acquire = acquireNode.Value;
		bool complete = false;
		List<AsyncAcquire> satisfied = null;
		
		// To access shared mutable state we must acquire the lock
		lock(theLock) {
			
			/**
			 * Here, the async request can be already satisfied or cancelled.
			 */ 
			if (!acquire.done) {
				// Remove the async acquire request from queue and mark it as done.
				asyncAcquires.Remove(acquireNode);
				complete = acquire.done = true;
				
				// If after removing the async acquire is possible to satisfy any
				// pending async acquire(s) do it 
				if (asyncAcquires.Count > 0 && permits >= asyncAcquires.First.Value.acquires)
					satisfied = SatisfyPendingAsyncAcquires();
			}
		}

		// If we cancelled the async acquire, release the resources associated with it,
		// and complete the underlying task.
		if (complete) {
			// Complete any satisfied async acquires
			if (satisfied != null)
				CompleteSatisfiedAsyncAcquires(satisfied);
			
			// Dispose the resources associated with the cancelled async acquire
			acquire.Dispose(canceling);
			
			// Complete the TaskCompletionSource to RanToCompletion with false (timeout)
			// or Canceled final state (cancellation).
			if (canceling)
            	acquire.SetCanceled();		// cancelled
			else
				acquire.SetResult(false);	// timeout
        }
	}
```
		
- Depois de adquirir a posse do _lock_, este método começa por testar se o _async acquire_ já foi completado ou cancelado, isto é, se o campo `AsyncAcquire.done` é igual a `true`. Este método, que é invocado assincronamente relativamente aos metodos `AcquireAsync`, `Release` ou ao outro _cancellation handler_, pode encontrar o respectivo _async acquire_ já satisfeito ou cancelado, situação em que não há nada para fazer.

- Se o _async acquire_ ainda estiver activo, é removido da fila de espera e marcado como completo. Tendo em consideração à semântica das operações _acquire_ e _release_ adoptadas nesta implementação de semáforo, é possível que o cancelamento do _async acquire_ que se encontra à cabeça da fila de espera crie condições para satisfazer outros _async acquires_ que se encontrem a seguir na fila de espera. Assim, este método testa se existem _async acquires_ em fila de espera e, em caso afirmativo, testa se o número de autorizações disponíveis é suficiente para satisfazer o _async acquire_ que se encontra, agora, à cabeça fila; em caso afirmativo, é invocado o método `SatisfyPendingAsyncAcquires` para completar todos os _async acquires_ que possam ser satisfeitos como o número de autorizações correntemente disponíveis.

- Depois de libertar o _lock_, se o cancelamento teve sucesso, completa-se o respectivo processamento, nomedamente: completa-se as _tasks_ do _async acquires_ eventualmente satisfeitos; invoca-se o método `AsyncAcquire.Dispose` para libertar os recursos associados aos _cancellation handlers_ e, finalmente, completa-se a _task_ subjacente no estado `RanToCompletion` com o resultado `false` no caso do cancelamento ter sido por _timeout_ ou completa-se a _task_ no estado `Canceled` se o cancelamento foi feito por via do _cancellation token_.

- Para terminar a implementação do semáforo com interfaces assíncrona e síncrona, falta disutir a implementaçao da interface síncrona com base na interface assíncrona, o que vamos fazer a seguir.

- Primeiro vamos analisar o método auxiliar `TryCancelAcquireAsyncByTask` que é responsável por tentar cancelar um _async acquire_ a partir da respectiva _task_, cujo código se apresenta a seguir.

```C#
	/**
	 * Try to cancel an asynchronous acquire request identified by its task.
	 *
	 * Note: This method is needed to implement the synchronous interface.
	 */
	private bool TryCancelAcquireAsyncByTask(Task<bool> acquireTask) {
		AsyncAcquire acquire = null;
		List<AsyncAcquire> satisfied = null;
		// To access the shared mutable state we must acquire the lock
		lock(theLock) {
			foreach (AsyncAcquire _acquire in asyncAcquires) {
				if (_acquire.Task == acquireTask) {
					acquire = _acquire;
					asyncAcquires.Remove(_acquire);
					acquire.done = true;
					if (asyncAcquires.Count > 0 && permits >= asyncAcquires.First.Value.acquires)
						satisfied = SatisfyPendingAsyncAcquires();
					break;
				}
			}
		}
		// If we canceled the async acquire, process the cancellation
		if (acquire != null) {
			// After release the lock, complete any satisfied acquires
			if (satisfied != null)
				CompleteSatisfiedAsyncAcquires(satisfied);
			
			// Dispose the resources associated with this async acquire and complete
			// its task to the Canceled state.
			acquire.Dispose();
			acquire.SetCanceled();
			return true;
		}
		return false;
	}
```

- Este método começa por adquirir o _lock_ e depois faz uma pesquisa sequencial na fila de espera para ver localizar o _async acquire_ que tem subjacente a _task_ passada como argumento. Se o _async acquire_ for localizado, é realizado o processamento normal de cancelamento, já descrito no método anterior, e o método devolve `true`; se o _async acquire_ já não estiver na fila de espera (porque já foi completado ou cancelado), não é feito nenhum processamento e o método devolve `false`.

- O método `Acquire` permite adquirir autorizações do semáforo com interface síncrona sendo o código apresentado a seguir:

```C#
    /**
	 * Acquire one or multiple permits synchronously, enabling, optionally,
	 * a timeout and/or cancellation.
	 */
	public bool Acquire(int acquires = 1, int timeout = Timeout.Infinite,
						CancellationToken cToken = default(CancellationToken)) {
		Task<bool> acquireTask = AcquireAsync(acquires, timeout, cToken); 
		try {
			return acquireTask.Result;
		} catch (ThreadInterruptedException) {
			/**
			 * The acquirer thread was interrupted while waiting for task completion!
			 * Try to cancel the async acquire operation.
			 * Whether the cancellation was successful, throw interrupted exception.
			 */
			if (TryCancelAcquireAsyncByTask(acquireTask))
				throw;		// throw interrupted exception
			
			/**
			 * Here we known that the async acquire was already completed or cancelled.
			 * So we must return the underlying result, ignoring possible interrupts,
			 * while wait for task completion.
			 */
			try {
				do {
					try {
						return acquireTask.Result;
					} catch (ThreadInterruptedException) {
						// ignore interrupts while waiting fro task's result
					} catch (AggregateException ae) {
                		throw ae.InnerException;
					}
				} while (true);
			} finally {
				// Anyway re-assert first interrupt on the current thead.
				Thread.CurrentThread.Interrupt();
			}
		} catch (AggregateException ae) {
			// The acquire thrown an exception, propagate it synchronously
			throw ae.InnerException;
		}
	}

```

- Este método começa por invocar o método `AcquireAsync` para realizar a operação _acquire_ assincronamente e armazena em `acquireTask` a _task_ que representa a operação assíncrona. A seguir é invocada a propriedade `Task<bool>.Result`, que aguardar que a operação assíncrona termine e devolve o respectivo resultado, que será o valor de retorno do método `Acquire`.
	
- A maior parte do código deste método é para tratar a circunstância da _thread_ corrente poder ser interrompida enquanto está bloqueada a aguardar a conclusão da operação assíncrona. Por isso o acesso à propriedade `Task<bool>.Result` está dentro de um bloco _try_ que especifica _exception handlers_ para as excepções `ThreadInterruptedExecption` e `AggregateException`.

- Se o acesso à propriedade `Task<bool>.Result` lançar a excepção `AggregateException`, isso indica que foi especificado um número de autorizações menor do que um ou superior ao número máximo de autorizações que o semáforo pode ter sob sua custódia. Nesta situação, o método `AcquireAsync` lança uma `AggregateException` que envolve a excepção `ArgumentException`. Assim, em termos genéricos, o método síncrono implementado por este padrão de desenho deverá propagar a excepção que representada pela propriedade `AggregateException.InnerException`.

- Se a _thread_ que invoca o método `Acquire` for interrompida enquando aguarda a conclusão da operação assíncrona, o acesso à propriedade `Task<bool>.Result` lança a excepção `ThreadInterruptedException`. Como resposta, é invocado o método `TryCancelAcquireAsyncByTask` para tentar cancelar a operação _acquire_ assíncrona. Se a operação assíncrona for cancelada com sucesso, o método `Acquire` termina, lançando `ThreadInterruptedException`. No caso contrário, é necessário aguardar incondicionalmente pelo resultado da operação _acquire_ assíncrona que poderá ser qualquer um dos resultados possíveis; antes de devolver esse resultado a _thread_ que chamou o método `Acquire` deve garantir que a interrupção não se perde, por isso é invocado o método `Thread.Interrupt` sobre  `Thread.CurrentThread`.
 
## Implementação de Sincronizadores com Interface Assíncrona em _Java_
 
- A implementação em _Java_ de sincronizadores com interface assíncrona segue o padrão de desenho apresentado para o .NET _Framework_, com algumas adaptações, nomeadamente:

	- Em _Java_, o tipo `java.util.concurrent.CompletableFuture<T>` oferece funcionalidade equivalente aos tipos `System.Threading.Tasks.Task`, `System.Threading.Task<TResult>` e `System.Threading.TaskCompletionSource<TResult> do .NET _Framework_.
	
	- O _Java_ não define nenhum enquadramento para implementar o cancelamento de operações assíncronas, como acontece no .NET _Framework_. Assim foi acrescentada à interface assíncrona um método para accionar o cancelamento - `tryCancelXxxAsync` - que recebe como argumento a instância do `CompletableFuture<T>` que representa a operação assíncrona.
	
	- Para suportar a funcionalidade de _timeout_ em _Java_ não foi usada a classe `java.util.Timer`, porque a respectiva implementação cria uma _background thread_ por cada instância da classe `Timer`. Na nossa implementação, usamos uma instância da classe `java.util.concurrent.ScheduledThreadPoolExecutor`, configurada com uma única _worker thread_, para suportar todos os _timers_. (A classe `System.Threading.Timer`, do .NET _Framework_, tem uma implementação semelhante.)
	

### Implementação de um Semáforo com Interface Assíncrona em _Java_

- Começamos por apresentar a classe `Delayer` que vai ser usada para implementar _timers_, disponível no ficheiro [Delayer.java](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/synchs-async/Delayer.java) e cujo código é o seguinte:

```Java

import java.util.concurrent.*;

/**
 * This class supports one-shot timers
 */
public final class Delayer {
	
	/**
	 * Thread factory used to create the daemon worker thread that
	 * the timer's callbacks
	 */
    private static final class DaemonThreadFactory implements ThreadFactory {
        public Thread newThread(Runnable runnable) {
            Thread worker = new Thread(runnable);
            worker.setDaemon(true);
            worker.setName("AsyncDelayScheduler");
            return worker;
        }
    }
	
	// The scheduled thread pool executor
    private static final ScheduledThreadPoolExecutor delayer;
    
	// Static initializer
    static {
        (delayer = new ScheduledThreadPoolExecutor(1, new DaemonThreadFactory())).
                            setRemoveOnCancelPolicy(true);
    }

	/**
	 * Starts a timer sthat fires after the specified delay
	 */
    public static ScheduledFuture<?> delay(Runnable command, long delay, TimeUnit unit) {
        return delayer.schedule(command, delay, unit);
    }
}

```

- Esta classe implementa _timers_ e usa a classe `java.util.concurrent.ThreadPoolExecutor` cujo método `schedule` faz o agendamento de `Runnable` que executam após decorrer o tempo especificado como argumento. Este método devolve um objecto que implementa a interface `java.util.concurrent.ScheduledFuture<V>. Esta interface define métodos que permitem, por exemplo, cancelar o _timer_ ou obter o tempo que falta para o _timer_ expirar.
	
- Foi definida uma classe que implementa a interface `java.util.concurrent.ThreadFactory` para definir o método de fabrico (`newThread`) da _worker thread_ usado pelo _scheduled thread pool executor_, de modo a configurar estas _threads_ como _daemon_ para que as aplicações possam terminar mesmo que estas _threads_ estejam activas.

- Todos os métodos da classe `Delayer` são estáticos, pelo que a criação do _scheduled thread pool executor_ num inicializador estatico. O _scheduled thread pool executor_ é configurado com uma _worker thread_, com a nossa implementação de `ThreadFactory` e configurado para remover os _timers_ imediatamente da fila no momento do cancelamento.

- A seguir apresentamos a definição da classe `SempahoreAsync` e da classe `AsyncAcquire` usada para representar os _async acquires_ pendentes assim como os restantes campos que definem o estado do semáforo.

```Java
public class SemaphoreAsync {

	/**
	 * The type used to represent each pending async acquire
	 */
	private class AsyncAcquire extends CompletableFuture<Boolean> implements Runnable {
		final int acquires; 			// the number of the requested permits
		ScheduledFuture<?> timer;		// timeout's timer
		boolean done;					// true when the async request was completed or cancelled
		
		/**
		 * Construct a acquire request object
		 */
		AsyncAcquire(int acquires) {
			this.acquires = acquires;
		}

		/**
		 * This is the timeout cancellation handler
		 */
		 @Override
		public void run() {
			List<AsyncAcquire> satisfied = null;
			boolean complete = false;
			synchronized(theLock) {
				if (!done) {
					asyncAcquires.remove(this);
					complete = done = true;
					if (asyncAcquires.size() > 0 && permits >= asyncAcquires.peek().acquires)
						satisfied = satisfyPendingAsyncAcquires();
				}
			}
			if (complete) {
				// Complete previously satisfied requests
				completeSatisfiedAsyncAcquires(satisfied);
				// Complete this completable future with false, indicating timeout
				complete(false);
			}
		}

		/**
		 * Disposes the resources associated with the async acquire
		 */
		void close() {
			if (timer != null)
				timer.cancel(false);
		}
	} 
	
	// The lock that synchronizes access to the shared mutable state
 	private final Object theLock;

	// The available number of permits	
	private int permits;

	// The maximum number of permits
	private final int maxPermits;

	// The queue of pending asynchronous acquires
	private final LinkedList<AsyncAcquire> asyncAcquires;
```

- A definição da classe `AsyncAcquire` é semelhante ao que fizemos para o .NET _Framework_, com a excepção de que existem campos relacionados com o cancelamento e de ser conveniente definir nesta classe o _timeout cancellable handler_. A lógica implementada por este _handler_ é exactamente igual ao que fizemos anteriormente para o .NET.

- O método `AsyncAcquire.close` cancela o _timer_ quando existe um definido e o _async acquire_ é satisfeiro ou explicitamente cancelado.

- Também nesta implementação foi conveninte definir instâncias do tipo `CompletableFuture<Boolean` para devolver os resultados constantes, com o seguinte código:

```Java
	//  Completed futures used to return true and false results and IllegalArgumentException
    private static final CompletableFuture<Boolean> trueFuture = CompletableFuture.completedFuture(true);
	private static final CompletableFuture<Boolean> falseFuture = CompletableFuture.completedFuture(false);
	private static final CompletableFuture<Boolean> illegalArgExceptionFuture =
			 		CompletableFuture.failedFuture(new IllegalArgumentException("acquires"));

```

- Os contrutores do semáforo e os métodos `satisfyPendingAsyncAcquires` e `completeSatisfiedAsyncAcquires` é exactamente igual à que foi explicada para a implementação usando o .NET _Framework_.

- Devido ao facto de em _Java_ não estar definido um _cancellation framework_, foi acrescentada à interface pública um método que permite cancelar um _async acquire_ pendente, cujo código se apresenta a seguir.

```Java
	/**
	 * Try to cancel an asynchronous request identified by the underlying
	 * completable future.
	 * 
	 * Note: This method is used to cancel asynchronous acquires and also to
	 *       implement the synchronous interface.
	 */
	public boolean tryCancelAcquireAsync(CompletableFuture<Boolean> acquireFuture) {
		AsyncAcquire acquirer = (acquireFuture instanceof AsyncAcquire) ? (AsyncAcquire)acquireFuture : null;
		List<AsyncAcquire> satisfied = null;
		boolean complete = false;
		if (acquirer == null)
			throw new IllegalArgumentException("acquireFuture");
		synchronized(theLock) {
			if (!acquirer.done) {
				asyncAcquires.remove(acquirer);	// no operation if object is not in the list
				complete = acquirer.done = true;
				// Check to see if now, we ca satisfy other pending async acquires
				if (asyncAcquires.size() > 0 && permits >= asyncAcquires.peek().acquires)
					satisfied = satisfyPendingAsyncAcquires();
			}
		}
		if (complete) {
			// Complete the CompletableFutures of satisfied requests
			completeSatisfiedAsyncAcquires(satisfied);
			
			// Dispose resoure and complete completable future exceptionally
			acquirer.close();
			acquirer.completeExceptionally(new CancellationException());
			return true;
		}
		return false;
	}
```

- A lógica deste método é semelhante à que implementámos anteriormente para o .NET sem haver a necessidade de pesquisar a fila de espera para localizar a respectiva instância do `AsyncAcquire`; como a classe `AsyncAcquire` estende a classe `CompletableFuture<Boolean>` basta fazer uma coerção para obter o `CompletableFuture<>`.
	
- O método `doAcquireAsync` é o método que implementa a funcinalidade base do _async acquire_ e o respectivo código é o seguinte:

```Java
	/**
	 * Base method to acquire asyncronously the specified number of permits
	 * enabling, optionally, the timeout.
	 */
	private CompletableFuture<Boolean> doAcquireAsync(int acquires, boolean timed, long timeout, TimeUnit unit) {
		// Validate  the argument acquires
		if (acquires < 1 || acquires > maxPermits)
			return illegalArgExceptionFuture;
				
		synchronized(theLock) {
			// If the queue is and there are sufficient permits, update the
			// permits a returna a CompletableFuture<> completed with true.
			if (asyncAcquires.size() == 0 && permits >= acquires) {
				permits -= acquires;
				return trueFuture;
			}
			// The current request must be pending, so we must check for immediate timeout
			if (timed && timeout == 0)
				return falseFuture;

			// Create an async acquire object and insert it in the pending queue
			AsyncAcquire acquirer = new AsyncAcquire(acquires);
			asyncAcquires.addLast(acquirer);

			/**
			 * If a timeout was specified, start a timer.
			 * Since that all paths of code that cancel the timer execute on other
			 * threads and must aquire the lock, we has the guarantee that the field
			 * "acquirer.timer" is correctly set when the method AsyncAcquire.close()
			 * is called.
			 */
			if (timed)
				acquirer.timer = Delayer.delay(acquirer, timeout, unit);
			return acquirer;
		}
	}
```

- Este método é mais simples que o que apresentámos nas implementação para o .NET _Framework_ porque apenas é suportado o cancelamento por _timeout_. Como implementação dos _timers_ é feita na classe `Delayer`, definida anteriormente, sabemos que os _cancellation handlers_ executam sempre na _worker thread_ do _scheduled thread pool executor_, pelo que o _lock_ dá nas necessárias garantias de visibilidade e atomicidade no acesso ao estado partilhado mutável. 

- O método `release` permite devolver autorizações ao semáfor e o código é o seguinte:

```Java
	/**
	 * Release the specified number of permits.
	 */
	public void release(int releases) {
		List<AsyncAcquire> satisfied = null;
		synchronized(theLock) {
			if (releases + permits < permits || permits + releases > maxPermits)
				throw new IllegalStateException("Exceeded the maximum number of permits");
			permits += releases;
			satisfied = satisfyPendingAsyncAcquires();
		}
		// After we release the lock do the cleanup and complete the released
		// CompletableFutures.
		// We do this after release the lock to prevent reentrancy when synchronous
		// continuations are executed.
		completeSatisfiedAsyncAcquires(satisfied);
	}
	
```

- A implementação deste método é exactamente igual à que apresentámos anteriormente para o .NET _Framework_.

- 
 
	
____

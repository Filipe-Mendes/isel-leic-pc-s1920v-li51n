# Aula 25 - Sincronização em Programação Assíncrona/Sincronizadores com Interface Assíncrona (I)

___

## Sumário

- Sincronização em programação assíncrona: sincronização no acesso a dados partilhados mutáveis e sincronização de controlo.

- Mecanismos usados nos dois tipos de sincronização: _locks_ e instruções atómicas no acesso a dados partilhados mutáveis; sincronizadores com interface assíncrona na sincronização de controlo ou mecanismos de comunicação.

- Necessidade de suportar cancelamento em sincronizadores com interface assíncrona; problemas que o cancelamento assíncrona levanta na implementação, comparando com as implementações baseadas em monitor onde as acções de cancelamento (timeout e interrupção) sendo de origem assíncrona eram passíveis de serem sincronizadas usando o _lock_ do monitor.

- Padrão de desenho para implementar sincronizadores com interface assíncrona no .NET _Framework_ e no _Java_.


## Sincronização em Programação Assíncrona

- A ideia de sincronizar a execução de _theads_ cooperantes está associada à programação síncrona. O seu objectivo é o definir pontos de sincronização em que as _threads_ se bloqueiam quando não estão reunidas as condições para que a sua execução pudesse prosseguir; por exemplo, o processamento dos dados obtidos com uma operação de I/O deve bloquear a _thread_ corrente após lançar a operação de I/O até que a mesma esteja concluída. A necessidade de sincronização é determinada pela necessidade de garantir as pré-condições necessárias para executar determinados processamentos, não pela necessidade de bloquear _thread_ que executa uma determinada sequência de acções num fio de processamento autónomo; o bloqueio das _threads_ é um **meio** para condicionar a execução de cada átomo de processamento às necessárias pré-condições.  

- Em programação assíncrona não existem _threads_ associadas a cada uma das sequências de processamento que decorrem em paralelo nas nossas aplicações. Contudo, as acções em cada sequência de processamento também estão, eventualmente, condicionadas por pré-condições. Assim, ainda que **não seja relevante saber qual a _thread_ que executa o quê** é necessario que sejam asseguradas as pré-condiçoes que condicionam as sucessivas acções de uma determinada sequência de processamento.

- Na sincronização no acesso a dados partilhados mutáveis, onde o bloqueio de _threads_ só ocorre quando existe simultaneidade no acesso aos dados partilhados, consideramos que a utilização de _locks_ com interface síncrona (bloqueante) é razoável mesmo em programação assícrona, dado que os períodos de bloqueio são, em regra, pouco frequentes e breves, pelo que o desempenho é globalmente melhor do recorrer a _locks_ com uma interface assíncrona para evitar, de todo, o bloqueio das _worker threads_. 

- Na sincronização de controlo, não é possível, à partida, prever ou limitar o tempo que as _threads_ poderão ficar bloqueadas quando são utilizados sincronizadores com interface síncrona. Por isso, a alternativa é **utilizar sincronizadores com interfaces assíncronas**, onde a **espera pelas condições de sincronização se faz sem bloquear as _worker threads_**. 


## Implementação de Sincronizadores com Interface Assíncrona

- Na implementação se sincronizadores com interface assíncrona vamos usar o modelo _Task-based Asynchronous Pattern_ (TAP). As operações de sincronização com semântica _acquire_ retornam de imediato devolvendo uma _task_ que representa a operação em curso. Depois, o código invocante utiliza uma das técnicas da programação assíncrona - agendamento de continuações ou suspensão da execução quando se utilizam métodos assíncronos - para prosseguir o processamento após ser obtida a sincronização.

- Dado a ausência de afinidade de cada atómo de processamento das aplicações assíncronas às _threads_ que o executa, é considerado conveniente que as operações de sincronização com semântica _acquire_ suportem o cancelamento. Assim, será sempre possível agir sobre as operações assíncronas em curso não só para observar o seu progresso ou resultado (o que é feita usando a _task_ subjacente), mas também proceder ao respectivo cancelamento quando a lógica das aplicações assim o determinar. Quando implementarmos sincronizadores com interface assíncrona no .NET _Framework_, vamos suportar o cancelamento das operações assíncronas por _timeout_ e, directamente, usando o .NET _cancellation framework_.

- Em versões recentes do NET _Framework_, existe um sincronizador que suporta interface assíncrona na operação _acquire_ que é implementado pela classe `System.Threading.SemaphoreSlim`. Esta implementação do semáforo define operações _acquire_ com interface síncrona (método `SemaphoreSlim.Wait`) e com interface assíncrona (método `SemaphoreSlim.WaitAsync`). No padrão de desenho que abordaremos adiante vamos também suportar os dois tipos de interfaces, mas implementando a interface síncrona usando a interface assíncrona, o que não acontece na implementação do `SemaphoreSlim` do .NET _Framework_. 

- A implementação de sincronizadores com interface assíncrona segue o estilo que anteriormente designámos por "estilo _kernel_". A diferença é que não existe uma _thread_ bloqueada por cada operação com semântica _acquire_ pendente. Em vez de ter uma _thread_ bloqueada numa variável condição de um monitor que será notificada quando a operação for concluída ou cancelada, temos uma _task_ que será completada quando a operação for concluída ou cancelada.

- O padrão de desenho que vamos apresentar a seguir é tem uma estrutura semelhante à implementação de sincronizadores ao "estilo _kernel_", nomeadamente:

	- Toda a acesso ao estado partilhado mutável é protegida por um _lock_;
	
	- Cada operação assíncrona pendente é representada por um objecto _asynchronous request_ inserido numa fila de espera; estes objectos têm associada uma instância do tipo `TaskCompletionSource<TResult>` que controla a _task_ que representa que representa a operação assíncrona em curso; este tipo de objecto deverá ter um campo com os argumentos da operação assícrona se existir (e.g., no sémaforo haverá um campo com o número de autorizações solicitadas); o resultado da operação será naturalmente obtido com a propriedade `Task.Result`;
	
	- O cancelamento por _timeout_ ou por via do _cancellation token_, pode ocorrer em simultâneo com a conclusão normal do pedido assíncrono ou mesmo com o cancelamento por parte da outra forma de cancelamento. Assim, antes de proceder ao cancelamento, removendo o _asynchronous request_ da fila de espera é necessário testar se o pedido já foi satisfeito ou cancelado. Na implementação de sincronizadores com base em monitor ao "estilo kernel", também podia haver uma _race_ entre a conclusão normal da operação _acquire_ e a detecção por parte da _thread_ invocante de que ocorreu uma condição de cancelamento (retorno do método `Monitor.Wait` por _timeout_ ou por interrupção). Assim, qualquer que fosse a condição de saída do método `Monitor.Wait`, era sempre testado o campo `done` do objecto _request_ para determinar se a operação tinha sido concluída normalmente e, em caso afirmativo, ignorava-se o cancelamento da operação _acquire_;

	- Enquanto que na implementação de interfaces síncronas há uma _thread_ associada a cada operação _acquire_ pendente, a indicação de conclusão da operação ou o accionamento do seu cancelamento é sempre comunicada à _thread_ quando esta se encontra bloqueada numa variável condição do monitor; a própria implementação do monitor garante que o retorno do método `Monitor.Wait` já feito na posse do _lock_ do monitor, portanto podemos dizer que o processamento do cancelamento é centralizado e síncrono. Na implementação de interfaces assíncronas, não existe nenhum _thread_ associada às operações _acquire_ pendentes, pelo que os eventuais canceladores (_timer_ e _callback_ registado na instância de `CancellationToken`) executam assíncronamente no contexto de _threads_ arbitrárias e pode executar mais do que um cancelador simultaneamente e ainda em simultâneo com uma operação _realese_. Assim, será necessário adquirir a posse do _lock_ em todos os caminhos de código que vão aceder ao objecto que decreve a operação _acquire_ assíncrona.
	
	- Um aspecto importante a ter em consideração, na implementação destes sincronizadores, é saber em que _thread_ são executados os _callbacks_ dos canceladores. Este aspecto é importante para se determinar que garantias de exclusão nos são dadas pelo _lock_ ou mesmo determinar se é possível ocorrerem situações de _deadlock_.
	
	- No .NET _Framework_, sabemos que o _callback_ associado a uma instância do tipo `System.Threading.Timer` executa numa _worker thread_ do _thread pool_. Assim, mesmo que o _timer_ seja criado por uma _thread_ que tem a posse do _lock_ (como veremos adiante é o caso), o _lock_ garante que o _callback_ bloqueia a _worker thread_ enquanto o _lock_ não for libertado por parte da _thread_ que lançou o _timer_. Assim, existe a garantia de que o estado partilhado mutável fixado antes de libertar o _lock_ é visto pela _thread_ que executa o _callback_ do _timer_ depois desta adqurir a posse do _lock_. Assim, existe a garantia de visbilidade e atomicidade no acesso ao estado partilhado mutável e não existe a possibilidade de ocorrerem situações de _deadlock_.
	
	- Relativamente aos _callbacks_ registados em instâncias do tipo `CancellationToken`, sabemos que o .NET _Framework_ os executa: (a) quando o cancelamento já foi accionado, o _callback_ executa sincronamente na _thread_ que invoca o método `CancellationToken.Register`; (b) se o cancelamento ainda não foi accionado, o _callback_ executará mais tarde pela _thread_ que invocar o método `CancellationTokenSource.Cancel`. Esta execução síncrona tem que ser analisada com cuidado, dado que os _locks_ que vamos utilizar no .NET _Framework_ e em _Java_ suportam a acquisição recursiva por parte da _thread_ que já detém a posse do _lock_. Assim, sendo o _cancellation handler_ registado na posse do _lock_ quando a execução é síncrona, a acquisição do _lock_ no início do _cancellation handler_ não bloqueia a _thread_ invocante e, se bloqueasse, ocorreria _deadlock_. A solução para resolver este tipo de problema, passa por: (a) fazer o registo do _cancellation handler_ como operação terminal da secção crítica, imediatamente antes da libertação do _lock_, e; (b) não haver nenhum código no _cancellation handler_ que dependa de alterações ao estado partilhado mutável que sejam feitas após o retorno da chamada ao método `CancellationToken.Register`. Pelo facto do _cancellation handler_ executar na mesma _thread_ que alterou o estado partilhado mutável utilizado pelo _cancellation handler_, existem totais garantias de visibilidade dessas alterações. No .NET _Framework_ existem condições para aplicar esta abordagem ao registo e processamento de _cancellation handlers_, sendo posssível libertar imediatamente todos os recursos alocados em caso de cancelamente. Adiante, quando fizermos a análise do código do `SemaphoreAsync` explicaremos os detalhes da implementação.
	
- Vamos explicar a implementação segundo o padrão de desenho com base no ficheiro [SemaphoreAsync.cs]() que vamos analisar por excertos.

- Começamos com o início da definição do tipo `SemaphoreAsync`, a definição do tipo de dados que armazena cada _asynchronous request_, assim como os outros elementos do estado partilhado.

```C#
public class SemaphoreAsync {
	// The type used to represent each asynchronous acquire request
	private class AsyncAcquire: TaskCompletionSource<bool> {
		internal readonly int acquires;
		internal readonly CancellationToken cToken;
		internal CancellationTokenRegistration cTokenRegistration;
		internal Timer timer;
		internal bool done;		// true when the async request is completed or cancelled
		
		internal AsyncAcquire(int acquires, CancellationToken cToken) : base() {
			this.acquires = acquires;
			this.cToken = cToken;
		}

		/**
		 * Disposes the resources associated with this async acquire.
		 *
		 * Note: when this method is called we are sure that the fields "timer"
		 *       and "cTokenRegistration" are correctly affected
		 */
		internal void Dispose(bool canceling = false) {
			// The CancellationTokenRegistration is disposed off after the
			// cancellation handler is called.
			if (!canceling && cToken.CanBeCanceled)
				cTokenRegistration.Dispose();
			timer?.Dispose();
		}
	}
	
	// The lock - we do not use the monitor functionality
	private readonly object theLock = new object();

	// Available and maximum number of permits	
	private int permits;
	private readonly int maxPermits;

	// The queue of pending asynchronous requests
	private readonly LinkedList<AsyncAcquire> asyncAcquires;
	
```

- O tipo `AsyncAcquire` tem um campo para armazenar o número de autorizações solicitadas (`acquires`), um campo para armazenar o _cancellation token_ (´cToken´), um campo para armazenar a instância do tipo `Timer` (`timer`) que está definida no caso de ter sido especificado um _timeout_ e, por último, um campo que indica se o pedido assíncrono já foi completado ou cancelado (`done`). As instâncias deste tipo são inseridas numa _asynchronoues request queue_ e são acedidas na posse do _lock_ global. 

- O acesso ao estado partilhado mutável é protegido pelo _lock_ do monitor implícito associado ao objecto `theLock`. O número de autorizações disponíveis está armazenado no campo `permits` e o número máximo de autorizações sob custódia do semaforo está armazenado no campo imutável `maxPermits`. Além disso, o campo `asyncAcquires` contém os pedidos de _acquire_ assíncronos pendentes armazenados por ordem de chegada.

- A seguir, declaramos os _delegates_ usados para armazenar os _cancellation handlers_ e duas instâncias do tipo `Task<bool>` já concluídas para devolver os resultado `true` e `false`. 

```C#
	/**
	 * Delegates used as cancellation handlers for asynchrounous requests 
	 */
	private readonly Action<object> cancellationHandler;
	private readonly TimerCallback timeoutHandler;
	
	/**
	 *  Completed tasks use to return true and false results
	 */
	private static readonly Task<bool> trueTask = Task.FromResult<bool>(true);
	private static readonly Task<bool> falseTask = Task.FromResult<bool>(false);
    
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

- Para além da validação dos argumentos o construtor inicia os _delegates_ que implementam os _cancellation handlers_ para _timeout_ e cancelamento explícito. Estes _delegates_ aceitam como argumento uma instância do tipo `LinkedLisNode<AsyncAcquire>` e invocam o mesmo método `AcquireCancellatioHandler` passando para além do nó da lista um _boolean_ que indica se foi accionado o cancelamento directo ou cancelamento por _timeout_. A seguir, armazena o número máximo de autorizações num campo imutável (`maxPermits`). Por último inicializa a fila de pedidos de _acquire_ assícronos pendentes (`asyncAcquire`) e o número inicial de autorizações disponíveis (`permits`).
	
 
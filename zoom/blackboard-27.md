# Aula 27 - Implementação de Servidores TCP Escaláveis
___

## Sumário

- Desenho de servidores TCP _multithreaded_ escaláveis no .NET framework, com os seguintes requisitos: (i) limitação por desenho do número máximo de ligações com os clientes abertas em cada momento; (ii) _shutdown_ gracioso desencadeado localmente; (iii) funcionalidade de registo de relatórios com base numa _thread(s)_ de baixa prioridade.

- Sugestões para definir a estrutura da implementação de uma _transfer queue_ com interface assíncrona, que é necessário na resolução da terceira série de exercícios.

## .NET _Core_

- O .NET _Core_ é uma versão _cross-platform_ do .NET _Framework_ que permite desenvolver _websites_, serviços e aplicações consola nas plataformas _Linux_, _macOS e _Windows_. Está disponível para _download_ em [.NET _Core_](https://dotnet.microsoft.com/download). Nos exemplos desta aula utilizamos o .NET _Core_.

## Servidores TCP Escaláveis

- Usando o .NET _Framework_ e os métodos assíncronos do C# torna-se simples desenhar um servidor TCP escalável, qualquer que seja a sua funcionalidade. Se a implementação nunca bloquear as _worker threads_ - o que pode ser conseguido usando as interfaces assíncronas das operações de I/O e sincronizadore com interface assíncrona (como por exemplo, o `System.Threading.SemaphoreSlim` ou o `SemaphoreAsync`) - existe a garantia de o _thread pool_ poderá sempre fazer uma gestão optimizada dos recursos disponíveis no sistema, nomeadamente, dos processadores e da memória, para optimizar o _throughput_ medido em número normalizado de _work items_ executados por unidade de tempo.

- No desenho que vamos apresentar, utilizamos _locks_ com interface síncrona na sincroniação no acesso aos dados partilhados mutáveis, mas dado que a contenção expectável é baixa, consideramos que isso não compromete a escalabilidade.


### Servidor TCP de Eco

- No ficheiro [server.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tcp/echo-raw/server/server.cs) consta uma implementação de um servidor de eco escalável (usando um _byte stream_ na comunicação), cujos aspectos relevantes iremos explicar a seguir.

- O comunicação cliente/servidor via TCP é feitas através de uma abstração designada _socket_, que é suportada pelo sistema operativo. No protocolo TCP - protocolo orientado à ligação - são utilizados duas configurações distintas da abstração _socket_ e implementados no .NET _Framework_ pelos tipos `TcpListener` e `TcpClient`definidos no _namespace_ `System.Net.Sockets`. Como o próprio nome indica, o tipo `TcpListener` é usado pelos servidores e tem subjacente um _socket_ configurado para receber pedidos de ligação dos clientes num dado porto da máquina. Depois de ser estabelecida uma ligação entre o cliente e o servidor, a comunicação sobre essa ligação é feita através de uma instância do tipo `TcpClient` que tem subjecente um _socket_ configurado para comunicação bidirecional entre cliente e servidor, isto é: o que o cliente escrever no _socket_ é lido pelo servidor no respectivo _socket_; o que o servidor escrever no _socket_ é lido pelo cliente no respectivo _socket_; este canal de comunicação é encerrado quando o cliente e o servidos fecham as respectivas extremidades.

- No arranque, um servidor começa por criar uma instância do tipo `TcpListener` que é associada a um determinado porto. Depois, invoca o método `TcpListener.Start` para instruir o _socket_ para aceitar ligações dos clientes. A seguir, um servidor _single-threaded_ entrará num ciclo onde invoca o método `TcpListener.Accept` para aceitar a ligação de um cliente; aquele método retorna uma instância do tipo `TcpClient` que o servidor utilizará posteriormente para comunicar com o cliente até que ambos decidam fechar a ligação. Após ser fechada uma ligação, o servidor _single-threaded_ voltará a chamar o método `TcpListener.Accept` para aceitar uma nova ligação, e assim sucessivamente. Num servidor _multi-threaded_, como aquele que vamos discutir, são mantidas activas várias ligações com clientes sendo o respectivo processamento feito em paralelo por várias _worker threads_.

- O processamento no cliente é mais simples. Por cada ligação ao servidor, o cliente cria uma instância do tipo `TcpClient` e depois invoca o método `TcpClient.Connect` para ligar o _socket_ ao servidor. Após o retorno daquele método, fica com um canal de comunicação bidirecional com o servidor até que ambos decidam fechar o _socket_.

- O código que apresentamos a seguir mostra a definição da classe `TcpMultiThreadedTapEchoServer`, a definição de algumas constantes, os vários campos desta classe e o respectivo construtor.

```C#
/**
 * A Tcp multithreaded echo server, using TAP interfaces and C# asynchronous methods.
 */
public class TcpMultiThreadedTapEchoServer {
	private const int SERVER_PORT = 13000;
	private const int BUFFER_SIZE = 1024;
	private const int MIN_SERVICE_TIME = 50;
	private const int MAX_SERVICE_TIME = 500;

	// The maximum number of simultaneous connections allowed.
	private const int MAX_SIMULTANEOUS_CONNECTIONS = 20;

	// Constants used when we poll to detect that the server is idle.
	private const int WAIT_FOR_IDLE_TIME = 10000;
	private const int POLLING_INTERVAL = WAIT_FOR_IDLE_TIME / 20;

	// A random generator private to each thread.
	private ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));
	
	// The listen server socket
	private TcpListener server;
	
	// Total number of connection requests.
	private volatile int requestCount = 0;
	
	// Construct the server
	public TcpMultiThreadedTapEchoServer() {
    	// Create a listen socket and bind it to the server port in the localhos IP address.
    	server = new TcpListener(IPAddress.Loopback, SERVER_PORT);
		
		// Start listen connections from listen socket.
		server.Start();
	}
````

- Não há assim muito a dizer sobre este código, face ao que já dissemos atrás. Um aspecto interessante é a existência de do campo `random` do tipo `ThreadLocal<Random>` que armazena uma instância do tipo `Random` que é confinado a cada _thread_ dado que a implementação deste tipo não é _thread-safe_. A iniciação do gerador de números pseudo-aleatórios é feitos com diferentes sementes (o valor da propriedade `Thread.ManagedThreadId` da respectiva _thread_) para assegurar que as sequência de número pseudo-aleatórios geradas por _threads_ diferentes são efectivamente diferentes. Este gerador de número pseudo-aleatórios é usado para simular tempos de serviços aleatórios compreendidos entre `MIN_SERVICE_TIME` e `MAX_SERVICE_TIME - 1` milésimos de segundo.
	
- O construtor desta classe cria a instância do _socket_ `TcpListener`, associa-o ao porto `SERVER_PORT` e coloca o _socket_ a aceitar pedidos de ligação dos clientes.

- Neste estrutura de servidor TCP, o processamento de cada ligação é feito pelo método assíncrona `ServeConnectionAsync` que recebe como argumentos a instância de `TcpCliente` que vai usar para comunicar com o servidor e o `CancellationToken` através do qual poderá ser accionado o _shutdown_ do servidor. O código do método `ServeConnectAsync` é o seguinte:

```C#
	/**
	 * Serves the connection represented by the specified TcpClient socket, using an asynchronous method.
	 * If we want to cancel the processing of all already accepted connections, we
	 * must propagate the received CancellationToken.
	*/
	private async Task ServeConnectionAsync(TcpClient connection,
											CancellationToken cToken = default(CancellationToken)) { 
		using (connection) {
			try {
				// Get a stream for reading and writing through the client socket.
				NetworkStream stream = connection.GetStream();
				byte[] requestBuffer = new byte[BUFFER_SIZE];
				
				// Receive the request (we know that its size is smaller than BUFFER_SIZE bytes);
				int bytesRead = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
				
				Stopwatch sw = Stopwatch.StartNew();

				// Convert the request content to ASCII and display it.
				string request = Encoding.ASCII.GetString(requestBuffer, 0, bytesRead);
				Console.WriteLine($"-->[{request}]");
				
				/**
				 * Simulate asynchronously a random service time, and after that, send the response
				 * to the client.
				 */
				await Task.Delay(random.Value.Next(MIN_SERVICE_TIME, MAX_SERVICE_TIME), cToken);
				
				string response = request.ToUpper();
				Console.WriteLine($"<--[{response}({sw.ElapsedMilliseconds} ms)]");
				
				// Convert the response to a byte array and send it to the client.
				byte[] responseBuffer = Encoding.ASCII.GetBytes(response);
				await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
				
				// Increment the number of processed requests.
				Interlocked.Increment(ref requestCount);
			} catch (Exception ex) {
				Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
			}
		}
	}
```

- Este método começa por obter a instância de `NetworkStream` que permita a comunicação bidirecional com o servidor através da instância de `TcpClient` subjacente à ligação. A seguir, converte o _byte stream_ para caracteres ASCII e mostra o pedido do cliente na consola. Depois, é invocado o método `Task.Delay` para suspender o método assíncrono durante o tempo especificado como argumento para simular um "tempo de serviço". Finalmente, os caracteres que contam do pedido do cliente são convertidos para letras maiúsculas, a respectiva _string_ é convertida para um _byte stream_ e a resposta é enviada ao cliente. O bloco `using` assegura o fecho da instância do tipo `TcpClient` usada na comunicação com o servidor.

- O método `ListenAsync` contém o ciclo onde o servidor aceitas as ligações dos clientes. Trata-se de um método assíncrono que retorna ao respectivo chamador assim que atingir o primeiro ponto de suspensão. É neste método que está a lógica que limita por desenho o número máximo de ligações simultâneas com os clientes. Quando este método assíncrono termina existe a garantia de que foram processadas todas as ligações dos clientes aceites pelo servidor. O código do método `ListenAsync` é o seguinte:

```C#
	/**
 	 * Asynchronous method that listens for connections and calls the ServeConnectionAsync method.
	 * This method limits, by design, the maximum number of simultaneous connections.
	 */
	public async Task ListenAsync(CancellationToken cToken) {
		// This set stores the set of all tasks launched but for which there is no
		// certainty that they have been completed.
		// Using this set we will limit the maximum number of active simultaneous
		// connections.
		var startedTasks = new HashSet<Task>();
		
		// Accept connections until the shutdown of the server is requested.
		while (!cToken.IsCancellationRequested) {
			try {
				var connection = await server.AcceptTcpClientAsync();
				
				/**
				 * Add the task returned by the ServeConnectionAsync method to the task set.
				 */
				startedTasks.Add(ServeConnectionAsync(connection, cToken));
				
				/**
				 * If the defined limit of connections was reached: (1) we start removing from the
				 * set all the already completed tasks; (2) if no tasl can be removed, await
				 * unconditionally until at least one of the active tasks complete its processing,
				 * and then remove it from the set.
				 */
				if (startedTasks.Count >= MAX_SIMULTANEOUS_CONNECTIONS) {
					if (startedTasks.RemoveWhere(task => task.IsCompleted) == 0)
						startedTasks.Remove(await Task.WhenAny(startedTasks));
				}
			} catch (ObjectDisposedException) {
				// benign exception - occurs when when stop accepting connections
			} catch (Exception ex) {
				Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
			}
		}
        
		/**
	 	 * Before return, wait for completion of tasks processing of all accepted
		 * connections.
		 */	
		if (startedTasks.Count > 0)
        	await Task.WhenAll(startedTasks);
	}
```

- O ciclo principal deste método, que se repete enquanto o `CancellationToken` não indicar cancelamento, mas também pela captura da excepção `ObjectDisposedException` que é lançada pelo método `TcpListener.AcceptTcpClientAsync` quando é invocado o método `TcpListener.Stop` para que o _listener socket_ deixe de aceitar ligações.

- O controlo do número máximo de ligações simultâneas é feito utilizando o conjunto `startedTasks`. De cada vez que é aceite uma ligação a _task_ devolvida pelo método `ServeConnectionAsync` é acrescentada ao conjunto. Depois, se o número de _tasks_ no conujunto tiver atingido o máximo de ligações simultâneas permitidas (`MAX_SIMULTANEOUS_CONNECTIONS`), começamos por remover os elementos do conjunto que armazenam _tasks_ que já terminaram, se o número máximo de ligações for real - da ordem dos milhtes- é natural que sejam muitas. Isto é um processamento cujo custo é porporcional ao número máximo de ligações simultâneas, mas uma vez que são removidas todas as _tasks_ que já terminaram este custo será amortecido pois tenderá a ser pouco frequente. Na situação improvável de nenhuma das _tasks_ do conjunto ter terminado, usamos o combinador `Task.WhenAny` para suspender o método incondicionalmente até que termine uma das _tasks_ pertencentes ao conjunto.

- Por fim, este método aguarda que todas as _tasks_ correspondentes às ligações dos clientes aceites pelo servidor tenham terminado. Quando este método assíncrono é concluído já não existe nenhuma actividade no servidor desencadeada por pedidos dos clientes.

- A limitação do número máximo de ligações com os clientes activas também pode ser implementado com base num semáforo com interface assíncrona (como `System.Threading.SemaphoreSlim` ou `SemaphoreAsync`). O semáforo será iniciado com o numéro um número de autorizações igual ao número máximo de ligações simultâneas; o método `ListenAsync` deve adquirir assincronamente uma autorização antes de invocar o método `TcpListen.AcceptClientAsync`; por outro lado, no fim do método `ServeConnectionAsync` seria devolvida uma autorização ao semáforo. Assim, quando fossem esgotadas as autorizações sob custódia do semáforo, o método `ListenAsync` será suspenso até que fosse fechada uma das ligações activas. Recomenda-se a solução aqui proposta em vez da solução baseada no semáforo com interface assíncrona, pelas seguintes razões: (a) a solução aqui proposta é mais eficiente dado que a instância do tipo `HashSet<Task>` usada no controlo é local ao método assíncrono `ListenAsync` e, por isso, é acedida sem o _overhead_ de sincronização, como acontece se for usado uma semáforo; (b) o código que limita o número máximo de ligações fica centralizado no método `ListenAsync` e em vez de ficar distribuído pelos métodos `ListenAsync` e `ServeConnctionAsync`; (c) para aguardar que todas as ligações sejam fechadas para concluir o _shutdown_ e mais natural usar o _task combinator_ `Task.WhenAll` do que fazer o mesmo tipo de sincronização usando o semáforo, isto é, teria que se aguardar até que todas as autorizações fossem devolvidas ao semáforo (com `await semphoreAsync.AsyncAcquire(MAX_SIMULTANEOUS_CONNECTIONS)`).

- O método `ShutdownAndWaitTerminationAsync` é também um método assícrono que desencadeia o _shutdown_ do servidor e aguarda o respectivo encerramento gracioso. O código deste método é o seguinte:

```C#
	/**
	 * Start shudown the server and wait for termination.
	 */
	private async Task  ShutdownAndWaitTerminationAsync(Task listenTask, CancellationTokenSource cts) {
		/**
		 * Before we close the listener socket, try to accept all connections already accepted by
		 * the operating system's sockets layer.
		 * Since that it is possible that we never see no connections pending, due
		 * to an uninterrupted arrival of connection requests, we poll only for
		 * a limited amount of time.
		 */	
		for (int i = 0; i < WAIT_FOR_IDLE_TIME; i += POLLING_INTERVAL) {
			if (!server.Pending())
				break;
			await Task.Delay(POLLING_INTERVAL);
		}
		
		/**
		 * Stop accepting new connection requests.
		 */
		server.Stop();
		
		/**
		 * Set the cancellation token to the canceled state. Any client waiting client requests will
		 * be cancelled, and all calls to the ServeConnectionAsync method will return. Note that only
		 * will be cancelled the asynchronous operations to which the cancellation token was passed.
		 * The others will terminate normally.
		 */
		cts.Cancel();	

		/**
		 * Now wait until the completion of the ListenAsync method, before close the
		 * application.
		 */
		await listenTask;
	}
```

- Antes de fechar o _listener socket_, este método começar por tentar esperar que todas as ligações de clientes ao servidor já aceites pelo _socker layer_ do sistema operativo tendo sido processadas, isto é, o método `TcpListener.Pending` devolva `true`. Quer isso aconteça quer não, é iniciado o processo de _shutdown_ é iniciado instruindo o _server socket_ para deixar de aceitar ligações com a chamada ao método `TcpListener.Stop`. A seguir, propaga-se a informação de cancelamento através da instância global do tipo `CancellationTokenSource` para que todos os pedidos de clientes pendentes de serviço sejam cancelados. Por último, o método aguarda que o método assíncrono `ListenAsync` termine, o que indica que todas as ligações de clientes aceites pelo servidor foram processadas.

- Por último, o método `Main` do servidor que é também um método assíncrono tem o seguinte código:

```C#
	/**
	 * Server entry point
	 */
	public static async Task Main() {
		
		// Create the instance of the echo server.
		TcpMultiThreadedTapEchoServer echoServer = new TcpMultiThreadedTapEchoServer();
		
		// The cancellation token source used to propagate the shutdonw of the server.
		CancellationTokenSource cts = new CancellationTokenSource();
		
		/**
		 * Start listen and processing connectios.
		 * This is an asynchronous method that returns on the first await that will
		 * suspend the execution of the method.
		*/
		Task listenTask = echoServer.ListenAsync(cts.Token);
		
		/**
		 * Wait an <enter> press from the console to terminate the server.
		 */
		Console.WriteLine("--Hit <enter> to exit the server...");
		await Console.In.ReadLineAsync();
		
		/**
		 * After pressing Shutdown, trigger the server shutdown and wait for
		 * all the processing in progress to finish.
		 */
		await echoServer.ShutdownAndWaitTerminationAsync(listenTask, cts);
			
		// Display the number of requests processed
		Console.WriteLine($"--{echoServer.requestCount} requests where processed");
	}
```

- É criada uma instância do tipo `TcpMultiThreadedTapEchoServer` que implementa um servidor de eco assim como uma instância do tipo `CancellationTokenSource` que fornece o `CancellationToken` que vamos usar para propagar a intenção de _shutdown_ para o código que processa os pedidos dos clientes.

- A seguir invoca-se o método assíncrono `ListenAsync` que vai começar a aceitar as ligações dos clientes e desencadear accionar o respectivo processamento. Este método retorna ao chamador assim que encontra o primeiro ponto de suspensão, sendo a `Task` que representa o método armazenada na variável local `listenTask`.

- Após mostrar uma mensagem na consola, o método `Main` invoca o método `ReadLineAsync` sobre a instância do tipo `System.IO.TextReader` que está associado ao teclado. Este método suspende a execução do método `Main` até que seja digitada a tecla <enter>. Após ser digitado a tecla <enter>, o método `Main` e reatado e invoca o método `TcpMultiThreadedTapEchoServer.ShutdownAndWaitTerminationAsync` para desencadear o processo de _shutdown_ do servidor e aguardar que o processo de _shutdown_ gracioso esteja completo.


### Registo de Relatórios

- Para suportar a funcionalidade de registo de relatórios sem que isso tenha uma interferência importante no tempo de processamento dos pedidos dos clientes, sugere-se a implementação desta funcionalidade tendo em consideração o seguinte:

	- O registo dos relatórios em memória não volátile (ficheiro local ou remoto) deverá ser feito por uma _logger thread_ cuja prioridade deve ser inferior à prioridade das _worker threads_ que processam os pedidos dos clientes (que é `ThreadPriority.Normal`), para que a operação potencialmente bloqueante do registo de relatório não seja executada pelas _worker threads_ que processam os pedidos dos clientes, contribuindo assim para o aumento dos tempos de serviço.
	
	- Como é necessário proceder ao desacoplamento das dinâmicas entre as _worker threads_ e da _logger threads_ é necessário intercalar uma _message queue_ para suportar a comunicação dos registos numa relação produtores/consumidor. Esta fila deve ser limitada em dimensão, pois se não tiver limite poderá conduzir a situações de exaustão da memória por excesso de acumulação de registos, em situações em que a _logger thread_ não consegue executar pelo facto do processamento dos pedidos dos clientes ocupar completamente os processadores disponíveis. Assim, sugere-se a implementação de uma _message queue_ com capacidade limitada; se for solicitado um registo quando a fila está cheia, o relatório é descartado sendo essa informação devolvida às _worker threads_ que processam os pedidos dos clientes.
	
	- A funcionalidade de registo deverá ser um componente autónomo `Logger` que para além do construtor deve implementar os métodos `Start`, `Log` e `Shutdown`. O método `Start` deve lançar a _logger thread_; o método `Log`, que nunca deve bloquear a _thread_ invocante em sincronização de controlo, permite registar um relatório e deve informar o chamador se o registo teve ou não sucesso; o método `Shutdown` deve iniciar o _shutdown_ do _logger_; isto significa qeu não são aceites mais pedidos de registo de relatórios mas são registados em memória não volátil todos os relatórios que já foram aceites; para que seja possível fazer o encerramento gracioso do servidor o método `Shutdown` deve bloquear a _thread_ invocante até que todos os relatórios aceites tenham sido registados e que a _logger thread_ tenha terminado.     

	
### Cliente do Servidor TCP de Eco

- No ficheiro [client.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tcp/echo-raw/client/client.cs) consta uma implementação de um cliente para o servidor de eco (usando _byte stream_ na comunicação), cujos aspectos relevantes vamos explicar a seguir.

- O método assícrono `SendRequestAndReceiveResponseAsync` contém o código base do cliente e a sua implementação é a seguinte:

```C#
	/**
	 * Send a request to the server and display its response.
	 */
	private static async Task SendRequestAndReceiveResponseAsync(string host, string requestMessage) {		
		using (TcpClient connection = new TcpClient()) {
			try {
				// Start a stop watch timer to compute the roundtrip time.
				Stopwatch sw = Stopwatch.StartNew();
			
				/**
			 	 * Connect the TcpClient socket to the server and get the associated stream.
				 */		
				await connection.ConnectAsync(host, SERVER_PORT);
				NetworkStream stream = connection.GetStream();
				
				/**
				 * Translate the message to a byte stream and send it to the server.
				 */
				byte[] requestBuffer = Encoding.ASCII.GetBytes(requestMessage);
				await stream.WriteAsync(requestBuffer, 0, requestBuffer.Length);
				Console.WriteLine($"-->[{requestMessage}]");         

				/**
				 * Receive the server's response and display it.
				 */
				byte[] responseBuffer = new byte[BUFFER_SIZE];
				int bytesRead;
				while ((bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
					Console.WriteLine($"<--[{Encoding.ASCII.GetString(responseBuffer, 0, bytesRead)} ({sw.ElapsedMilliseconds}) ms]");
				sw.Stop();
				Interlocked.Increment(ref requestCount);
			} catch (Exception ex) {
				Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");		
			}
		}
	}
```

- Este método cria uma instância do tipo `TcpClient` e solicita uma ligação ao servidor na `SERVER_PORT` invocando o método assíncrono `TcpCliente.ConnectAsync`. A seguir obtém a instância de `NetworkStream` que permite realizar escritas e leituras na comunicação com o servidor usando o canal de comunicaçã estabelecido pelo _socket_. A seguir é enviada uma mensagem para o servidor e mostrada na consola a repectiva resposta. A instância do tipo `TcpClient` está no âmbito de uma instrução `using` pelo que o _socket_ é fechado quando esta instrução termina.

- Com o objectivo de fazer testes de _stress_ ao servidor, o método `Main` deste cliente pode ser configurado para enviar vários pedidos ao servidor em paralelo. O código deste método é o seguinte:

```C#
	/**
	 * Send continuously batches of requests until a key is pressed.
	 */
	private const int REQS_PER_BATCH = 25;
	
	public static async Task Main(string[] args) {
		bool executeOnce = false;
		
		string request = (args.Length > 0) ? args[0] : "--default request message to be echoed--";
		
		Task[] tasks = new Task[REQS_PER_BATCH];
		Stopwatch sw = Stopwatch.StartNew();
		do {
			for(int i = 0; i < REQS_PER_BATCH; i++)
				tasks[i] = SendRequestAndReceiveResponseAsync("localhost", String.Format($"#{i:D2}: {request}"));
			await Task.WhenAll(tasks);
		} while (!(executeOnce || Console.KeyAvailable));
		Console.WriteLine($"--completed {requestCount} request in {sw.ElapsedMilliseconds} ms");
	}
```

- Este método envia lotes com dimensão `REQS_PER_BATCH` de pedidos aos servidor e termina quando for permita uma tecla. 


### Implementação de um Servidor TCP com Comunicação via Pedido/Resposta em JSON

- No directório [src/tcp/echo_json](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/tree/master/src/tcp/echo-json) encontra-se a implementação de um servidor TCP de eco e do respectivo cliente com comunicação via JSON, como é solicitada na Série de Exercícios 3. Estas implementações usam o _package NuGet_ `Newtonsoft.Json`.
 

## Sugestões para a Implementação da `TransferQueueAsync`

- Na Série de Exercícios 3 solicita-se a implementação de um servidor TCP que exponha remotamente implementações do sincronizador _transfer queue_ com acesso remoto. O tipo `TransferQueueAsync` deverá seguir o padrão de desenho descrito no [ficheiro] (https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/zoom/blackboard-25.md). Para uniformizar as resoluções, deixamos aqui uma sugestão pata a definição dos tipos de objecto _asynchronous request_ e das filas de espera a utilizar.

```C#
class TransferQueueAsync<T> where T: class {

	// Base type for the two async requests
	private class AsyncRequest<V> : TaskCompletionSource<V> {
		// same as AsyncAcquire on the SemaphoreAsync
	}
	
	// Type used by async take requests
	private class AsyncTake: AsyncRequest<T> {
		internal AsyncTake(CancellationToken cToken) : base(cToken) {}
	}
	
	// Type used by async transfer take request
	private AsyncTransfer : AsyncRequest<bool> {
		internal AsyncTake(CancellationToken cToken) : base(cToken) {}	
	}
	
	// Type used to hold each message sent with put or with transfer 
	private class Message {
		internal readonly T message;
		// This field holds the reference to thi AsyncTransfer when the
		// when the underlying message was sent with transfer, null otherwise.		
		internal readonly AsyncTransfer transfer; 
		
		internal Message(T message, AsyncTransfer transfer = null) {
			this.message = message;
			this.transfer = transfer;
		}
	}
	
	// Queue of messages pending for reception
	private readonly LinkedList<Message> pendingMessage;
	
	// Queue of pending async take requests
	private readonly LinkedList<AsyncTake> asyncTakes;
	
	...
}

```

___




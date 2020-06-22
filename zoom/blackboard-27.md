# Aula 27 - Implementação de Servidores TCP Escaláveis
___

## Sumário

- Desenho de servidores TCP _multithreaded_ escaláveis no .NET framework, com os seguintes requisitos: (i) limitação por desenho do número máximo de ligações com os clientes abertas em cada momento; (ii) _shutdown_ gracioso desencadeado localmente; (iii) funcionalidade de registo de mensagens de log usando _thread(s)_ de baixa prioridade.

- Sugestões para definir a estrutura da implementação de uma _transfer queue_ com interface assíncrona, que é necessário na resolução da terceira série de exercícios.

## .NET _Core_

- O .NET _Core_ é uma versão _cross-platform_ do .NET _Framework_ que permite desenvolver _websites_, serviços e aplicações consola nas plataformas _Linux_, _macOS e _Windows_. Está disponível para _download_ em [.NET _Core_](https://dotnet.microsoft.com/download). Nos exemplos desta aula vamos utilizar o .NET _Core_.

### Servidores TCP Escaláveis

- Usando o .NET _Framework_ e os métodos assíncronos do C# torna-se simples desenhar um servidor TCP escalável, qualquer que seja a sua funcionalidade. A garantia de escalabilidade decorre do facto de não ser necessário bloquear as _worker thread_ para fazer sincronização de controlo. (Neste desenho, consideramos ser razoável usar sincronizadoes com interface síncrona na sincronazão no acesso a dados partilhados mutáveis). Nestas condições, o _thread pool_ do .NET _Framework_ terá **quase sempre** condições para utilizar o número óptimo de _worker threads_. (O **quase sempre** decorre do facto de poder haver bloqueios das _worker thread_ na sincronização no acesso aos dados partilhados mutáveis.)

- No ficheiro [server.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tcp/echo-raw/server/server.cs) consta uma implementação de um servidor de eco (usando _bytes_ na comunicação) escalável que a seguir iremos detalhar os aspectos relevantes da sua implementação (TODO).

- No ficheiro [client.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/tcp/echo-raw/client/client.cs) consta uma implementação de um servidor de eco (usando _bytes_ na comunicação) escalável que a seguir iremos detalhar os aspectos relevantes da sua implementação (TODO).

### Implementação de um Servidor TCP com Comunicação via Pedido/Resposta em JSON

- No directório [src/tcp/echo_json](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/tree/master/src/tcp/echo-json) encontra-se a implementação de um servidor TCP de eco e do respectivo cliente com comunicação via JSON, como é solicitada na Série de Exercícios 3. Estas implementações usam o _package NuGet_ `Newtonsoft.Json`.
 

## `TransferQueueAsync`

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




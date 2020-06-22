# Aula 27 - Implementação de Servidores TCP Escaláveis
___

## Sumário

- Desenho de servidores TCP _multithreaded_ escaláveis no .NET framework, com os seguintes requisitos: (i) limitação por desenho do número máximo de ligações com os clientes abertas em cada momento; (ii) _shutdown_ gracioso desencadeado localmente; (iii) funcionalidade de registo de mensagens de log usando _thread(s)_ de baixa prioridade.

- Sugestões para definir a estrutura da implementação de uma _transfer queue_ com interface assíncrona, que é necessário na resolução da terceira série de exercícios.

### Servidores TCP _Multithreaded_

###_Sockets_ TCP:

TcpListener

TcpClient


## `TransferQueueAsync`


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




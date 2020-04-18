# Aula 10 - Implementação de Sincronizadores (IV)

____


## Implementação de Sincronizadores ao "Estilo *Kernel*"

### Resumo sobre o "Estilo *Kernel*"

- Por cada operação *acquire* é definido um tipo de dados para representar o *Request* subjacente e uma fila de espera onde se encontram os *requests* pendentes de conclusão.

- As instâncias do tipo *Request* constituem <ins>um canal de comunicação privado</ins> entre as *threads* que se bloqueiam na operação *acquire* e as *threads* que vão consumar essas operações *acquires* quando executam as respectivas operações *release*. 

- Em termos genéricos, as instâncias do tipo *Request* deverão ter campos para: (i) `AcquireArgs`, opcional, descrevendo os argumentos da operação *acquire*; (ii) `AcquireResult`, opcional, para armazenar o resultado da operação *acquire*; (iii) `done` um campo do tipo `boolean`, obrigatório, que indica se a operação *acquire* subjacente já foi, ou não, concluída por uma *releaser thread*.

- A estrutura geral do código das operações *acquire* consta dos seguintes passos:
	
	- Após entrar no monitor, a *acquirer thread*, começa por considerar as operações em fila de espera de acordo com a respectiva disciplina; se na fila de espera não se encontram operações prioritárias, testa o estado de sincronização para determinar se o *acquire* é possível (predicado `canAcquire`); em caso afirmativo, actualiza o estado de sincronização (método `acquireSideEffect`) e devolve o respectivo resultado, se houver; no caso contrário, continua no próximo passo;
	- A seguir, a *acquirer thread* cria uma instância do objecto *Request* e insere-o na respectiva fila de espera;
	- Depois, entra num ciclo onde se bloqueia no monitor e permanece até que ocorra uma das seguintes três situações: (i) o campo `done` foi afecatdo com `true` indicando que o *acquire* foi completado; (ii) expire o tempo especificado para limite do tempo de espera, ou; (iii) a espera da *thread* seja interrompida.
	- Neste estilo, como as operações *acquire* pendentes são realizadas pelas *releaser threads* pode ocorrer uma *race condition* entre a realização do *acquire* e a desistência por *timeout* ou interrupção. Assim, antes da desistência é necessário confirmar se o *request* foi ou não concluído; em caso afirmativo, a *acquirer thread* já não poderá desistir, pois não consideramos ser sempre viável desfazer operações *acquire*.
	- Na estrutura do código que se propõe o teste da condição de *timeout* é sempre feito com a garantia de que o *request* ainda não foi concluído. Contudo, quando o bloqueio da *acquirer thread* for interrompido é necessário testar o campo `done` e se este for `true` fazer um retorno normal do método *acquire* garantindo que a interrupção não é perdida.




#### Considerações sobre o processamento das interrupções no "Estilo *Kernel*"

- É importante referir que a problemática da interrupção das *threads* bloqueadas nas operações *acquire* é diferente da que foi referida para o "estilo monitor", relativamente a perda de notificações que pode acontecer no .NET quando uma *thread* é interrompida depois de ser notificada numa variável condição e antes de reentrar no monitor (comportamento que não se verifica no *Java*).

- No "estilo *kernel*" o problema não é uma eventual perda de notificação.

- Neste estilo, as *acquirer threads* expõem, antes de se bloquearem, objectos *request* às *release threads* para que estas realizem as respectivas operações *acquire* em seu proveito.

- Assim, é possível que o bloqueio de uma *acquirer thread* seja interrompido ao mesmo tempo que uma *releaser thread* está dentro do monitor e completa a respectiva operação *acquire*, uma vez que a interrupção não está sujeita à disciplina de exclusão mútua imposta pelo monitor. Resultado: a *acquirer thread* retorna da operação de *wait* sobre a variável condição com `InterruptedException`, mas o campo `done` do respectivo objecto *request* está a `true`, indicando que a operação *acquire* foi realizada.

- Como não é sempre viável fazer *undo* das operações *acquire*, a solução proposta é o retorna nornal do método *acquire*, com a garantia que a interrupção da *thread* não é perdida (invocando-se o método `Thread.currentThread().interrupt`).





### Notificação específica de *threads* no "Estilo *Kernel*"










- Os monitores implícitos no *Java* e no .NET, pelo facto de suportarem apenas uma variável condição anónima, não são adequados para implementar a maioria dos sincronizadores que requerem a utilização o "estilo *kernel*".

- Na maior parte das implementações é necessário implementar explicitamente uma fila de espera por cada operação *acquire*, sendo a notificação das *threads* feita individualmente.

- Como os monitores implícitos apenas suportam uma variável condição, e não existe nenhuma garantia de que a ordem das *threads* na fila de espera da variável condição corresponde à ordem são notificadas corresponde à ordem pelas quais as *threads* são libertadas (que depende da disciplina da fila de espera explícitla), não existe alternativa a ter que se usar notificação com *broadcast* (`Object.notifyAll` ou `Monitor.PulseAll`) para ter garantia que se notifica a *thread* alvo em todas as situações.

- A notificação de *threads* bloqueadas que não tenham condições para realizar a respectiva operação *acquire* provoca duas comutações de contexto que obviamente introduzem um *overhead* que era dejesável ser eliminado.

- A notificação específica de *threads* consta em notificar apenas as *threads* cujas operações *acquire* estão concluídas, o que no "estilo *kernel*" é do conhecimento das *releaser threads* responsáveis pelas notificações. 

- Ser selectivo na notificação das *threads* bloqueadas no monitor só é possível se forem utilizados monitores que suportem múltiplas variáveis condição.

- Devem ser utilizadas as variáveis condição suficientes para que sejam notificadas apenas as *threads* que vêm as respectivas operações *acquire* concluídas. Existem duas situações a considerar: (a) as *acquirer threads* são notificadas em grupo (situação que acontece no *manual-reset event*), então podem usar a mesma variável condição e a notificação é feita com *broadcast* (notificando todas as *threads* bloqueadas na variável condição); (b) as *acquire threads* são notificadas individualmente (situação que acontece no semáforo ou *auto-reset event*), então cada *thread* tem que se bloquear numa variável condição privada e a notificação é feita com *notify* (notificar a única *thread* bloqueada na variável condição).

- Para implementar notificações específicas é necessário usar um monitor explícito no *Java* ou um monitore implícito estendido no .NET (apresentada a seguir).

- Quando se usa um monitor explícito do *Java* e se pretende notificação individualizada das *acquirer threads* tem que acrescentar um campo com a variável condição ao objecto *Request* que descreve a operação *acquire*.

- Quando se usam um monitor estendido .NET, as variáveis condição pode ser representadas por qualquer objecto, pelo que a variável condição pode ser representada pelo objecto *Request* ou, em alternativa, pelo nó da lista usado pelo objecto *Request* (`LinkedListNode<Request>`).



#### Extensão ao monitor implícito do .NET por forma a suportar múltiplas variáveis condição

```C#
/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Extension to the System.Threading.Monitor class in order to support Lampson and Redell
 * monitors with an arbitrary number of condition variables.
 *
 * NOTE: This implemetation has an importante limitation. It does not support waiting on condition
 * variables by threads than entered "the monitor" more than once (we this happens the monitor's lock
 * is not completely released by the wait method).
 *
 * Carlos Martins, April 2019
 *
 ***/

using System;
using System.Threading;

public static class MonitorEx {
	
	/**
	 * Acquire a monitor's lock ignoring possible interrupts.
	 * Through its out "interrrupted" parameter this method informs the caller if the
	 * current thread was interrupted while it was trying to acquire the monitor's lock.
	 */
	public static void EnterUninterruptibly(object mlock, out bool interrupted) {
		interrupted = false;
		do {
			try {
				Monitor.Enter(mlock);
				break;
			} catch (ThreadInterruptedException) {
				interrupted = true;
			}
		} while (true);
	}
	
	/**
	 * This method waits on the specified condition of a multi-condition monitor.
	 *
	 * This method is called with "monitor" locked and the condition's lock unlocked.
	 * On return, the same conditions are meet: "monitor" locked and the condition's lock unlocked.
	 */
	
	public static void Wait(object monitor, object condition, int timeout = Timeout.Infinite) {
		// if the monitor and condition are the same object, we just call Monitor.Wait on "monitor"
		if (monitor == condition) {
			Monitor.Wait(monitor, timeout);
			return;
		}
		
		/**
		 * if the monitor and condition are different objects, we need to release the monitor's
		 * lock before wait on the condition variable of the condition monitor.
		 *
		 * first, we need to enter the "condition's implicit monitor" before release the monitor's
		 * lock, in order to prevent the loss of notifications.
		 * if a ThreadInterruptException is thrown, we must return the exception with the monitor's
		 * lock locked. We considerer this case as the exception was thrown by the method
		 * Monitor.Wait(condition).
		 */
		
		// acquire the condition monitor's lock
		Monitor.Enter(condition);
		// release the monitor's lock; from here onwards it is possible to notify the condition,
		// but because the condition monitor's only will be released when the waiter thread
		// enters the Monitor.wait method, no notifications would be lost.
		Monitor.Exit(monitor);
		try {
			// wait on the condition monitor's condition variable
			Monitor.Wait(condition, timeout);
		} finally {
			// release the condition monitor’s lock
			Monitor.Exit(condition);
			
			// re-acquire the monitor's lock uninterruptibly
			bool interrupted;
			EnterUninterruptibly(monitor, out interrupted);
			// if the thread was interrupted while trying to acquire the monitor's lock, we consider
			// that it was interrupted when in the waiting on the condition variable, so we throw
			//  ThreadInterruptedException.
			if (interrupted)
				throw new ThreadInterruptedException();
		}
	}
		
	/**
	 * This method notifies one thread that called MonitorEx.Wait using the same monitor
	 * and condition variable objects.
	 *
	 * This method is called with the monitor's lock held, and returns under the same
	 * conditions.
	 */
	public static void Pulse(object monitor, object condition) {
		// if monitor and condition refers to the same object, we just call Monitor.Pulse on monitor.
		if (monitor == condition) {
			Monitor.Pulse(monitor);
			return;
		}
		
		/**
		 * If monitor and condition refer to different objects, in order to call Monitor.Pulse on
		 * condition we need to acquire condition monitor's lock.
		 * We must acquire the condition monitor's lock filtering ThreadInterruptedException,
		 * because this method is not used for wait purposes, so it must not throw that exception.
		 */
		
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		
		// notify the condition variable of the condition monitor and leave the corresponding monitor.
		Monitor.Pulse(condition);
		Monitor.Exit(condition);
		
		/*
		 * if the current thread was interrupted when acquiring the condition monitor's lock,
		 * we re-assert the interruption, so the exception will be raised on the next call to
		 * a managed wait operation.
		 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}

	/**
 	 * This method notifies all threads that called MonitorEx.Wait using the same monitor
 	 * and condition variable objects.
 	 *
 	 * This method is called with the monitor's lock held, and returns under the same
 	 * conditions.
 	 */
	public static void PulseAll(object monitor, object condition) {
		// if monitor and condition refers to the same object, we just call Monitor.PulseAll on monitor.
		if (monitor == condition) {
			Monitor.PulseAll(monitor);
			return;
		}
	
		/**
	 	 * If monitor and condition refer to different objects, in order to call Monitor.PulseAll on
	 	 * condition we need to acquire condition monitor's lock.
	 	 * We must acquire the condition monitor's lock filtering ThreadInterruptedException,
	 	 * because this method is not used for wait purposes, so it must not throw that exception.
	 	 */
	
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
	
		// notify the condition variable of the condition monitor and leave the corresponding monitor.
		Monitor.PulseAll(condition);
		Monitor.Exit(condition);
	
		/*
	 	 * if the current thread was interrupted when acquiring the condition monitor's lock,
	 	 * we re-assert the interruption, so the exception will be raised on the next call to
	 	 * a managed wait operation.
	 	 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
}
```

### *Message Queue*  ao "estilo *kernel*" com base na extensão ao monitor implícito do .NET

- O código que se apresenta a seguir resulta da adaptação do pseudo-código apresentado anteriormente à implementação da *message queue*, usando também nomes mais sugestivos para os métodos auxiliares:
	- O objecto `Request` para além do campo `done` necessita de ter um campo equivalente ao `acquireResult`, aqui designado `receivedMsg`, para armazenar a mensagem que vai ser entregue à respectiva *receiver thread*;
	- O método `canAcquire, aqui designado `canReceive`, indica se existe pelo menos uma  mensagem pendente para recepção;
	- O método `acquireSideEffect`, aqui designado `receiveSideEffect`, retira a próxima mensagem da fila e retorna-a para ser entregue à *receiver thread*; 
	- O método `updateOnRelease`, aqui designado `UpdateStateOnSend`, insere a mensagem enviada na lista com as mensagens pendentes para recepção;
	- As assinatura dos métodos *acquire* (`Receive`) e *release* (`Send`) foram ajustadas para corresponder aos tipos dos parâmetros formais e dos valores de retorno;
	- Neste sincronizador a desistência de uma *receiver thread* na operação *acquire* não cria condições para que outras *receiver threads* bloqueadas possam completar a respectiva operação, pelo que o código que trata este aspecto não foi incluído.

```C#
/**
 * Message queue following the kernel style, using an *implicit-extended .NET monitor*,
 * with support for timeout on the receive operation.
 * 
 * Notes:
 *   1. This implementation uses specific thread notifications.
 *   2. In this synchronizer when a thread gives up the acquire operation due to timeout
 *      or interruption, no other acquire can be satisfied, so the code that addresses
 *      this situation was not included.
 */

public class MessageQueueKernelStyleImplicitExtendedMonitorSpecificNotifications<T> {
	// extended .NET monitor provides synchronization of the access to the shared
	// mutable state, supports the control synchronization inherent to acquire/release
	// semantics.
	// Using the extension implemented by the MonitorEx class, other implicit .NET
	// monitors, associated to the Request objects, will be used as condition variables
	// subordinate to the monitor represented by "monitor". 
	private Object monitor = new Object();
	
	// the instance of this type is used to hold a receive request
	private class Request {
		internal T receivedMsg; // received message
		internal bool done;     // true when done
	}
	
	// queue of pending receive requests
	private LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// synchronization state: list of messages pending for reception
	private LinkedList<T> pendingMessages = new LinkedList<T>()
		
	// initialize the message queue
	public MessageQueueKernelStyleImplicitExtendedMonitorSpecificNotifications() { }
	
	// returns true if there is an pending message, which means that receive
	// can succeed immediately
	private bool CanReceive() { return pendingMessages.Count > 0; }
	
	// when a message is received, it must be removed from the pending message list
	private T ReceiveSideEffect() {
		T receivedMessage = pendingMessages.First.Value;
		pendingMessages.RemoveFirst();
		return receivedMessage;
	}
	
	// add the sent message to the pending messages list
	private void UpdateStateOnSend(T sentMessage) { pendingMessages.AddLast(sentMessage); }
	
	// receive the next message from the queue
	public bool Receive(out T receivedMsg, int timeout = Timeout.Infinite) {
		lock (monitor) {
			if (reqQueue.Count == 0 && CanReceive()) {
				receivedMsg = ReceiveSideEffect();
				return true;
			}
			// add a request to the end of the reqQueue
			Request request = new Request();
			reqQueue.AddLast(request);
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0) {
					// the specified time limit has expired.
					// Here we know that our request was not satisfied.
					reqQueue.Remove(request);
					receivedMsg = default(T);
					return false,
				}
				try {
					MonitorEx.Wait(monitor, request, timeout);  //block on private condition variable
				} catch (ThreadInterruptedException) {
					// if the acquire operation was already done, re-assert interrupt
					// and return normally; else remove request from queue and throw
					// ThreadInterruptedException.
					if (request.done) {
						Thread.CurrentThread.Interrupt();
						break;
					}
					reqQueue.Remove(request);
					throw;
				}
			} while (!request.done);
			receivedMsg = request.receivedMsg;
			return true;
		}
	}

	// send a message to the queue
	public void Send(T sentMsg) {
		lock (monitor) {
			UpdateStateOnSend(sentMsg);
			if (reqQueue.Count > 0) {
				Request request = reqQueue.First.Value;
				reqQueue.RemovestFirst();
				request.receivedMsg = ReceiveSideEffect();
				request.done = true;
				// notify waiting thread on its private condition variable
				MonitorEx.Pulse(monitor, request);
			}
		}
	}
	
	// send a message to the queue (optimized)
	public void SendOptimized(T sentMsg) {
		lock (monitor) {
			if (reqQueue.Count > 0) {
				// deliver the message directly to a blocked thread
				Request request = reqQueue.First.Value;
				reqQueue.RemoveFirst();
				request.receivedMsg = sentMsg;
				request.done = true;
				// notify waiting thread on its private condition variable
				MonitorEx.Pulse(monitor, request);
            } else {
                // no receiver thread waiting, so the message is left in the pending
				// messages queue
				UpdateStateDueToSend(sentMsg);
			}
		}
	}
}
```

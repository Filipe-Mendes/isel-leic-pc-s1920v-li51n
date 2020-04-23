
# Aula 11 - Optimização da Implementação de Sincronizadores ao Estilo *Kernel*

____

- O "estilo *kernel*", na sua formulação genérica, exige a utilização de uma fila de espera por cada operação *acquire* assim como um objecto *request* por cada *acquirer thread* que se bloqueia. Nas actuais linguagens *managed* (e.g., *Java*, C#, *Kotlin*) isto não representa um *overhead* significativo. Contudo, existem sincronizadores em que não é necessário criar um objecto *request* por cada *acquirer thread* que se boqueia ou pode mesmo não ser necessário usar uma fila de espera.

- O sincronizador *manual-reset event* é o caso de um sincronizador que tem que ser implementado o "estilo *kernel*" pelo facto da sua interface pública oferece um método que permite fazer *reset* ao estado do evento, podendo anular o efeito de um anterior *set* sem que as *acquirer threads* bloqueadas aquando do *set* possam reentrar no monitor e constatar que o evento estava sinalizado.

- As optimizações apresentadas a seguir baseiam-se na constatação de que nos sincronizadores em que todas as *threads* bloquedas numa operação *acquire* são libertadas ao mesmo tempo e a operação *acquire* não tem argumentos, pode usar-se uma das seguintes optimizações: (a) partilhar do objecto *request* entre várias *threads* e implementar uma versão simplificada da fila de espera, ou; (b) não ser mesmo necessário usar objecto *request* nem fila de espera.

- A seguir, apresentam-se três implementações do sincronizador *manual-reset event*: (i) a primeira segue o "estilo *kernel*" sem qualquer optimização; (ii) a segunda, partilha o objecto *request* entre todas as *acquirer threads* e usa uma versão simplificada de fila de espera, e; (iii) a terceira versão que não usa objecto *request* nem fila de espera explícita.


## Sem optimizações

- Nesta implementação faz-se um tratamento diferente da interrupção daquele que foi proposto na formulação genérica do "estilo kernel", que é dar prioridade à notificação da onclusão da operação *acquire* sobre a interrupção da *acquirer thread*.

- Contudo, nas operações *acquire* que não tenham efeitos colaterais sobre o estado de sincronização, como é o caso do *manual-reset event* não existe problema em dar prioridade à interrupção sobre a notificação da conclusão da operação *acquire*. O raciocínio subjacente a esta opção é o seguinte:

	- O mecanismo da interrupção é normalmente usado para alertar as *threads* de que devem fazer algo considerado "excepcional" (e.g., terminação graciosa), enquanto que a notificação da conclusão da operação *acquire* faz parte do seu processamento considerado "normal";
	
	- A solução proposta na formulação genérica do "estilo *kernel*" adia a notificação de que a *acquirer thread* foi interrompida. Esta é, de facto, a única solução, para suportar interrupções, em sincronizadores onde não é possível fazer *undo* da operação *acquire* (e.g., `BroadcastBox<T>` ou o `Exchanger<T>).
	
	- Contudo, em sincronizadores em que a operação *acquire* não tem efeitos colaterais sobre o estado de sincronização (como acontece no *manual-reset event*), fa sentido das prioridade ao comportamento que acima designámos por "excepcional" sobre aquele que designámos por "normal".
	
	- Para deixar registado que existe esta alternativa, vamos usá-la nas três implementações do *manual-reset event*.

```C# 
public class ManualResetEvenKernelStyletNaive {
	// the implict .NET monitor
	private readonly object monitor = new object();
	
	// The resquest object is just a "bool"
	private readonly LinkedList<bool> reqQueue = new LinkedList<bool>();
	
	// synchronization state: // true when the event is signaled
	private bool signalState;
	
	public ManualResetEvenKernelStyletNaive(bool initialState = false) { signalState = initialState; }
	
	public bool Wait(int timeout = Timeout.Infinite) {
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// enqueue a request on the request queue
			LinkedListNode<bool> requestNode = reqQueue.AddLast(false);
			 
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until our request is satisfied, the specified timeout expires
			// or the thread is interrupted.

			do {
				if ((timeout = th.Value) == 0) {
					
					// remove our request from "reqQueue"
					reqQueue.Remove(requestNode);
					return false;		// return failure: timeout expired
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// as this acquire operation has no side effects we can choose to
					// throw ThreadInterruptedException instead of giving the operation
					// completed successfully.
					// anyway, we must remove the request from the queue if it is stiil
					// inserted.
					if (!requestNode.Value)
						reqQueue.Remove(requestNode);
					throw;
				}
			} while (!requestNode.Value);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			signalState = true;
			if (reqQueue.Count > 0) {
				LinkedListNode<bool> request = reqQueue.First;
				do {
					request.Value = true;
				} while ((request = request.Next)!= null);
				reqQueue.Clear();
				Monitor.PulseAll(monitor);
			}
		}
	}
	
	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}
```
## Partilhando o objecto *request* e usando uma versão simplificada de fila de espera

- Nesta implementação todas as *threads* bloqueadas na operação *acquire* partilham o mesmo objecto *request* sendo a fila de espera implementada apenas com base numa referência.

- O objecto *request* deve ter o campo *done* e, no caso da respectiva operação *acquire* ter resultado um campo para armazenar o resultado a ser entregue a todas as *threads* (No BroadcastBox<T> pode fazer-se esta optimização e é uma situação em que o objecto *request* tem que ter um campo para entregar a mensagem a todas as *receiver threads* quando é enviada uma mensagem.)

- A fila de espera pode ser implementada com uma referência para o objecto *request* e com um contador para armazenar o número de *waiting threads*. Embora esta informação não seja necessária na implementação do *manual-reset event* vamos considerá-la para tornar a implementação da fila de espera mais genérica. (Adiante, na implementação de um *read/write lock* veremos como essa informação é utilizada.)

```C#
public class ManualResetEvenKernelStyleShareRequest {
	private readonly object monitor = new object();
	
	// this request object is shared by all waiters
	private class SharedRequest {
		internal waiters;
		internal bool done;
		
		internal SharedRequest() { waiters = 1; } 
	}
	
	// the pseudo-"request queue"
	
	private SharedRequest reqQueue = null; 

	private bool signalState;	// true when the event is signaled
	
	public ManualResetEvenKernelStyleShareRequest(bool initialState = false) {
		signalState = initialState;
	}
	
	/**
	 * Methods to manipulate the "simplified-request queue"
	 */

	// add a waiter to the queue
	private SharedRequest EnqueueWaiter() {
		if (reqQueue == null) 
			reqQueue = new SharedRequest();
		else
			reqQueue.waiters++;
		return reqQueue;
	}
	
	// remove a waiter froom the queue
	public void RemoveWaiter() {
		if (--reqQueue.waiters == 0)
			reqQueue = null;
	}
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// enqueue a request on the request queue
			SharedRequest request =  EnqueueWaiter();
			 
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until the event our request is satisfied, the specified timeout expires
			// or the thread is interrupted.

			do {
				if ((timeout = th.Value) == 0) {				
					// remove our request from "waiters queue"
					RemoveWaiter();
					return false;		// return failure: timeout expired
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// as this acquire operation has no side effects we can choose to
					// throw ThreadInterruptedException instead of giving the operation
					// completed successfully.
					// anyway, we must remove the request from the queue if it is stiil
					// inserted.
					if (!request.done)
						RemoveWaiter();
					throw;
				}
			} while (!request.done);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			signalState = true;
			if (reqQueue != null) {
				reqQueue.done = true;
				reqQueue = null;		// remove all waiters
				Monitor.PulseAll(monitor);
			}
		}
	}
	
	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}
```

## Sem usar objecto *request* nem fila de espera

- Quando a operação *acquire* não tem resultado como é o caso no *manual-reset event* a única coisa que as *acquirer threads* necessitam de testar quando reentram no monitor, após notificação, é se já ocorreu a operação *release* (neste caso, o *set* do evento) após as mesmas se terem bloqueado.

- Se acrescentarmos ao estado de sincronização um contador que seja incrementado por cada operação *release* (o *set* do evento), é possível às *acquirer threads* saber se já houve uma operação *release*, bastando para tal comparar o valor do contador antes do bloqueio (cuja cópia é obtida antes do bloqueio) com o valor do que o contador após a notificação. As contadores com esta uilização, no âmbito da implementação de sincronizadores, é costume designar por "geração" do estado de sincronização.

```C#
public class ManualResetEventSlimOptimized {
	// implicit .NET monitor
	private readonly object monitor = new object();
	// synchronization state
	private bool signalState;	
	// current state generation
	private int signalStateGeneration;
	
	public ManualResetEventSlimOptimized(bool initial = false) { signalState = initial; }
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {	
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until the event is signalled, the specified timeout expires or
			// the thread is interrupted.
			
			int arrivalGeneration = signalStateGeneration;
			do {
				if ((timeout = th.Value) == 0)
					return false;		// timeout expired
				Monitor.Wait(monitor, timeout);
			} while (arrivalGeneration == signalStateGeneration);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			if (!signalState) {
				signalState = true;
				signalStateGeneration++;
				Monitor.PulseAll(monitor);
			}
		}
	}

	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}
```
## Implementação de um *Read/Write Lock*

- O *read/write lock* é um sincronizador que tem algumas particularidades interressantes e que também pode ser parcialmente optimizado usando as técnicas apresentadas anteriormente.

- Este sincronizador suporta a sincronização no acesso a estado partilhado mutável em dois modos: (a) modo exclusivo (*write lock*), onde o acesso para escrita é permitido apenas a uma *thread* de cada vez, e; (b) modo partilhado (*read lock*), onde é permitido o acesso para leitura dos dados partilhados a múltiplas *threads* simultanemente.

- Uma implementação correcta deste sincronizador deverá assegurar que não deve haver a possibilidade de *starvation* das *threads* leitoras relativamente às *threads* escritoras nem vice-versa. Para evitar esta *starvation* pode utilizar-se uma das duas seguintes semânticas:

	- *Hoare*, no seu artigo onde propõe o conceito de monitor, propõe a seguinte semântica: (a) um pedido de *write lock* pendente impede a concessão de novos *read locks*; (b) quando é libertado um *write lock* são concedidos todos *read locks* a 
	- No *kernel* do *Linux* a implementação do *read/write lock* resolve o problema do *startvation* das *threads* leitoras e escritores servindo todos os pedidos de *lock read* e *lock write* com uma fila de espera com disciplina FIFO. Nesta abordagem, sempre que a fila de espera não está vazia as solicitações de *read* ou *write* *lock* são colocadas em fila de espera; quando o *lock* fica livre, é conceido o *lock* à *writer thread* que esteja à cabeça da fila de espera ou a uma ou mais *reader threads* que se encontrem adjacentes à cabeça da fila de espera.

- A implementação que se apresenta a seguir segue a semântica proposta por *Hoare*. São utilizadas duas filas de espera, uma normal onde são inseridos os pedidos pendentes de *write lock* e outra simplificada onde constam os pedidos de *real lock* pendentes. São também utilizadas notificações específicas para optimizar as comutações de *threads*.

```Java
class ReadWriteLockOptimized {
	private final Lock mlock; 			// the monitor's lock
	private final Condition okToRead;	// condition variable where readers are blocked
	private int state = 0; // -1 when writing, 0 when free, > 0 when reading (# of readers)
	
	// All waiting readers share the same request, because
	// because the respective request is guaranteed in group
	private static class LockReadRequest {
		int waiters = 1; 			// created by the first waiting reader
		boolean done;				// set to true when the request is satisfied
	}

	// request object used for writers
	private static class LockWriteRequest {
		final Condition okToWrite;	// conditon ver where the writer is waiting
		boolean done; 				// set true when the request is satisfied

		LockWriteRequest(Condition oktow) { okToWrite = oktow; }
	}
	// We use a queue for waiting readers and a queue for waiting writers.
	// For each queue node holds an object with a boolean fields that says if
	// the requested access was already granted or not.
	private LockReadRequest readReqQueue;	// null when queue is empty
	private final LinkedList<LockWriteRequest> writeReqQueue;

	// Constructor.
	public ReadWriteLockOptimized() {
		mlock = new ReentrantLock();
		okToRead = mlock.newCondition();
		readReqQueue = null;
		writeReqQueue = new LinkedList<LockWriteRequest>();
	}

	/*
	 * Methods that implement the lock read request queue
	 */

	private LockReadRequest enqueueReader() {
		if (readReqQueue == null)
			readReqQueue = new LockReadRequest();
		else
			readReqQueue.waiters++;
		return readReqQueue;
	}

	private void removeReader() {
		if (readReqQueue != null)
			readReqQueue.waiters--;
		else
			System.out.println("Ooops!!!");
	}

	private int waitingReaders() {
		return readReqQueue != null ? readReqQueue.waiters : 0;
	}

	private void clearReaderQueue() { readReqQueue = null; }

	// Acquire the lock for read (shared) access
	public void lockRead() throws InterruptedException {
		mlock.lock();
		try {
			// if there isn’t blocked writers and the resource isn’t being written, grant
			// read access immediately
			if (writeReqQueue.size() == 0 && state >= 0) {
				state++;
				return;
			}

			// otherwise, create a request object and enqueue it
			LockReadRequest request = enqueueReader();
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					okToRead.await();
				} catch (InterruptedException ie) {
					// if the requested shared access was granted, we must re-assert interrupt
					// exception, and return normally.
					if (request.done) {
						Thread.currentThread().interrupt();
						break;
					}
					// otherwise, we remove the request from the queue and re-throw the exception
					removeReader();
					throw ie;
				}
				// if shared access was granted then return; otherwise, re-wait
			} while (!request.done);
		} finally {
			mlock.unlock();
		}
	}

	// auxiliary method: grant access to all waiting readers
	private boolean grantAccessToWaitingReaders() {
		if (waitingReaders() > 0) {
			state += waitingReaders(); 	// account with all new active readers
			readReqQueue.done = true;
			clearReaderQueue();
			okToRead.signalAll(); 		// notify all waiting readers
			return true;
		}
		return false;
	}

	// auxiliary method: grant access to the first waiting writer
	private void grantAccessToAWaitingWriter() {
		if (writeReqQueue.size() > 0) {
			LockWriteRequest request = writeReqQueue.poll();	// remove the first element of the queue
			request.done = true;	// mark write request as granted
			state = -1; 			// exclusive lock was taken
			request.okToWrite.signal(); // notify waiting writer at its private condition variable
		}
	}

	// Acquire the lock for write (exclusive) access
	public void lockWrite() throws InterruptedException {
		mlock.lock();
		try {
			// if the lokc isn’t held for read nor for writing, grant the access immediately
			if (state == 0) {
				state = -1;
				return;
			}
			// create and enqueue a request for exclusive access
			LockWriteRequest request = new LockWriteRequest(mlock.newCondition());
			writeReqQueue.addLast(request);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					request.okToWrite.await();
				} catch (InterruptedException ie) {
					// if exclusive access was granted, then we re-assert exception, and return
					// normally
					if (request.done) {
						Thread.currentThread().interrupt();
						break;
					}
					// othwewise, remove the request from the queue, and return throwing the
					// exception.
					writeReqQueue.remove(request);

					// when a waiting writer gives up, we must grant shared access to all
					// waiting readers that has been blocked by this waiting writer
					if (writeReqQueue.size() == 0 && waitingReaders() > 0 && state >= 0)
						grantAccessToWaitingReaders();
					throw ie;
				}
				// if the request was granted return, else re-wait
			} while (!request.done);
		} finally {
			mlock.unlock();
		}
	}

	// Release read (shared) lock
	public void unlockRead() {
		mlock.lock();
		try {
			// decrement the number of active readers
			// if this is the last active reader, and there is at least a blocked writer,
			// grant access
			// to the writer that is at front of queue
			if (--state == 0 && writeReqQueue.size() > 0)
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}

	// Release the write (exclusive) lock
	public void unlockWrite() {
		mlock.lock();
		try {
			state = 0;		// mark lock as free
			if (!grantAccessToWaitingReaders())
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}
}
```

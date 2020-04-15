# Aula 08 - Implementação de Sincronizadores (III)

____


## Limitações na implementação de Sincronizadores usando o "Estilo Monitor"

- A semântica da notificação no monitor proposta por *Lampson* e *Redell* não garante **atomicidade** entre o código que é executado dentro do monitor por uma *releaser thread* antes da notificação de uma *thread* bloqueadas e o código executado pela *acquirer thread* após retorno da operação de *wait* sobre uma variável condição. (Recorda-se que esta atomicidade era garantida na semântica de sinalização proposta por *Brich Hansen* e *Hoare*).

- A semântica de *Lampson* e *Redell* permite que entre a alteração ao estado de sincronização feita numa operação *release* antes da notificação de uma *thread* bloqueada no monitor e a reacção a essa alteração de estado por parte da *acquirer thread* notificada, **terceiras** *threads* possam entrar no monitor (devido ao *barging*) e possam modificar o estado de sincronização.

- Nos sincronizadores em que o estado de sincronização reflete sempre o resultado das operações *acquire* e *release* realizadas anteriormente, como é o caso do semáforo ou da *message queue* esta falta de atomicidade é sempre viável uma implementação usando o "estilo monitor".

- Contudo, em sincronizadores onde a semântica é definida em termos de transições de estado, existem operações de *reset* do estado de sincronização ou seja necessário garantir uma disciplina específica na conclusão das operações *acquire* (e.g., FIFO), a interferência de **terceiras *threads*** complica ou inviabiliza as implementações seguindo o "estilo monitor". São exemplos:

	- No *manual-reset event* ou no *auto-reset event* que por suportarem um operação de *reset*, é possível ocorrer uma operação de *reset* entre uma operação de *set* e a *thread(s)* notificada(s) reentrar(em) no monitor. Nesta situação, a semântica da operação *set* (libertar todas as *threads* no *manual-reset event* ou uma *thread* no *auto-reset event*) pode não se verificar.
			
	- O sincronizador *exchanger* (disponível no *Java*), que suporta a troca de mensagens entre pares de *threads*, tem que ser implementado ao "estilo monitor" com base numa máquina de estados que impeça a interferência de **terceiras *threads*** enquanto não é concluída uma troca. Neste sincronizador, a primeira *thread* do par tem que se bloquear a aguardar a chegada de uma segunda *thread*; quando isto acontece, é preciso consumar a troca sem a intervenção de outras *threads*. Uma eventual **terceira *thread*** deverá ser a primeira *thread* da próxima troca.
	
	- Quando existir a necessidade de implementar disciplinas de fila de espera especícifas (e.g., FIFO ou LIFO), é sempre necessário implementar explicitamente as filas de espera o que sendo possível usando o "estilo monitor" (como se explica no Exemplo 3 das folhas da disciplina) fica bastante mais simples usado outra obordagem.
	
## Soluções

- Alguns dos problemas enunciados anteriormente podem ser resolvidos implementando as operações *acquire* com base em máquinas de estados de modo a impedir que as **terceiras *threads*** possam aceder ao estado de sincronização antes da conclusão das operações *acquire* viabilizadas por uma posterior operação *release*. Exemplos:

	- A implementação do *exchanger* com base numa máquina de estados, poder-se-ia considerar o *exchanger* num de três estados: IDLE quando aguarda a chegada da primeira *thread* de um par; EXCHANGING, depois da chegada da primeira *thread* do par e antes da chegada da segunda *thread*; COMPLETING, após a chegada da segunda *thread* e até que a primeira *thread* do par reentre no monitor, após a notificação, e recolha o objecto oferecido para *thread* com que pareou. Quando a primeira *thread* reentra no monitor completa a troca e faz transitar o *exchanger* para o estado IDLE. Qualquer *thread* que entre no monitor quando o *exchanger* estiver no estado COMPLETING, bloqueia-se a aguardar que o *exchanger* transite para o estado IDLE (tornando a primeira *thread* de uma próxima troca), ou EXCHANGING (tornando-se a segunda *thread* de uma próxima troca já iniciada por outra *thread*).

- As soluções em que as operações *acquire* são baseadas em máquinas de estados não permitem resolver facilmente as situações em que a semântica de sincronização é definida em função de transições de estado como acontece, por exemplo, no sincronizador *broadcast box* (exercício 2 da Série de Exercícios 1).


## Implementação de Sincronizadores ao "Estilo *Kernel*"

- A solução que permite implementar toda e qualquer semântica de sincronização segue um padrão que vamos designar por **estilo kernel** (nas folhas da disciplina, este estilo foi designado por **delegação de execução**).

- A ideia que está por detrás do "estilo *kernel*" é simples: <ins>realizar atomicamente - pelas *releases threads* - o processamento da operação *release* assim como a conclusão do processamento de todas as operações *acquire* pendentes que são viabilizadas pela respectiva operação *release*</ins>.

- A título de curiosidade, o "estilo *kernel*" **resgata** a atomicidade subjancente à semântica da sinalização proposta por *Brinch Hansen* e *Hoare*. Recorda-se que esta semântica garantia atomicidade entre o código realizado na operação *release* antes da sinalização de uma *thread* bloqueada no *acquire*  com o código realizado na operação *acquire* após o retorno da chamada a *condition.wait*.

- Como o monitor de *Lampson* e *Redell* não garante aquela atomicidade, <ins>o **estilo kernel** recupera-a movendo o código que actualiza o estado de sincronização na operação *acquire* após o bloqueio para a operação *release* que cria condições para concluir a operação *acquire*</ins>.


### Considerações Gerais sobre o "Estilo *Kernel*"

- Por cada operação *acquire* é definido um tipo de dados para representar o *Request* subjacente e uma fila de espera onde se encontram os *requests* pendentes de conclusão.

- As instâncias do tipo *Request* constituem um canal de comunicação privado entre as *threads* que se bloqueiam na operação *acquire* e as *threads* que vão consumar essas operações *acquires* quando executam as respectivas operações *release*. 

- Em termos genéricos, as instâncias do tipo *Request* deverão ter campos para: (i) `AcquireArgs`, opcional, descrevendo os argumentos da operação *acquire*; (ii) `AcquireResult`, opcional, para armazenar o resultado da operação *acquire*; (iii) `done` um campo do tipo `boolean`, obrigatório, que indica se a operação *acquire* subjacente já foi, ou não, concluída por uma *releaser thread*.

- A estrutura geral do código das operações *acquire* consta dos seguintes passos:
	
	- Após entrar no monitor, a *acquirer thread*, começa por considerar as operações em fila de espera de acordo com a respectiva disciplina; se na fila de espera não se encontram operações prioritárias, testa o estado de sincronização para determinar se o *acquire* é possível (predicado `canAcquire`); em caso afirmativo, actualiza o estado de sincronização (método `acquireSideEffect`) e devolve o respectivo resultado, se houver; no caso contrário, continua no próximo passo;
	- A seguir, a *acquirer thread* cria uma instância do objecto *Request* e insere-o na respectiva fila de espera;
	- Depois, entra num ciclo onde se bloqueia no monitor e permanece até que ocorra uma das seguintes três situações: (i) o campo `done` foi afecatdo com `true` indicando que o *acquire* foi completado; (ii) expire o tempo especificado para limite do tempo de espera, ou; (iii) a espera da *thread* seja interrompida.
	- Neste estilo, como as operações *acquire* pendentes são realizadas pelas *releaser threads* pode ocorrer uma *race condition* entre a realização do *acquire* e a desistência por *timeout* ou interrupção. Assim, antes da desistência é necessário confirmar se o *request* foi ou não concluído; em caso afirmativo, a *acquirer thread* já não poderá desistir, pois não consideramos ser sempre viável desfazer operações *acquire*.
	- Na estrutura do código que se propõe o teste da condição de *timeout* é sempre feito com a garantia de que o *request* ainda não foi concluído. Contudo, quando o bloqueio da *acquirer thread* for interrompido é necessário testar o campo `done` e se este for `true` fazer um retorno normal do método *acquire* garantindo que a interrupção não é perdida.
	- É importante referir aqui que a problemática da interrupção das *threads* bloqueads nas operações *acquire* é diferente da que foi referida para o "estilo monitor", relativamente a perda de notificações que pode acontecer nos monitores implícitos do .NET quando uma *thread* é interrompida depois de ser notificada numa variável condição e antes de reentrar no monitor (comportamento que não se verifica no *Java*).
	- No "estilo *kernel*" o problema não é uma eventual perda de notificação. Neste estilo, as *acquirer threads* expõem, antes de se bloquearem, os seus objectos *request* às *release threads* para que estas realizem as respectivas operações *acquire*. Assim, é possível que o bloqueio de uma *acquirer thread* seja interrompido ao mesmo tempo que uma *releaser thread* está dentro do monitor a realizar a respectiva operação *acquire*, uma vez que a interrupção não está sujeita à disciplina de exclusão mútua imposta pelo monitor. Resultado: a *acquirer thread* retorna da operação de *wait* sobre a variável condição com `InterruptedException`, mas o campo `done` do respectivo objecto *request* está a `true`, indicando que a operação *acquire* foi realizada. Não se considerando ser sempre viável fazer sempre *undo* das operações *acquire*, a solução proposta é retornar nornalmente do método *acquire* garantido que a interrupção não se perde (invocando `Thread.currentThread.interrupt`). 
	- Existe um aspecto a ter em atenção na implementação das desistências (por *timeout* ou interrupção), especialmente quando se implementam filas de espera com disciplina FIFO. Existem sincronizadores (como o semáforo que implementamos adiante), em que a desistência por parte de uma *acquirer thread* que se encontre à cabeça da respectiva fila de espera, pode criar condições para que sejam realizadas um ou mais *acquires* que se encontram a seguir na fila de espera (consultar comentários no código que implmenta o semáforo).


### Pseudo-código do sincronizador genérico basedo num monitor implícito do *Java* 

```Java
 /**
 * The generic synchronizer based on an *implicit Java monitor*, with support
 * for timeout on the acquire operation.
 *
 * Notes:
 *  1. This implementation takes obviously into account the possible interruption of
 *     the threads blocked on the condition variables of the monitor;
 *  2. The code structure is slightly changed due to the possibility of cancellation
 *     of the acquire operations due to timeout or interruption.
 */

class GenericSynchronizerKernelStyleImplicitMonitor {
    // implicit Java monitor that synchronizes access to the mutable shared state
    // and supports also the control synchronization on its condition variable.
    private final Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private static class Request {
        final AcquireArgs acquireArgs;  // acquire arguments
        AcquireResult acquireResult;    // acquire result
        boolean done;                   // true when the acquire is completed

        Request(AcquireArgs args) { acquireArgs = args; }
    }
    
    // queue of pending acquire requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();
    
	// synchonization state
    private SynchState syncState;

    public GenericSynchronizerKernelStyleImplicitMonitor(InitializeArgs initiallArgs) {
        initialize "syncState" according to information specified in "initialArgs";
    }

    // returns true if the synchronization state allows the acquire on behalf of the
    // thread that is at the head of the queue or the current thread if the queue is empty.
    private boolean canAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" allows an immediate acquire accordng to "acquireArgs";
    }

    // returns true if the state of synchronization allows the acquire on behalf of
    // the thread that is at the head of the queue.
    private boolean currentSynchStateAllowsAquire() {
        returns true if the current synchronization state allow(s) acquire(s);
    }

    // executes the processing associated with a successful acquire and
    // returns the proper acquire result (if any)
    private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "syncState" according to "acquireArgs" after a successful acquire;
        return "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation according to "releaseArgs".
    private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        update "syncState" according to "releaseArgs";
	}
	
	/**
	 * Methods that are independent of the synchronizer semantics
	 */

	// generic acquire operation; returns null when it times out
    public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout) throws InterruptedException {
        synchronized(monitor) {
			/**
			 * if the current thread was previously interrupted, throw the appropriate exception.
			 *
			 * this anticipates the launch of the interrupt exception that would otherwise be thrown
			 * as soon as the current thread invoked a "managed wait" (e.g., invoked Object.wait to
			 * block itself in the monitor condition variable).
			 */
			if (Thread.interrupted())
				throw new InterruptedException();
			
			// if the request queue is empty and the current synchronization state allows
			// an immediate acquire, do the acquire side effect and return the proper result.
        	if (reqQueue.size() == 0 && canAcquire(acquireArgs))
				return acquireSideEffect(acquireArgs);
			
			// the current thread must wait on the monitor condition variable;
			// create a Request object and enqueue it at the end of request queue
			Request request = new Request(acquireArgs);
			reqQueue.addLast(request);
			
			TimeoutHolder th = new TimeoutHolder(millisTimeout);
			do {
				try {
					if (th.isTimed()) {
						if ((millisTimeout = th.value()) <= 0) {
							// the timeout limit has expired - here we are sure that the
							// acquire resquest is still pending. So, we must remove the
							// request from the queue.
							reqQueue.remove(request);
							
							// after remove the request of the current thread from queue, in
							// some synchronizers (e.g, the semaphore implemented below) **it is
							// possible** that the current synhcronization allows now to satisfy
							// one or more queued acquires.
							if (currentSynchStateAllowsAquire())
								performPossibleAcquires();
								
							// return failure
							return null;
						}
						monitor.wait(millisTimeout);
					} else
						monitor.wait();
				} catch (InterruptedException ie) {
					// the current thread may be interrupted when the requested acquire
					// operation is already performed by a releaser thread, in which case
					// you can no longer give up
					if (request.done) {
						// re-assert the interrupt and return normally, indicating to the
						// caller that the acquire operation was completed successfully.
						Thread.currentThread().interrupt();
						break;
					}
					// remove the request from the request queue
					reqQueue.remove(request);
					
					// after remove this request of the current thread from queue, in
					// some synchronizers (e.g, the semaphoreimplemented below) **it is
					// possible** that the current synhcronization allows now to satisfy
					// one or more queued acquires.
					if (currentSynchStateAllowsAquire())
						performPossibleAcquires();

					// throw InterruptedException
					throw ie;
				}
			} while (!request.done);
			// the requested acquire operation completed successfully, so return its result.
			return request.acquireResult;
		}
	}

	// perform as many acquires as possible
	private void performPossibleAcquires() {
		boolean notify = false;
		while (reqQueue.size() > 0) {
			Request request = reqQueue.peek();
			if (!canAcquire(request.acquireArgs))
				break;
			// satisfy the request, this involves: (i) removing the request from the queue;
			// (ii) updating the synchronization state; (iii) affecting the result of the
			// acquire operation, and; (iv) marking the request as completed.
			reqQueue.removeFirst();
			request.acquireResult = acquireSideEffect(request.acquireArgs);
			request.done = true;
			notify = true;
		}
		if (notify) {
			// even if we release only one thread, we do not know what position it is
			// in the condition variable queue, so it is necessary to notify all blocked
			// threads to make sure that the target thread(s) are notified.
			monitor.notifyAll();
		}
	}
    
	// generic release operation
	public void release(ReleaseArgs releaseArgs) {
		synchronized(monitor) {
			// update synchronization state
			updateStateOnRelease(releaseArgs);
			// satisfy as many acquires as possible
			performPossibleAcquires();
		}
	}
}
```

### Semáforo usando o "estilo *kernel*" com base num monitor implícito do *Java* 

- O código que se apresenta a seguir resulta da adaptação do pseudo-código apresentado anteriormente à implenentação de um semáforo ou a operação *acquire* pode solicitar um número arbitrário de autorizações e a operação *release* pode devolver ao semáforo também um número arbitrário de autorizações. Na adaptação do pseudo-código genérico para implementar o semáforo foi tido em consideração o seguinte:
	- O objecto `Request` necessita de dois campos, dado que a operação *acquire* não tem resultado: `acquires` para armazenar o número de autorizações solicitado pela respectiva operação *acquire* e `done` para indicar se a operação *acquire*, já foi, ou não realizada;
	- O método `canAcquire` tem como argumento o número de autorizações solicitadas e devolve `true` se existirem autorizações suficientes sob custódia do semáforo;
	- O método `acquireSideEffect` actualiza as autorizações disponíveis tendo em consideração as autorizações concedidas pela respectiva operação *acquire*;
	- O método `updateOnRelease` actualiza o número de autorizações sob custódia do semáforo de acordo com as autorizações devolvidas pela respectiva operação *release*;
	- O método `currentSynchStateAllowsAcquire` testa se existem *acquires* em fila de espera e, em caso afirmativo, é possível realizar o acquire que se encontra à cabeça da fila de espera;  
	- A assinatura dos método `acquire` e `release` foram ajustadas para corresponder aos tipos dos parâmetros formais e dos valores de retorno.


``` Java
/**
 * Semaphore following the kernel style, using an *implicit Java monitor*, with
 * support for timeout on the acquire operation.
 */

class SemaphoreKernelStyleImplicitMonitor {
	// implicit Java monitor that synchronizes access to the mutable shared state
	// and supports also the control synchronization on its condition variable.
	private final Object monitor = new Object();
	
	// the request type
	private static class Request {
		final int acquires;     // the number of requested permits
		boolean done;           // true when completed
		
		Request(int acquires) { this.acquires = acquires; }
	}
	
	// the queue of pending acquire requests
	private final LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// the synchronization state: the number of available permits
	private int permits;
		
	// initialize the semaphore
	public SemaphoreKernelStyleImplicitMonitor(int initialPermits) {
		if (initial < 0)
			throw new IllegalArgumentException("initialPermits")
		permits = initialPermits;
	}
	
	// if there are sufficient permits, return true; false otherwise.
	private boolean canAcquire(int acquires) { return permits >= acquires; }
    
	// if there are threads in the queue, return whether the number of available
	// permits is sufficient to satisfy the request of the thread that
	// is at the front of the queue
	private boolean currentSynchStateAllowsAcquire() {
		return reqQueue.size() > 0 && permits >= reqQueue.peek().acquires;
	}
	
	// after acquire deduct the permissions granted
	private void acquireSideEffect(int acquires) { permits -= acquires; }
	
	// update the available permits in accordance with the permits released
	private void updateStateOnRelease(int releases) { permits += releases; }

	// acquires the specified number of permits; return false when it times out
	public boolean acquire(int acquires, long millisTimeout)  throws InterruptedException {
		synchronized(monitor) {
			// if the was previously interrupted, throw the appropriate exception
			if (Thread.interrupted())
				throw new InterruptedException();
			// if the queue is empty and there are sufficient permits, decrement the
			// number of available permits, and return success
			if (reqQueue.size() == 0 && canAcquire(acquires)) {
				acquireSideEffect(acquires);
				return true;
			}
			
			// the queue is not empty or there are not sufficient permits, so the
			// current thread must enqueue a request and wait on the monitor
			// condition variavel that a releaser thread complete the request
			// on its behalf.
			Request request = new Request(acquires);
			reqQueue.addLast(request);

			TimeoutHolder th = new TimeoutHolder(millisTimeout);
			do {
				try {
					if (th.isTimed()) {
						if ((millisTimeout = th.value()) <= 0) {
							// the specified time limit has expired
							reqQueue.remove(request);

							// if the request was at the head of the queue and there are more blocked
							// threads and some permits available, it is possible that this withdrawal
							// creates conditions to satisfy requests from the next thread.
							// for example, if after the remove of the request of the current thread,
							// if there are two permits available and there are two threads in the queue
							// requesting one permits each, there are mow conditions to satisfy the
							// requests of those threads.
							if (currentSynchStateAllowsAcquire())
								performPossibleAcquires();
							
							// return failure
							return false;
						}
						monitor.wait(millisTimeout);
					} else
						monitor.wait();
				} catch (InterruptedException ie) {
					// if the acquire operation was already done, re-assert interrupt
					// and return normally; else remove request from queue and throw
					// InterruptedException.
					if (request.done) {
						Thread.currentThread().interrupt();
						return true;
					}
					reqQueue.remove(request);

					// if this request was at the head of the queue and there are more blocked
					// threads and some permits available, it is possible that this withdrawal
					// creates conditions to satisfy requests from the next thread.
					// for example, if after the remove of the request of the current thread,
					// if there are two permits available and there are two threads in the queue
					// requesting one permits each, there are mow conditions to satisfy the
					// requests of those threads.
					if (currentSynchStateAllowsAcquire())
						performPossibleAcquires();
					
					// propagate InterrupedException
					throw ie;
				}
			} while (!request.done);
			// return success
			return true;
		}
	}

	// perform the possible pending acquires
	private void performPossibleAcquires() {
		boolean notify = false;
		while (reqQueue.size() > 0) {
			Request request = reqQueue.peek();
			if (!canAcquire(request.acquires))
				break;
			
			// complete a pending aqcuire request: (i) remove the request from the queue;
			// (ii) consume the requested permits; (iii) mark the request as completed;
			// (iv) ensure that that acquirer thread is notified. 
			reqQueue.removeFirst();
			acquireSideEffect(request.acquires);
			request.done = true;
			notify = true;
		}
		if (notify) {
			// even if we release only one thread, we do not know its position of the queue
			// of the condition variable, so it is necessary to notify all blocked threads,
			// to make sure that the thread(s) in question is notified.
			monitor.notifyAll();
		}
	}

	//releases the specified number of permits
	public void release(int releases) {
		synchronized(monitor) { 
			// return the release permits to the semaphore
			updateStateOnRelease(releases);
			// satisfies as many acquires as is possible
			performPossibleAcquires();
		}
	}
}
```

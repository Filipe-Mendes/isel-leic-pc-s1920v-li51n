# Aula 08 - Implementação de Sincronizadores (II)

____
### Pseudo-código para o Sincronizador Genérico com Base num Monitor Explícito do *Java*

```Java
class GenericSynchronizerMonitorStyleExplicitMonitor {
	// explicit Java monitor that supports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Lock lock = new ReentrantLock();
	private final Condition okToAcquire = lock.newCondition(); 
	
	// synchronization state
	private SynchState synchState;
	
	/**
	 * Synchronizer dependent methods
	 */

	// initialize the synchronizer
	public GenericSynchronizerMonitorStyleExplicitMonitor(InitializeArgs initialState) {
        initialize "synchState" according to information specified by "initialState";
    }

	// returns true if synchronization state allows an immediate acquire
	private boolean canAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
    }

	// executes the processing associated with a successful acquire
	private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result";
    }

	// update synchronization state due to a release operation
	private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        // update "syncState" according to "releaseArgs";
    }

	/**
	 * Synchronizer independent methods
	 */

	// generic acquire operation; returns null when it times out
	public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout)
		 					     throws InterruptedException {
		lock.lock();
		try {
			if (canAcquire(acquireArgs))
				return acquireSideEffect(acquireArgs);	
			boolean isTimed = millisTimeout >= 0;
			long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
			do {
				if (isTimed) {
					if (nanosTimeout <= 0)
						return null; // timeout
					nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
				} else
					okToAcquire.await();
			} while (!canAcquire(acquireArgs));
			// successful acquire after blocking
			return acquireSideEffect(acquireArgs);
		} finally {
			lock.unlock();
		}
	}

	// generic release operation 
	public void release(ReleaseArgs releaseArgs) {
		lock.lock();
		try {
			updateStateOnRelease(releaseArgs);
			okToAcquire.signalAll();
			// or okToAcquire.signal() if only one thread can have success in its acquire
		} finally {
			lock.unlock();
		}
	}
}
```

### *Message Queue* ao Estilo Monitor com Base num Monitor Explícito do *Java*

- Neste sincronizador faz sentido alterar os nomes dos métodos, isto é, em vez de designar os métodos por *acquire* e *release* faz mais sentido desingar os métodos por *receive* e *send*, respectivamente.

- Este código foi escrito a partir do pseudo-código anterior, começando por concretizar os tipos genéricos:
	 - `SynchState`, `LinkedList<T>` que armazena as mensagens enviadas pendentes de recepção;
	 - `InitializeArgs`, `void`;
	 - `AcquireArgs`, `void`;
	 - `AcquireResult`, `T`, que referencia a mensagem recebida ou `null`se houver desistência por *timeout*;
	 - `ReleaseArgs`, `T` que especifica a mensagem enviada para a fila.
	 
- A seguir, foram concretizados os métodos cujo código depende da semântica do sincronizador:
	- `CanAcquire` que devolve `true` se a lista das mensagens pendentes de recepção não estiver vazia;
	- `AcquireSideEffect`remove a próxima mensagem da lista e devolve essa mensagem (que será o `AcquireResult`do método *receive*):
	- `UpdateOnRelease` acrescenta a mensagem enviada com o método *send* à lista das mensagens pendentes.

- Finalmente, na parte do código que não depende da semântica do sincronizador:
	- Como cada chamada a *send* apenas viabiliza o sucesso de uma chamada a *receive* a notificação deve ser feita com `Condition.signal;
	- Nos monitores *Java* não se coloca o problema das perda de notificação quando existe simultaneadade entre notificação e interrupção.

- Tendo em consideração o que foi dito anteriormente, a implementação da *message queue* é aquela que apresentamos a seguir.

```Java
class MessageQueueMonitorStyleExplicitMonitor<T> {
	// explicit Java monitor that supports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Lock lock = new ReentrantLock();
	private final Condition okToAcquire = lock.newCondition(); 
	
	// synchronization state: messages sent yet not received
	private final LinkedList<T> pendingMessages = new LinkedList<>();
	
	/**
	 * Synchronizer dependent methods
	 */

	// initialize the synchronizer
	public MessageQueueMonitorStyleExplicitMonitor() {}

	// returns true if there is at least one pending message
	private boolean canAcquire() { return pendingMessages.size() > 0; }

	// removes a message from the pending messages list and returns it
	private T acquireSideEffect() {
		return pendingMessages.poll();
    }

	// add the sent message to the pendingMessages list
	private void updateStateOnRelease(T sentMessage) {
    	pendingMessages.addLast(sentMessage);
    }

	/**
	 * Synchronizer independent methods
	 */

	// generic acquire operation; returns null when it times out
	public T receive(long millisTimeout) throws InterruptedException {
		lock.lock();
		try {
			if (canAcquire())
				return acquireSideEffect();	
			boolean isTimed = millisTimeout >= 0;
			long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
			do {
				if (isTimed) {
					if (nanosTimeout <= 0)
						return null; // timeout
					nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
				} else
					okToAcquire.await();
			} while (!canAcquire());
			// successful acquire after blocking
			return acquireSideEffect();
		} finally {
			lock.unlock();
		}
	}

	// generic release operation 
	public void send(T sentMessage) {
		lock.lock();
		try {
			updateStateOnRelease(sentMessage);
			// a release allows only one acquire
			okToAcquire.signal();
		} finally {
			lock.unlock();
		}
	}
}
```

## Série de Exercícios 1

- Os sincronizadores `BoundedLazy<T>`e `TransferQueue<T>` podem ser implementados usando o "estilo monitor". Nos restantes é recomendada a utilização do "estilo Kernel" que será abordado a seguir.


### Exercício 1

- Implemente o sincronizador ​*bounded ​lazy*​, para suportar a computação de valores apenas quando são necessários. A interface pública deste sincronizador, em ​*Java*​, é a seguinte:

```Java
   public class BoundedLazy<E> {
     public BoundedLazy(Supplier<E> supplier, int lives);
     public Optional<E> get(long timeout) throws InterruptedException;
}
```

- A chamada do método ​**get** deve ter o seguinte comportamento: (a) caso o valor já tenha sido calculado, e ainda não tenha sido usado **​lives** vezes, retorna esse valor; (b) caso o valor ainda não tenha sido calculado ou já tenha sido usado ​**lives** vezes, inicia o cálculo de novo valor, chamando **​supplier** na própria ​*thread* invocante (depois de *sair* do monitor) e retorna o valor resultante; (c) caso já exista outra ​*thread* a realizar esse cálculo, espera até que o valor esteja calculado; (d) retorna *​empty* caso o tempo de espera exceda ​timeout​, e; (e) lança ​InterruptedException se a espera da ​thread for interrompida. Caso a chamada a ​*supplier* resulte numa excepção, o objeto passa para um estado de erro, lançando essa excepção em todas as chamadas a **get**​.

- A semântica de sincronização deste sincronizador pode ser bem implementada com uma máquina de estados, com os seguintes estados:
	- UNCREATED: o valor não está disponível porque não foi ainda calculado ou porque já foram consumidas todas as vidas de um valor calculado previamente;
	- CREATING: encontra-se uma *thread* a calcular o valor;
	- CREATED: o valor está disponível para ser usado *lives* vezes;
	- ERROR: ocorreu uma excepção na chamada a **supplier** e o sincronizador ficou em estado de erro.

- Dados immutáveis (qualificador `final`):
	- *supplier*
	- *lives*
	
- Estado de sincronização mutável:
	- *state*
	- *value*
	- *current_lives*
	- *exception*

#### Estrutura do método `BoundedLazy<T>.get` usando um monitor implícito do *Java* 
	
``` Java
Optional<T> get(long timeout) throws InterruptedException {
	synchronized(monitor) {
		// execute state machine algorithm
	}
	// a thread will compute the value when state == CREATING
	T v = null;
	Exception ex = null;
	try {
		v = supplier.get();
	} catch(Exeception _ex) {
		ex = _ex;
	}
	// after theh value is computed, reacquire the lock, update state and notify waiters
	synchronized(monitor) {
		if (ex != null) {
			exception = ex;
			// set state to ERROR
			// notify all blocked threads
			throw ex; 
		} else {
			value = v;
			current_lives = lives;
			state = CREATED;
			// notify all bloqued threads
			return Optional.of(value);
		}
	}
}
```

### Acquisição dos Locks do Monitor Implícito .NET e Reentrada no Monitor

- No .NET pode ser lançada a excepçao ThreadInterruptException na aquisição do *lock* dos monitores implícitos, nas circunstâncias em que a *thread* invocante seja interrompida enquanto aguarda a aquisição do *lock*. (Em *Java* isto não acontece com os monitores implícitos e só acontece nos monitores explícitos se o *lock* for adquirido com o método `Lock.lockInterruptibly`).

- Assim, a reaquisição obrigatória do *lock* de um monitor implícito em .NET deve ser feita do seguinte modo:

``` C#
class MonitorEx {

	public static EnterUninterruptible(object monitor, out bool interrupted) {
		interrupted = false;
		do {
			try {
				Monitor.Enter(monitor);
				break;
			} catch (ThreadInterruptedException) {
				interrupted = true;
			}
		} while (true);
	}
}
```

- O código de reaquisição do *lock* do monitor deverá ser o seguinte:

``` C#
	bool interrupted = false;
	MonitorEx.EnterUninterruptible(monitor, out interrupted);	// monitor enter
	try {
		// critical section executed inside the monitor
	} finally {
		Monitor.Exit(monitor);		// monitor leave
		// if we were interrupted, re-assert interruption
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
```

## Limitações na implementação de Sincronizadores ao "Estilo Monitor"

- Os monitores com a semântica de notificação de *Lampson* e *Redell* não garantem atomicidade entre o código que é executado dentro do monitor por uma *releaser thread* antes da notificação de uma *threads* bloqueadas e o código executado pela *acquirer thread* após retorno da operação de *wait* sobre uma variável condição. (Recorda-se que esta atomicidade era garantida pela semântica de sinalização proposta por *Brich Hansen* e *Hoare*).

- A semântica de *Lampson* e *Redell* permite que entre a alteração ao estado de sincronização feita numa operação *release* antes da notificação de uma *thread* bloqueada no monitor e a reacção a essa alteração de estado por parte da *acquirer thread* notificada, **terceiras** *threads* possam entrar no monitor (devido ao *barging*) e possam modificar o estado de sincronização, interferindo com a *acquirer thread* notificada no *realese*. (Para efeitos de simplificação do texto, refere-se a notificação apenas de uma *acquirer thread*; se forem notificadas várias *acquirer threads* nem sequer poderíamos falar em atomicidade, a menos que, com acontece com a semântica de *Brinch Hansen* e *Hoare* fosse estabelecida uma cadeia, onde cada as *threads* notificadas recebia o estdado do monitor deixado pela anterior.

- Nos sincronizadores em que o estado de sincronização reflete sempre o resultado das operações *acquire* e *release* realizadas anteriormente, como é o caso do semáforo ou de uma *message queue* esta  falta de atomicidade não torna inviável a implementação de sincronizadores usando o "estilo monitor". Não é possível garantir a ordem com que são realizadas as operações *acquire* viabilizadas por um *release*, mas não existe quebra da semântica de sincronização. No caso do semáforo, o seu estado de sincronização reflecte sempre o número de autorizações sob custódia do semáforo; no caso da *message queue* o seu estado de sincronização reflete sempre as mensagens que foram enviadas e ainda não foram recebidas.

- Contudo, em sincronizadore onde a semântica é definida em termos de transições de estado ou onde existem operação de *reset* do estado de sincronização, a interferência de **terceiras *threads*** desaconselha implementações segundo o "estilo monitor". São exemplos:

	- No *manual-reset event* ou no *auto-reset event* que por suportarem um operação de *reset*, é possível ocorrer uma operação de *reset* entre uma operação de *set* e a *thread(s)* notificada(s) reentrar(em) no monitor. Nesta situação, a semântica da operação *set* (libertar todas as *threads* no *manual-reset event* ou uma *thread* no *auto-reset event*) pode não se verificar.
		
	- Para evitar *starvation* das *threads* leitoras e escritoras na implementação do *read/write lock*, *Hoare* propôs a seguinte semântica: (a) quando existirem *threads* escritoras bloqueadas no *lock*, não são concedidos mais *read locks*, até que todos os existentes seja libertados - isto impede *starvation* das *threads* escritoras por parte das *threads* leitoras -, e; (b) quando é libertado o *write lock*, é garantido o *read lock* a todas as *threads* leitoras que se encontrem <ins>nesse momento</ins> bloqueadas, mas não às *threads* leitoras que tentem adquirir o *read lock* posteriromente - assim, é necessário distinguir entre as *threads* leitoras que se encontram bloqueadas e aquelas que tentem adquirir o *read lock* posteriormente - isto garante, que as *threads* escritoras não provocam *startvation* às *threads* leitoras;
	
	- O sincronizador *exchanger* que suporta a troca de mensagens entre pares de *threads*, também so pode ser implementado ao "estilo monitor" usando uma máquina de estados. Neste sincronizador, a primeira *thread* do par tem que se bloquear a aguardar a chegada da segunda *thread*; quando isto acontece, é preciso consumar a troca se a intervenção de **terceira(s)** *thread(s)*. Neste caso, a "terceira" *thread* deverá ser a primeira do próximo par.
	
	- Quando existe a necessidade de implementar disciplinas de fila de espera especícifas, é sempre necessário implementar explicitamente as filas de espera.
	
## Soluções

- Alguns dos problemas enunciados anteriormente podem ser resolvidos implementando as operações *acquire* com base em máquinas de estados de modo a impedir que uma terceira *thread* acedam ao estado de sincronização antes da conclusão das operações *acquire* viabilizadas por uma operação *release*. Exemplos:

	- A implementação do *exchanger* com base numa máquina de estados, poderia considerar três estados: *idle* quando aguarda a chegada da primeira *thread*; *exchanging*, depois da chegada da primeira *thread* de um par; *closing* depois da chegada da segunda *thread* até que a primeira *thread* reentre no monitor após notificação. Quando a primeira *thread* reentra no monitor completa a troca e coloca o *exchanger* no estado *idle*. Qualquer *thread* que entre no monitor quando o *exchanger* está no estado *closing*, bloqueia-se a aguardar que o *exchanger* transite para o estado *idle* (tornando a primeira *thread* de uma próxima troca), ou *exchanging* (tornando-se a segunda *thread* de uma próxima troca já iniciada por outra *thread*).

- As solução em que as operações *acquire* são baseadas em máquinas de estados não permitem resolver as situações em que a semântica de sincronização é definida em função de transições de estado como acontece no caso do *read/write lock*.

### Implementação de Sincronizadores ao "Estilo *Kernel*"

- A solução que permite implementar toda e qualquer semântica de sincronização segue um padrão que vamos designar por **estilo kernel** (nas folhas da disciplina, este estilo foi designado **delegação de execução**).

- A ideia que está por detrás do "estilo *kernel*" é simples: <ins>realizar atomicamente o processamento da operação *release* e a conclusão do processamento de todas as operações *acquire* pendentes que são viabilizadas pela operação *release*</ins>.

- A título de curiosidade, o "estilo *kernel*" procura resgatar a atomicidade proposta por *Brinch Hansen* e *Hoare* para a sinalização das *threads* bloqueadas no monitor. Esta semântica, garantia atomicidade do código realizado pela operação *release* antes da sinalização com o código realizado na operação *acquire* após o retorno da chamada a *condition.signal*. Como o monitor de *Lampson* e *Redell* não garante aquela atomicidade, <ins>o **estilo kernel** recupera-a movendo o código que actualiza o estado de sincronização na operação *acquire* após o bloqueio para a operação *release* que cria condições para concluir a operação *acquire*</ins>.

   

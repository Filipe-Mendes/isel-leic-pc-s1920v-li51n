# Aula 19 - Optimização em Sincronizadores Implementados com Base em Monitor

___

- Nas implementações de sincronizadores com base em monitor que fizemos anteriormente, todas as operações sobre o sincronizador implicam a aquisição e a libertação do _lock_ do monitor subjacente. Como vimos em aulas anteriores, isso implica que o custo mínimo de uma operação de _acquire_, _release_ ou _reset state_ terá no mínimo o custo de duas instruções atómicas, o que corresponde à aquisição e libertação do _lock_ na ausência de contenção.

- Tirando partido das garantias de visibilidade dadas pelas variáveis atómicas e das garantias de visibilidade de atomicidade e visibilidade é possível combinar técnicas usadas nos algoritmos _nonblocking_ com a utilização de monitores para optimizar o _fast-path_ das operações _acquire_ e _release_ que correspondem às operações _acquire_ que podem ser feitas de imediato e as operações _release_ que não têm que desbloquear _thread(s)_ bloqueadas na respectiva operação _acquire_.

- Muitos sincronizadores de utilização comum que estão disponíveis nas plataformas _Java_  e .NET como por exemplo, _manual-reset event_, _count down event_, _count down latch_, _semaphore unfair_ e _linked blocking queue_ podem ser implementados utilizando a técnica que abordaremos a seguir.


### Condições Necessária para Aplicação da Optimização

- Para aplicar esta optimização nos _fast-path_ da operações com as semânticas _acquire_ e _release_ em sincronizadores implementados com base em monitor, é necessário que se verifiquem as duas condições seguintes:

	- A implementação do sincronizador deve ser feita usando o "estilo monitor";
	
	- Ser possível implementar as operações elementares `tryAcquire` e `doRelease` usando técnicas _nonblocking_. 

### Algoritmo da Optimização

- Partimos do princípio que é possível implementar as operações `tryAcquire` e `doRelease` usando técnicas _nonblocking_.

- Ficará assim apenas por resolver o problema de garantir que qualquer _acquirer thread_ que se bloquei no monitor será desbloqueda, pois, na operação _acquire_, não será possível garantir a atomicidade da operação _check-then-act_ que determina a necessidade de bloqueio e o bloqueio efectivo da _acquirer thread_. Esta falta de atomicidade terá que ser de alguma forma resolvida na operação _release_, garantindo que quando há um _race_ entre uma operação _acquire_ que bloqueia a _thread_ invocante e um _release_ que deveria desbloquear essa _thread_ isso de facto acontece.

- O algoritmo proposto para garantir a sincronização de controlo - bloqueio no _acquire_ e notificação no _release_ - não depende da semântica de um sincroniador concreto. Este algoritmo baseia-se em ter, para além do estado de sincronização, a variável _volatile_ `waiters` que contém o número de _threads_ bloqueadas; a visibidade desta variável é garantida pelo facto de ser declarada como `volatile`; contudo, a atomicidade será garantida pelo _lock_ do monitor, uma vez que a mesma apenas vai ser incrementada e decrementada na operação _acquire_ com a posse do _lock_ do monitor. O algoritmo para resolver o problema da não atomicidade do _check-then-act_ é o seguinte:

	- Uma _thread_ que chame a operação _acquire_ e cuja chamada a `tryAcquire` devolve `false` adquire o _lock_ do monitor;
	
	- Após a aquisicão do _lock_, incrementa a variável _volatile_ `waiters` para indicar que vai bloquear-se numa variável condição do monitor (as alterações da variável `waiters` - incrementar e decrementar - não necessita de usar uma instrução atómica, dado que as alterações são sempre feitas na posse do _lock_);
	
	- Após garantir que o incremento da variável `waiters` é visivel a todos os processadores (em _Java_ isto é garantido pela semântica da escrita `volatile`, contudo, no .NET _framework_ é necessário invocar o método `Interlocked.MemoryBarrier` para ter as mesmas garantias de visibilidade) e antes de se bloquear, a _thread_ volta a invocar o método `tryAcquire`; se este método devolver `true` a operação _acquire_ termina com sucesso; no caso contrário, a _thread_ bloqueia-se numa varíavel condição do monitor; Após ser notificada a _acquirer thread_, volta a chamar o método `tryAcquire` para determinar se pode realizar a operação _acquire_ ou se tem que se voltar a bloquear (ciclo típico das implementações ao "estilo monitor");
	
	- A operação _release_ começa com a actualização do estado de sincronização invocando o método `doRelease` garantindo que a actualização fica visível a todos os processadores (a visbilidade é sempre garantida se a actualização for feita com uma instrução atómica ou escrita _volatile_ em _Java_, contudo, no .NET _framework_ é necessário invocar o método `Interlocked.MemoryBarrier`); a seguir, a _releaser thread_ testa se existem _threads_ bloqueadas no monitor (isto é, se a variável `waiters` é maior do que zero) e, em caso afirmativo, adquire o _lock_ do monitor; após adquirir a posse do _lock_ do monitor, repete o teste da variável `waiters` para confirmar se existem efectivamente _threads_ bloqueadas (este teste é necessário para evitar notificações desnecessárias nas situações em que uma _acquirer thread_ não se bloqueia efectivamente após incrementar a variável `waiters`). Se houver _threads_ bloqueadas no monitor, a _releaser thread_ procede às necessárias notificações.
	  
- Este algoritmo funciona sempre qualquer que seja a forma como são intercaladas as acções realizadas pela _acquirer thread_ e pela _releaser thread_, porque:

	- A _acquirer thread_ ao perceber que tem que se bloquear anuncia a todos os processadores: "vou bloquear-me!"; imediatamente a seguir pergunta: "mas é mesmo necessário bloquear-me?". Em caso de resposta negativa, isto é, `tryAcquire` devolve `true` a operação _acquire_ tem sucesso e a _thread_ não se bloqueia.
	
	- A _releaser thread_ começa por anunciar a todos os processadores: "eu viabilizo uma ou mais operações _acquire_!". A seguir, pergunta: "existem _threads_ bloqueadas que possam ver as suas operações _acquire_ satisfeitas por este _release_?". Em caso afirmativo, adquire a posse do _lock_ do monitor e procede às necessária(s) notificação(ões).

- Em todas as combinações possiveis das acções na _acquirer thread_ e da _release thread_ não existe nenhuma possibilidade de uma _acquirer threas_ se bloquear sem que seja notificada por uma _releaser thread_ concorrente.

- Existe mais um aspecto importante a ter em atenção que é a necessidade de ter que ser usado o método `tryAcquire` para testar e alterar o estado de sincronização, mesmo quando no método _acquire_ uma _thread_ tem a posse do _lock_ do monitor. A razão para isto deve-se ao facto do estado de sincronização ser acedido por  _threads_ que não estão na posse do _lock_ (exactamente no método ´doRelease´ e na chamada a `tryAcquire`no início da operação _acquire_).


### Implementação do Semáforo

- A seguir apresenta-se a implementação em _Java_ de um semáforo que suporta operações _acquire_ de uma única autorização e operações _release_ de múltiplas autorizações.  

- O número de autorizações sob custódia do semáforo é armazenado numa instância de `AtomicInteger`, logo é trivial implementar as operações `tryAcquire` e `doReleasse` usando o suporte para operações atómicas.


```Java
import java.util.Random;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.*;

public final class Semaphore {

	private final AtomicInteger permits;
	private volatile int waiters;
	private final Lock lock;
	private final Condition okToAcquire;
	 
	// Constructor
	public Semaphore(int initial) {
		if (initial < 0)
			throw new IllegalArgumentException();
		lock = new ReentrantLock();
		okToAcquire = lock.newCondition();
		permits = new AtomicInteger(initial);
	}
	
	public Semaphore() { this(0); }
	
	// tries to acquire one permit
	public boolean tryAcquire() {
		while (true) {
			int observedPermits = permits.get(); 
			if (observedPermits == 0)
				return false;
			if (permits.compareAndSet(observedPermits, observedPermits - 1))
				return true;
		}
	}
	
	// releases the specified number of permits
	private void doRelease(int releases) {
		permits.addAndGet(releases);
		// Java guarantees that this write is visible before any subsequent reads
	}
	
	// Acquire one permit from the semaphore
	public boolean acquire(long timeout, TimeUnit unit) throws InterruptedException {
		// try to acquire one permit, if available
		if (tryAcquire())
			return true;
		
		// no permits available; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// if a time out was specified, get a time reference
		boolean timed = timeout > 0;
		long nanosTimeout = timed ? unit.toNanos(timeout) : 0L;
		
		lock.lock();
		try {
			
			// the current thread declares itself as a waiter..
			waiters++;
			/**
			 * Java: JMM guarantees non-ordering of previous volatile write of "waiters"
			 * with the next volatile read of "permits"
			 */
			try {		
				do {
					// after increment waiters, we must recheck if acquire is possible!
					if (tryAcquire())
						return true;
					// check if the specified timeout expired
					if (timed && nanosTimeout <= 0)
						return false;
					if (timed)
						nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
					else
						okToAcquire.await();
				} while (true);
			} finally {
				// the current thread is no longer a waiter
				waiters--;
			}	
		} finally {
			lock.unlock();
		}
	}
	
	public void acquire() throws InterruptedException {
		acquire(-1, TimeUnit.MILLISECONDS);
	}

	public boolean acquire(int timeoutMillis) throws InterruptedException {
		return acquire(timeoutMillis, TimeUnit.MILLISECONDS);
	}
	
	// Release the specified number of permits
	public void release(int releases) {
		doRelease(releases);	// this has volatile write semantics so, it is visible before read waiters
		if (waiters > 0) {	
			lock.lock();
			try  {
				// We must recheck waiters, after enter the monitor in order
				// to avoid unnecessary notifications 
				if (waiters > 0) {
					if (waiters == 1 || releases == 1)
						okToAcquire.signal(); // only one thread can proceed execution
					else
						okToAcquire.signalAll(); // more than only one thread can proceed  execution
				}
			} finally {
				lock.unlock();
			}
		}
	}

	// Release one permit
	public void release() { release(1); }
}

```

### Implementação do _Manual-Reset Event_

- A seguir apresenta-se a implementação em C# de um _manual-reset event_.

- O estado deste sincronizador que é relevante nos _fast-paths_ é um `boolean` pelo que é trivial implementar as operações `tryAcquire` e `doReleasse` porque nem sequer se coloca o problema da atomicidade.

- Esta implementação ilustar em duas situações a necessidade de invocar o método `Interlocked.Memory` para garantir imediatamente a visibilidade a todos os processadores das escritas nas variáveis `signaled` e `waiters`.

```C#
using System;
using System.Threading;

public sealed class ManualResetEventSlim_ {
	private volatile bool signaled;		// true when the event is signaled
	private volatile int waiters;		// the current number of waiter threads - atomicity granted by monitor
	private int setVersion;				// the version of set operation - atomicty granted by monitor
	private readonly object monitor;
	
	// Constructor
	public ManualResetEventSlim_(bool initialState) {
		monitor = new object();
		signaled = initialState;
	}
	
	// return true when tha Wait must return
	private bool tryAcquire() { return signaled; }
	
	// set signaled to true and make it visible to all processors
	private void DoRelease() {
		signaled = true;
		/**
		 * In order to guarantee that this write is visible to all processors, before
		 * any subsequente read, notably the volatile read of "waiters" we must
		 * interpose a full-fence barrier.
		 */
		Interlocked.MemoryBarrier();
	}
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {
	
		// If the event is signalled, return true
		if (tryAcquire())
			return true;
		
		// the event is not signalled; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// if a time out was specified, get a time reference
		TimeoutHolder th  = new TimeoutHolder(timeout);
		
		lock(monitor) {
		
			// get the current setVersion and declare the current thread as a waiter.						
			int sv = setVersion;
			waiters++;
			
			/**
			 * before we read the "signaled" volatile variable, we need to make sure that the increment
			 * of *waiters* is visible to all processors.
			 * In .NET this means interpose a full-fence memory barrier.
			 */			
			Interlocked.MemoryBarrier();
			
			try {
				/**
			 	 * after declare this thread as waiter, we must recheck the "signaled" in order
			 	 * to capture a check that ocorred befor we increment the waiters.
			 	 */
				if (tryAcquire())
					return true;

				// loop until the event is signalled, the specified timeout expires or
				// the thread is interrupted.
				do {				
					// check if the wait timed out
					if ((timeout = th.Value) == 0)
						// the specified time out elapsed, so return failure
						return false;
				
					Monitor.Wait(monitor, timeout);
				} while (sv == setVersion);
				return true;
			} finally {
				// at the end, decrement the number of waiters
				waiters--;
			}
		}
	}
		
	// Set the event to the signalled state
	public void Set() {
		DoRelease();
		// after set the "signaled" to true and making sure that it is visble to all
		// processors, check if there are waiters
		if (waiters > 0) {		
			lock(monitor) {
				// We must recheck waiters after acquire the lock in order
				// to avoid unnecessary notifications
				if (waiters > 0) {
					setVersion++;
					Monitor.PulseAll(monitor);
				}
			}
		}
	}

	// Reset the event
	public void Reset() { signaled = false; }
}
```

___


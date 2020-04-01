# Aula 07 - Implementação de Sincronizadores (I)

____
## Conceito de Monitor

- O conceito de monitor define um meta-sincronizador adequado à implementação de sincronizadores (ou *schedulers* de "recursos").

- Unifica todos os aspectos envolvidos na implementação de sincronizadores: <ins>os dados partilhados</ins>, <ins>o código que acede a esses dados</ins>, <ins>o acesso aos dados partilhados em exclusão mútua</ins> e <ins>a possibilidade de bloquear e desbloquear *threads* em coordenação com a exclusão mútua</ins>.

- Este mecanismo foi proposto inicialmemte como construção de uma linguagem de alto nível (Concurrent Pascal) semelhante à definição de classe nas linguagens orientadas por objectos.

- Foram considerados dois tipos de procedimentos/métodos: os procedimentos de entrada (públicos),que podem ser invocados de fora do monitor e os procedimentos internos (privados) que apenas podem ser invocados pelos procedimentos de entrada.

- O monitor garante, que num determinado momento, <ins>quanto muito uma *thread* está *dentro* do monitor<ins>. Quando uma *thread* está dentro do monitor é atrasada a execução de qualquer outra *thread* que invoque um dos seus procedimentos de entrada. 

- Para bloquear as *threads* dentro do monitor *Brinch Hansen* e *Hoare* propuseram o conceito de variável condição (que, de facto, nem são variáveis nem condições, são antes filas de espera onde são blouqeadas as *threads*). A ideia básica é a seguinte: quando as *threads* não têm condições para realizar a operação *acquire* que pretendem bloqueiam-se nas variáveis condição; quando outras *threads* a executar dentro do monitor alteram o estado partilhado sinalizam as *threads* bloqueadas nas variáveis condição quando isso for adequado.

### Semântica de Sinalização de *Brinch Hansen* e *Hoare*

- **Esta semântica de sinalização não se encontra implememtada em nenhum dos *runtimes* actuais que suportam o conceito de monitor**

- Esta semântica da sinalização requer que uma *thread* bloqueadas numa variável condição do monitor execute imediatamente assim que outra *thread* sinaliza essa variável condição; a *thread* sinalizadora reentra no monitor assim que a *thread* sinalizada o abandone.

### Semântica de Notificação de *Lampson* e *Redel*

- Foi implementada na linguagem Mesa que suportava concorrência.

- **Esta semântica de sinalização é a que é implememtada por todos os *runtimes* actuais que suportam o conceito de monitor**

- Considerando que a semântica de sinalização proposta por *Brinch Hansen* e *Hoare* era demasiado rígida (entre outros aspectos, não permitia a interrupção ou aborto das *threads* bloqueadas dentro dos monitores, propuseram uma alternativa à semântica da sinalização.

- Quando uma *thread* estabelece uma condição que é esperada por outra(s) *thread(s)*, eventualmente bloqueada, notifica a respectiva variável condição. Assim a operação *notify* é entendida como um aviso ou conselho à *thread* bloqueada e tem como consequência que esta reentre no monitor algures no futuro.

- O *lock* do monitor tem que ser readquirido quando uma *thread* bloqueada pela operação *wait* reentra no monitor. **Não existe garantia de que qualquer outra *thread* não entre no monitor antes de uma *thread* notificada reentrar (fenómeno que se designa por *barging*). Além disso, após uma *thread* notificada reentrar no monitor não existe nenhuma garantia que o estado do monitor seja aquele que existia no momento da notificação (garantia dada pela semântica de *Brinch Hansen* e *Hoare*). É, por isso, necessário que seja reavaliado o predicado que determina o bloqueio.

- Esta semântica tem como primeira vantagem não serem necessárias comutações adicionais no processo de notificação.

- A segunda vantagem é ser possível acrescentar mais três formas de acordar as *threads* bloqueadas nas variáveis condição: (a) por ter sido excedido o limite de tempo especificado para espera (*timeout*); (b) interrupção ou aborto de uma *thread* bloqueada; (c) fazer *broadcast* numa condição, isto é, acordar todas as *threads* nela bloqueadas.

## Monitores Disponíveis em *Java* e .NET

### Monitores Implícitos em *Java*
 
 - São associados de forma *lazy* aos objectos, quando se invoca a respectiva funcionalidade.
 
 - Suportam apenas uma variável condição anónima.
 
 - O código dos *procedimentos de entrada* (secções críticas) é definido dentro de métodos ou blocos marcados com `synchronized`. A funcionalidade das variáveis condição está acessivel usando os seguintes métodos da classe `java.lang.Object`: `Object.wait`, `Object.notify` e `Object.notifyAll` (broadcast).
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* é reportada sempre a notificação e só depois será reportada a interrupção.

#### Implementação do *Single Resource* com base num  monitor implícito do *Java* 

```Java
public class SingleResource {
	private final Object monitor; // the monitor
	private boolean busy;         // the synchronization state
	
	public SingleResource(boolean busy) {
		monitor = new Object();
		this.busy = busy;
	}
	
	public SingleResource() { this(false); }
  
	// acquire resource
	public void acquire() throws InterruptedException {
		synchronized(monitor) {	// this block is a critical section executed inside the monitor
			while (busy)
				monitor.wait();	// block current thread on monitor's condition variable
			busy = true;
		}
	}
  
	// release the previously acquire resource
	public void release() {
		synchronized(monitor) {	// this block is a critical section executed inside the monitor
			busy = false;
			monitor.notify();	// notify one thread blocked in the monitor by acquire
		}
	}
}
```

### Monitores Explícitos em *Java*
 
 - São implementados pelas classes `java.util.concurrent.locks.ReentrantLock` e `java.util.concurrent.locks.ReentrantReadWriteLock` que implementam as interfaces `java.util.current.locks.Lock`e `java.util.current.locks.Condition`.
 
 - Suportam um número arbitrário de variáveis condição.
 
 - O código dos "procedimentos de entrada" tem que explicitar a aquisição e libertação do *lock* do monitor. Exemplo:
 
 
 ```Java
monitor.lock();
try {
	 // critical section
} finally {
	lock.unlock();
}
```

- As variáveis condição são acedidas através dos métodos definidos na interface `java.util.concurrent.locks.Condition`, nomeadamente: `Condition.await`, `Condition.awaitNanos`, `Condition.signal` e `Condition.signalAll`.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* é reportada sempre a notificação e só depois, eventualmente, é reportada a interrupção. 
 
#### Implementação do *Single Resource* com base num  monitor explícito do *Java*

```Java

import java.util.concurrent.locks.*;

public class SingleResourceEx {
	private final Lock monitor;       // the lock
	private final Condition nonBusy;  // the condition variable
	private boolean busy;             // the synchronization state
	
	public SingleResourceEx(boolean busy) {
		monitor = new ReentrantLock();		// create the monitor
		nonBusy = monitor.newCondition();	// get a condition variable of the monitor
		this.busy = busy;
	}
	
	public SingleResourceEx() { this(false); }
  
	// acquire resource
	public void acquire() throws InterruptedException {
    	monitor.lock();		// enter the monitor, that is, acquire the monitor's lock
   		try { 	// this block is the critical section executed inside the monitor
			while(busy)
				nonBusy.await();	// block current thread on the onBusy conditon variable,
					 				// obviously leaving the monitor
			busy = true;	// acquire the resource
		} finally {
			monitor.unlock();	// release the lock, that is, leave the monitor
		}
	}
  
	// release the previously acquire resource 
	public void release() {
		monitor.lock();
		try {
			busy = false;		// mark resource as free
			nonBusy.signal();	// notify one thread blocked on onBusy condition variable; if there
								// is at least one thread blocked, it will reenter the monitor and
								// try to acquire the resource
		} finally {
			monitor.unlock();
		}
	}
}
```

### Monitores Implícitos em .NET
 
 - São associados de forma *lazy* às instâncias dos tipos referência (objectos), quando se invoca a respectiva funcionalidade.
 
 - Suportam apenas uma variável condição anónima.
 
 - Estão acessíveis usando os métodos estáticos da classe `System.Threading.Monitor`, nomeadamente: `Monitor.Enter`, `Monitor.TryEnter`, `Monitor.Exit`, `Monitor.Wait`, `Monitor.Pulse` e `Monitor.PulseAll`. O código dos "procedimentos de entrada" (secções críticas) pode ser defindo com a construção `lock` do C# que é equivalente aos blocks `synchronized`no *Java*.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* pode ser reportada a interrupção e ocultada a notificação. **Assim, em situações em que se notifica apenas uma *thread* pode ser necessário capturar a excepção de interrupção para regenerar uma eventual notificação que possa ter ocorrido em simultâneo**.

#### Implementação do *Single Resource* com base num  monitor implícito do .NET

```C#
using System.Threading;

public class SingleResource {
	private readonly object monitor;	// the monitor with the associated condition variable
	private bool busy;					// the synchronization state
  
	public SingleResource(bool busy = false) {
		monitor = new object();
		this.busy = busy;
	}
	
	/**
	 * Synchronizer specific methods - following the generic synchronizar code pattern
	 */
	 
	 private bool CanAcquire() { return !busy; }
  
	 private void AcquireSideEffect() { busy = true; }
  
  	private void UpdateOnRelease() { busy = false; }
  
	// acquire the resource
	public void Acquire() {
		lock(monitor) {	// this block is the critical section executed "inside" the monitor
			try {	// since we are notifying with Monitor.Pulse we must catch the interrupted exception
					// in order to regenerate eventual losed notification due to thread interruption
				while (!CanAquire())
					Monitor.Wait(monitor);	// block the current thread on the monitor's condition variable
			} catch (ThreadInterruptedException) {
				// as is possible that the current thread was notified (and interrupted), if
				// the resource is busy, we must regenerate the notification, that is, notify 
				// another blocked thread if there is one
				if (!busy)
					Monitor.Pulse(monitor);
				
				throw;    // rethrow ThreadInterruptedException
			}
			// the resource if free, mark it as busy
			AcquireSideEffect();
		}
	}
  
	// release the previously acquired resource
	public void release() {
		lock(monitor) {
			updateStateOnRelease();		// mark the resource as free
			Monitor.Pulse(monitor);         // notify one of the threads block on the monitor's condition variable.
											// Opps! This notification an be losed due to an simultaneous INTERRUPT.
											// This issue is taken into account in the acquire() método.
			/**
			 * if you are a brute force enthusiast, you can work around this problem
			 * by never using Monitor.Pulse and always notifying with Monitor.PulseAll.
			 */
		 }
  	}
}
```

### Extensão aos Monitores Implícitos em .NET

 - A classe MonitorEx, disponível em `src/utils/MonitorEx.cs` implementa uma extensão aos monitores implícitos do .NET que suportam monitores com múltiplas condições suportados nos monitores implícitos de múltiplos objectos. Um dos objectos representa o monitor e uma variável condição e os outros apenas representam variáveis condição. Os método `MonitorEx.Wait`, `MonitorEx.Pulse` e `MonitorEx.PulseAll` recebem como argumentos dois objectos: o objecto que representa o monitor e o objecto que representa a condição; quando se está a usar a condição do objecto que representa o monitor os dois objectos são iguais.
 
 - Será discutida a implementação deste tipo de monitor quando for discutida a <ins>notificação específica de *threads*</ins> especialmente relevante na impletação de sincronizadores ao "estilo kernel".
 
____

## *Timeouts* em Sincronizadores Implementados com Base em Monitor

- Na implementação de sincronizadores com base em monitor, o suporte para *timeout* nas operações *acquire* tem que ter em consideração que as *threads* podem bloquear-se várias vezes nas variáveis condição antes de poderem realizar a operação *acquire*.

- Ainda que estejam disponíveis *overloads* dos métodos *wait/await* nas variáveis condição que permitam especificar um limite para o tempo de bloqueio (*timeout*), não se pode passar simplesmente a estes métodos o valor do *timeout* especificado pelo chamador da operação *acquire*.

- Se, por exemplo, for especificado um timeout de 10 segundos numa chamada à operação *acquire*, este deve ser o valor especificado na primeira chamada ao método *wait*. Contudo, se a *thread* for notificada após 6 segundos e o estado de sincronização não permitir realizar a operação *acquire*, a *thread* tem que voltar a bloquear-se.

- No segundo e posterior chamadas ao método *wait*, não se devem passar o *timeout* dos 10 segundos, mas o valor remanescente, que, no nosso exemplo, são 4 segundos.

- Para simplificar o suporte de *timeouts* na implementação de sincronizadores com base em monitor, estão disponíveis o tipo TimeoutHolder para *Java* e .NET, nos ficheiros, `src/utils/TimeoutHolder.java` e `src/utils/TimeoutHolder.java`, cujo código se apresenta a seguir.

- Os monitores explícitos do *Java* têm um *overload* do método *wait*, `awaitNanos` que recebe como argumento o valor do *timeout* em nanosegundos e devolve o número de nanosegundos que faltavam para expirar o *timeout* ou um valor menor ou igual a `0L` se o *timeout* tiver expirado. Quando se usa este tipo de monitor deve usar-se esta funcionalidade.


#### Implementação do tipo `TimeoutHolder` em *Java*

```Java
import java.util.concurrent.TimeUnit;

public class TimeoutHolder {
	private final long deadline;		// timeout deadline: non-zero if timed
	
	public TimeoutHolder(long millis) {
		deadline = millis > 0L ? System.currentTimeMillis() + millis: 0L;
	}
	
	public TimeoutHolder(long time, TimeUnit unit) {
		deadline = time > 0L ? System.currentTimeMillis() + unit.toMillis(time) : 0L;
	}
	
	// returns true if a timeout was defined
	public boolean isTimed() { return deadline != 0L; }
	
	// returns the remaining timeout
	public long value() {
		if (deadline == 0L)
			return Long.MAX_VALUE;
		long remainder = deadline - System.currentTimeMillis();
		return remainder > 0L ? remainder : 0L;
	}	
}
```

#### Implementação do tipo `TimeoutHolder` em .NET

```C#
public struct TimeoutHolder {
	private refTime;			// the timeout is referred to this timestamp
	private int timeout;		// the remainig timeout
	
	public TimeoutHolder(int timeout) {
		this.timeout = timeout;
		// if the timeout is zero (immediate) ou Infinite, we do not need a time reference
		this.refTime = (timeout != 0 && timeout != Timeout.Infinite) ? Environment.TickCount : 0;
	}
	
	// returns the remaining timeout
	public int Value {
		get {
			if (timeout != 0 && timeout != Timeout.Infinite) {
				// take the current timestamp, and subract elapsed time if any
				int now = Environment.TickCount;
				if (now != refTime) {
					int elapsed = now - refTime;
					refTime = now;
					timeout elapsed < timeout ? timeout - elapsed: 0;
				}
			}
			return timeout;
		}
	}
}
```

____
### Pseudo Código do Sincronizador Genérico ao "Estilo Monitor" em .NET usando Monitor Implícito

- Xxx

```C#

public class GenericSynchronizerMonitorStyleImplicitMonitor {
	// implicit .NET monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private readonly object monitor = new Object();
	
	// synchronization state
	private SynchState synchState;
	
	// initialize the synchronizer
	public GenericSynchronizerMonitorStyleImplicitMonitor(InitializeArgs initialArgs) {
		initialize "synchState" according to information specified by "initialArgs";
	}
	
	/**
	 * Synchronizer specific methods
	 */
	
	// returns true if synchronization state allows an immediate acquire
	private bool CanAcquire(AcquireArgs acquireArgs) {
		returns true if "synchState" satisfies an immediate acquire according to "acquireArgs";
	}
		
	// executes the processing associated with a successful acquire
	private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
		update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation
	private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
		update "syncState" according to "releaseArgs";
	}
	
	/**
	 * Synchronizer independent methods
	 */

	// The acquire operation
	public bool Acquire(AcquireArgs acquireArgs, out AcquireResult result, int timeout = Timeout.Infinite) {
		lock(monitor) {
			if (CanAcquire(acquireArgs)) {
				// do the acquire immediately
				result = AcquireSideEffect(acquireArgs);
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0) {
					result = default(AcquireResult);
					return false;
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// if a notification was made with Monitor.Pulse, this single notification
					// may be lost if the blocking of the notified thread was interrupted.
					// so, if an acquire is possible, we notify another blocked thread, if any.
					// anyway we propagate the interrupted exception
					
					if (CanAcquire(acquireArgs))
						Monitor.Pulse(monitor);
					throw;
				}
			} while (!CanAcquire(acquireArgs));
			// now we can complete the acquire after blocking in the monitor
			result = AcquireSideEffect(acquireArgs);
			return true;
		}
	}

	// The release operation
	public void Release(ReleaseArgs releaseArgs) {
		lock(monitor) {
			UpdateStateOnRelease(releaseArgs);
			Monitor.PulseAll(monitor);	/* or Monitor.Pulse if only one thread can have success in its acquire */
		}
    }
}

```

### Implementação de um Semáforo ao "Estilo Monitor" com Base num Monitor Implícito do .NET

- Este código foi escrito a partir do pseudo-código anterior, começando por concretizar os tipos genéricos:
	 - `SynchState`, `int`, cujo valor é o número de autorizações sob custódia do semáforo;
	 - `InitializeArgs`, `int` com o número de autorizações sob custódia do semáforo após inicialização;
	 - `AcquireArgs`, `int` com o número de autorizações solicitadas ao semáforo pela operação *acquire*
	 - `AcquireResult`, `void` dado que o método *acquire* apenas devolve a indicação se a operação foi realizada ou houve desistência por *timeout*;
	 - `ReleaseArgs`, `int` que especifica o número de autorizações a devolver ao semáforo pela operação *release*.

- A seguir, foram concretizados os métodos cujo código depende da semântica do sincronizador, neste caso, do semáforo:
	- `CanAcquire` que devolve `true` se o número de autorizações sob custódia do semáforo é suficiente para satisfazer o respectivo *acquire*;
	- `AcquireSideEffect`que actualiza o estado do semáforo subtraindo o número de autorizações concedidas pela operaçao *acquire* ao número de autorizações sob custódia do semáforo;
	- `UpdateOnRelease` que soma às autorizações sob custódia do semáforo as autorizações entregues com a operação de *release*.

- Finalmente, na parte do código que não depende da semmantica do sincronizador, por estarmos a utilizar monitores implícitos do .NET, <ins>foi necessário ponderar como ia ser feita a notificação das *threads* bloqueadas no monitor para decidir se era, ou não, necessário capturar e processar a *interrupted exception* no método *acquire*</ins>:
	- No caso deste semáforo, a operação *release* pode devolver um número arbitrário de autorizações à custódia do semáforo e podem existir *acquires* bloqueados também por solicitárem um número arbitrario de autorizações;
	- Dado o ponto anterior só podemos ter a certeza de que são notificadas todas as *threads* que podem ver as suas operações *acquire* satisfeitas, se notificarmos todas as *threads* bloqueadas na variável condição do monitor, usando `Monitor.PulseAll`;
	- **Quando se usa `Monitor.PulseAll` para notificar as *threads* bloqueadas na variável condição no monitor não se coloca o problema da perda de notificações**, pelo que <ins>não é necessário</ins> capturar e processar a *interrupted exception* no método *acquire*, com o objectivo de regenerar eventuais notificação perdidas devido à interrupção das *threads* bloquadas na variável condição do monitor.

- Tendo em consideração tudo o que foi dito anteriormente, a implenentação do semáforo usando o "estilo monitor" com suporte para *timeout* na operação *acquire* e processando correctamente a interrupção das *threads* boqueadas no monitor é o que se apresenta a seguir. 
	
```C#
/**
 * Semaphore following the "monitor style", using an *implicit .NET monitor*, with
 * support for timeout on the acquire operation.
 */

public class SemaphoreMonitorStyleImplicitMonitor {
    // implicit .NET monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private readonly Object monitor = new Object();
	
	// synchronization state
	private int permits;	// the number of permits in the custody semaphore 
	
	/**
	 * Synchronizer specific methods
	 */
	
	// initialize the semaphore
	public SemaphoreMonitorStyleImplicitMonitor(int initial = 0) {
		if (initial < 0)
			throw new ArgumentException("initial");
		permits = initial;
	}
	
	// if the pending permits e equal or greater to the request,
	// we can acquire immediately
	private bool CanAcquire(int acquires) { return permits >= acquires; }
	
	// deduce the acquired permits
	private void AcquireSideEffect(int acquires) { permits -= acquires; }
	
	// take into account the released permits
	private void UpdateStateOnRelease(int releases) { permits += releases; }
	
	// acquire "acquires" permits
	public bool Acquire(int acquires, int timeout = Timeout.Infinite) {
		lock(monitor) {
			if (CanAcquire(acquires)) {
				// there are sufficient permits available, take them
				AcquireSideEffect(acquires);
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0)
					return false;
				Monitor.Wait(monitor, timeout);
			} while (!CanAcquire(acquires));
			AcquireSideEffect(acquires);
			return true;
		}
	}
	
	// release "releases" permits
	public void Release(int releases) {
		lock(monitor) {
			UpdateStateOnRelease(releases);
			Monitor.PulseAll(monitor);	// a release can satisfy multiple acquires, so notify all blocked threads
		}
	}
}
```
 
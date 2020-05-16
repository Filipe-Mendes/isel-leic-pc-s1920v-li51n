
# Aula 16 - Sincronização _NonBlocking_ (II)

____

## Algoritmos _Nonblocking_

- Os algoritmos baseados em _locking_ correm o risco de várias falhas de _liveness_. Se uma _thread_ com a posse de um _lock_ é atrasada devido a uma operação de I/O bloqueante, _page fault_ ou outros tipos de atraso, é possível que nenhuma das _threads_ que usam o mesmo _lock_ possam progredir.

- Um algoritmo é designado **_nonblocking_** se a falha ou bloqueio de uma _thread_ não pode causar a falha ou bloqueio de outra _thread_; um algoritmo é designado **_lock-free_** se, a cada passo, **alguma** _thread_ pode progredir.

- Os algoritmos que usam exclusivamente CAS na coordenação entre _threads_ podem, se construídos correctamente, ser simultamente _nonblocking_ e _lock-free_. Um CAS sem contenção tem sempre sucesso e se múltiplas _threads_ disputam um CAS, há sempre uma _thread_ que ganha e, por isso, progride. Os algoritmos _nonblocking_ são imunes a _deadlock_ ou inversão de prioridades (embora possam exibir _starvation_ ou _livelock_ pelo facto de poderem envolver múltiplas tentativas). Atrás vimos um algoritmo _nonblocking_: `CasCounter`.

- São conhecidos bons algoritmos _nonblocking_ para muitas estruturas  de dados comuns, incluindo _stacks_, _priority queues_ e _hash tables_ - contudo a concepção de novos algoritmos é uma tarefa que deve ser deixada para os especialistas.

- os algoritmos _nonblocking_ são consideravelmente mais complexos que os seus equivalentes baseados em _lock_. Um aspecto central na criação de algoritmos _nonblocking_ é descobrir como se pode limitar o âmbito das actualizações atómicas a uma única variável, enquanto ainda se mantém a consistência dos dados. Em colecções basedas em estruturas ligadas como as filas, às vezes é possível expressar transformações de estado como alterações em _links_ individuais e usar `AtomicReference` para representar cada _link_ que deva ser actualizado atomicamente.  

- A maioria dos algoritmos _nonblocking_ baseados em CAS, onde o estado partilhado mutável seja representado por uma única variável atómica, têm os os passos que se descrevem a seguir.

1. É obtida uma cópia do estado partilhado mutável (`observedValue`);

2. Em função do valor da cópia `observedValue`, podemos ter uma de três situações: (i) se for possível prosseguir com a operação, determinar o novo valor do estado partilhado (`updatedValue`) e passar ao passo 3; (ii) no caso da operação não ser possível, proceder adequadamente, isto é, aguardar algum tempo e repetir 1 (_spin wait_ ou _backoff_), devolver a indicação de que a operação não é possível ou lançar uma excepção; (iii) o valor de `observedValue` indica já ter sido alcançado um estado final inalterável (por exemplo, na inicialização _lazy_ após ter sido criada a instância do recurso subjacente), a operação é dada como concluída normalmente;

3. Invocar CAS para alterar o estado partilhado para `updatedValue` se o seu valor ainda for `observedValue`. Pode ocorrer uma de três situações: (i) o CAS tem sucesso, concluindo a operação; (ii) o CAS falha devido a colisão com outra _thread_ (situação comum), repetir 1, podendo eventualmente esperar algum tempo (_spin wait_ ou _backoff_); (iii) o CAS falha, mas devido a outra _thread_ já ter feita a operação que se pretendia (por exemplo, na inicialização _lazy_ quando mais do que uma _thread_ cria instâncias do resurso subjacente no passo 2.i), a operação e dada como concluída, após eventual _cleanup_ da instância do recurso criado especulativamente no passo 2.i.
	 
- Para ilustrar a aplicação deste padrão apresenta-se a seguir a implementação da classe `LazyInit<E>`. Nesta classe, o estado partilhado mutável é armazenado numa instância de `AtomicReference<E>` que é iniciada com `null`.

```Java	
import java.util.function.Supplier;
import java.util.function.Consumer;
import java.util.concurrent.atomic.AtomicReference;
	
public final class LazyInit<E> {
	private final Supplier<E> supplier;
	private final Consumer<E> cleanup;
	 
	private AtomicReference<E> resource;
	
	public LazyInit(Supplier<E> supplier, Consumer<E> cleanup) {
		this.supplier = supplier;
		this.cleanup = cleanup;
		resource = new AtomicReference<>(null);
	}
	
	// returns the instance of E, creating it at first time
	public E getInstance() {
		// step 1
		E observedResource = resource.get();
		
		// step 2
		if (observedResource != null)
			// outcome 2.iii
			return observedResource;
		
		// outcome 2.i
		E updatedResource = supplier.get();
		
		// step 3.
		if (resource.compareAndSet(null, updatedResource))
			// outcome 3.i
			return updatedResource;
		
		// outcome 3.iii: do cleanup, if sepecified, and return the resource
		// created by some other thread
		
		if (cleanup != null)
			cleanup.accept(updatedResource);
		
		return resource.get();
	}
}
```

- Este é um caso, pouco comum, onde a falha do CAS não implica fazer uma nova tentativa, pois indica que foi atingido um estado final inalterável; isto é, a instância do recurso subjacente foi criada por uma das _threads_ que o solicitaram e, a partir disso, todas as outras _threads_ vão obter essa mesma instância.

___	


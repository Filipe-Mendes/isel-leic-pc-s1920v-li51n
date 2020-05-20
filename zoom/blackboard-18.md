
# Aula 18 - Sincronização _NonBlocking_ (III)

____

## Algoritmos _Nonblocking_

- A maioria dos algoritmos _nonblocking_ baseados em CAS, onde o estado partilhado mutável seja representado por uma única variável atómica, têm os passos que se descrevem a seguir.

1. É obtida uma cópia do estado partilhado mutável (`observedValue`);

2. Em função do valor da cópia `observedValue`, podemos ter uma de três situações: (i) se for possível prosseguir com a operação, determinar o novo valor do estado partilhado (`updatedValue`) e passar ao passo 3; (ii) no caso da operação não ser possível, proceder adequadamente, isto é, aguardar algum tempo e repetir 1 (_spin wait_ ou _backoff_), devolver a indicação de que a operação não é possível ou lançar excepção; (iii) o valor de `observedValue` indica já ter sido alcançado um estado final inalterável (por exemplo, na inicialização _lazy_ após ter sido criada a instância do recurso subjacente), a operação é dada como concluída normalmente;

3. Invocar CAS para alterar o estado partilhado para `updatedValue` se o seu valor ainda for `observedValue`. Pode ocorrer uma de três situações: (i) o CAS tem sucesso, concluindo-se a operação; (ii) o CAS falha devido a colisão com outra _thread_ (situação comum), então repetir 1, podendo eventualmente esperar algum tempo (_spin wait_ ou _backoff_); (iii) o CAS falha, mas devido a outra _thread_ já ter feita a operação que se pretendia fazer (por exemplo, na inicialização _lazy_ quando mais do que uma _thread_ cria instâncias do resurso subjacente no passo 2.i), a operação e dada como concluída, após eventual _cleanup_ da instância do recurso criado especulativamente no passo 2.i.
	 	 
- Uma implementação de um semáforo, em que espera é feita com _spin wait_, usando um algoritmo _nonblocking_ será:

```Java
import java.util.concurrent.atomic.AtomicInteger;

public class SpinSemaphore {
	// shared mutable state: the number of permits available
	private final AtomicInteger permits;
	private final int maxPermits;
	
	public SpinSemaphore(int initialPermits, int maxPermits) {
		if (initialPermits < 0 || initialPermits > maxPermits)
			throw new IllegalArgumentException();
		permits = new AtomicInteger(initialPermits);
		this.maxPermits = maxPermits;
	}
	
	public SpinSemaphore(int initialPermits) { this(initialPermits, Integer.MAX_VALUE); }
		
	// acquire the specified number of permits
	public boolean acquire(int acquires, long millisTimeout) {
		TimeoutHolder th = new TimeoutHolder(millisTimeout);
		
		while (true) {
			// step 1.
			int observedPermits = permits.get();	// must be a volatile read in order to get the most recent value
			// step 2
			if (observedPermits >= acquires) {
				// outcome 2.i
				int updatedPermits = observedPermits - acquires;
				// step 3
				if (permits.compareAndSet(observedPermits, updatedPermits))
					return true;	// outcome 3.i
				else
					// outcome 3.ii
					continue;
			}
			// outcome 2.ii
			if (th.value() <= 0)
				return false;
			Thread.yield();			// yield processor before retry
		}
	}
	
	// try to acquire one permit immediately
	public boolean tryAcquire(int acquires) { return acquire(acquires, 0L); }
	
	// releases the speciified number of permits, checking maximum value and overflow
	public void release(int releases) {
		while (true) {
			// step 1
			int observedPermits = permits.get();
			// step 2
			int updatedPermits = observedPermits + releases;
			if (updatedPermits > maxPermits || updatedPermits < observedPermits)
				// outcome 2.ii
				throw new IllegalStateException();
			// outcome 2.i
			// step 3
			if (permits.compareAndSet(observedPermits, updatedPermits))
				// outcome 3.i
				return;
			// outcome 3.ii
		}
	}
	
	// releases the speciified number of permits, unchecked
	public void uncheckedRelease(int releases) {
		// this is an unconditionl operation, so we can use the method AtomicInteger.addAndGet()
		permits.addAndGet(releases);
	}
}

```

- O estado partilhado mutável - número de autorizações disponíveis no semáforo - é armazenado numa  instância de `AtomicInteger`. Para além do CAS, esta classe suporta outras operações atómicas como, por exemplo, `incrementAndGet`, `decrementAndGet` ou `addAndGet`. Adiante, discute-se em que circunstâncias estas operações poderão ser úteis.

- O método `acquire` - que tem semântica _acquire_, isto é, a _thread_ invocante espera que estejam disponíveis as autorizações solicitadas ou que expire o _timeout_ especificado - segue o algoritmo com os três passos apresentado anteriormente, onde os passos fazem o seguinte:

1. Obtém uma cópia do número de autorizações disponíveis e armazena na variável local `observedPermits`; o número de autorizações disponiveis pode estar a mudar ao mesmo tempo que decorre este processmento por acção de outras _threads_;
	 
2. Testa-se se o valor `observedPermits` é suficiente para satisfazer o _acquire_: (i) em caso afirmativo, calcula-se o novo valor do número de autorizações disponíveis, armazena-se na variável local `updatedPermits e passa ao passo 3; (ii) no caso, contrário, testa se expirou o _timeout_ especificado, indica ao sistema operativo que está em _spin_ cedendo o processador e repete o processo a partir do passo 1;
	 
3. Invoca o método `AtomicInteger.compareAndSet` para actualizar o número de autorizações disponíveis para `updatedPermits` se o seu valor actual for `observedPermits`; (i) se a operação CAS tem sucesso, o método `acquire` termina com sucesso; (ii) se a operação CAS falhar (o valor actual de `permits` já não é igual a `observedPermits`), repete o processo a partir do passo 1.
	 
- O método `release`, que tem semântica _release_ nunca atrasa a execução da _thread_ invocante e pode ter como consequência libertar _threads_ que estão a ser atrasadas (bloqueadas) pela operação _acquire_ - segue os mesmos três passos:

1. Obtém uma cópia do número de autorizações disponíveis e armazena na variável local `observedPermits`; o número de autorizações disponíveis pode estar a mudar aos mesmo tempo que decorre este processamento por acção de outras _thread_;
	
2. Calcula o novo número de autorizações disponíveis e armazena na variável local `updatedPermits`; a seguir testa se foi excedido o número máximo de autorizações ou se a soma provocou _overflow_: (i) se não houve uma situação de erro, passa ao passo 3; (ii) se houve uma situação de error, lança `IllegalStateException`;
	
3. Invoca o método `AtomicInteger.compareAndSet` para actualizar o número de autorizações disponíveis para `updatedPermits` se o seu valor actual for `observedPermits`; (i) se a operação CAS tem sucesso, o método `release` termina com sucesso; (ii) se a operação CAS falhar (o valor actual de `permits` já não é igual a `observedPermits`), repete o processo a partir do passo 1.

- O método `uncheckedRelease` ilustra uma situação em que se pode utilizar uma instrução atómica incondicional, neste caso `AtomicInteger.addAndGet` para somar atomicamente a `permits` o número de autorização devolvidas ao semáforo. Como não existe uma construção _check-then-act_ que tenha que ser atómica é apenas necessário garantir a atomicidade e visibilidade da actualização (soma) o que o método `AtomicInteger.addAndGet` garante.

- Sendo a alteração estado partilhado mutável feita com uma instrução atómica (`AtomicInteger.compareAndSet` ou `AtomicInteger.addAndGet`) que emite uma escrita volatile, a alteração do estado partilhado e todas as escritas feitas anteriormente ficam imediatamente visíveis a todas as _threads_.


### _Treiber Stack_ (1986)

- Os _stacks_ são as estruturas de dados ligadas mais simples que existem: cada elemento refere-se apenas a outro elemento e cada elemento é referido apenas por uma única referência. A classe `TreiberStack`, apresentada a seguir, mostra com se constrói um _stack_ usando referências atómicas.

```Java
public class TreiberStack<E> {
	// the node
	private static class Node<V> {
		Node<V> next;	// next node
		final V item;
			
		Node(V item) {
			this.item = item;
		}
	}
	
	// the stack top
	private final AtomicReference<Node<E>> top = new AtomicReference<>(null);
	
	// push an item onto stack
	public void push(E item) {
		Node<E> updatedTop = new Node<E>(item);
		while (true) {
			// step 1
			Node<E> observedTop = top.get();	// volatile read
			// step 2.i - link the new top node to the previous top node
			updatedTop.next = observedTop;
			// step 3.
			if (top.compareAndSet(observedTop, updatedTop))	// volatile write
				// outcome 3.i
				break;
			// outcome 3.ii
		}
	}
	
	// try to pop an item from the stack
	public E tryPop() {
		Node<E> observedTop;
		while (true) {
			// step 1
			observedTop = top.get();	// volatile read
			// step 2
			if (observedTop == null)
				// outcome 2.ii: the stack is empty
				return null;
			// outcome 2.i - compute the updated stack top
			Node<E> updatedTop = observedTop.next;
			// step 3.
			if (top.compareAndSet(observedTop, updatedTop))	// volatile write
				// outcome 3.i: success
				break;
			// outcome 3.ii: retry
		}
		return observedTop.item;
	}
}
```

- O _stack_ e uma lista ligada de elementos `Node` com raiz em `top`, cada um dos quais contém um valor e um _link_ para o próximo elemento. O método `push` prepara um novo nó cujo _link_ refere o nó correntemente no topo do _stack_, e depois usa CAS para tentar instalar o nó no topo do _stack_. Se o mesmo nó está ainda no topo do _stack_ (`obserdedTop`) o CAS tem sucesso; se o nó no topo do _stack_ mudou entretanto (porque outra _thread_ acrescentou ou removeu elementos deste que tomámos a cópia do topo do _stack_), o CAS falha e processo repete-se desde o início. Em qualquer dos casos, o estado do _stack_ está sempre consistente após o CAS.

- `CasCounter` e `TreiberStack` ilustram características de todos os algoritmos _nonblocking_: algum trabalho é feito especulativamente e pode ter que ser refeito. No `TreiberStack`, quando contruímos o `Node` que representa o novo elemento, esperamos que o valor da referência `next` esteja correcto no momento em que o nó for instalado no _stack_, mas estamos preparado para tentar novamente no caso de contenção.

- Os algoritmos _nonblocking_ como `TreiberStack` derivam a sua _thread safety_ do facto que, tal como acontece no _locking_, `compareAndSet` providenciar ambas as garantias de atomicidade e visibilidade. Quando uma _thread_ altera o estado do _stack_, ela fá-lo com um `compareAndSet`, que tem os efeitos na memória de uma escrita _volatile_. Quando uma _thread_ examina o estado do _stack_, fá-lo chamando o método `AtomicReference.get`, que tem os efeitos na memória de uma leitura _volatile_. Assim, quaisquer alterações feitas por uma _thread_ são publicadas em segurança para qualquer outra _thread_ que observe o estado da lista. E a lista é modificada com uma operação `compareAndSet` que atomicamente actualiza a referência `top` ou falha se detectar a interferência de outra(s) _thread(s)_.


## _Michael and Scott Queue_ (1996) 

- Os três algorimtmos que vimos até aqui, o contador, o _spin_ semáforo e o _stack_ ilustram o padrão básico da utilização de CAS para actualizar um valor especulativamente, voltando a tentar se a actualização falha. <ins>O truque para criar algoritmos _nonblocking_ é limitar o âmbito das alterações atómicas a uma variável</ins>. Com o contador e o _spin_ semáforo isto é trivial, como o _stack_ é bastante directo, mas com estruturas de dados mais complexas como as filas, tabelas de _hash_ ou árvores, pode ficar muito complicado.

- Uma _linked queue_ é mais complicada que o _stack_ porque deve suportar acesso rápido a ambos os extremos, cabeça e cauda. Para fazer isto, é necessário manter ponteiros para os nós que se encontram à cabeça na cauda da fila. Para fazer isto, são mantidos dois ponteiros separados um para a cabeça e outro para a cauda. Dois ponteiros referem o nó que se encontra na cauda: o ponteiro `next` do último elemento corrente e o ponteiro para a cauda. Para inserir um novo elemento com sucesso, ambos os ponteiros têm que ser actualizados - atomicamente. À primeira vista, isto não pode ser feito com variáveis atómicas; são necessárias duas operações CAS separadas para actualizar os dois ponteiros e se o primeiro CAS tem sucesso e o segundo falha a fila é deixada num estado inconsistente. E, mesmo que ambas as operações tenham sucesso, outra _thread_ poderá tentar aceder à fila entre o primeiro e o segundo CAS. Para gizar um algoritmo _nonblocking_ para uma _linked queue_ requer um plano para ambas as situações.

- Precisamos de dois truques para desenvolver este plano. O primeiro é garantir que a estrutura de dados está sempre num estado consistente, mesmo no meio de uma actualização com múltiplos passos. Desta forma, se a _thread_ A estiver no meio de uma actualização quando chega a _thread_ B, esta pode perceber que uma operação está apenas parcialmente concluída, pelo que sabe que não deve tentar aplicar imediatamente a sua própria actualização. Então B pode esperar (examinando repetidamente o estado da fila) até que A termine, de modo a que as duas _threads_ não se interponham.

- Embora este truque por si próprio seria suficinete para permitir que as _threads_ acedam à estrutura de dados sem danificá-la, se uma _thread_ falhar no meio de uma actualização, mais nenhuma _thread_ poderá aceder à fila. Para tornar este algorimo **_nonblocking_**, temos que garantir que a falha de uma _thread_ não possa impedir outras _threads_ de progredir. Assim, o segundo truque é garantir que, se B chegar e encontrar a estrutura de dados no meio de uma actualização que está a ser feita por B, já esteja incorporada na estrutura de dados informação suficiente para que B **complete a actualização por A**. Se B **ajuda** A terminando a operação de A, então B pode prosseguir com a sua própria operação sem necessidade de esperar por A. Quando a _thread_ A chegar ao fim da sua operação, descobrirá que B fez o trabalho por si. A classe `MichaelScottQueue`, mostrada a seguir, ilustra a parte de inserção do algoritmo _nonblocking_ proposto por _Michael and Scott_ para a _linked queue_. Como acontece em múltiplos algoritmos de filas, uma fila vazia consiste de nó sentinela or _dummy_, e os pointeiros `head` e `tail` são iniciados de modo a ambos referirem a sentinela. O ponteiro `tail` refere a sentinela (se a fila estiver vazia), o último elemento da fila ou (no caso de estar no meio de uma operação de inserção) refere o penúltimo elemento da fila.

```Java
public class MichaelScottQueue<E> {

	// the queue node
	private static class Node<V> {
		final AtomicReference<Node<V>> next;
		final V data;

		Node(V data) {
			next = new AtomicReference<Node<V>>(null);
			this.data = data;
		}
	}

	// the head and tail references
	private final AtomicReference<Node<E>> head;
	private final AtomicReference<Node<E>> tail;

	public MichaelScottQueue() {
		Node<E> sentinel = new Node<E>(null);
		head = new AtomicReference<Node<E>>(sentinel);
		tail = new AtomicReference<Node<E>>(sentinel);
	}

	// enqueue a datum
	public void enqueue(E data) {
		Node<E> newNode = new Node<E>(data);

		while (true) {
			Node<E> observedTail = tail.get();
			Node<E> observedTailNext = observedTail.next.get();
			if (observedTail == tail.get()) {	// confirm that we have a good tail, to prevent CAS failures
				if (observedTailNext != null) { /** step A **/
					// queue in intermediate state, so advance tail for some other thread
					tail.compareAndSet(observedTail, observedTailNext);		/** step B **/
				} else {
					// queue in quiescent state, try inserting new node
					if (observedTail.next.compareAndSet(null, newNode)) {	/** step C **/
						// advance the tail
						tail.compareAndSet(observedTail, newNode);	/** step D **/
						break;
					}
				}
			}
		}
	}
	
	// try to dequeue a datum
	public E tryDequeue() {
		// TODO
	}
}
```

- Inserir um elemento envolve a actualização de dois ponteiros. A primeira actualização liga o novo nó no fim da lista através da actualização do campo `next` do corrente último elemento; o segundo avança o ponteiro `tail` de modo a passar a apontar o último elemento. Entre estas duas operações a fila está num estado **intermédio**. Após a segunda actualização, a fila fica, de novo, num estado **estável**.

- A observação chave que viabiliza ambos os truques necessários é que se a fila está num estado estável, o campo `next` do nó apontado pelo `tail` é `null`, e se a fila está num estado intermédio, `tail.next` é diferente de `null`. Assim, qualquer _thread_ pode imediatamente saber o estado da fila observando `tail.next`. Além disso, se a fila está num estado intermédio, ela pode ser restaurada para um estado estável avançando o pointeiro `tail` um nó, terminado a operação por qualquer que seja a _thread_ que está no meio da inserção de um novo elemento.

- `MichaelScottQueue.enqueue` começa por testar se a fila está num estado intermédio antes de tentar inserir um novo elemento (passo A). Se a fila estiver num estado intermédio, então alguma _thread_ está num processo de inserir um elemento (entre os seus passos C e D). Em vez de esperar que essa _thread_ termine, a _thread_ corrente **ajuda** a _thread_ que está a fazer a inserção terminando a operação por ela, avançando o ponteiro `tail` (passo B). Depois, repete o teste e no caso de outra _thread_ ter começado a inserção de um novo elemento avança de novo o ponteiro `tail` até que a fila esteja num estado estável, para que possa dar iníicio à sua própria inserção. 

- O CAS no passo C, que liga o novo nó na cauda da fila, pode falhar se duas _threads_ tentarem inserir um elemento ao mesmo tempo. Nesse caso, não nenhum dano é causado: não foi feita nenhuma alteração, e a _thread_ corrente pode simplesmente recarregar o ponteiro `tail` e tentar de novo. Quando o passo C tem sucesso, é considerado que a inserção teve efeito, o segundo CAS (passo D) é considerado "_cleanup_", uma vez que ele pode ser realizado ou pela _thread_ que está a realizar a inserção ou por qualquer outra _thread_. Se o CAS no passo D falha, a _thread_ que está a realizar a inserção retorna normalmenteem vez de voltar a tentar o CAS, porque não é necessário - outra _thread_ já terminou o seu trabalho no passo B! Isto funciona porque antes de qualquer _thread_ tentar ligar um novo nó, ela primeiro determina se a fila necessita de _cleanup_, testando se `tail.next` é diferente de `null`; em caso afirmativo, avança o ponteiro `tail` (podendo até avançar várias vezes) até que a fila fique num estado estável.

- Neste algoritmo há um aspecto que nem sempre é bem percebido e está relacionado com o nó sentinela, criado no construtor, e com o qual a fila é iniciada. Este nó só se mantém como sentinela até que sejam removido o primeiro nó da fila (avançando o ponteiro `head`). Depois de serem inseridos e removidos nós da fila, o nó sentinela é o último nó que foi removido da fila e que é referenciado pelo ponteiro `head`. 


### Passos dos Algoritmos _NonBlocking_ Usando o "Truque" da Ajuda

- Nos algoritmos _nonblocking_ baseados em CAS onde se use o truque da **ajuda** para resolver o problema das actualizações que implicam a alteração de mais do que uma variável atómica, a ordem dos CAS sobre as variáveis atómicas tem que ser a mesma em todas as _threads_. A _thread_ que tiver sucesso no primeiro CAS **ganha** o direito de prosseguir com a actualização até ao fim, podendo eventualmente beneficiar da **ajuda** de outras _threads_. Além disso, é necessário que operações diferentes (e.g., inserções e remoções numa fila) não façam CAS sobre as mesmas variáveis. No caso da _Michael and Scott queue_, isso explica a necessidade do primeiro nó da fila ser uma sentinela. Assim, a remoção é decidida com CAS sobre o ponteiro `head` enquanto que a inserção é decidida com CAS sobre o ponteiro `tail.next`, sendo o avanço do ponteiro `tail` a actualização posterior que pode ser **ajudada** por outras _threads_.

- Um aspecto importante neste algoritmos é que as variáveis atómicas não devem ser lidas mais do que uma vez em cada tentativa de realização da actualização. Por exemplo, a variável local `observedTailNext` deve ser obtida com o valor do ponteiro para a cauda lido para `observedTail` e não voltando a ler `tail.get`, uma vez que entre os dois acessos o ponteiro `tail` pode ter sido alterado por outra _thread_, situação em que as cópias `observedTail` e `observedTailNext` não estariam coerentes.

- Outro aspecto a realçar sobre o código da `MichaelScottQueue` é ter sido condicionada a tentativa de actualização à confirmação de que o valor da variável local `observedTail` ainda é igual ao valor corrente de `tail`. Considerando que a execução das _threads_ pode ser atrasada (_page-fault_, interrupção, preempção) após a chamada a `tail.get` quando se afectou a variável `observedTail`, aquele teste evita que se prossiga com uma operação CAS (que é dispendiosa) que irá seguramente falhar.   

- Os algoritmos _nonblocking_ que usam o truque da ajuda, podem ser descritos com os seguintes passos, particularizando para o caso da inserção na _Michael and Scott queue_.

1. É obtida uma cópia das variáveis que representam o estado partilhado mutável (`observedTail` e `observedTailNext`);
	 
2. É testado se a estrutura de dados se encontra num estado intermédio (`observedTailNext != null`); em caso afirmativo, executa-se a ajuda (`tail.compareAndSet(observedTail, observedTailNext)`) sem testar se houve sucesso - pois a ajuda pode estar a ser feitas por outras _threads_ - e repete-se de imediato o passo 1; se a estrutura de dados está num estado estável, prepara-se a a actualização e passa-se ao passo 3;
	 	 
3. Invocar CAS sobre a variável atómica sobre a qual é disputada a actualização atómica (neste caso, `observedTail.next` que vai ser afectado com `newNode`) se o seu valor ainda for aquele obtido no passo 1 e validado no passo 2 (`null`). Pode ocorrer uma de duas  situações: (i) o CAS tem sucesso, sendo a seguir realizadas as restantes actualizações atómicas (aqui `tail.compareAndSet(observedTail, newNode)`), tendo em consideração que as mesmas pode falhar devido à possibilidade de **ajuda** por parte de outras _threads; (ii) o CAS falha devido a colisão com outra _thread_ (situação comum), então repetir a partir de 1, podendo eventualmente esperar algum tempo (_spin wait_ ou _backoff_).


### _Atomic Field Updaters_

- A listagem seguinte ilustra uma parte do algoritmo usado pelo `TreiberStack`, mas a implementação é um pouco diferente da apresentada anteriormente. Em vez de representar `top` com uma referência atómica, usa-se uma simples referência _volatile_ e fazem-se as actualizações atómicas usando a funcionalidade da classe `AtomicReferenceFiedUpdater`, cuja implementação se baseia em reflexão.  
 
 
 ```Java
 public class TreiberStack<E> {
 	// the node
 	private static class Node<V> {
 		Node<V> next;
 		final V item;
			
 		Node(V item) {
 			this.item = item;
 		}
 	}
	
 	// the stack top
 	private volatile Node<E> top = null;
	
 	// the atomic field updater that allows execute atomic operation on the "top" volatile field
 	private static AtomicReferenceFieldUpdater<TreiberStack, Node> topUpdater =
 		AtomicReferenceFieldUpdater.newUpdater(TreiberStack.class, Node.class, "top");
	
 	// push an item onto stack
 	public void push(E item) {
 		Node<E> updatedTop = new Node<E>(item);
 		while (true) {
 			// step 1
 			Node<E> observedTop = top;
 			// step 2.i - link the new top node to the previous top node
 			updatedTop.next = observedTop;
 			// step 3.
 			if (topUpdater.compareAndSet(this, observedTop, updatedTop))
 				// outcome 3.i
 				break;
 			// outcome 3.ii
 		}
 	}
	...
 }
  ```
 
 - As classes _atomic field updater_ (disponíveis nas versões `Integer`, `Long` e `Reference`) representam uma "vista", baseada em reflexão, de um campo `volatile` existente de modo a que possa ser usadas operações atómicas em campos `volatile` existentes. As classes _updater_ não têm construtores; para criar uma destas classes, invoca-se o método de fabrico `newUpdater`, especificando a classe onde o campo _volatile_ está definido e o nome do campo, e no caso da referências especifica-se também a classe que define o tipo da referência. As classes _field updater_ não estão vinculadas a uma instância específica; podem ser usadas para actualizar o campo em apreço em qualquer instância da classe alvo.
 
 - As garantias de atomicidade dadas pelas classes _updater_ são mais fracas que as garantias dadas pelas classes atómicas regulares porque não é possível garantir que os campos subjacentes não possam ser modificados directamente - o método `compareAndSet` e os métodos aritméticos garantem atomicidade apenas no que diz respeito apenas a outras _threads_ que também usem os métodos do _atomic field updater_.
 
 - Nesta versão do `TreiberStack`, as actualizações do campo `top` do _stack_ são aplicadas usadp o método `compareAndSet` de `topUpdater`. Esta abordagem um tanto tortuosa é usada inteiramente pro razões de desempenho. Não é este o caso, mas na ´MichaelScottQueue` a substituição usar no campo `next` uma referência _volatile_ em vez de uma instância de `AtomicReference` para cada nó contribui para reduzir o custo das operações de inserção. No entanto, em quase todas as situações, as varáveis atómicas comuns têm um bom desempenho - em apenas alguns casos, será conveniente usaros _atomic field updaters_.
 
 
### O Problema ABA

- O problema ABA é uma anomalia que pode surgir com o uso ingénuo do _compare-and-swap_ em algoritmos onde os nós possam ser reciclados (principalmente em ambinetes sem _garbage collection_). Um CAS pergunta efectivamente "É o valor de **V** ainda **A**?", e prossegue com a actualização se for. Na maioria das situações, incluindo os exemplos apresentados neste capítulo, isso é completamente suficiente. No entanto, por vezes nós realmente queremos perguntar "O valor de **V** mudou desde da minha última observação em que obtive o valor **A**?". Para alguns algoritmos, alterando **V** de **A** para **B** e depois de novo para **A** ainda constitui uma situação que requer a repetição de algum passo do algoritmo.

- Este problema ABA pode ocorrer me algoritmos que fazer a sua própria gestão de memória para os objectos usados nos nós das estuturas de dados ligadas. Neste caso, o facto da cabeça da lista ainda se referir a um nó previamente observado não é suficiente para garantir que o conteúdo da lista não se alterou. Se não consegue evitar o problema ABA deixando a gestão dos nós ao _garbage collector_, existe ainda um solução relativamente simples: em vez de actualizar o valor de uma referência, actualize um **par** de valores, uma referência e um número de versão. Mesmo que o valor muda de **A** para **B** de de novo para **A**, os números de versão serão diferentes. A classe `AtomicStampedReference` (e a sua prima `AtomicMarkableReference`) providencia actualizações atómicas num par de variáveis. `AtomicStampedReference` actualiza um par referência-inteiro, permitindo referências "versionadas" que são imunes ao problema ABA (embora teoricamente o contador possa dar a volta). Da mesma forma, `AtomicMarkableReference` actualiza um par referência-booleano que utilizado em alguns algoritmos para permitir que um nó permanece numa lista enquanto marcado como eliminado.

- Muitos processadores providenciam uma operação CAS _double-wide_ (CAS2 ou CASX) que pode operar num par ponteiro-inteiro, o que torna esta operação razoavelmente eficiente. A partir do _Java_ 6, `AtomicStampedReference` não utiliza CAS _double-wide_ mesmo nas plataformas que o suportam. (CAS _double-wide_ é diferente de DCAS, que opera em duas posições de memória arbitrárias; a partir de 2015, o DCAS não é suportado por nenhum processador de utilização generalizada.)



### Operações Atómicas no .NET _Framework_

- No .NET _framework_ as operações atómicas estão disponíveis através de métodos estáticos da classe `System.Threading.Interlocked`, nomeadamente `Add`, `CompareExchange`, `Decrement`, `Exchange` e `Increment`. As operações atómicas estão disponíveis para os tipos `Int32` e `Int64`. As operações atómicas _exchange_ e _compare-exchange_ estão disponíveis para os tipos `Int32`, `Int64`, `Single`, `Double`, `Object`, `IntPtr` e para qualquer tipo referência `T`.   

- A classe `System.Threading.Interlocked` define também método estático `Read` que permite realizar leituras atómicas de `Int64` (64-bit), e dois métodos para interpor barreiras de memória com semântica _full-fence_: `MemoryBarrier` e `MemoryBarrierProcessWide`. O método `MemoryBarrier` interpõe uma barreira _full-fence_ no processador que executa a _thread_ que fez a chamada (um dos efeitos desta barreira é fazer _flush_ da _write queue_ do processador) e é normalmente implementada com a instrução atómica _exchange_. O método `MemoryBarrierProcessWide` interpõe uma barreira _full-fence_ em todos os processadores que estão a executar _threads_ do processo corrente. (Em _Winodws_ este método faz uma chamada ao à API `FlushProcessWriteBuffers` e no _Linux_ chama o _system call_ `sys_membarrier`).

- Tal como acontece no _Java_, no .NET _framework_ as variáveis sobre as quais são feitas operações atómicas para garantir que os acessos normais de leitura e escrita têm semântica de _volatile read_ e _volatile write_, respectivamente.

- O .NET _framework_ também define a classe `System.Threading.Volatile` que define os métodos `Read` e `Write` que realizam, respectivamente, leituras e escritas com semântica _volatile_ em variáveis/campos que não tenham sido declarados com o modificador `volatile` (por exemplo, variáveis locais a métodos que não podem ser declaradas como `volatile`). O método `Read`interpõe uma barreira _full-fence_ antes da ler o valor da variável e o método `Write` interpõe uma barreira _full-fence_ antes de escrever o novo valor na variável. A título de curiosidade, salienta-se que a escrita de uma variável como o método `Volatile.Write` tem a mesma semântica que uma escrita com semântica _volatile_ em _Java_.

- A seguir apresenta a implementação da classe `TreiberStack` escrita em C#.

```C#
using System;
using System.Threading;

// Treiber stack implementation
public class TreiberStack<E>  where E: class {

	// the node used to store data items
	private class Node<V> {
		internal Node<V> next;
		internal readonly V item;
			
		internal Node(V item) {
			this.item = item;
		}
	}
	
	// the stack top
	private volatile Node<E>top = null;
	
	// push an item onto stack
	public void Push(E item) {
		Node<E> updatedTop = new Node<E>(item);
		while (true) {
			// step 1
			Node<E> observedTop = top;
			// step 2.i - link the new top node to the previous top node
			updatedTop.next = observedTop;
			// step 3.
			if (Interlocked.CompareExchange(ref top, updatedTop, observedTop) == observedTop)
				// outcome 3.i: success
				break;
				
			// outcome 3.ii: retry
		}
	}
	
	// try to pop an item from the stack
	public E TryPop() {
		Node<E> observedTop;
		while (true) {
			// step 1
			observedTop = top;	// must be a volatile read
			// step 2
			if (observedTop == null)
				// outcome 2.ii: the stack is empty
				return null;
			// outcome 2.i - compute the updated stack top
			Node<E> updatedTop = observedTop.next;
			// step 3.
			if (Interlocked.CompareExchange(ref top, updatedTop, observedTop) == observedTop)
				// outcome 3.i: success
				break;
				
			// outcome 3.ii: retry
		}
		return observedTop.item;
	}
}

```
## Resumo

- Os algoritmos _nonblocking_ mantêm _thread safety_ utilizando primitivas de concorrência de baixo nível como é o caso da operação atómica _compare-and-swap_ em vez de utilizar _locks_. Essas primitivas de baixo nível são exposta em _Java_ através de classes de variáveis atómicas as quais podem também ser usadas como "_better volatile variable_" providenciando actualizações atómicas para valores inteiros e referências para objectos. No .NET _framework_ as mesmas operações atómicas estão disponíveis através da classe `System.Threading.Interlocked`. Recorda-se que existe uma diferença importante estre as escritas _volatile_ no _Java_ e no .NET; em _Java_ a escrita normal de uma variável _volatile_ fica imediatamente visível a todos os processadoes (a escrita é feita com uma instrução atómica, por exemplo, _exchange_) o que não acontece em .NET, onde as escritas _volatile_ normais podem ficar retidas no _write buffer_ do processador e não imediatamente visíveis aos outros processadores. (Como dissemos atrás, isto permite o chamado _realease/acquire hazerd_, que permite que a escrita _volatile_ de uma variável possa ser reordenada com a leitura _volatile_ de outra variável).

- Os algoritmos _nonblocking_ são difíceis de conceber e implementar, mas podem oferecer melhor escalabilidade sobre condições típicas e grande resistência a falhas de _liveness_. Muitos dos avanços no desempenho concorrente de uma versão da JVM para a próxima vêm da utilização de algoritmos _nonblocking_, quer na JVM quer nas bibliotecas da plataforma.

 
___
 
 
 
 
 
 
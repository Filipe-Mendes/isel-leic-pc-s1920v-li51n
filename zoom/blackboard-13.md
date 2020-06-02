
# Aula 13 - Modelos de Memória (II)

____


## O que é um Modelo de Memória e porque precisamos de ter um?

- Suponha que uma *thread* afecta um valor a `aVariable`:

```Java
	aVariable = 3;
```

- Um modelo de memória responde à pergunta: "Sobre que condições uma *thread* que leia a variável vê o valor 3?".

- Isto parece uma pergunta idiota, mas na ausência de sincronização, existem razões para para que uma *thread* não imediatamente - ou mesmo chegar a ver - os resultados de uma operação realizada por outra *thread*. Existem várias razões para isto poder acontecer:

	- Os **compiladores** podem gerar instruções por uma ordem diferente daquela que seria "óbvia" e sugerida pelo código fonte ou armazenar variáveis em registos em vez de o fazer na memória;
	
	- Os **processadores** podem executar instruções em paralelo, de forma especulativa ou fora da ordem de programa;
	
	- As *caches* podem alterar a ordem pela qual a escrita das variáveis é transferida para a memória principal; os valores armazenados nas *caches* locais aos processadores não são visíveis aos outros processadores;

- Todos estas factores podem impedir uma *thread* de ver o valor mais recente  de uma variável e podem provocar que as acções sobre a memória realizadas nas outras *threads* possam parecer ser executadas fora de ordem - se não for actualizada a adequada sincronização.


### Modelos de Memória das Plataformas

- Numa arquitectura *shared-memory multiprocessor*, cada processdor tem a sua própria *cache* que é periodicamente reconciliada com a memória principal. As várias arquitecturas providenciam diferentes graus de *cache choerence*, alguns providenciam garantias mínimas que permitem a diferentes processadores ver valores diferentes para a mesma localização da memória.

- O sistema operativo, compilador e *runtime* (e por vezes o também o programa) devem prefazer as diferenças entre aquilo que o *hardware* providencia e aquilo que a obtenção  de *thread safety* exige.

- O **modelo de memória de uma arquitectura** diz quais são as garantias que os programas podem esperar do sistema de memória, e especifica as instruções especiais necessárias (designadas por ***memory barriers*** ou ***fences***) para obter as garantias adicionais de coordenação que são necessárias quando se partilham dados entre *threads*.

- Para isolar os projectistas das diferenças entre os modelos de memória das várias arquitecturas, o *Java* e o .NET providenciam os seus próprios modelos de memória; a *Java Virtual Machine* e o *Common Language Runtime* lidam com as diferenças em os seus modelos de memória e os modelos de memória das aqruitecturas, inserindo as barreiras de memória necessárias nos sítios adequados.   

### Barreiras de Memória

- Os vários processadores e arquitecturas suportam uma variedades de instruções especiais para interpor barreiras de memória. Contudo, os modelos de memória do *Java* e do .NET podem ser completamente explicados com a referência a apenas três tipo de barreiras de memória: *acquire barrier*, *release barrier* e *full-fence*.

#### *Acquire Barrier*

- Uma instrução barreira com semântica *acquire* (normalmente associada a um *read*) impõe que o efeito da instrução seja globalmente visível **antes** do efeito de todas as instruções **subsequentes**. Por outras palavras, a instrução barreira impede que as instruções que vêm **depois** possam ser movidas para **antes** da barreira. Este tipo de barreira não coloca qualquer limitação ao movimento das instruções que vêm antes da barreira. Graficamente:
   
```
   |	  
===|======= *acquire barrier* 
   |   ^
   V   |
       |
```

#### *Release Barrier*

- Uma instrução barreira com semântica *release* (normalmente associada a um *write*) impõe que o efeito da instrução seja globalmente visível **depois** do efeito de todas as instruções que vêm **antes** da instrução barreira. Por outras palavras, a instrução barreira impede que as instruções que vêm **antes** da barreira sejam movidas para **depois** da barreira. Este tipo de barreira não coloca qualquer limitação ao movimento das instruções que vêm depois da barreira. Graficamente:

```
   |
   |   ^
   v   |
=======|=== *release barrier*
       |
```

#### *Full-Fence*

- Combina a semântica *acquire* seguida da semântica *release* (associada às instruções atómicas *read-modify-write* e à instrução *mfence*). Isto é, o efeito da instrução barreira é globalmente vísivel **depois** do efeito de todas as instruções que vêm **antes** da barreira e **antes** do efeito de todas as instruções que vêm **depois**. Salienta-se que a sequência de uma instrução com semântica *realease* seguida de uma instrução com semântica *aqcuire* não forma uma *full-fence*, porque as respectivas semânticas não impedem que as instruções barreira sejam reordenadas entre si. No .NET uma escrita *volatile* (*release*) pode ser reordenada com uma leitura *volatile* (*acquire*) que venha a seguir, o que, como veremos adiante, não acontece em *Java*. Graficamente:

```
   |	  
   V
============ *full-fence* 
       ^
       |
```

### _Barries_ Inerentes aos blocos _synchronized_

- As barrieras de memória interpostas com os blocos `synchronized` visam garantir que as instruções que constituem a secção crítica não pode ser movidas para fora do bloco `synchronized`. Isto é, a aquisição do _lock_ interpõe uma barreira _acquire_ e a libertação do _lock_ interpõe um barreira _release_. As instruções que vêm antes de depois do bloco `synchronized` podem ser movidas para dentro do bloco, se isso contribuir para alguma otimização. Graficamente:

```				
                             |
synchronized(monitor) {  ====|= acquire barrier
                          ^  V
                          |
	critical section;
	
    |
    |
    V  ^
} =====|== release barrier
       |

```

## Reordenação

- As várias razões pelas quais as operações sobre a memória podem ser atrasadas ou executar fora de ordem podem ser agrupadas na categoria geral de **reordenação** (***reordering***).

- Consideremos o seguinte código:

```Java
public class PossibleReordering {
	static int x = 0, y = 0;
	static int a = 0, b = 0;
	
	public static void main(String... args) throws InterruptedException {
		Thread one = new Thread(() -> {
			a = 1;
			x = b;
		});
		Thread other = new Thread(() -> {
			b = 1;
			y = a;
		});
		one.start(); other.start();
		one.join(); other.join();
		System.out.printf("(x: %d, y: %d)\n", x, y);
	}
}
```

- Este programa ilustra quão difícil é racicionar acerca do comportamento de um programa mesmo simples como é o caso se não se utilizar a adequada sincronização.

- É fácil imaginar como `PossibleReordering` pode imprimir (x: 1, y: 0) ou (x: 0, y: 1) ou (x: 1, y: 1): *thread* `one` pode executar até à conclusão antes de `other` arrancar; `other` pode executar até à conclusão antes de `one` arrancar, ou as acções de `one` e `other` pode ser intercaladas. Mas estranhamente, `PossibleReordering` pode também imprimir (x: 0, y: 0).

- Se as instruções realizadas pela *thread* `one` forem reordenadas temos uma sequência que conduz à impressão de (x: 0, y: 0).

```
                   +----------+          reorder           +-------+   
Thread one:    --->| x = b(0) |--------------------------->| a = 1 |
                   +----------+                            +-------+

                                +-------+     +----------+
Thread other:  ---------------->| b = 1 |---->| y = a(0) |
                                +-------+     +----------+
```

- Este programa é trivial e, mesmo assim, é melindroso enumerar os resultados possíveis. A reordenação ao nível das operações sobre a memória pode levar a que os programas se comportem de forma inesperada. É proibitivamente deficil racicionar acerca da ordem de execução das instruções na ausência de sincronizção; é muito mais fácil garantir que os nossos programas usam sincronização adequadamente.

- A sincronização impede o compilador, *runtime* e *hardare* de reordenar as operações sobre a memória de formas que iriam comprometer as garantias de visibilidade dadas pelo *Java Memory Model*.

 
## Modelo de Memória do *Java*

- O JMM está especiificado em termos de **acções de sincronização**, que incluem leituras e escritas de variáveis `volatile`, acquisição e libertação de _locks_ dos monitores, _start_,  _joining_ e interrupção de _threads.

- O JMM define uma relação de ordem parcial chamada _happens-before_ sobre todas as **acções de sincronização dentro do programa.

- Para garantir que a _thread_ que executa a **acção B** pode ver os resultados da **Acção A** (sejam ou não as acções A e B executadas em _threads_ diferentes), deve existir uma relação **_happens-before_** entre a acção A e a acção B.

- Na ausência de uma relação _happens-before_ que ordene duas operações a JVM é livre de as reordenar se assim o entender.

- Ocorre um **_data race_** quando uma variável é lida por uma ou mais _threads_ e escrita por pelo menos uma _thread_ (estado partilhado mutável), quando as leituras e as escritas não estão ordenadas por _happens-before_.

- Um programa **correctamente sincronizado** é um programa sem _data races_; os programas correctamente sincronizados exibem uma característica que se chama **consistência sequencial**, significando que todas as acções de sincronização dentro do programa obedecem a uma ordem global fixa.

- A _happens-before_ B: isto que dizer que A interpõe uma barreira com semântica _release_ e B interpõe uma barreira com semântica _acquire_.

## As Regras da Relação _Happens-Before_

- **_Program order rule_**. Cada acção numa _thread_ H-B qualquer na _thread_ que venha depois segundo a ordem de programa.

- **_Monitor lock rule_**. A libertação do _lock_ de um monitor H-B da aquisição do _lock_ do mesmo monitor.

- **_Volatile variable rule_**. Uma escrita num campo `volatile` H-B de uma subsequente leitura do mesmo campo. (Esta semãntica também se aplica às classes atómicas).

- **_Thread start rule_**. Uma chamada ao método `Thread.start` H-B de qualquer acção realizada pela _thread_ lançada.

- **_Thread termination rule_**. Qualquer acção numa _thread_ H-B de qualquer outra _thread_ detectar que a _thread_ terminou (ou com `Thread.join` ou com `Thread.isAlive`).

- **_Interruption rule_**. Uma _thread_ que chama `Thread.interrupt` noutra _thread_ H-B da _thread_ interrompida detectar a interrupção (com InterruptedException, Thread.isInterrupted e Thread.interrupted).  



```Java
class SimpleThread extends Thread {
	private int arg1, arg2;
	private int result;
	
	public SimpleThread(int arg1, int arg2) {
		this.arg1 = arg1;
		this.arg2 = arg2;
	}
	
	public void run() {	// acquire barrier
		result = arg1 + arg2;
	} // release barrier
	
	public static void main(String... args) throws InterruptedException {
		SimpleThread st = new SimpleThread(5, 6);
		st.start();		// release barrier
		
		st.join();
		// acquire barrier
		System.println(result);
	}
}
```

- **_Finalizer Rule_**. O fim do construtor de um objecto H-B do arraque do _finalizer_ desse objecto.

- **_Transitivity_. Se A H-B B e B H-B C então A H-B C.


============



- O modelo de memória do *Java* (*Java Memory Model*) está especificado em termos de **acções de sincronização**, que incluem leituras e escritas das variáveis `volatile`, aquisição e libertação dos *locks* nos monitores, lançamento de *threads*, terminação de *threads* e sincronização com a terminação (*joining*) e interrupção de *threads*.

- Estas **acções de sincronização** são as construções da linguagem *Java* que interpõem as barreiras de memória com semântica _acquire_ e _release_.

- Uma relação de ordem parcial é uma relação num conjunto que é antisimétrica, reflexiva e transitiva, mas para quaisquer dois elementos x e y do conjunto, não é obrigatório que existe relação entre x e y ou y e x. Usamos relações de ordem parcial para exprimir preferências; podemos preferir um bife a um cachorro e Mozart a Mahler, mas não pretendemos necessariamente ter uma preferência entre bife e Mahler.

- O JMM define uma relação de ordem parcial (antisimétrica, transitiva e redlexiva) dedignada ***happens-before*** entre as **acções* dentro do programa.

	- Para garantir que a *thread* executando a **acção B** pode ver os resultado da **acção A** (ocorram A e B ou não na mesma *thread*) tem que haver uma realção ***happens-before*** entre as acções A e B. Na ausência de uma ordenação ***happens-before*** entre duas operações, a JVM tem a liberdade de reordenar as operações como bem entender.
	
	- Dito de outra forma: se existe uma relação _happens-before_ entre A e B, significa que a **acção A interpõe uma barreira com semântica _release_** e a **acção B interpõe uma barreira com semântica _acquire_**.

- Ocorre um ***data-race*** quando uma variável é lida por mais do que uma *thread* e escrita por pelo menos uma *thread* (pertence ao que designamos por **estado partilhado mutável**), mas as leituras e escritas, mas as leituras e escritas não estão ordenados por *happens-before*.

- Um programa **correctamente sincronizado** é um programa sem *data-races*; programas correctamente sincronizados exibem **consistência sequencial**, significando isso que todas as acções dentro do programa parecem ocorrer numa ordem global fixa.

- **O modelo de memória do _Java_ não permite a reordenação entre as acções de sincronização**. (O modelo de memória do .NET não dá esta garantia.) 

### Regras da Relação *Happens-Before* 

- ***Program order rule***. Cada acção numa *thread* _happens-before_ qualquer acção nessa *thread* que venha antes na ordem do programa.

- ***Monitor lock rule***. Um **_unlock_** do *lock* de um monitor _happens-before_ qualquer subsequente **_lock_** do mesmo monitor. Por outras palavras, a *thread* que entra num monitor tem visibilidade a todas as alterações do estado partilhado feitas pela última *thread* que saiu do monitor.

- **_Volatile field rule_**. A **escrita** de um campo `volatile` _happend-before_ qualquer subsequente leitura do mesmo campo. Por outras palavras, a escrita de um campo `volatile` fica imediatamente visível a todas as *threads*. (Na arquitectura x86-64 isto implica que as escritas `volatile` sejam feitas usndo uma instrução atómica, normalmente um _exchange_.)

- **_Thread Start Rule_**. A **chamada ao método `Thread.start`** numa _thread_ _happens-before_ **qualquer acção realizada pela _thread_** cuja execução é lançada. Por outras palavras, todas as escritas na memória feitas antes da chamada ao método `Thread.start` ficam visíveis à _thread_ lançada no início do respectivo método `run`.

- **_Thread Termination Rule_**. **Qualquer acção numa _thread_** _happens-before_ **qualquer outra _thread_ detectar que a _thread_ terminou**, ou depois de retornar com sucesso de `Thread.join` ou porque o método `Thread.isAlive` retornou `false`. Por outras palavras, todas as escritas na memória feita por uma _thread_ são visíveis a qualquer _thread_ que, de alguma forma, detecte que a _thread_ em apreço termine.

### Exemplo de utilização das _thread start rule_ e _thread termination rule_

- Segundo a **_thread start rule_**, os campos escritos no construtor de `AddThread` são visíveis no método `run` que é executado pela nova _thread_, sem necessidade de sincronização adicional.

- Segundo a **_thread termination rule_**, os campos escritos pela nova _thread_ no método `run` ficam visíveis no método `main` após o retorno do método `Thread.join`, sem necessidade de sincronização adicional.

```Java
class StartAndTerminationThreadRuleDemo {
	
	private static class AddThread extends Thread {
		private int arg1, arg2;
		private int result;
	
		public AddThread(int arg1, int arg2) {
			this.arg1 = arg1;
			this.arg2 = arg2;
		}
	
		// executed by the started thread
		public void run() {	// Thread.start interposes an acquire barrier
			result = arg1 + arg2;
		} // on termination interposes a release barrier
	}

	public static void main(String... args) throws InterruptedException {
		var addt = new AddThread(20, 22);
		addt.start();		// interposes a release barrier
		addt.join();		// after return, Thread.join interposes an acquire barrier
		System.out.printf("sum is: %d\n", addt.result);
	}
}
```

- **_Thread Interruption Rule_**. **Uma _thread_ que invocando `Thread.interrupt` sobre outra _thread_** _happens-before_ a **_thread_ interropida detectar a interrupção** (ou porue foi lançada a excepção `InterruptedException` ou porque invocou `Thread.isInterrupted` ou `Thread.interrupted`).Por outras palavas, as escritas na memória feitas antes de invocar o método `Thread.interrupt` são visíveis à _thread_ interrompida após esta detectar a interrupção.

### Exemplo de utilização das _thread interruption rule_

- Segundo a **_thread interruption rule_**, os campos escritos antes da chamada ao método `Thread.interrupt` são visíveis à _thread_ interrompida depois desta detectar a interrupção, sem necessidade de sincronização adicional.


```Java
class ThreadInterruptionRuleDemo {
	
	private static class AddOnIntrThread extends Thread {
		int intrArg1, intrArg2;
		
		public void run() {
			try {
				Thread.sleep(5000);
			} catch (InterruptedException ie) { // interposes an acquire barrier (action B)
				System.out.printf("sum is: %d\n", intrArg1 + intrArg2);
			}
		}
	}

	public static void main(String... args) throws InterruptedException {
		var addt = new AddOnIntrThread();
		addt.start();
		Thread.sleep(1000);
		addt.intrArg1 = 20;
		addt.intrArg2 = 22;
		addt.interrupt();		// interposes a release barrier (action A)
		System.out.println("...main exits");
	}
}
```

- **_Finalizer Rule_**. **O fim do construtor de um objecto** _happens-before_ **o início do _finalizer_ desse objecto**. Por outras palavras, todas as escritas feitas no construtor são visíveis no _finalizer_.

- **_Transitivity_**. Se A _happens-before_ B, e B _happens-before_ C, então A _happens-before_ C. 

#### _Happens-Before_ versus Barreiras de Memória

- Acções de Sincronização com semântica _release_ (referida acima por **acção A**):
	- Libertação de um _lock_;
	- Escrita num campo `volatile`;
	- Terminação de uma _thread_;
	- Interrupção de uma _thread_;
	- Fim da execução do construtor (relativamente ao _finalizer_);

- Acções de Sincronização com semântica _acquire_ (referida acima por **acção B**):
	- Acquisição de um _lock_;
	- Leitura de um campo `volatile`;
	- Detecção da terminação de uma _thread_;
	- Detecção da interrupção por parte da _thread_ interrompida;
	- Início da execução do _finalizer_ (relativamente ao fim do construtor);
	
### Ordenação Imposta pela Relação _Happens-Before_
	
- Mesmo as acções do programa sejam apenas parcialmente ordenadas, as acções de sincronização - acquisições e libertações de _locks_ e leituras e escritas de campos `volatile` - são totalmente ordenadas. Isto torna sensato descrever _happens-before_ em termos de "subsequentes" acquisições de _lcoks_ e leituras de variáveis `volatile`.

- A figura seguint ilustra a relação _happens-before_ quando duas _threads_ se  sincronizam usando um _lock_ comum. 

```
  Thread A
+-----------+
|    y = 2  |
+-----------+
      |
      V
+-----------+
|  lock M   |
+-----------+
      |
      v
+-----------+
|    x = 1  |
+-----------+
      |
      v
+-----------+    Everything before
|  unlock M |    unlock on M...                                   Thread B
+-----------+    ------------------------------------------>    +-----------+
      |                                 ... is visible to       |  lock M   |
      v                                 everything after        +-----------+
                                        the lock on M                 |
                                                                      V
                                                                +-----------+
                                                                |  i = x(1) |
                                                                +-----------+
                                                                      |
                                                                      V
                                                                +-----------+
                                                                |  unlock M |
                                                                +-----------+
                                                                      |
                                                                      V
                                                                +-----------+
                                                                |  j = y (2)|
                                                                +-----------+
                                                                      |
                                                                      V
```


- Todas as acções dentro da _thread_ A são ordenadas pelo regra da ordem de programa, assim como o são as acções realizadas pela _thread_ B.

- Porque a _thread_ A liberta o _lock_ M e a _thread_ B adquire subsequentemente M, todas as acções em A antes de libertar o _lock_ M são, pela regra da relação _happens-before_ relacionada com _locks_, ordenadas antes das acções realizadas na _thread_ B depois da aquisição do _lock_ M. Quando duas _threads_ se sincronizam em _locks_ **diferentes**, não podemos afirmar nada acerca da ordenação das acções realizadas pelas duas _threads_.

### _Piggybacking_ na Sincronização da Biblioteca Standard

- Outras relações _happens-before_ garantidas pelo biblioteca de classes do _Java_:
	 - Colocar um item de dados numa colecção _thread-safe_ _happens-before_ outra _thread_ obter esse item da colecção;
	 - Decrementado um `CountDownLatch` _happens-before_ uma _thread_ retornar de `await` sobre esse _latch_;
	 - Devolver uma autorização a um `Semaphore` _happens-before_ adquirir uma autorização no mesmo semáforo;
	 - As acções realizadas por uma _task_ representada por um `Future` _happens-before_ outra _thread_ retornar com sucesso do método `Future.get`;
	 - Submeter um `Runnable` ou um `Callable` a um `Executor` _happend-before_ a _task_ começar a execução, e;
	 - Uma _thread_ chegando a uma `CyclicBarrier` ou `Exchanger` _happes-before_ as outras _threads_ serem libertadas da mesma barreira ou ponto de troca. Se a `CyclicBarrier` especifica uma acção para executar quando a barreira abre, a chegada da última _thread_ do grupo à barreira _happens-before_ a execução da acção da barreira que por sua vez _happens-before_ todas as _thread_ serem libertadas da barreira.

	 
## Publicação de Objectos

- Os riscos da publicação imprópria de objectos são consequências da ausência de uma ordenação _happens-before_ entre a publicação de um objecto partilhado e o acesso ao mesmo objecto por parte de outra _thread_.

### Publicação Insegura

- A possibilidade de reordenação na ausência de uma relação _happens-before_ explica porquê a publicação de um objecto sem sincronização pode permitir a outra ver **um objecto parcialmente construído**.

- A iniciação de um objecto envolve a escrita em variáveis - os campos do objecto. Do mesmo modo, a publicação do objecto envolve a escrita noutra variável - a referência partilhada para o novo objecto.

- Se não for garantido que a plublicação da referência partilhada _happens-before_ outra _thread_ carregar a referência a referência partilhada para aceder ao objecto, então a escrita da referência para o novo objecto pode ser reordenada (na perspectiva d _thread_ que vai consumir o objecto) com as escritas nos campos do objecto feitas no construtor. Se isso acontecer, a outra _thread_ pode ver uma valor actualizado na referência partilhada, mas **valores desactualizados em alguns ou todos os elementos do estado do objecto** - isto é, um objecto parcialmente construído.

- A publicação insegura pode acontecer como resultado de uma iniciação _lazy_ incorrecta, como se mostra no seguinte programa:

```Java
public class UnsafeLazyInitialization {
	private static Resource resource = null;
	
	public static Resource getInstance() {
		if (resource == null)
			resource = new Resource();
		return resource;
	}
}
```

- À primeira vista o único problema deste código parece ser a _race condition_ inerente à falta de atomicidade na implementação da construção _check-then-act_ que leva a que possam ser criados mais do que uma instância de `Resource`. Em algumas circunstâncias, tal como quando todas as instâncias de `Resource` sejam idênticas podemos negligenciar esse facto (além da ineficiência de criar instâncias de `Resource` mais do que uma vez).

- Infelizmente, mesmo que aqueles defeitos sejam negligenciados, `UnsafeLazyInitialization` não é mesmo assim segura, porque **outra _thread_ pode observar uma referência para um objecto parcialmente construído**.  

- Suponha que a _thread_ A é a primeira a invocar `getInstance`. Ela vê que `resource` é `null`, instancia um novo `Resource` e afecta `resource` com a respectiva referência. Quando, mais tarde a _thread_ B chama `getInstance`, poderá ver que `resource` já tem um valor diferente de `null` e, por isso, ir usar o `Resource`já construído. Isto pode parecer inofensivo à primeira vista, mas <ins>não existe nenhuma relação _happens-before_ entre a escrita de `resource` pela _thread_ A e a leitura de `resource` pela _thread_ B. Foi usado um _data race_ para publicar o objecto e, por isso, não é garantido que a _thread_ B veja o estado correcto de `Resource`</ins>.

- O construtor de `Resource` altera os campos da instância de `Resource` acabada de alocar, alterando os valores por omissão (escritos pelo construtor de `Object`) para os seus valores iniciais. Uma vez que nenhuma das _threads_ usa sincronização, _thread_ B pode possivelmente ver as acções da _thread_ A por uma ordem diferente daquela que a _thread_ A as executou. Assim, mesmo que a _thread_ A tenha inicializado o `Resource` antes de afectar `resource` para a referenciar, a _thread_ B pode ver a escrita em `resource` ocorrer **antes** das escritas dos campos de `Resource` realizadas no construtor. Assim, a _thread_ B pode ver um `Resource` <ins>parcialmente construído</ins>.

- Com a excepção dos **objectos imutáveis**, não é seguro usar um objecto iniciado por outra _thread_ a menos que a publicação _happens-before_ da _thread_ consumidora utilizar o objecto.

____


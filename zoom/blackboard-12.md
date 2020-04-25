
# Aula 12 - Modelos de Memória (I)

____

### Visibilidade

- Num ambiente *single-threaded* se escrever o valor numa variável a depois ler essa variável sem intervenção de escritas, pode esperar obter o mesmo valor de volta. Isto parece **natural**. Pode ser difícil ao princípio, mas quando a escrita e a leitura ocorrem em *threads* diferentes, **isto não é simplesmente o que acontece**. Em geral, não existe **nenhuma garantia** de a *thread* leitora irá ver o valor escrito por outra *thread* em tempo útil ou mesmo se chega a ler de todo.

- `NoVisibility` pode ficar em ciclo indefinidamente porque o valor de `ready` pode nunca ficar visível à *thread* leitora. Mesmo mais estranho, `NoVisibilty` pode imprimir zero porque a escrita em `ready` pode ficar visível à *thread* leitora **antes** da escrita em `number`, um fenómeno designado **reordenação**.

- Não existe nenhuma garantia que as operações realizadas numa *thread* serão realizadas pela ordem de programa, desde que a reordenação não seja detectada por essa *thread* - <ins>mesmo que a reordenação seja aparente para as outras *threads*.

#### Exemplo de Não Visibilidade

```Java
public class NoVisibility {
	private static int number;
	private static boolean ready;	// when true, validates number

	private static class ReaderThread extends Thread {
		public void run() {
			while (!ready)
				;
			System.out.println(number);		// 42?
		}
	}	

	public static void main(String... args) throws InterruptedException {
		new ReaderThread().start();
		Thread.sleep(100);	// allow ReaderThread to start before set shared data
		number = 42;
		ready = true;
	}
}
```

- No exemplo, a intenção era que a *reader thread* fizesse *spin* até que visse o campo `ready` com o valor `true` para depois mostrar na consola o valor do campo `number`, que deveria ser 42. Embora esse seja o comportamento óbvio se compilarmos and executarmos este programa conctatamos que a *reader thread* não chega a mostrar nenhum valor, pois nunca chega a ver o valor `true` no campo `ready`.

- A razão para este comportamento é o facto do compilador JIT optimizar o ciclo `while` no método `ReaderThread.run` produzindo, de facto, código semelhante ao seguinte:

```Java
	public void run() {
		if (!ready) {
			while (true)
				;
		}
		System.out.println(number);		// if ready is false, never shows 42!
	}	
```

- Esta optimização é legal porque o compilador observa que o campo `ready` não está a ser alterado no corpo do ciclo `while`, pelo que lê o campo apenas uma vez e decide função do valor lido, isto é, se o campo for `true` continua para a instrução seguinte e se o campo for `false` entra num ciclo infinito. Esta optimização é referida na literatura como *hoisting optimization* que corresponde ao compilador calcular antes do ciclo as espressões que estando dentro dos ciclos são invariantes, isto é não existe código que altere, neste caso, não é alterada o campo `ready`.

- Contudo se comentarmos a chamada ao método `Thread.sleep` constatamos que o programa  tem o comportamento esperado. A razão para esse comportamento é que quando a *reader thread* começa a executar o método `run` o campo `ready` já foi afectado pelo *thread* primária e tem o valor `true`. Pelo contrário com a chamada ao método `Thread.sleep`, atrasando a execução da *thread* primário, quando a *reader thread* começa a execução do método `run` o valor do campo `ready` é `false`.

- Se o método `run` for escrito da seguinte forma:

```Java
	public void run() {
		while (!ready)
			Thread.yield();		// yield processor
		System.out.println(number);		// always shows 42
	}	
```

- O comportamente volta a ser o comportamento esperado, isto é, a *reader thread* mostra na consola o valor 42. A razão para este comportamento deve-se ao facto do compilador já não poder inferir que o campo `ready`não é alterado no ciclo por haver uma chamada a um método que não está definido na mesma classe.

- Considermos agora o que acontece com o mesmo exemplo em .NET. Considere o seguinte código:

``` C#
using System;
using System.Threading;

public class NoVisibility {
	private static int number;
	private static bool ready;
	
	private static void ReaderThreadBody() {
		Console.WriteLine($"ready: {ready}");
		while (!ready)
			;
		Console.WriteLine(number);
	}
	
	public static void Main() {
		new Thread(ReaderThreadBody).Start();
		Thread.Sleep(100);
		number = 42;
		ready = true;
	}
}
```

- Se este código for compilado sem optimizações com: `csc -optimize- NoVisibility.cs` funciona como se espera. Tudo indica que nem o compilador de C# nem o compilador JIT fazem a optimização *hoisting*.
 
- Se este código for compilado com optimizações com: `csc -optimize+ NoVisibility.cs` deixa de funcionar. Tudo indica que o compilador de C# ou o compilador JIT fazem a optimização *hoisting*.

- Mesmo mais estranho de tudo, e para a qual não tenho explicação plausível, é o facto de ao remover a linha `Console.WriteLine($"ready: {ready}");` o comportamento do programa é sempre o esperado. Verifique este comportamento depois da aula quando estava a concluir este documento. Após recorrer ao *visual studio debugger* para observar o código máquina, verifique que se houver uma chamada a um método (qualquer método) antes do ciclo é feita a optimização de *hoisting* e se não houver nenhuma chamada aquela optimização não é feita.

### Conclusão		

- **Para garantir a visiilidade das operações sobre a memória feitas por *threads* diferentes é necessário usar sincronização**.

- A sincronização para garaantir a visibilidade pode ser feita usando *locks* (como temos estado a fazer até agora), instruções atómicas (veremos adiante) ou se houver necessidade de garantir atomicidade pode ser usado o qualificativo `volatile`.

- Este exemplo funcionaria sempre correctamente (em *Java* e em .NET) se o campo `ready` fosse declarado do seguinte modo:
```Java
	private static volatile ready;
```

- Nos próximos tópicos veremos quais as implicações deste qualificativo nos modelos de memória do *Java* e do .NET.


## O que é um modelo de memória e porque precisamos de ter um

- Suponha que uma *thread* afecta um valor a `aVariable`:

```Java
	aVariable = 3;
```

- Um modelo de memória responde à questão "Sobre que condições uma *thread* que leia a variável vê o valor 3?".

- Isto parece uma pergunta idiota, mas na ausência de sincronização, existem razões para para que uma *thread* não imediatamente - ou mesmo chegar a ver - os resultados de uma operação realizada por outra *thread*. Existem várias razões para isto poder acontecer:

	- Os compiladores podem gerar instruções por uma ordem diferente daquela que seria "óbvia" e sugerida pelo código fonte ou armazenar variáveis em registos em vez de o fazer na memória;
	
	- Os processadores podem executar instruções em paralelo ou fora da ordem de programa;
	
	- As *caches* podem alterar a ordem pela qual a escrita das variáveis é transferida para a memória principal; os valores armazenados nas *caches* locais aos processadores não são visíveis aos outros processadores;

- Todos estas factores podem impedir uma *thread* de ver o valor mais recente  de uma variável e podem provocar que as acções sobre a memória realizadas nas outras *threads* possam parecer ser executadas fora de ordem - se não for actualizada a adequada sincronização.


### Modelos de Memória das Plataformas

- Numa arquitectur *shared-memory multiprocessor*, cada processdor tem a sua própria *cache* que é periodicamente reconciliada com a memória principal. As arquitecturas dos processadores providenciam vários graus de *cache choerence*, alguns providenciam garantias mínimas que permitem a diferentes processadores ver valores diferentes para a mesma localização da memória a virtualmente a  qualquer momento.

- O sistema operativo, compilador e *runtime* (e por vezes o programa também) devem prefazer as diferenças entre aquilo que o *hardware* providencia e aquilo que a *thread safety* exige.

- O **modelo de memória** de uma arquitectura diz aos programas quais as garantias que podem esperar do sistema de memória, e especifica as instruções especiais necessárias (designadas por *memory barriers* ou *fences*) para obter garantias adicionais de coordenação necessárias quando se partilham dados.

- Para proteger os projectistas das diferenças entre os modelos de memória das várias arquitecturas, o *Java* e o .NET providenciam os seus próprios modelos de memória e a JVM e o CLR lidam com as diferenças em os seus modelos de memória e os modelos de memória das aqruitecturas inserindo barreiras de memória nos sítios adequados.   

### Barreiras de Memória

- Os vários processadores e arquitecturas suportam uma grande variedades de instruções especiais para interpor barreiras de memória. Contudo, os modelos de memória do *Java* e do .NET podem ser completamente explicados com a referência a três tipo de barreiras de memória: *acquire barrier*, *release barrier* e *full-fence*.

#### *Acquire Barrier*

- Uma instrução barreira com semântica *acquire* (associada a um *read*) implica que o efeito da instrução é globalmente visível **antes** do efeito de todas as instruções subsequentes. Por outras palavras, a instrução barreira impede que as instruções que vêm depois possam ser movidas para **antes** da barreira. Este tipo de barreira não coloca qualquer limitação ao movimento das instruções que vêm antes da barreira.  
   
- Graficamente:

```
   |	  
---|------- *acquire barrier* 
   |   ^
   V   |
	   |
```

#### *Release Barrier*

- Uma instrução barreira com semântica *release* (associada a um *write*) implica que o efeito da instrução é globalmente visível **depois** do efeito de todas as instruções que vêm antes da instrução barreira. Por outras palavras, a instrução barreira impede que as instruções que vêm antes da barreira sejam movidas para **depois** da barreira. Este tipo de barreira não coloca qualquer limitação ao movimento das instruções que vêm depois da barreira.

- Graficamente:

   |
   |   ^
   v   |
-------|--- *release barrier*
       |

#### *Release Barrier*

- Combina a semântica *acquire* seguida da semântica *release* (*read-modify-write* ou instrução *mfence*). Isto é, o efeito da instrução é globalmente vísivel **depois** do efeito de todas as instruções que vêm **antes** da barreira e **antes** do efeito de todas as instruções que vêm **depois**. Salienta-se que a sequência de uma instrução com semântica *realease* seguida de uma instrução com semântica *aqcuire* não forma uma *full-fence*, porque as respectivas semânticas não impedem que as instruções barreira sejam reordenadas entre si. No .NET uma escrita *volatile* (*release*) pode ser reordenada com uma leitura *volatile* (*acquire*) que venha a seguir, o que, como veremos adiante, não acontece em *Java*.   

- Graficamente:
 
   |	  
   V
----------- *full-fence* 
       ^
       |

____


# Aula 14 - Modelos de Memória (III)

____

## Publicação de Objectos

- Os riscos da publicação imprópria de objectos são consequências da ausência de uma ordenação _happens-before_ entre a publicação de um objecto partilhado e o acesso ao mesmo objecto por parte de outra _thread_.

### Publicação Insegura

- A possibilidade de reordenação na ausência de uma relação _happens-before_ explica porquê a publicação de um objecto sem sincronização pode permitir a outra _thread_ ver **um objecto parcialmente construído**.

- A iniciação de um objecto envolve a escrita em variáveis - os campos do objecto. Do mesmo modo, a publicação do objecto envolve a escrita noutra variável - a referência partilhada para o novo objecto.

- Se não for garantido que a publicação da referência partilhada _happens-before_ outra _thread_ carregar a referência a referência partilhada para aceder ao objecto, então a escrita da referência para o objecto pode ser reordenada (na perspectiva da _thread_ que vai consumir o objecto) com as escritas nos campos do objecto feitas pelo construtor. Se isso acontecer, a outra _thread_ pode ver uma valor actualizado na referência partilhada, mas **valores desactualizados em alguns ou todos os elementos do estado do objecto** - isto é, ver um **objecto parcialmente construído**.

- A publicação insegura pode acontecer como resultado de uma iniciação _lazy_ incorrecta, como se mostra no seguinte programa:

```Java
public class UnsafeLazyInitialization {
	private static Resource resource = null;
	
	public static Resource getInstance() {
		// check-then-act - atomicity
		if (resource == null)
			resource = new Resource();
		else
			;
		return resource;
	}
}
```

- À primeira vista o único problema deste código parece ser a _race condition_ inerente à falta de atomicidade na implementação da construção _check-then-act_ que leva a que possam ser criadas mais do que uma instância de `Resource`. Em algumas circunstâncias, tal como quando todas as instâncias de `Resource` sejam idênticas podemos negligenciar esse facto (além da ineficiência de criar instâncias de `Resource` mais do que uma vez).

- Infelizmente, mesmo que aqueles defeitos sejam negligenciados, a classe `UnsafeLazyInitialization` não é segura, porque **outra _thread_ pode observar uma referência para um objecto parcialmente construído**.  

- Suponha que a _thread_ A é a primeira a invocar `getInstance`. Ela vê que `resource` é `null`, instancia um novo `Resource` e afecta `resource` com a respectiva referência. Quando, mais tarde a _thread_ B chama `getInstance`, poderá ver que `resource` já tem um valor diferente de `null` e, por isso, ir usar o `Resource`já construído. Isto pode parecer inofensivo à primeira vista, mas <ins>não existe nenhuma relação _happens-before_ entre a escrita de `resource` pela _thread_ A e a leitura de `resource` pela _thread_ B. Foi usado um _data race_ para publicar o objecto e, por isso, não é garantido que a _thread_ B veja o estado correcto de `Resource`</ins>.

- O construtor de `Resource` altera os campos da instância de `Resource` acabada de alocar, alterando os valores por omissão (escritos pelo construtor de `Object`) para os seus valores iniciais. Uma vez que nenhuma das _threads_ usa sincronização, _thread_ B pode possivelmente ver as acções da _thread_ A por uma ordem diferente daquela que a _thread_ A as executou. Assim, mesmo que a _thread_ A tenha inicializado o `Resource` antes de afectar `resource` para a referenciar, a _thread_ B pode ver a escrita em `resource` ocorrer **antes** das escritas dos campos de `Resource` realizadas no construtor. Assim, a _thread_ B pode ver um `Resource` **parcialmente construído**.

- Com a excepção dos **objectos imutáveis**, não é seguro usar um objecto iniciado por outra _thread_ <ins>a menos que a publicação _happens-before_ da _thread_ consumidora utilizar o objecto</ins>.

### Publicação Segura

- Os idiomas de publicação segura garantem que o objecto publicado é visível às outras _threads_ porque garantem que a publicação _happens-before_ a _thread_ consumidora carregar a referência para o objecto publicado.

- Se a _thread_ A coloca X numa `BlockingQueue` (e nenhuma outra _thread_ modifica X) e a _thread_ B recupera X da fila, é garantido que a _thread_ B vê X como a _thread_ A deixou. Isto deve-se ao facto das implementações de `BlockingQueue` terem sincronização interna suficiente para garantir que o `put` _happens-before_ o `take`. Do mesmo modo, usando uma variável partilhada protegida por un _lock_ ou uma variável partilhada `volatile` garante que as leituras e escritas nessas variáveis são ordenadas por _happens-before_.

- Esta garantia _happens-before_ e realmente uma promessa mais forte de visibilidade e ordenação do que aquela que é feita pela publicação segura. Quando X é publicado seguramente de A para B, a publicação segura garante o estado de X, mas não o estado de outras variáveis que A possa ter alterado. Mas se a _thread_ A ao colocar X numa fila _happens-before_ a _thread_ B recuperar X dessa fila, não apenas a _thread_ B vê X no estado que a _thread_ a deixou (assumindo que X não foi entretanto modificado pela _thread_ A ou por qualquer outra _thread_), mas a _thread_ B vê **tudo o que _thread_ A fez antes de colocar X na fila**.

### Idiomas para Publicação Segura

- Por vezes, faz sentido protelar a iniciação de objectos cuja inicialização é dispendiosa até que os mesmos sejam realmente necessários, mas já constatámos atrás que uma má utilização da _lazy initialization_ pode trazer problemas. A classe `UnsafeLazyInitialization` pode ser corrigida tornando o método `getResource` `synchronized`, como se mostra a seguir.

```Java
public class SafeLazyInitialization {
	private static Resource resource;
	
	public synchronized static Resource getInstance() {
		if (resource == null)
			resource = new Resource();
		return resource;	
	}
}
```

- Tendo em consideração de que o percurso de código através do método `getInstance` é relativamente curto, se `getInstance` não for chamdo frequentemente por múltiplas _threads_, existe pouca contenção sobre o _lock_, pelo que esta solução pode oferecer uma performance adequada.

- O tratamento de campos estáticos com inicializadores (ou seja, valores que são inicializados num _static initialization block_) é de algum modo especial e oferece garantias adicionais de _thread safety_. Os _static initializers_ são executados pela JVM no momento de inicializaçãoda respectiva classe, depois do carregamento da classe na memóriA, mas antes da classe poder ser utilizada por qualquer _thread_. Como a JVM adquire um _lock_ durante a initialização das classes e este _lock_ é também adquirido por cada _thread_ pelo menos uma vez para garantir que a classe foi carregada, as escritas na memória feitas durante a inicialização estática são automaticamente visíveis a todas as _threads_. Assim, objectos inicializados estaticamente não requerem nenhuma sincronização explícita nem quando estão a ser iniciados durante a construção ou quando estão a ser referenciados. Contudo, isto aplica-se apenas ao estado _as-constructed_ - se o objecto é mutável, é ainda necessária por parte dos leitores e escritores para tornar as modificações subsequentes visíveis e evitar corrupção dos dados.

- A seguinte classe demonstra a _lazy initialization_ usando a sincronzação subjacente a iniciação dos objectos classe.

```Java
public class EagerInitialization {
	private static Resource resource = new Resource();	// static initializer
	
	public static Resource getResource() { return resource; }
	
	//...
}
```

- Esta classe usa aquilo que se designa por _eager initialization_ elimina os custos de sincronização inerentes a cada chamada ao método `getInstance` na classe `SafeLazyInitialization`. Esta técnica pode ser combinada com o _lazy class loading_ da JVM para criar uma técnica de inicialização _lazy_ que não necessita de sincronização no caminho de código comum.

- O _lazy initialization holder class idiom_ apresentado a seguir usa uma classe cujo único propósito é iniciar o `Resource`.

```Java
public class ResourceFactory {
	private static class ResourceHolder {
		static Resouce resource = new Resource();
	}
	
	public  static Resource getResource() { return ResourceHolder.resource; }
	
	// ... other functionality
}
```

- A JVM difere a inicialização da classe `ResourceHolder` até que a mesma seja efectivamente usada e, porque `Resource` é iniciado com um inicializador estático, não é necessária sincronização adicional.

- A primeira chamada ao método `getResource` por parte de qualquer _thread_ fará com que a classe `ResourceHolder` seja carregada na máquina virtual e inicializada, momento em que a inicialização de `Resource` acontece num inicializador estático.


### _Double-checked locking_

- Nenhum livro sobre concorrência ficará completo sem a discussão infame antipadrão _double-checked locking_ (DCL) que é mostrado no código apresentado a seguir.

- Nas primeiras JVMs, a sincronização, mesmo a sincronização sem disputa tinha um custo de performance significativo. Em consequência, foraam inventados truques inteligentes (ou pelo menos aparentemente inteligentes) para reduzir o impacto da sincronização - alguns bons, alguns maus e outros "perigosos" - DCL pertence à categoria dos "perigosos".  

```Java
public class DoubleCheckedLocking {
	private static /* volatile */ Resource resource;
	
	public static Resource getInstance() {
		if (resource == null) {
			synchronized(DoubleCheckedLocking.class) { // acquire barrier
				if (resource == null)
					resource = new Resource();	// WR1, WR2, WR3(reference resource) : reordering: WR3 <--> WR1 e WR2
			} // release barrier
		} else
			;	// do not acquire the lock
		return resource;
	}
}
```

-  De novo, porque a performance das iniciais JVMs deixava muito a desejar, a inicialização _lazy_ foi frequentemente utilizada para evitar operações dispendiosas potencialmente desnecessárias ou para reduzir o tempo de arranque das aplicações. 

- Um método que implemente inicialização _lazy_ adequadamente escrito requer sincronização. Mas à época, a sincronização era lenta e, mais importante, não completamente entendida: os aspectos relacionados com a exclusão mútua (garantia de atomicidade) eram bem percebidos, mas os aspectos relacionados com a visibilidade não.

- O DCL pretendia oferecer o melhor de dois mundos - inicialização _lazy_ sem pagar a penalização associada à sincronização no caminho de código mais executado (depois do objecto ter sido criado). A forma como funciona passa por testar primeiro se a inicialização era necessária sem usar sincronização, e se a referência `resource` não fosse `null`, usar essa referência. No caso contrário, sincronizar e testar de novo se o `Resource` tinha sido inicializado, assegurando que apenas uma _thread_ iria realmente inicializar o `Resource` partilhado. O caminho de código mais comum - obter a referência para um `Resource` já construído - não usa sincronização. É aí que está o problema, já referido anteriormente, ser possível a uma _thread_ **ver um `Resource` parcialmente construído**.

- O problema real com o DCL e a assunção de que a pior coisa que pode acontecer quando se lê uma referência para um objecto partilhado sem sincronização é erradamente ver um valor obsoleto (neste caso, `null`); nessa situação, o idioma DCL compensa esse risco testando de novo na posse do _lock_.

- **Mas o pior caso e realmente consideralvelmente pior - é possível ver o valor corrente da referência mas valores obsoletos no estado do objecto**, significando isso que o objecto possa ser usado num estado inválido ou incorrecto.

- Alterações subsequentes na JVM (_Java_ 5.0 e posteriores) tornaram possível que o DCL funciona desde que `resource` seja tornado `volatile`, e o impacto disso na performance é pequeno uma vez que que as leituras `volatile` são usualmente um pouco mais dispendiosos que as leituras não `volatile`.

- Contudo, este é um idioma cuja utilidade já passou amplamente - as razões que motivaram a criação do idioma (a lentidão da sincronização sem disputa/_uncontended_) já não estão em cima da mesa, tornando o idioma menos efectivo como optimização. O idioma **_lazy initialization holder_** oferece os mesmos benefícios e é mais fácil de compreender.


## _Initialization Safety_ 

- A garantia de _initialization safety_ permite que **objectos imutáveis** adequadamente construídos possam ser partilhados entre _threads_ sem sincronização, independentemente da forma como são publicados - ou mesmo se publicados usando um _data race_. (Isto significa que `UnsafeLazyInitialization` é realmente seguro se `Resource` for imutável, isto naturalmente apenas do ponto de vista da visibilidade).

- Sem _initialization safety_, objectos supostamente imutáveis como as instâncias de `String` podiam parecer variar o seu valor se não fosse usada sincronização em ambas as _threads_ a que publica e a que consome. A segurança da arquitectura baseia-se na imutabilidade de `String`; a ausência de _initialization safety_ podia criar vulnerabilidades de segurança que permitiria que código malicioso fizesse _bypass_ aos testes que garantem a segurança.

- A _initialization safety_ garante que para objectos **adequadamente construídos** todas as _threads_ irão ver os valores correctos nos campos `final` cujos valores são definidos no construtor, independentemente da forma como o objecto é publicado. Mais, todas os objectos que possam ser **alcançados** através de um campo `final` de um objecto adequadamente construído (e.g., os elementos de um _array_ `final` ou o conteúdo de um `HashMap` referenciado por um campo `final`) são também garantidos estar visíveis a todas as _threads_. (Isto aplica-se aos objectos que são alcançaveis **apenas** através dos campos `final` do objecto sob construção.)

- Para os objectos com campos `final`, a _initialization safety_ proibe a reordenação de qualquer parte da construção com o carregamento inicial da referência para o objecto. Todas as escritas para os campos `final` feitas pelo construtor, assim como qualquer variáveis alcançaveis através desses campos, ficam "congelados" quando o construtor completa, e qualquer _thread_ que obtenha uma referência para esse objecto tem a garantia de ver o mesmo valor que é pelo menos tão actualizado como o valor congelado. As escritas que inicializam as variáveis alcançaveis a partir de campos `final` não são reordenadas com as operações que se seguem ao congelamento após construção.

- A _initialization safety_ significa que `SafeStates`, mostrado adiante, pode ser seguramente publicado mesmo através de inicialização _lazy_ não segura ou expondo a referência para `SafeStates` num campo publico estático sem qualquer sincronização, mesmo que a classe se baseie num ´HshSet´ que não é _thread safe_. 

- Formalmente, um objecto diz-se imutável se:

	- O seu estado não é alterado após a construção;
	
	- Todos os campos do objecto são `final`;
	
	- Foi adequdamente construído (a referência _this_ **não foge** durante a construção).

 
 ```Java
 public class SafeStates {
 	private final Map<String, String> states;
	
	public SafeStates() {
		states = new HashMap<String, String>();
		states.put("alaska", "AK");
		states.put("alabama", "AL");
		...
		states.put("wyoming", "WY");
	}
	
	public String getAbbreviation(String stateName) {
		return states.get(stateName);
	}
 }
 
 ```

- Contudo, um conjunto de pequenas alterações a `SafeStates` iria comprometer a sua _thread safety_. Se o campo `states` não for `final`, ou se outro método que não o construtor, modificasse o seu conteúdo, _initialization safety_ não seria suficiente forte para garantir acesso seguro a `SafeStates` sem sincronização. Se `SafeStates` tivesse outros campos não `final`, outras _threads_ podem ainda ver valores incorretos nesses campos. E permitir que o objecto fuja durante a construção invalida as garantias de _initialization safety_.

**_Initialization safety_ dá garantias de visibilidade apenas aos campos final e aos valores que são alcançaveis através de campos `final` com o estado que tinham quando terminou a execução do construtor. Para valores alcançaveis através de campos não `final` ou valores que podem mudar após construção, é necessário usar sincronização para garantir a visibilidade.**

### Resumo sobre o _Java Memory Model_

O *Java Memory Model* especifica quando as acções de uma _thread_ sobre a memória são garantidas ser visíveis a outras _threads_. As especificidades envolvem garantir que as operações são ordenadas por uma relação de ordem parcial designada **_happens-before_**, a qual é especificada ao nível de operações individuais sobre a memória e **acções de sincronização**. Na ausência de sincronização suficiente, podem acontecer coisas estranhas quando as _threads_ acedem a dados partilhados.


## Modelos de Memória do .NET _Framework_

- Para o .NET _Framework_, até agora, já foram especificados três modelos de memória:

	- O modelo de memória especificada na proposta de normalização submetida à ECMA (_European Computer Manufactures Assocition_), designado adiante por modelo ECMA 1.1;
	
	- Modelo de memória implementado no .NET _Framework_ versão 1.x, que foi o modelo implementado nas primeiras versões do .NET só suportada por plataformas basedas em processadoes x86;
	
	- Modelo de memória da versão 2.0 e versões posteriores.
	
- Considerando que já não interessa detalhar o modelo de memória do .NET _Framework_ 1.x, vamos apenas detalhar os modelos ECMA 1.1 e o modelo de memória do .NET _Framework_ 2.0 e versões seguintes. 
	

### Modelo de Memória ECMA 1.1

- Os acessos à memória são classificados como acessos ordinários e acessos _volatile_.

- Os acessos _volatile_ não podem ser criados eliminados ou amalgamados (isto é, multiplos acessos à mesma variável substituídos por um único acesso).

- Os acessos ordinários à memória são constrangidos pelas seguintes regras:

	- O comportamento de cada _thread_ executando isoladamente não pode ser alterado (isto significa que uma leitura ou uma escrita feitos por uma dada _thread_ para uma dada localização de memória não pode ultrapassar uma escrita da mesma _thread_ para a mesma localização de memória);
	
	- A aquisição de _locks_ e a leitura de campos _volatile_ têm semântica de barreira _acquire_;
	
	- A libertação de _locks_ e a escrita em campos _volatile_ têm semântica de barreira _release_;
	
	- As instruções atómicas têm semântica de barreira _acquire_ seguido da semântica de barreira _release_, isto é, interpõem uma barreira com semântica _full-fence_.

- Ao contrário do modelo de memória do _Java_ que não permite a reordenação das **acções de sincronização** nomeadamente as leituras e escrita `volatile`, o modelo de memória do .NET _Framework_ **apenas garante que é respeitada a semântica _acquire_ da leitura `volatile` e a semântica _realease_ da escrita `volatile`**. Assim, no .NET _Framework_:

	- Uma leitura `volatile` nunca é reordenada com uma escrita `volatile` que venha a seguir porque a semântica _acquire_ da leitura impede que a escrita que vem a seguir de passe para antes da leitura; por outro lado, a semântica _release_ da escrita impede que a leitura que vem antes da escrita passe para depois escrita;
	
	- Uma escrita `volatile` pode ser reordenada com uma leitura `volatile` que venha a seguir porque a semantica _realease_ da escrita não impede que a leitura que vem depois passe para antes da escrita; por outro lado, a semântica _acquire_ da leitura não impede que a escrita que vem antes da leitura passe para depois; 	

	- Esta característica deste modelo de mmória costuma ser designada por _Release/Acquire Hazard_. Para eveitar esta reordenação é necessário interpor explicitamente uma _full-fence_ (barreira completa) o que pode ser feito invocando o método `System.Threading.Interlcoked.MemoryBarrier`.


- Embora o modelo de memória ECMA para o .NET _Framework_ especifique apenas garantias mínimas, a única implementação do _runtime_ do .NET _Framework_ 1.x foi em processadores x86, o que significa que o _runtime_ do .NET implementou uma modelo de memória da arquitectura x86 (contudo, não é exactamente esse modelo devido às optimização feitas pelo compilador JIT), que tem como caracteristica importante não reordenar as escritas na memória - apresenta _Total Store Order_ - e também não permitir muita liberdade de movimentos das leituras

- Na versão 2.0 do .NET _Framework_, o suporte para máquinas com modelos de memória mais relaxados, como aquelas que usam o processador IA-64 criaram um problema. Os utilizadores do .NET _Framework_ podiam, de facto, estar a basear-se num modelo de memória com mais garantias do que o modelo ECMA 1.1, providenciado pela implementação em x86, e o seu código poderia falhar de forma não determinística quando executasse em plataformas basedas no processador IA-64.

- A _Microsoft_ decidiu que os clientes ficariam melhor servidos com um modelo de memória com maiores garantias que assegurava que a maioria do código já escrito, que funcionava nas plataformas baseadas em x86, também funcionaria nas outras plataformas. Contudo, também foi considerado que o modelo x86 era demasiado restritivo: <ins>não permitia grande liberdade no movimento dos _reads_ e os compiladores optimizadores precisam realmente de flexibilidade para reordenar as leituras para fazerem bem o seu trabalho</ins>.

- O resultado é o modelo de memória do .NET _Framework_ 2.0 e versões posteriores. As regras deste modelo de memória são:

	1. Todas as regras do modelo ECMA 1.1;
	
	2. Não podem ser introduzidos _reads_ ou _writes_;
	
	3. Um _read_ pode ser removido se estiver adjacente a outro _read_ da mesma localização feito pela mesma _thread_. Um _write_ pode ser removido se estiver adjacente a outro _write_ para a mesma localização feito pela mesma _thread_. A regra 5 pode ser usada para tornar os _reads_ e os _writes_ adjacentes antes de aplicar esta regra;
	
	4. **Os _writes_ não podem ser movidos para depois de outros _writes_ feitos pela mesma _thread_**.
	
	5. Os _reads_ podem ser movidos para antes no tempo, mas nunca ultrapassando um _write_ para a mesma localização feito pela mesma _thread_.
	
- Tal como no modelo de memória do x86, os _writes_ estão fortemente constrangidos, mas ao contrário do modelo x86, os _reads_ podem ser movidos e também podem ser eliminados.

- Este modelo não permite a introdução de _reads_ (ordinários e `volatile`) para permitir a escrita de código _lock-free_ onde as variáveis lidas por uma _thread_ podem estar a ser alteras simultaneamente por outras _threads_; assim, se o compilador introduzisse _reads_ que não estão especificados no programa, assumindo que a variável não se tinha alterado releativamente a uma leitura anterior, isso comprometia a correcção dos algoritmos _lock-free_ ou _low-lock.

 
## Principais Diferenças entre os Modelos de Memória do _Java_ e do .NET _Framework_

- Os dois aspectos essenciais que um programador deve reter relativamentre aos modelos de memória destes dois _runtimes_ são os seguintes:

	- Ao contrário do modelo de memória do _Java_, o modelo de memória .NET _Framework_2.0_ **não permite a reordenação da escritas realizadas pela mesma _thread_** o que torna segura a publicação de objectos sem sincronização, porque **nunca será possível a observação por qualquer _thread_ de objectos parcialmente construídos**.

	- Ao contrário do modelo de memória do _Java_, o modelo de memória do .NET _Framewok_ permite a reordenação da escrita `volatile` numa variável seja reordenada com a leitura `volatile` de outra variável que venha a seguir pela ordem de programa (_Release/Acquire Hazard_). Contudo, esta reordenação pode ser impedida interpondo uma _full-fence_ entre a escrita e a leitura, utilizando o método `System.Threading.Interlocked.MemoryBarrier`.


## Publicação e Fuga de Objectos

- **Publicar** um objecto significa torná-lo acessível a código fora do âmbito corrente, como, por exemplo, armazenar a referência para o objecto acessível a outro código, retornar a referência de um método não privado ou passar a referência para um método de outra classe. Em muitas situações, pretendemos que os os objectos e os seus aspectos internos **não sejam publicados**. Noutras situações, pretendemos publicar o objecto para utilização geral, mas faze-lo de forma _thread-safe_ pode requerer a utilização de sincronização. Publicar váriáveis que armazenam estado interno dos objectos compromete o encapsulamento e dificulta a manutenção dos invariantes; publicar objectos antes dos mesmos estarem completamente construídos pode comprometer a _thread safety_.

- Quando um objecto é publicado quando não o devia ter sido é considerado como tendo **fugido**. Adiante veremos variás circunstâncias em que os **objectos podem fugir**.

- A forma mais flagrante de publicação é armazenar a referência para o objecto num campo estático público, onde qualquer classe ou _thread_ poderá vê-lo. O método `initialize` instancia um novo `HashSet<Secret>` e publica-o, armazenando a respectiva referência no campo público `knownSecrets`:

```Java
public static Set<Secret> knownSecrets;

public void initialize() {
	knownSecrets = new HashSet<Secret>();
}
```

- A publicação de um objecto pode indirectamente publicar outros objectos. Se acrescentarmos um `Secret` ao conjunto `knownSecrets`, publicamos também o `Secret`, porque qualquer código que pode iterar sobre o conjunto e obter uma referência para o novo `Secret`. Similarmente, retornando uma referência de um método não privado também publica o objecto devolvido. `UnsafeStates`, a seguir, publica o _array_, supostamente privado, que contém as abreviaturas dos estado:

```Java
class UnsafeStates {
	private String[] states = new String[] {
		"AK", "AL", ...
	};
	
	public String[] getStates() { return states; }
}
```

- Publicar ´states´ desta forma é problemático porque qualquer chamador do método ´getStates´ poderá modificar o seu conteúdo. Nesta caso, o _array_ `states` **fugiu** do âmbito a que era suposto pertencer, pois foi efectivamente tornado público quando era suposto ser privado.

- A publicação de um objecto publica igualmente quaisquer objectos referenciados pelos seus campos não privados. Generalizando, qualquer objecto **alcançavel** a partir de um objecto publicado seguindo uma qualquer cadeia de referências armazenadas em campos não privados e chamadas a métodos são também publicados.

- Na perspectiva de uma class `C`, um método _alien_ é um método cujo comportamento não seja completamente especificado por `C`. Isto inclui métodos noutras classes assim como métodos que possam ser substituíveis (_overriden_) (i.e., que não são nem `private` nem `final`) na própria classe `C`. A passagem de um objecto para um método _alien_ deve ser considerada uma publiação desse objecto. Uma vez que não podemos saber que código irá realmente ser invocado, não sabemos se o método _alien_ não publica o objecto ou retém a referência para o objecto que poderá mais tarde se usado a partir de outra _thread_.

- Se outra _thread_ faz efectivamente alguma coisa com a referência publicada não interessa realmente, porque o risco de actualização indevida está ainda presente. Uma vez que um objecto **fuja**, deve assumir que outra classe ou _thread_ pode, maliciosamente ou descuidadamente utilizá-lo indevidamente. Isto é uma razão convincente para usar encapsulamento: torna mais prático analisar a correcção dos programas e mais difícil a violação acidental das restrições do projecto.

- O último mecanismo através do qual um objecto ou o seu estado interno pode ser publicado é através da <ins>publicação de uma _inner_ classe de instância</ins>, como se mostra no seguinte programa:

```Java
public class ThisEscape {
	public ThisEscape(EventSource source) {
		source.registerListener(
			new EventListener() {
				public void onEvent(Event e) {
					doSomething(e);
				}
		});
	}
}
```

- Quando `ThisEscape` publica o `EventListener`, é também implicitamente publicada a instância de `ThisEscape` exterior, porque as instâncias das _inner_ classes contêm uma referência oculta para  a instância da classe a que estão associadas.

### Práticas de Construção Seguras

- `ThisEscape` ilustra um caso especial de **fuga**: a referência `this` foge durante a construção do objecto. Quando a _inner_ instância de `EventListener` é publicada também o é a instância de `ThisEscape`. Mas como um objecto só terá um estado previsível e consistente depois do retorno do seu construtor, a sua publicação dentro do seu construtor pode publicar um objecto parcialmente construído. Isto é verdade <ins>mesmo que a publicação seja a última instrução do construtor</ins>. Se a referência `this` foge durante a construção, o objecto é considerado como **inadequadamente construído**. Mais especificamente, a referência `this` não deve fugir da posse da _thread_ corrente até ao retorno do construtor. A referência `this` pode ser armazenada algures pelo construtor desde que não seja **usada** por outra _thread_ antes de terminar o construtor. A classe`SafeListener`, mostrada adiante, demonstra esta técnica.

**Nunca permita que a referência `this` fuja durante a construção de um objecto**.

- Um erro comom que pode levar a referência `this` a fugir durante a construção é iniciar uma _thread_ no construtor. Quando é criada _thread_ no construtor de um objecto, quase sempre é partilha a referência `this` com a nova _thread_: ou explicitamente (passado-a para o construtor) ou implicitamente (porque a `Thread` ou `Runnable` é uma classe interna da classe do objecto). A nova _thread_ pode então ser capaz de ver o objecto dono antes deste estar completamente construído. Não existe nada de errado em **criar** uma _thread_ no construtor, mas é melhor não **iniciar** a _thread_ imediatmente. Em alternativa, de expor-se um método `start` ou `initialize` que faça a **iniciação** da _thread_ criada no construtor.

- A chamada a um método de instância substituível (nem `private` nem `final`) a partir do construtor pode também permitir a fuga do `this`.   

- Se for tentado a registar um _event listener_ ou iniciar uma _thread_ a partir do construtor, pode evitar a construção imprópria usando um construtor privado e um método de fabrico público, como se mostra no código seguinte.

```Java
public class SafeListener {
	private final EventListener listener;
	
	private SafeListener() {
		listener = new EventListener() {
			public void onEvent(Event e) {
				doSomething(e);
			}
		};
	}
	
	public static SafeListener newInstance(EventSource source) {
		SafeListener safe = new SafeListener();
		source.registerListener(safe.listener);
		return safe;
	}
}
```

____
 




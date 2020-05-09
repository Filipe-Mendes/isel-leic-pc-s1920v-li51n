
# Aula 15 - Modelos de Memória (IV)

___

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

- Na perspectiva de uma class `C`, um método _alien_ é um método cujo comportamento não seja completamente especificado por `C`. Isto inclui métodos noutras classes assim como métodos que possam ser substituíveis (_overriden_) (i.e., que não são nem `private` nem `final`) na própria classe `C`. **A passagem de um objecto para um método _alien_ deve ser considerada uma publicação desse objecto**. Uma vez que não podemos saber que código irá realmente ser invocado, não sabemos se o método _alien_ não publica o objecto ou retém a referência para o objecto que poderá mais tarde se usado a partir de outra _thread_.

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

## _Thread Confinement_

- O acesso a estado partilhado mutável requer sincronização; **uma forma de evitar este requisito é não partilhar objectos**. Se os objectos forem apenas acedidos por uma única _thread_, não é necessária sincronização. Esta técnica, designada por **_thread confinement_**, é uma das formas mais simples de obter _thread safety_. Quando um objecto está confinado a uma _thread_, a sua utiliação é automaticamente _thread safe_, mesmo que o objecto em si não o seja.

- O _toolkit_ gráfico _Swing_ usa _thread confinement_ extensivamente. As componentes visuais do _Swing_ e os objectos que representam os modelos de dados não são _thread safe_; em alternativa, a segurança é obtida confinando estes objectos à _thread_ que processa os eventos no _Swing_ (_event dispatcher thread_). Para usar o _Swing_ correctamente, o código que executa noutras _threads_, que não a _event dispatcher thread_, não deve aceder directamente a esses objectos. (Para simplificar a vida aos programadores, o _Swing_ providencia o método `SwinngUtilities.invokeLater` que promove a execução de um `Runnable` na _event dispatcher thread_.) Muitos erros de concorrência nas aplicações que usam _Swing_ estão relacionadas com a utilização destes objectos que são confinados à _event dispatcher thread_ por parte de outras _threads_.

- Outra aplicação comum do _thread confinement_ é a utilização dos objectos `Connection` do JDBC (_Java Database Connectivity_) que são geridos em _pool_. A especificação JDBC não exige que os objectos `Connection` sejam _thread safe_. Nas aplicações servidoras típicas, as _threads_ solicitam uma ligação no _pool_, usam essa ligação para processar um único pedido e, por último, devolvem a ligação ao _pool_.

- Uma vez que muitos pedidos, como _servlet requests_ ou chamadas a EJB (_Enterprise Java Beans_), são processados sincronamente por uma única _thread_ e o _pool_ não dispensa a mesma ligação a outra _thread_ até que a mesma lhe seja devolvida. Este padrão de gestão de ligações confina implicitamente a `Connection` à _thread_ durante e duração do processamento dos pedidos ao servidor. 

- Tal como a linguagem não tem nenhum mecanismo para obrigar a que uma variável seja protegida por um _lock_, não existe forma se confinar um objecto a uma _thread_. O _thread confinement_ é um elemento da concepção dos programas que deve ser respeitadp pela implementação. A linguagem e as bibliotecas base providenciam mecanismos que podem ajudar a manter _thread confinement_ - variáveis locais e a classe `ThreadLocal` - mas mesmo com estes mecanismos, **é ainda da responsabilidade do programador garantir que os objectos _thread confined_ não fogem da respectiva _thread_**.


### _Ad-hoc Thread Confinement_

- _Add-hoc thread confinement_ descreve as situações em que a responsabilidade de manter o _thread confinement_ é inteiramente da implementação. _Add-hoc thread confinement_ pode tronar-se frágil porque nenhumas da _features_ da linguagem, como, pr exemplo, os modificadores de visibilidade ou variáveis locais, ajudam a confinar o objecto à respectiva _thread_.

- A decisão de usar _thread confinement_ é consequência da decisão de implementar um subsistema particular, por exemplo um _toolkit_ gráfico (GUI) como um subsistema _single-threaded_. Os subsistemas _signle-threaded_ podem, por vezes, oferecer benefícios em simplicidade que prevalece sobre a fragilidade do _Ad-hoc thread confinement_. Outra razão para tornar um subsistema _single-threaded_ e evitar _deadlocks_; esta é uma das principais razões porque a maioria do _frameworks_ GUI são _single-threaded_.

- Um caso especial de _thread confinement_ aplica-se às variáveis _volatile_. É seguro fazer operações _read-modify-write_ em variáveis `volatile` partilhadas desde que haja a garantia que as variáveis `volatile` são escritas por uma única _thread_. Neste caso, estamos a confinar a **modificação** a uma única _thread_ para evitar _race conditions_, e as garantias de visibilidade das variáveis `volatile` garantem que as outras _threads_ vêm sempre o resultado da última escrita.

### _Stack Confinement_

- O _stack confinement_ é um caso especial de _thread confinement_, no qual um objecto pode apenas ser alcançado através de variáveis locais a um método. Tal como o encapsulamento facilita a preservação de invariantes, as variáveis locais podem facilitar o confinamento de objectos a uma _thread_. As variáveis locais estão intrinsecamente confinadas à _thread_ corrente; elas são armazenadas no _stack_ da _thread_ corrente, que não está acessível a outras _threads_.

- Para variáveis locais de tipos primitivos, como `numPairs` no método `loadTheArc`, não é possível violar o _stack confinment_ mesmo que tentemos. Em _Java_ não existe forma de obter uma referência para uma variável de um tipo primitivo, portanto a semântica da linguagem garante que as variáveis locais de tipos primitivos são sempre _stack confined_.

```Java
public int loadTheArc(Collection<Animal> candidates) {
	SortedSet<Animal> animals;
	int numPairs = 0;
	Animal candidate = null
	
	// animals confined to method, do not let them escape!
	animals = new TreeSet<Animal>(new SpeciesGenderComparator());
	animals.add(candidates);
	for (Animal a : animals) {
		if (candidate == null || !candidate.isPotentialMate(a))
			candidate = a;
		else {
			ark.load(new AnimalPair(candidate, a));
			numPairs++;
			++candidate = null;
		}
	}
	return numPairs;
}
```

- Manter _stack confinement_ em referências para objectos requer que o programador garanta que a referência não foge. Em `loadTheArc`, instanciamos um `TreeSet` e armazenamos a referida referência em `animals`. Neste ponto, existe exactamente uma referência para o `Set`, numa variável local sendo, por isso, confinada à _thread_ corrente. Contudo, se publicarmos, de alguma forma, a referência para o `Set`, o confinamento seria violado e `animals` iria **fugir**.

- Usar um objecto _non-thread-safe_ no contexto de uma _thread_ é ainda _thread-safe_. Contudo, é necessário cuidado: o requisito de desenho deconfinamento do objecto à _thread_ corrente, ou a consciência de que o objecto não é _thread-safe_, existe, frequentemente, apenas na cabeça do projectista quando o código é escrito. Se este confinamento não for bem documentado, futuros mantenedores podem erroneamente permitir a fuga do objecto.
 
### `ThreadLocal`

- Uma forma mais formal de manter _thread confinement_ é com a utilização da classe `ThreadLocal`, que permite associar um valor _per-thread_ com um objecto do tipo **_value-holding_**. `ThreadLocal` providencia os metodos de acesso `get`  e `set` que mantêm uma cópia separada do valor para cada_ _thread_ que o utiliza, portanto o método `get` retorna o valor mais recente passado ao método `set` pela **_thread_ correntemente em execução**.

- As variáveis _thread-local_ são frequentemente usadas para evitar a partilha em desenhos baseados em _Singletons_ ou variáveis globais. Por exemplo, uma aplicação _single-threaded_ pode manter uma _connection_ JDBC que seja iniciada no arranque para evitar que tenha que passar a referência para `Connection` a todos os métodos. Uma vez que as _connections_ JDBC podem não ser _thread-safe_, uma aplicação _multi-threaded_ que usa uma _connection_ global sem coordenação adicional também não é _thread-safe_. Usando uma instância de `ThreadLocal` para armazenar a _connection_ JDBC, como se mostra na listagem seguinte, cada _thread_ terá a sua prórpria _connection_.

```Java
private static ThreadLocal<Connection> connectionHolder =
	new ThreadLocal<Connection> () {
		public Connect initialValue() {
			return DriverManager.getConnection(DB_URL);
		}
	};

public static Connection getConnection() {
	return connectionHolder.get();
}

```

- No .NET está disponível exactamente o mesmo mecanismo, com pequenas mínimas:

```C#
private static ThreadLocal<Connecction> connectionHolder =
	new ThreadLocal<Connection> (() => return DriverManager.getConnection(DB_URL));

public static Connection DbConnection {
	get { return connectionHolder.Value; }
}

```

- Esta técnica pode também ser usada quando uma operação usada frequentemente requer um objecto temporário como um _buffer_ e pretende evitar a realocação do objecto temporário em cada invocação. Por exemplo, antes do _Java_ 5.0, o método `Integer.toString` usava `ThreadLocal` para armazenar o _buffer_ com 12 _bytes_ usado para formatar o seu resultado, em alternativa a usar um _buffer_ estático partilhado (o que exigiria a utilização de _locking_) ou alocar um novo _buffer_ em cada invocação. (Esta técnica é improvável ser um ganho de performanca a menos que a operação seja realizada com alta frequência ou a alocação seja invulgarmente dispendiosa. No _Java_ 5.0, isto foi substituído com uma solução mais directa de alocar um novo _buffer_ por cada invocação, sugerindo que algo tão banal como poupar a alocação de um _buffer_ temporário, não é um ganho de performance.)

- Quando uma _thread_ chama o método `ThreadLocal.get` pela primeira vez, o método `initialValue`é consultado para providenciar o valor initial para a _thread_ corrente. Conceptualmente, pode pensar-se em `ThreadLocal<T>` como armazendo um `Map<Thread, T>`, que armazena os valores específicos das _threads_, embora a implementação não seja essa; os valores específicos das _threads_ são armazenados na respectica instância do tipo `Thread`; quando uma _thread_ termina, os valores específicos da _thread_ podem ser colectados pelo GC.

- Se estiver a adaptar uma aplicação _single-threaded_ para um ambiente _multi-threaded_, poderá preservar a _thread-safety_ convertendo as variáveis globais partilhadas para `ThreadLocal`s, se a semântica das variáveis globais partilhadas o permitir; uma _cache_ com âmbito da aplicação não seria tão útil se fosse transformada num número arbitrário de _thread-local caches_.

- `ThreadLocal` é amplamente usado na implementação de **_application frameworks_**. Por exemplo, os contentores J2EE associam uma contexto de transacção com a _thread_ em execução durante uma chamada EJB. Isto é facilmente implementável usando um `ThreadLocal` estático que armazena o contexto da transacão: quando o código do _framework_ necessita de determinar a transacção corrente, obtém o contexto de transacção no `ThreadLocal`. Isto é conveniente na medida em que reduz a necessidade de passar o contexto de transacção para cada método.

- É fácil abusar do `ThreadLocal` considerando a sua propriedade de _thread confinement_ como licensa para usar variáveis globais ou como uma forma de criar argumentos "escondidos" nas chamadas aos métodos. Tal como as variáveis globais, as variáveis _thread-local_ pode prejudicar a reutilização e introduzir acoplamentos escondidos entre classes e, por isso, devem ser usadas com critério.

## Imutabilidade

- A outra conclusão final sobre a necessidade de evitar a sincronizaçao é usar objectos **imutáveis**. Quase todos os riscos de atomicidade e visibilidade que descrevemos até agora com o acesso a valores obsoletos, perder actualizações ou observar um objecto num estado inconsistente, tem a ver com o acesso simultâneo de várias _threads_ ao **estado partilhado mutável**. Se o estado de um objecto não for modificável, **esses riscos e complexidades simplesmente desaparecem**.

- Um objecto ímutável é um objecto cujo estado não pode ser alterado após a construção. Os objectos imutáveis são inerentemente _thread-safe_; os seus invariantes são estabelecidos no construtor e, como o estado não poder ser modificado, esses invariantes verficam-se durante todo o tempo de vida do objecto.

**Os objectos imutáveis são sempre _thread-safe_**.

- Os objectos imutáveis são **simples**. Eles podem apenas estar num estado que é determinado pelo construtor. Um dos aspectos mais difícies na concepção de programas é racicionar acerca dos estado possíveis de objectos complexos. Por outro lado, o raciocínio acerca do estado dos objectos imutáveis é trivial.

- Os objectos imutáveis são também _safer_. Passar um objecto mutável para código não confiável ou publicar o objecto onde código não confiável pode aceder-lhe é perigoso - o código não confiável pode modificar o seu estado ou, pior, reter uma referência para o objecto e modificar o seu estado, mais tarde, noutra _thread_. Por sua vez, os objectos imutáveis não podem ser subvertidos desta forma por código malicioso ou _buggy_, portanto eles são seguros para partilhar e publicar livrememte sem ter a necessidade de realizar cópias defensivas dos mesmos.

- Nem a _Java Language Specification_ nem o _Java Memory Model_ definem formalmente a imutabilidade, mas a imutabilidade **não** é equivalente a simplesmente declarar todos os campos de um objecto como `final`. Um objecto cujos campos são todos declarados como `final` pode ainda ser mutável, na medida em que campos `final` podem armazenar referências para objectos mutáveis.

- Um objecto é **imutável** se:

	- O seu estado não pode ser modificado após a construção;
	
	- Todos os seus campos são `final`, e;
	
	- For **adequadamente construído** (a referência `this` não foge durante a construção).

- Os objectos imutáveis podem ainda usar objectos mutáveis para gerir o seu estado como se mostra na classe `ThreeStooges`. Ainda que o `Set` que armazena os nomes seja mutável, o desenho de `ThreeStooges` torna impossível modoficar o `Set` depois da construção. A referência `stooges` é `final`, então todo o estado do objecto é alcançavel através de um campo `final`. O último requisito, construção adequada, é facilmente obtido uma vez que o construtor não faz nada que permita que a referência `this` fique acessível a outro código que não seja o construtor e o respectivo chamador. 

```Java
public final class ThreeStooges {
	private final Set<String> stooges = new HashSet<String>();
		
	public ThreeStooges() {
		stooges.add("Moe");
		stooges.add("Larry");
		stooges.add("Curly");
	}
	
	public boolean isStooge(String name) { return stooges.contains(name); }
}
```

- É tecnicamente possível ter objectos imutáveis sem que todos os campos sejam `final` - `String` é uma dessas classes - mas isso baseia-se num raciocínio delicado acerca de _data races_ benignos que requerem um conhecimento profundo do _Java Memory Model_. (Para os curiosos: `String` calcula o _hash code_ de forma _lazy_ na primeira vez que o método `hashCode` é chamado e faz _cache_ do resultado num campo não `final`, mas isto apenas funciona porque o campo pode ter apenas um valor que não seja o valor por omissão que é sempre o mesmo de cada vez que for calculado, pois é derivado de forma determinística a partir do estado que é imutável.)

- Como o estado de um programa está sempre a alterar-se, podemos ser tentados a pensar que os objectos imutáveis têm uma utilização limitada, mas isso não é verdade. Existe uma diferença entre um objecto ser imutável e a referência para ele ser imutável. O estado do programa armazenado em objectos imutáveis pode ainda ser alterado **substituindo** os objectos imutáveis com uma nova instância que armazena o novo estado; a seguir mostramos um exemplo de aplicação desta técnica.

### Campos `final`

- A palavra chave `final` suporta a construção de objectos imutáveis. Os campos `final` não podem ser modificados (embora os objectos que eles referem possam ser modificados se não forem imutáveis), mas também têm uma semântica especial no _Java Memory Model_. É a utilização de campos `final` que torna possível a garantia de **_initialization safety_** que permite que os objectos imutáveis possam ser livremente acedidos e partilhados por múltiplas _threads_ sem necessidade de sincronização.

**Assim como é uma boa prática declarar todos os campos como `private` a menos que seja necessário um grau de visibilidade superior, é também boa prática tornar todos os campos `final`, a menos que os mesmo necessitem ser mutáveis**. 


### Usando _volatile_ para publicar objectos imutáveis

- Uma _servlet_ de factorização realiza duas operações que deve ser atómicas: actualizar o resultado _cached_ e condicionalmente obter os factores presentes na _cache_ se o número corresponder ao que é solicitado. Quando um grupo de itens de dados deve ser actualizado atomicamente, considere criar uma classe _holder_ imutável para armazenar esses dado, como a classe `OneValueCache`, mostrada a seguir:

```Java
class OneValueCache {
	private final BigInteger lastNumber;
	private final BigInteger[] lastFactors;
	
	public OneValueCache(BigInteger number, BigInteger[] factors) {
		lastNumber = number;
		lastFactors = Array.copyOf(factors, factors.length);
	}
	
	public BigInteger[] getFactors(BigInteger number) {
		if (lastNumber == null || !lastNumber.equals(number))
			return null;
		else
			return Arry.copyOf(lastFactors, lastFactors.length);
	}
}
```

- As _race conditions_ inerentes ao acesso e à actualização de múltiplas variáveis relacionadas entre si pode ser eliminadas usando um objecto _holder_ imutável para armazenar todas as variáveis. Com um objecto _holder_ mutável, seria necessário usar _locking_ para garantir a atomicidade na actualização das múltiplas compoenentes do estado; com um objecto imutável, uma vez que uma _thread_ adquira uma referência para ele, não terá que se preocupar com a possibilidade de outra _thread_ modificar o estado do objecto. Se as variáveis necessitam de ser actualizadas, então será criado um novo objecto _holder_, mas quaisquer _threads_ que ainda estejam a trabalhar com um objecto _holder_ anterior ainda vêm nele um estado consistente.

- A classe `VolatileCachedFactorizer` usa a classe `OneCaheValue` para armazenar o número em _cache_ e os respectivos factores. Quandp uma _thread_ afecta o campo _volatile_ `cache` os novos dados em _cache_ ficam immediatamente visíveis às outras _threads_.

- As operações relacionadas com a _cache_ não podem interferir umas com as outras porque `OneValueCache` é imutável e o campo `cache` é apenas acedido uma vez em cada um dos percursos de código relevantes. Esta combinação de um objecto _holder_ imutável para armazenar múltiplas variáveis de estado relacionadas relacionadas por um invariante, e uma referência _volatile_ usada para garantir visbilidade imediata, permite que `VolatileCacheFactorizer` seja _thread-safe_ mesmo sem utilizar _locking_ explicito.

```Java
public class VolatileCacheFactorizer implements Servlet {
	private volatile OneValueCache cache = new OneValueCache(null, null);
	
	public void service(ServletRequest req, ServletResponse resp) {
		BigInteger number = extractFromRequest(req);
		BigInteger[] factors = cache.getFactors(number);
		if (factors == null) {
			factors = factor(number);
			cache = new  OneValueCache(number, factors);
		}
		encondeIntoResponse(resp, factors);
	}
}

````

## Publicação Segura

- Até aqui centrámos a nossa atenção em garantir que um objecto **não era publicado**, nas situações em que é suposto estar confinado a uma _thread_ ou dentro de outro objecto. Naturalmente, por vezes, **pretendemos partilhar objectos entre _threads_*** e, neste caso, devemos faze-lo de forma segura. Infelizmente, o simples armazenamento da referência para um objecto num campo público, como se mostra no programa, não é suficiente para publicar o objecto de forma segura.

```Java
// Unsafe publication
public Holder holder;

public void initialize() {
	holder = new Holder(42);
}
```

- Pode ser surpreendente quanto este exemplo de aparência inofensiva pode falhar. Devido a problemas de visibilidade, o `Holder` pode parecer a outra _thread_ como estando num estado inconsistente, mesmo que o seus invariantes sejam correctamente estabelecidos no construtor! Esta publicação imprópria pode permitor que outra _thread_ observe um **objecto parcialmente construído**.

### Publicação Imprópria: quando objectos bons vão mal

- Não se pode confiar na integridade de objectos parcialmente construídos. Uma _thread_ observadora pode ver o objecto num estado inconsistente, e mais tarde ver o seu estado alterar-se subitamente, mesmo que o estado do objecto não tenha sido alterado desde a publicação. Considere a seguinte definição da classe `Holder`:

```Java
public class Holder {
	private int n;
	
	public Holder(int n) { this.n = n; }
	
	public void assertSanity() {
		if (n != n)
			throw new AssertionError("This statement is false")
	}
}
```

- Se uma instância de `Holder` for publicado usando publicação insegura - da forma referida anteriormente - e uma _thread_ diferente da que fez a publicação invovar o método `assertSanity`, esse método **pode** lançar a excepção `AssertionError`. O problema aqui não é a classe `Holder`, mas sim o facto de `Holder` não ter sido adequadamente publicado. Contudo a classe `Holder` pode ser tornada imune à publicação imprópria se declararmos o campo `n` como `final` o que torna `Holder` imutável.

- Por não ter sido usada sincronização para tornar o `Holder` visível às outras _threads_, dizemos que o `Holder` **não foi adequadamente publicado**. Duas coisas podem correr mal com os objectos inadequadamente publicados. Outras _threads_ podem ver um valor obsoleto no campo ´holder´, e assim ver uma referência ´null´ ou outro valor antigo mesmo que já tinha sido posto um valor no campo `holder`. Mas bem pior, outras _threads_ podem ver um valor actualizado referência `holder`, mas valores obsoletos para o estado de `Holder`.(Embora possa parecer que os valores dos campos afectados no construtor são os primeiros valores a ser escritos e, por isso, não há valores **mais velhos** para ver valores obsoletos, o construtor de `Object` escreve primeiro os valores por omissão em todos os campos antes dos construtores das subclasses serem executados. É, por isso, possível ver os valores por omissão para um campo como um valor obsoleto.)

- **Para tornar as coisas mesmo mais imprevisíveis, uma _thread_ pode ver um valor obsoleto na primeira vez que lê um campo e um valor mais actualizado da próxima vez que ler o campo, sendo essa a razão pela qual `assertSanity` pode lançar `AssertionError`**.

### Objectos Imutáveis e _Initialization Safety_

- Porque os objectos imutáveis são muito importantes, o _Java Memory Model_ oferece uma garantia especial de **_initialization safety_** para partilhar objectos imutáveis. Como vimos atrás, o facto da referência para um objecto ficar visível a outra _thread_ não significa necessariamente que o estado do objecto fique visível à _thread_ consumidora. <ins>>Para garantir uma visão consistente do estado do objecto é necessário usar sincronização</ins>.

- Por outros lado, os objectos imutáveis, podem ser acedidos de forma segura **mesmo quando não se usa sincronização para apublicar a referência para o objecto**. Para que se verifique esta garantia de _initialization safety_, devem ser cumpridos todos os requisitos da imutabilidade: estado não modificável, todos os campos são `final` e construção adequada. (Se `Holder`, mostrado anteriormente, fosse imutável, `assertSanity` nunca poderia lançar `AssertionError` mesmo que a referência não fosse adequadamente publicada.)

**Os objectos imutáveis pode ser usados com segurança por qualquer _thread_ sem necessidade de sincronização, mesmo quando não é usada sincronização para os publicar**.

- Esta garantia estende-se aos valores dos campos `final` dos objectos adequadamente construídos; os campos `final` pode ser acedidos em segurança sem sincroniação adicional. Contudo, se os campos `final` referem objectos mutáveis, é ainda necessária sincronização para aceder ao estado dos objectos que são referidos pelos campos `final`.


## Idiomas de Publicação Segura

- Os objectos que não sejam imutáveis devem **publicados em segurança**, o que usualmente implica sincronização em ambas _threads_, a que publica e a que consome. Por enquanto, vamos concentrar a nossa atenção em garantir que a _thread_ consumidora consiga ver o objecto no estado em que foi publicado; em breve, lidaremos com a visibilidade das modificações feitas após a publicação.

- Para publicar um objecto em segurança, ambos a referência para o objecto e o estado do objecto devem estar visíveis às outras _threads_ ao mesmo tempo. Um objecto adequadamente construído pode ser publicado em segurança por:

	- Iniciando a referência para o objecto num iniciador estático;
	
	- Armazenado a referência para o objecto num campo _volatile_ ou numa `AtomicReference`;
	
	- Armazenando a referência num campo `final` de um objecto adequadamente construído; ou
	
	- Armazenado a referência num campo que esteja adequadamente protegido por um _lock_.
	
- A sincronização interna nas colecções _thread-safe_ significa que colocar um objecto numa colecção _thread-safe_, como é o caso de `Vector` or `synchronizedList`, cumpre o último destes requisitos. Se a _thread_ A coloca o objecto X numa colecção _thread-safe_ e se a _thread_ B subsequentemente o recupera, a garantido a B ver o estado de X como A deixou, mesmo que o código aplicacional não use sincronização **explícita**. As colecções _thread-safe_ da biblioteca do _Java_ oferecem as seguintes garantias de publicação segura, mesmo que o Javadoc seja menos claro sobre este assunto:

	- Colocar uma chave ou valor numa `HashTable`, `synchronizedMap` ou `ConcurrentMap` publica-os em segurança para qualquer _thread_ que os recupere do ´Map´ (quer seja directamente quer seja usando um iterador);
	
	- Colocar um elemento num ´Vector´, ´CopyOnWriteArrayList´, `CopyOnWriteArraySet`, `synchronizedList` ou `synchronizedSet` publica em segurança para qualquer _thread_ que recupere o elemento da colecção;
	
	- Colocar um elemento numa `BlockingQueue` ou `ConcurrentLinkedQueue` publica-a em segurança para qualquer _thread_ que recupere o elemento da fila.

- Outros mecanismos de transferência de dados da biblioteca de classes (como por exemplo, ´Future´ e `Exchanger`) também constituem publicação segura.

- Utilizando um _static initializer_ é frequentemente a forma mais simples de publicar objectos pode ser construídos estaticamente.

```Java
public static Holder holder = new Holder(42);
```

- Os _static initializers_ são executados pela JVM no momento da inicialização das classes; devido à sincronização interna da JVM, este mecanismo garante a publicação em segurança de quaisquer objectos inicializados desta forma.


### Objectos Efectivamente Imutáveis

- **A publicação segura é suficiente para que outras _threads_ acedam sem necessidade de sincronização adicional aos objectos que não são modificados após publicação**. Todos os mecanismos de publicação segura garantem que o estado publicado de um objecto é visível a todas as _threads_ que lhe acedem assim que a referência para o objecto seja visível; se o estado do objecto não for alterado novamente, isso será suficiente para garantir que qualquer acesso é seguro.

- Os objectos que não são tecnicamente imutáveis, mas cujo estado não é mofificado após construção, são designados por **efectivamente imutáveis**. Este objectos: (a) não necessitam de cumprir a definição estrita de imutabilidade que fizemos atrás; (b) precisam apenas de ser tratados pelo programa como se fossem imutáveis após serem publicados. **A utilização de objectos efectivamente imutáveis pode melhorar a performance através da redução da necessidade de sincronização**.

**Objectos <ins>efectivamente imutáveis</ins> publicados em segurança podem ser usados seguramente por qualquer _thread_ sem sincronização adicional**.

- Por exemplo, `Date` é mutável, mas se a utilizar como se fosse imutável, pode eliminar o _locking_ que de outra forma seria necessário quando `Date` fosse partilhada entre _threads_. Suponha que pretende manter um `Map` armazenando a hora do último _login_ para cada utilizador:

```Java
public Map<String, Date> lastLogin =
	Collections.synchronizedMap(new HashMap<String, Date>());

```

- Se os valores de `Date` não forem modificados após serem colocados no `Map`, então a sincronizaçãpo subjacente à implementação do `synchronizedMap` é suficiente para publicar os valores `Date` correctamente e, por isso, não é necessário sincronização adicional quando se acede a esses valores.

### Objectos Mutáveis

- Se o estado de um objecto pode ser modificado após a sua construção, a publicação segura apenas garante a visibilidade do estado existente no momento da publicação. Deve ser usada sincronização não apenas para publicar o objecto mutável, mas também de cada vez que o objecto é acedido para garantir a visiblidade das modificações subsequentes. Para partilhar objectos mutáveis em segurança, eles devem ser publicados em segurança **e** ser ou _thread-safe_ ou protegidos por um _lock_.

- Os requisitos de publicação de um objecto depende da sua mutabilidade:

	- Objectos **imutáveis** podem ser publicados usando qualquer dos mecanismos disponíveis;
	
	- Objectos **efectivamente imutáveis** devem ser publicados em segurança;
	
	- Objectos **mutáveis* devem ser publicados em segurança e devem ser ou _thread-safe_ ou protegidos por um _lock_.

	
### Partilhando Objectos em Segurança

- Sempre que adquire uma referência para um objecto, deve saber o que lhe é permitido fazer com ele. È necessário adquirir um _lock_ antes de utilizar o objecto? É permitido modificar o estado do objecto, ou pode simplesmente lê-lo? Muitos erros de concorrência decorrem do facto de entender estas "regras de envolvimento" para um objecto partilhado.

- As políticas mais úteis para usar e partilhar objectos num programa concorrente são:

	- **_Thread-confined_**. Um objecto confinado a uma _thread_ é detido exclusivamente por e confinado a uma _thread_, e só pode ser modificado pela _thread_ que detém a sua posse.
	
	- **_Shared read-only_**. Um objecto partilhado _read-only_ pode ser acedido concorrentemente por múltiplas _threads_ sem sincronização adicional, mas não pode ser modificado por qualquer _thread_. Os objectos partilhados _read-only_ incluem os **objectos imutáveis** e os **objectos efectivamente imutáveis**.
	
	- **_Shared thread-safe_**. Um objecto _thread-safe_ realiza a sincronização internamente, portanto múltiplas _threads_ pode aceder-lhe através da respectiva interface pública sem sincronização adicional (é o caso dos sincronizadores que fizemos na Série de Exercícios 1).
	
	- **_Guarded_**. Um objecto protegido pode apenas ser acedido na posse do _lock_ que o protege. Os objectos protegidos incluem aqueles objectos que são encapsulados dentro de outros objectos _thread-safe_ e os objectos publicados que são supostos ser protegidos por um _lock_ específico. 

___


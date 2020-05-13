
# Aula 16 - Sincronização _NonBlocking_ (I)

____

## Variáveis Atómicas e Sincronização _Nonblocking_

- Muitas das classes do _package_ `java.util.concurrent`, como é o caso de `Semaphore` e `ConcurrentLinkedQueue`, oferecem melhor performance e escalabilidade do que as alternativas que usam `synchronized`. Neste tópico, vamos abordar a principal razão deste ganho de performance e escalabilidade : **as variáveis atómicas e sincronização _nonblocking_**.


- Muito do trabalho de investigação recente sobre algoritmos concorrentes têm vindo a ser focada em algoritmos _nonblocking_, que utilizam instruções máquina atómicas de baixo nível, como _compare-and-swap_ (CAS), em vez de _locks_ para garantir a integridade dos dados partilhados mutáveis face a acessos concorrentes. Os algoritmos _nonblocking_ são usados extensivamente em sistemas operativos e em JVMs no escalonamento de _threads_ e processos, _garbage collection_ assim como para implementar _locks_ e outros mecanismos de suporte à concorrência.

- Os algoritmos _nonblocking_ são consideravelmente mais complexos de conceber do que as alternativas baseadas em _lock_, mas podem oferecer vantagens significativas em termos de escalabilidade, performance e _liveness_. Estes algoritmos fazem a coordenação a um nível de granularidade mais fino e podem reduzir o _overhead_ associado ao _scheduling_ porque as _threads_ não se bloqueiam quando disputam o acesso aos mesmos dados. Além disso, estes algoritmos são imunes a problemas de _deadlock_ e de _liveness_. Em algoritmos baseados em _locks_, outras _threads_ verão a sua progressão comprometida se a _thread_ que detém a posse do _lock_ partilhado se bloquear, enquanto que os algoritmos _nonblockig_ são imunes à falha de _threads_ individuais. A partir do _Java_ 5.0 é possível implementar algoritmos _nonblocking_ eficientes usando as classes atómicas como, por exemplo, `AtomicInteger` e `AtomicReference`.

- As variáveis atómicas podem também ser usadas como "_better volatile variable_" mesmo que não esteja a desenvolver algoritmos _nonblocking_. As variáveis atómicas oferecem a mesma semântica de memória que as variáveis _volatile_, mas oferecem suporte adicional para actualizações com base em intruções atómicas do tipo _read-modify-write_ - tornado-as ideias para basear contadores, geradores de sequências ou coleta de estatisticas.

### Desvantagens do _Locking_

- A coordenação do acesso ao estado partilhado mutável, composto por um conjunto de variáveis, usando um protocolo de _locking_ consistente garante que qualquer _thread_ que detenha a posse do _lock_ que protege o estado partilhado mutável detém o acesso exclusivo a esse estado e que, quaisquer alterações feitas ao estado partilhado mutável serão visíveis a outras _threads_ que subsequentemente adquiram a posse do respectivo _lock_.

- As JVMs modernas optimizam a aquisição e libertação de _locks_ na ausência de contenção - na acquisição quando o _lock_ está livre ou na libertação quando não é necessário acordar _threads_ bloqueadas porque o _lock_ estr ocupadp. (No melhor caso, o custo da acquisição e da libertação de um _lock_ é da ordem de grandeza do custo de uma instrução atómica.) Contudo, se multiplas _threads_ tentam adquirir o _lock_ ao mesmo tempo e JVM tem que solicitar a ajuda do sistema operativo. Se chegar a este ponto, alguma _thread_ desafortunada terá que ser bloqueada e, mais tarde, ter a sua execução reatada. (Uma JVM não necessita necessariamemte de bloquear uma _thread_ se ela pretende adquirir um _lock_ na posse de outra _thread_; ela pode usar informação de _profiling_ para decidir adaptativamente entre bloqueio e _spining_ com base na informação sobre o tempo que o _lock_ esteve na posse de _threads_ em aquisições anteriores.) Quando é reatada a execução de uma _thread_ que se bloqueou, terá que esperar que o _scheduler_ lhe atribua um processador, o que depende do comportamento de outras _threads_ a executar no sistema. O bloqueio e reatamento de uma _thread_ tem um _overhead_ significativo e geralmente implica uma interrupção na sua execução de duração significativa. Para as classes desenhadas com base em _locking_ com operações simples (como acontece com as colecções sincronizadas, onde a maioria do métodos contém poucas instruções), a razão entre o _scheduling overhead_ e o trabalho útil pode ser bastante alta **quando o _lock_ é frequentemente disputado**.

- As variáveis _volatile_ são um mecanismo de sincronização com menor peso que o _locking_ porque não envolvem operaçoes de _context switch_ e de _thread scheduling_. Contudo, as variáveis _volatile_ têm limitações quando comparadas com o _locking_: ainda que providenciem as mesmas garantias de visibilidade, as variáveis _volatile_ não podem ser usadas para construir acções atómicas compostas (e.g., operações _check_then-act atómicas ou alterações envolvendo mais do que uma variável). Isto significa que as variáveis _volatile_ não podem ser utilizadas quando uma variável depende de outra  ou quando o novo valor da variável depende do seu valor anterior, como acontece nas operações _read-modify-write_. Isto limita a utilização das variáveis _volatile_, uma vez que não podem ser usadas para implementar ferramentas comuns como contadores ou _locks_. (É teoricamente possível, embora totalmente impraticável, usar a semântica _volatile_ para construir _locks_ e outros sincronizadores.)

- Por exemplo, enquanto a operação incremento (`++i`) pode parecer uma operação atómica, trata-se realmente de três operações distintas - ler o valor corrente da variável, somar um a esse valor, e depois escrever o valor obtido na variável. Para não se perder nenhuma actualização, toda a operação _read-modify-write_ deve ser atómica. Até agora, a única forma que vimos para fazer isto é utilizando _locking_, como se mostra na classe `Counter`:

```Java
public final class Counter {
	private long value = 0;
	
	public synchronized long get() { return value; }
	
	public synchronized long increment() {
		if (value == Long.MAX_VALUE)
			throw new IllegalStateException("counter overflow");
		return ++value;
	}
}
```

- A classe `Counter` é _thread-safe_, e quando utilizada em situações de pouca ou nenhuma contenção, apresenta boa performance. Mas sob contenção forte ou moderada, o desempenho será seriamente afectado devido ao _overhead_ dos _context switches_ e dos atrasos na execução associados à actividade de _scheduling_. Quando a posse de _locks_ é mantida por um curto intervalo de tempo, o bloqueio é uma penalização severa só por se tenta adquirir o _lock_ na "hora errada".

- O _locking_ tem ainda outras desvantagens. Quando uma _thread_ espera pela aquisição de um _lock_, ela não pode fazer absolutamente mais nada. Se a execução da _thread_ que detém a posse for atrasada - devido a um _page-fault_, processamento de uma interrupção, ter sido preterida pelo _scheduler_, etc. - então nenhuma _thread_ que necessitar de adquirir o _lock_ poderá progredir. Isto pode ser um problema sério se a _thread_ bloqueada for uma _thread_ de alta prioridade e a _thread_ que detém a posse do _lock_ for uma _thread_ de baixa prioridade - um fenómeno conhecido por **inversão de prioridades**. Mesmo tendo a _thread_ de alta prioridade precedência sobre as _threads_ de menor prioridade, ela terá que aguardar até que o _lock_ seja libertado para progredir e isso efectivamente diminui a sua prioridade (que por desenho é alta) para a prioridade (que por desenho é baixa) da _thread_ que detém a posse do _lock_. Se a _thread_ que detém a posse de um _lock_ fica bloqueada em permanência (devido a um ciclo infinito, _deadlock_, _livelock_, ou outra falha de _liveness_), quaisquer _threads_ que aguardam a aquisição do _lock_ nunca poderão progredir.

- Existem essencialmente duas técnicas para evitar o fenómeno de **inversão de prioridades**: **_priority ceiling_** e **_priority inheritance_**. Na técnica _priority ceiling_, é atribuída a cada _lock_ uma prioridade que é igual à prioridade da _thread_ mais prioritária que usa o _lock_ e cada _thread_ que adquirir a posse do _lock_ executa com essa prioridade enquanto detém a posse do _lock_. Na técnica _priority inheritance_, a _thread_ que detém a posse do _lock_ executa sempre com uma prioridade igual a prioridade mais alta de entre as prioridades das _threads_ que se encontram bloqueadas para adquirir o _lock_. (A título de curiosidade, regista-se que o _Linux_ suporta _priority inheritance_ no `FUTEX_LOCK_PI` disponível no _pthreads_ (modo utilizador) e o _Windows_ não tem nenhum tipo de _lock_ que suporte qualquer das duas técnicas para evitar inversão de prioridades.)

- Mesmo ignorando estes riscos, o _locking_ é simplesmente um mecanismo pesado para operações de grão fina como incrementar um contador. Seria bom dispor de uma mecanismo mais refinado para gerir a contenção entre as _threads_ - algo semelhante às variáveis _volatile_, mas com suporte para operações _read-modify-write_ atómicas. Felizmente, todos os processadores modernos oferecem precisamente esse mecanismo.
 
### Suporte _Hardware_ para a Concorrência

- O _locking_ exclusivo é uma técnica **pessimista** - assume que o pior (se não fechar a sua porta, espíritos malignos pode entrar em sua casa e desarrumar as suas coisas) e não progride até ter a garantia, através das aquisição dos _locks_ adequados, que as outras _threads_ não irão interferir.

- Para realizar operações de grão fino, existe uma solução alternativa que é frequentemente mais eficiente - a abordagem **optimista**, onde as _threads_ progridem com as actualizações, esperando que as mesmas possam ser concluídas sem a interferência de outras _threads_. Esta abordagem depende da capacidade de **detectar colisões** para determinar se houve interferência de outras _threads_ durante uma actualização, situação em que a operação falha e pode ser retentada (ou não). A abordagem optimista segue o velho ditado: <ins>"é mais fácil obter perdão do que autorização"</ins>, onde "mais fácil" aqui significa "mais eficiente".

- Os processadores desenhados para operar em sistemas multiprocessador providenciam instruções especiais para gerir o acesso concorrente a variáveis partilhadas. Os primeiros processadores tinham instruções atómicas do tipo _test-and-set_, _fetch-and-increment_ ou _swap_ (ou _exchange_) que eram suficientes para implementar _spin locks_, que por sua vez eram usados para implementar mecanismos mais sofisticados de suporte à concorrência. Actualmente, todos os processadores modernos têm alguma forma de suporte à **instrução atómica _read-modify-write_ condicional**, como _compare-and-swap_ (CAS) ou _load-linked/store-conditional_ (LL/SC). Os sistemas operativos e as JVMs usam essas intruções para implementar _locks_ e outras estruturas de dados concorrentes, mas até ao _Java_ 5.0 estas instruções ainda não estavam disponíveis para utilizar em classes _Java_ normais.

#### _Compare and Swap_

- A abordagem tomada pela maioria das arquitecturas de processdor, incluindo IA-32, x86-64 e _Sparc_ é suportar a instrução _compare-and-swap_ (CAS) ou _compare-and_exchange_ (CMPXCHG). (Outros processadores, como o _PowerPC_, implementam a mesma funcionalidade com uma par de instruções: _load linked_ e _store-conditional_). A instrução CAS tem três operandos: a localização na memória **V** onde a instrução vai operar, o valor anterior esperado **A** e o novo valor **B**. A instrução CAS actualiza **V** atomicamente para o novo valor **B**, mas apenas se o valor correntemente em **V** corresponde ao valor esperado **A**. Em qualquer dos casos, devolve o valor corrente de **V** (antes da eventual actualização). (A variante designada por _compare-and-set_ retorna a indicação de que a operação teve sucesso, o que acontece quando o valor corrente correspondia ao valor experado.) CAS significa "Eu penso que **V** deve ter o valor **A**; se for verdade, afecte **V** com **B**; no caso contrário, não faça alterações e diga-me que estou errado". CAS é uma técnica optimista - a actualização prossegue na esperança de ter sucesso, sendo detectada falha se outra _thread_ actualizou a variável partilhada desde a última observação. A classe `SimulatedCAS`, mostrada a seguir, ilusta a semântica (mas não a implementação e o desempenho) do CAS.

- Quando múltiplas _threads_ tentam actualizar a mesma variável simultaneamente usando CAS, uma ganha e actualiza o valor da variável, e o resto das _threads_ perde. Mas as _threads_ perdedoras não são punidas com o bloqueio, como aconteceria se falhassem a acquisição de um _lock_; em alternativa, elas são informadas de que não ganharam a corrida desta vez mas podem tentar de novo.


```Java
public class SimulatedCAS {
	private int value;
	
	public synchronized int get() { return value; }
	
	public synchronized void set(int value) { this.value = value; }
	
	public synchronized int compareAndSwap(int expectedValue, int newValue) {
		int oldValue = value;
		if (oldValue == expectedValue)
			value = newValue;
		return oldValue;
	}
	
	public boolean compareAndSet(int expectedValue, int newValue) {
		return compareAndSet(expectedValue, newValue) == expectedValue;
	}
}

```

- Como uma _thread_ que falha um CAS não é bloqueada, ela pode decidir se pretende tentar de novo, empreender outra qualquer acção de recuperação, ou simplesmente não fazer nada. (Não fazer nada pode ser uma resposta razoável à falha de um CAS; em alguns algoritmos _nonblocking_, como a _Michael Scott queue_ - apresentada adiante - um CAS falhado significa que alguém já fez o trabalho que estávamos a planear fazer.) Esta flexibilidade elimina muitos dos riscos de _liveness_ associado com o _locking_ (embora em casos incomuns pode introduzir o risco de _livelock_).

- O padrão típico para utilizar o CAS é ler primeiro o valor **A** de **V**, derivar o novo valor **B** a partir de **A** e depois usar CAS para alterar **V** de **A** para **B**, desde que nenhuma outra _thread_ tenha entretanto alterado **V** para outro valor. O CAS soluciona o problema de implementar sequências _read-modify-write_ sem usar _locking_, porque o CAS pode detectar a interferência de outras _threads_.

### Um Contador _Nonblocking_

- A class `CASCounter`, mostrada a seguir, implementa um contador _thread-safe_ usando CAS. A operação _increment_ segue a forma canónica - obtém o valor antigo, transforma-o para o novo valor (somando um), e usa CAS para afectar o contador com o novo valor. Se o CAS falha a operação é retentada imediatamente. Voltar a tentar repetidamente é usualmente uma estratégia razoável, embora em casos de extrema contenção poderá ser desejável esperar ou recuar, por algum tempo, antes de voltar a tentar para evitar _livelock_.


```Java
public class CASCounter {
	private SimulatedCAS value;
	
	public int getValue() { return value.get(); }

	// increment and get : an unconditional operation
	public int increment() {
		int observedValue, updatedValue;
		do {
			observedValue = value.get();
			updatedValue = observedValue + 1;
		} while (!value.compareAndSet(observedValue, updatedValue))
		return updatedValue;
	}
	
	// a conditional operation
	public boolean decrementIfGraterThanZero() {
		int observedValue, updatedValue;
		do {
			observedValue = value.get();
			if (observedValue == 0)
				return false;	// decrement is not possible
			updatedValue = observedValue - 1;
		} while (!value.compareAndSet(observedValue, updatedValue));
		return true;
	}
}

```

- `CASCounter` não bloqueia a _thread_ invocante, embora possa ter que tentar uma ou mais vezes se houver outras _threads_ a actualizar o contador ao mesmo tempo. (Teoricamente, poderá ser necessário repetir arbitrariamente muitas vezes se outras _threads_ forem ganhando a corrida pelo CAS; na prática, este tipo de _startvation_ raramente acontece.) (Na prática, se tudo o que precisa é um contador ou um gerador de sequência, deve usar `AtomicInteger` ou `AtomicLong`, que providenciam métodos atómicos para incrementer e decrementar.)

- À primeira vista, o contador baseado em CAS parecer ter pior desempenho do que o contador baseado em _lock_; tem mais operações e um fluxo de control mais complicado e depende da aparentemente complicada operação CAS. Contudo na realidade, os contadores baseados em CAS superam significamente em desempenho os contadores baseados em _lock_ se houver pouca contenção e, muitas vezes, mesmo que não haja contenção. O _fast path_ da acquisição do _lock_ sem contenção requer tipicamente pelo menos um CAS, mais algum processamento de gestão adicional, portanto é feito mais processamento no melhor caso de um contador baseado em _lock_ que no caso normal do contador baseado em CAS (a aquisição e libertação do _lock_ custa, no mínimo, duas instruções atómicas). Uma vez que o CAS tem sucesso a maioria das vezes (assumindo contenção baixa ou moderada), o _hardware_ irá predizer correctamente o _branch_ implícito no ciclo `while`, minimizando o _overhead_ associado a uma lógica de control mais complexa.

- A sintaxe da linguagem associada ao _locking_ pode ser compacta, mas o trabalho realizado pela JVM e pelo sistema operativo para gerir os _locks_ não é. O _locking_ implica percorrer um caminho de código relativamente complicado na JVM e pode envolver _locking_ ao nível do sistema operativo, bloqueio de _thread_ e _context switches_. No melhor caso, o _locking_ requer pelo menos um CAS, portanto a utilização de _locks_ tira o CAS da nossa vista mas não poupa qualquer custo de execução. Por outro lado, executar um CAS directamente no programa não envolve qualquer código da JVM, _system calls_ ou actividade de _scheduling_. O que parece ser um caminho de código mais longo ao nível aplicacional é de facto um caminho de código muito mais curto quando as actividades da JVM e do sistema operativo são tidas em consideração. A principal desvantagem do CAS é que força o chamador a lidar com a contenção (retentando, recuando ou desistindo) enquanto que os _locks_ lidam automaticamente com a contenção, bloqueando a _thead_ até que o _lock_ esteja disponível. (Na realidade, a maoir desvantagem do CAS é a dificuldade de construir correctamente os algoritmos circundantes.)

- O desempenho do CAS varia amplamente entre processadores. Num sistema com um único CPU, um CAS assume um custo da ordem de uma dezena de _clock cycles_ uma vez que não é necessária nenhuma sincronização entre processadores. Há quinze anos, o custo de um CAS sem contenção em sistemas com múltiplos CPUs variava entre dez e cerca de uma centena e meia de _clock cycles_. O desempenho do CAS é um alvo que se move rapidamente e varia não só entre arquitecturas mas mesmo entre as várias versões do mesmo processador. Forças competitivas provavelmente resultarão na melhoria contínua do desempenho do CAS.

- **Uma boa regra empírica é que o custo do _fast path_ para uma aquisição de _lock_ sem contenção e a libertação na maioria dos processadores é aproximadamente duas vezes o custo de um CAS.**

### Suporte da Instrução CAS na JVM

- Antes do _Java_ 5.0, não existia forma de utilizar a instrução _compare-and-swap_ sem escrever código nativo. No _Java_ 5.0, foi acrescentado suporte de baixo-nível para expor operações CAS em `int`, `long` e referências para objectos e a JVM compila essas operações para a forma mais eficiente de as executar no _hardware_ subjacente. Em plataformas que suportam a instrução atómica CAS, o _runtime_ faz _inline_ da instrução/instruções máquina apropriada(s); não pior caso, se não estiver disponível uma instrução do tipo CAS, a JVM usa um _spin_ _lock_ para garantir a atomicidade. Este suporte de baixo nével na JVM é usado pelas classes de variáveis atómicas (`AtomicXxx` do _package_ `java.util.concurrent.atomic`) para providenciar operações CAS eficientes em tipos numéricos e tipos referência; estas classes de variáveis atómicas são usadas, directa ou indirectamente, para implementar a maioria das classes pertecentes ao _package_ `java.util.concurrent`.

### Classes de Variáveis Atómicas

- As variáveis atómicas suportam operações de grão mais fino e são mais eficientes que os _locks_, pelo que são criticas para implementar código concorrente de alto desempenho em sistemas multiprocessador. As variáveis atómicas limitam o âmbito dda contenção a uma única variável; isto é a mais fina granularidade que podemos obter (supondo que o seu algoritmo possa ser implementado usando esta fina granularidade). O _fast path_ (sem contenção) para actualizar uma variável atómica não é mais lento do que o _fast path_ na aquisição de um _lock_, sendo usualmente mais rápido; o _slow path_ é definitivamente mais rápido que o _slow path_ para os _locks_ porque não envolve o bloqueio e _rescheduling_ de _threads_. Usando algoritmos baseados em variáveis atómicas em vez de _locks_, as _threads_ têm maior probabilidade de progredir sem demora e têm mais facilidade de recuperar se tiverem contenção.

- As classes de variáveis atómicas porvidenciam uma generalização das variáveis _volatile_ pode suportar operações _read-modify-write_ atómicas. `AtomicInteger` representa um valor do tipo `int`, e providencia métodos `get`e `set` com a mesma smêntica que as leituras e as escritas num `volatile int`. Este classe também disponibiliza um método atómico `compareAndSet` (o qual se tiver sucesso tem os mesmos efeitos sobre a memória de ambos leitura e escrita de uma variável _volatile_ - isto é, semântica de barreira _full-fence_), e por conveniência, métodos atómicos para somar, incrementar e decrementar. `AtomicInteger` possui uma semelhança superficial com uma classe `Counter` estendida, mas oferece maior escalabilidade sob contenção, porque pode explorar directamente o suporte à concorrência disponível no _hardware_ subjacente.

- Existem **doze classes de variáveis atómicas**, divididas em quatro grupos: escalares, _field updaters_, _arrays_ e variáveis compostas. As classes atómicas de utilização mais comum são as escalares: `AtomicInteger`, `AtomicLong`, `AtomicBoolean` e `AtomicReference`. Todas suportam CAS; as versões `Integer` e `Long` suportam também aritmética. (Para simular variáveis atómicas noutros tipos primitivos, pode fazer a coerção de valores do tipo `short` ou `byte` para e de `int` e usar `floatToIntBits` ou `doubleToLongBits` números em vírgula flutuante.)

- As classes _array_ atómicas (disponível nas versões `Integer`, `Long` e `Reference`) são _arrays_ cujos elementos podem ser actualizados atomicamente. Estas classes providenciam semântica _volatile_ no acesso aos elementos do _array_, uma _feature_ não disponível para os _arrays_ ordinários - um _array_ `volatile` tem semântica `volatile` apenas para a referência, não para os seus elementos. (Os outros tipos de classes atómicas são discutidos adiante.)

- Enquanto que as classes atómicas escalares estendem `Number`, ela não estendem as respectivas classes _wrapper_ dos tipos primitivos como `Integer` asn `Long`. De facto, elas não podem: as classes _wrapper_ dos tipos primitivos são imutáveis enquanto que as classes atómicas são mutáveis. As classes de variáveis atómicas não redefinem os métodos `hashCode` ou `equals`; cada instância é distinta. Tal como a maioria dos objectos mutáveis, não são bons cadidatos para usar como chaves em colecções baseadas em tabelas de _hash_.

### Variáveis Atómicas como "_Better Volatiles_"

- Atrás, usámos uma referência `volatile` para um objecto imutável para actualizar atomicamente múltiplas variáveis de estado. Esse exemplo baseava-se num _chech then act_, mas no caso particular o _race_ era inofensivo porque não era relevante se ocasionalmente se perdessem actualizações.

- Quando é necessário implementar sequências _check-then-act_ atómicas, podemos combinar a técnica usada em `OneValueCache` com referências atómicas para fechar a _race condition_ actualizando **atomicamente** a referência para um objecto imutável que contém os limites superior e inferior. `CasNumberRange` usa uma `AtomicReference` para um `IntPair` para armazenar o estado; utilizando `compareAndSet`pode actualizar-se os limites superior e inferior sem _race conditions_.  

```Java
public class CasNumberRange {

	private static class IntPair {
		final int lower;	// Invariant: lower <= upper
		final int upper;
		...
	}
	
	private final AtomicReference<IntPair> values =
		new AtomicReference<IntPair>(new IntPair(0, 0));
		
	public void setLower(int newLower) {
		while (true) {
			IntPair observedPair = values.get();
			if (newLower > observedPair.upper)
				throw new IllegalArgumentException(
					"Can't set lower to " + newLower + " > upper");
			IntPair newPair = new IntPair(newLower, obdervedPair.upper);
			if (values.compareAndSet(observedPair, newPair))
				break;
		}
	}
	
	// similarly to setUpper
}
```

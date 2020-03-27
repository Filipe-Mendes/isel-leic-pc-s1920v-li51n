## Monitor

- O conceito de monitor define um meta-sincronizador adequado à implementação de sincronizadores (ou schedulers de "recursos").

- Unifica todos os aspectos envolvidos na implementação de sincronizadores: osdados partilhados, o código que acede a esses dados, o acesso aos dados partilhados em exclusão mútua e a possibilidade de bloquear e desbloquear threads em coordenação com a exclusão mútua.

- Este mecanismo foi proposto inicialmemte como construção de uma linguagem dealto nível (Concurrent Pascal) semelhante à definição de classe nas linguagens orientadas por objectos.

- Foram considerados dois tipos de procedimentos/métodos: os procedimentos de entrada (públicos),que podem ser invocados de fora do monitor e os procedimentos internos (privados) que apenas podem ser invocados pelos procedimentos de entrada.

- O monitor garante, que num determinado momento, <ins>quanto muito uma *thread* está *dentro* do monitor<ins>. Quando uma *thread* está dentro do monitor é atrasada a execução de qualquer outra *thread* que invoque um dos seus procedimentos de entrada. 

- Para bloquear as *threads* dentro do monitor Brinch Hansen e Hoare  propuseram o conceito de variável condição (que, de facto, nem são variáveis nem condições, são antes filas de espera onde são blouqeadas as *threads*). A ideia básica é a seguinte: quando as *threads* não têm condições para realizar a operação *acquire* que pretendem bloqueiam-se nas variáveis condição; quando outras *threads* a executar dentro do monitor alteram o estado partilhado e sinalizam as *threads* bloqueadas nas variáveis condição quando isso for adequado.

#### Semântica de Sinalização de Brinch Hansen e Hoare

- **Esta semântica de sinalização não se encontra implememtada em nenhum dos *runtimes* actuais que suportam o conceito de monitor**

- Esta semântica da sinalização requer que uma *thread* bloqueadas numa variável condição do monitor execute imediatamente assim que outra *thread* sinaliza essa variável condição; a *thread* sinalizadora reentra no monitor assim que a *thread* sinalizad o abandone.

#### Semântica de Notificação de Lampson e Redell

- **Esta semântica de sinalização é a que é implememtada por todos os *runtimes* actuais que suportam o conceito de monitor**

- Foi implementada na linguagem Mesa que suportava concorrência.

- Considerando que a semântica de sinalização proposta por Brinch Hansen e Hoare era demasiado rígida (entre outros aspectos, não permitia a interrupção ou aborto das *threads* bloqueadas dentro dos monitores, propuseram uma alternativa à semântica da sinalização.

- Quando uma *thread* estabelece uma condição que é esperada por outra(s) *thread(s)*, eventualmente bloqueada, notifica a respectiva variável condição. Assim a operação *notify* é entendida como um aviso ou conselho à *thread* bloqueada e tem como consequência que esta reentre no monitor algures no futuro.

- O *lock* do monitor tem que ser readquirido quando uma *thread* bloqueada pela operação *wait* reentra no monitor. **Não existe garantia de que qualquer outra *thread* não entre no monitor antes de uma *thread* notificada reentrar (fenómeno que se designa por *barging*). Além disso, após uma *thread* notificada reentrar no monitor não existe nenhuma garantia que o estado do monitor seja aquele que existia no momento da notificação (garantia dada pela semântica de Brinch Hansen e Hoare). É, por isso, necessário que seja reavalido o predicado que determinou o bloqueio.

- Esta semântica tem como primeira vantagem não serem necessárias comutações adicionais no processo de notificação.

- A segunda vantagem é ser possível acrescentar mais três formas de acordar as *threads* bloqueadas nas variáveis condição: (a) por ter sido excedido o limite de tempo especificado para espera (*timeout*); (b) interrupção ou aborto da *thread* bloqueada; (c) fazer *broadcast* numa condição, isto é, notificar todas as *threads* nela bloqueadas.

### Monitores Implícitos em *Java*
 
 - São associados de forma *lazy* aos objectos, quando se invoca a respectiva funcionalidade.
 
 - Suportam apenas uma variável condição anónima.
 
 - O código dos "procedimentos de entrada" (secções críticas) é definido dentro de métodos ou blocos marcados com `synchronized`. A funcionalidade das variáveis condição está acessivel usando os seguintes métodos da classe `java.lang.Object`: `Object.wait`, `Object.notify` e `Object.notifyAll`.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* é reportada sempre a notificação e só depois, eventualmente, a interrupção.

### Monitores Explícitos em Java
 
 - São implementados pelas classes `java.util.concurrent.locks.ReentrantLock` e `java.util.concurrent.locks.ReentrantReadWriteLock` que implementam as interfaces `java.util.current.locks.Lock`e `java.util.current.locks.Condition`.
 
 - Suportam um número arbitrário de variáveis condição.
 
 - O código dos "procedimentos de entrada" tem que explicitar a aquisição e libertação do *lock* do monitor. Exemplo:
 ```
 monitor.lock();
 try {
   // critical section
 } finaly {
   lock.unlock();
 }
 ```

- As variáveis condição são acedidas através dos métodos definidos na interface `java.util.concurrent.locks.Condition`, nomeadamente: `Condition.await`, `Condition.awaitNanos`, `Condition.signal` e `Condition.signalAll`.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* é reportada sempre a notificação e só depois, eventualmente, a interrupção. 

 - São associados de forma *lazy* aos objectos, quando se invoca a respectiva funcionalidade.
 
 - Suportam uma variável condição anónima.
 
 - O código dos "procedimentos de entrada" (secções críticas) é definido dentro de métodos ou blocos marcados com `synchronized`. A funcionalidade das variáveis condição está acessivel usando os seguintes métodos da classe `java.lang.Object`: `Object.wait`, `Object.notify` e `Object.notifyAll`.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* é reportada sempre a notificação e só depois, eventualmente, a interrupção.

### Monitores Implícitos em .NET
 
 - São associados de forma *lazy* às instâncias dos tipos referência (objectos), quando se invoca a respectiva funcionalidade.
 
 - Suportam apenas uma variável condição anónima.
 
 - Estão acessíveis usando os métodos estáticos da classe `System.Threading.Monitor`, nomeadamente: `Monitor.Enter`, `Monitor.TryEnter`, `Monitor.Exit`, `Monitor.Wait`, `Monitor.Pulse` e `Monitor.PulseAll`. O código dos "procedimentos de entrada" (secções críticas) pode ser defindo com a construção `lock` do C# que é equivalente aos blocks `synchronized`no *Java*.
 
 - Quando a notificação de uma *thread* bloqueada ocorrer em simultâneo com a interrupção dessa *thread* pode ser reportada a interrupção e ocultada a notificação. **Assim, em situações em que se notifica apenas uma *thread* pode ser necessário capturar a excepção de interrupção para regenerar uma eventual notificação que possa ter ocorrido em simultâneo**.

### Extensão aos Monitores Implícitos em .NET

 - A classe MonitorEx, disponível em `src/utils/MonitorEx.cs` implementa uma extensão aos monitores implícitos do .NET que suportam monitores com múltiplas condições suportados nos monitores implícitos de múltiplos objectos. Um dos objectos representa o monitor e uma variável condição e os outros apenas representam variáveis condição. Os método `MonitorEx.Wait`, `MonitorEx.Pulse` e `MonitorEx.PulseAll` recebem como argumentos dois objectos: o objecto que representa o monitor e o objecto que representa a condição; quando se está a usar a condição do objecto que representa o monitor os dois objectos são iguais.
 
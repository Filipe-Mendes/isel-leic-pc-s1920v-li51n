# Aula 20 - _Thread Pools_

___

## Sumário

- Vantagens da gestão de _threads_ em _pool_; critérios a observar para manter um número óptimo de _worker threads_ durante a execução das aplicações;

- Características do _thread pool_ no .NET _Framework_; classe `System.Threading.ThreadPool`; critérios para injecção e retirada de _worker threads_; programa para monitorizar a injecção e retirada de _worker threads_;

- Características do _thread pool executor_ no _Java_; classe `java.util.concurrent.ThreadPoolExecutor`; critérios para injecção e retirada de _wroker threads_; programa para monitorizar a injecção e retirada de _threads_;


### _Thread Pools_

- A utilização de interfaces assíncronas baseadas em _callback_ remove completamente qualquer afinidade entre o código executado pelas aplicações e as _threads_ que o executa, a forma mais eficiente de gerir as _threads_ necessárias para executar as aplicações é centralizar a gestão _worker threads_ - criação de novas _worker threads_, _scheduling_ dos _work items_ para execução e a terminação de _worker threads_ consideradas em excesso - numa única entidade que se designa normalmente por **_thread pool_**.

- Concluímos na aula anterior, que o número óptimo de _worker threads_ em cada momento deve ser encontrado pela aplicação do seguinte princípio: **ter o menor número de _worker threads_ mas o número suficiente para utilizar todos os processadores do sistema na situação em que há processamento pendente para execução**.
 
- Quando mais centralizada for a gestão das _worker threads_, mais eficaz será uma gestão que consiga manter o número óptimo de _worker threads_ nas várias situações de carga e tendo em consideração a natureza do código (_cpu-bound_ ou _I/O-bound_) a executar.


### Características do _Thread Pool_ no .NET _Framework_

- Existe um único _thread pool_ para servir todos os _AppDomains_ a executar na mesma instância do CLR (_Common Language Runtime_), cuja funcionalidade base (o _thread pool_ também tem a funcionalidade de _Task Scheduler_) é acedida com métodos estáticos da classe `System.Threading.ThreadPool`, nomeadamente o método `ThreadPool.QueueUserWorkItem` que permite agendar _work items_ para execução.

- O _thread pool_ gere dois grupos de _worker threads_ designados por: _worker threads_ e _I/O completion port threads_. O primeiro grupo é responsável pela execução dos _work items_, tipicamente _cpu-bound_, agendados com o método `ThreadPool.QueueUserWorkItem`; como a designação do grupo sugere, o segundo grupo é usado no processamento das conclusões das operações de I/O assíncronas, sendo a sua gestão baseiada, no sistema operativo _Windows_, no mecanismo do _kernel_ designado por _I/O completion port_. (No _Java_, existe um suporte semelhante para operações de I/O assíncronas que não está associado aos _thread pools_, mas sim implementado no código das classes que implementa a interface `java.nio.channels.AsynchronousChannel`.) 

- O _thread pool_ admite a configuração do número mínimo e do número máximo de _worker threads_ para cada um dois grupos. O número mínimo de _worker threads_ - cujo valor por omissão é igual ao número de processadores - define o número de _thread_ que o _pool_ cria apenas em função do agendamento de _work items_; depois de atingido esse número, a injecção de novas _worker threads_ segue a política de injecção detalhada adiante.

- A funcionalidade básica do _thread pool_ é estendida com a implementação da técnica de _work-stealing_ que é usado pela _Task Parallel Libray_ como _Default Task SCheduler_. Para implementar _work-stealing_, para além da fila global onde são colocados os _work items_ agendados com `ThreadPool.QueueUserWorkItem` e os _work items_ agendados por _threads_ que não sejam _worker threads_, cada _worker thread_ tem uma fila privada onde são colocados os _work items_ por si agendados e de onde, preferencialmente, retira _work items_ para execução. Estas filas são acedidas diferentemente nas suas duas extremidades: (a) uma das extremidades é usada apenas pela respectiva _worker thread_ para inserir e remover _work items_, seguindo uma ordem LIFO e requer apenas uma sincronização simplificada, que é mais eficiente; (b) a outra extremidade, que pode ser acedida simultaneamente por várias _worker threads_ - quando as respectivas filas privadas e a fila global ficam vazias para "roubar" _work items_ nas filas privadas das outras _worker threads_- requer sincronização completa. Este desenho, para além de permitir outras optimizações, visa essencialmente dimunuir a contenção sobre a fila global de _work items_ em cenários onde o ritmo de agendamento de trabalho é muito elevado como acontece nos _frameworks_ baseados em _tasks_.

#### Injecção de _Worker Threads_

- No _thread pool_ do .NET _Framework_, existem dois mecanismos principais para injectar _worker threads_: um dos mecanismos visa **prevenir a _starvation_** e injecta _worker threads_ quando constata que não existe progresso na execução de _work items_ e uma **heurística _hill-climbing_** que procura **maximizar o _throughput_** (_work items_ executados por unidade de tempo com a necessária normalização) **enquanto utiliza o mínimo número de _worker threads_ possível**.

- O objectivo da prevenção da _starvation_ é **evitar a ocorrência de _deadlocks_**. Pode ocorrer _deadlock_ quando uma _worker thread_ se bloqueia num sincronizador que deva ser sinalizado por um _work item_ que está ainda pendente para execução do _thread pool_. Se existir um número fixo de _worker threads_, e todas essas _threads_ estiverem igualmente bloqueadas, o sistema deixará de ser capaz de progredir. **Nesta situação, acrescentar uma nova _worker thread_ resolve o problema**.

- O objectivo da heurística _hill-climbing_ é melhorar a utilização dos processadores quando as _worker threads_ se bloqueiam nas operações de I/O ou noutras operações de sincronização. Por omissão, o _thread pool_ cria uma _worker thread_ por processador. Se uma dessas _worker threads_ se bloquear, existe a hipótese de que um processador ficar subutilizado, dependendo da carga de trabalho geral do computador. A lógica de injecção de _worker threads_ não distingue entre uma _thread_ que esteja bloqueada - sem utilizar processador - e uma _thread_ que se encontre a executar uma operação demorada que faça uso intensivo do processador. Por isso, quando as filas do _thread pool_ contêm _work items_ pendentes, e os _work item_ activos que demorem muito tempo a executar (mais do que meio segundo) poderão ser injectadas novas _worker threads_ mesmo na ausência de uma situação de _deadlock_.

- O _thread pool_ do .NET tem a oportunidade de injectar _threads_ de cada  vez que um _work item_ completa ou a intervalos de 500 milésimos de segundo, o que for menor. O _pool_ usa esta oportunidade para tentar acrescentar _threads_ (ou retirá-las), guiado pelo _feedback_ das última alteração ao número de _workwer threads_. **Se o acrescentar _worker threads_ parecer melhorar o _throughput_, adicionará uma nova _thread_; caso contrário, reduzirá o número de _worker threads_**. Esta técnica é designada por uma heurística _hill-climbing_.

- Assim, uma razão para manter os _work items_ curtos é evitar a detecção de _starvation_, mas existe outra razão que é dar ao _thread pool_ mais oportunidades de melhorar o _throughput_ através do ajuste do número de _worker threads_. Quanto mais curtos for a duração dos _work items_ individuais, mais frequentemente o _thread pool_ poderá medir o _throughput_ e ajustar o número de _threads_ em função dessas medidas.


#### Retirada das _Worker Threads_

- As _worker threads_ activas terminam quando decorrer uma determinado intervalo de tempo (na implementação corrente são 20 segundos) sem que sejam utilizadas para executar nenhum _work item_. As _worker threads_ são mobilizadas com ordem LIFO, para aproveitar o aquecimento das _caches_ e também para facilitar a medida do tempo de inactividade.

### Programa para Monitorizar a Injecção e Retirada de _Worker Threads_

- [Aqui](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/thread-pool-monitor/ThreadPoolMonitor.cs) encontra-se um programa que permite monitorizar a injecção e retirada de _worker thread_ no _thread pool_ do .NET _Framework_ para dois tipos de _workload_: _cpu-bound_ e _i/o-bound_.

 


### Características dos _Thread Pools_ no _Java_

- O _Java_ não segue o mesmo princípio do .NET _Framework_ de centralizar a gestão num único _pool_ de _worker threads_. Existem duas classes que implementam _thread pools_ - `java.util.concurrent.ThreadPoolExecutor` e `java.util.concurrent.ForkJoinPool` - e podem ser criadas um número arbitrário de instâncias destas classes.

- A classe `ThreadPoolExecutor` implementa a funcionalidade básica de um _thread pool_ com um única fila para agendamento de _work items_. Pode ser configurado com os seguintes parâmetros: `corePoolSize`, `maximumPoolSize`, `keepAliveTime`, `workQueue`, `threadFactory` e `handler`. `corePoolSize` difine o número de _worker threads_ que o _pool_ matém sempre activas, e deve ser configurado com o número de processadores; `maximumPoolSize` define o número máximo de _worker threads_ que o _pool_ pode ter activas em simultâneo; `keepAliveTime` define o tempo máximo que uma _worker thread_ aguarda para que lhe seja atribuído trabalho, antes de terminar; `workQueue` permite definir uma implementação da interface `BlockingQueue<Runnable` que o _pool_ utiliza para colocar os _work items_ pedentes para execução (esta fila pode ser limitada ou ilimitada ou pode mesmo ser usada uma instância da classe `java.util.concurrent.SynchronousQueue<E>` que entrega directamente os _work items_ às _worker threads_); `threadFactory` deve ser uma implementação da interface `java.util.concurrent.ThreadFactory` e é usada pelo _pool_ para criar as _worker threads_; finalmente, `handle` deve ser uma implementação da interface `java.util.concurrent.RejectedExecutionHandle` que permite definir o _handler_ que é invocado quando é rejeitado o agendamento de um _work item_. 
	
- A classe `java.util.concurrente.Executors` define um conjunto de métodos de fabrico para criar instâncias da classe `ThreadPoolExecutor` com diversas configurações, nomeadamente, `newCachedThreadPool`, `newFixedThreadPool`, `newScheduledThreadPool`, `newSingleThreadExecutor`, etc.

- A classe `ForkJoinPool` suporta também a funcionalidade de _work-stealing_ e é usado pelo _fork/join framework_ e pela classe `java.util.concurrent.CompletableFuture`.

#### Injecção de `Worker Threads` no `ThreadPoolExecutor`

- No `ThreadPoolExecutor` a injecção de _worker threads_ é feita exclusivamente em função do agendamento de _work items_ para execução. Quando é solicitado o agendamento de um _work item_ para execução:
	
	1. Se exixtirem _worker threads_ _idle_, o _work item_ é entregue a uma dessas _threads_ para execução imediata;
	
	2. Se não existirem _worker threads_ _idle_ e o número de _worker threads_ activas ainda atingiu o valor especificado com `maximumPoolSize` é criada uma nova _worker thread_ a qual é entregue o _work item_ para execução imediata.
	
	3. Se o número de _worker threads_ activas já tiver atingido o valor máximo, o _work item_ é colocado na _work queue_ se esta ainda tiver capacidade; caso contrário, o _work item_ é rejeitado e o método que faz o agendamente lança `RejectedExecutionException.

#### Retirada das _Worker Threads_

- As _worker threads_ activas terminam quando decorrer o intervalo de tempo especificado com o parâmetro de construção `keepAliveTime` sem que sejam utilizadas para executar nenhum _work item_ e o número de _worker threads_ activas seja maior do que o valor especificado com o parâmetro de configuação `corePoolSize`. As _worker threads_ são mobilizadas com ordem LIFO, para aproveitar o aquecimento das _caches_ e também para facilitar a medida do tempo de inactividade.



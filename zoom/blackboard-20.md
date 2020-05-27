# Aula 20 - Introdução à Programação Assincrona (I)

___

## Sumário

- Invocação síncrona versus invocação assíncrona;
	
- _Asynchronous Programmig Model_ (APM), um dos modelos de invocação assíncrona definidos pelo .NET _Framework_;

- Paralelismo no processamento de pedidos numa aplicação servidora, usando exclusivamente invocação síncrona;
	
- Modelo de execução programas síncrono versus assíncrono, nomeadamente relativamente à afinidade de código a _threads_ específicas; 
	
- Principais custos associados à utilização de _threads_: custos fixos e proporcionais ao número total de _threads_ (memória não paginada utilizada nas estruturas de dados de suporte às _threads_ no _kernel_ do sistema operativo e pelo _stack_ de _kernel mode_ e espaço de endereçamento virtual para o _stack_ de _user mode_) e custos variáveis, devidos principalmente à actividade do _scheduler_ do sistema operativo, e que são proporcionais ao excesso de _threads_ no estado _ready_ relativamente ao número de processadores do sistema.

### Invocação Síncrona

- Considere a seguinte API síncrona

```C#

T Xxx(U u, ..., V v);

T t = Xxx(u, ..., v);

// use the result t
```

- Uma _thread_ que invoque uma API síncrona é retida nessa API até que esteja disponível o resultado da operação, independentemente do processamento associado à API seja ou não realizado pela _thread_ invocante.

- Por exemplo, no sistema operativo _Windows_ quando a API `ReadFile` é invocada sincronamente, a _thread_ invocante faz a preparação do processamento da leitura construindo _I/O Request Packet_ (IRP) que descreve o pedido de leitura; depois, entrega o IRP ao respectivo _device driver_, que em conjunto com o _hardware_ subjacente executa autonomamente a leitura no respectivo dispositivo periférico; enquanto o _device driver_ e o _hardware_ realizam a operação de leitura, a _thread_ invocante bloqueia-se num _manual-reset event_ até que a leitura seja concluída e os respectivos dados disponível, momento em o _devive driver_ sinaliza o _manual-reset event_ desbloqueando a _thread_, que então retorna da chamada à API `ReadFile`.

- No _Windows_ a interface com os _device drivers_ é assíncrona, mas são suportadas operações de I/O com invocação síncrona e com invocação assíncrona. Nas invocações síncronas, a operação de I/O é executada assincronamente, sendo usada sincronização para bloquear a _thread_ invocante até à respectiva conclusão, de modo a que a respectiva API possa retornar o resultado.  

- Tomando como referência em aplicação servidora, quando é usada exclusivamente a invocação síncrona de operações, a parelelização do processamento dos pedidos de vários clientes obriga à utilização de uma _thread_ independente por cada pedido que se processa em paralelo.

#### Conclusão

- A utilização de interfaces com invocação síncronas inviabiliza uma gestão optimizada das _threads_, que consomem recursos significativos no sistema (estes custos são caracterizados adiante).


### Invocação Assíncrona

- Consideremos agora a API assíncrona equivalente a `Xxx`, segundo o _Asynchronous Programming Mode_ (APM) do .NET _Framework_:

```C#

public interface IAsyncResult {
	bool IsCompleted;
	WaitHandle AsyncWaitHandler;
	Object AsyncState;
	bool CompletedSynchronously;
}

public delegate void AsyncCallback(IAsyncResult);

IAsyncResult BeginXxx(U u, ..., V v, AsyncCallback callback, object state);

T EndXxx(IAsyncResult asyncResult);

T t = EndXxx(BeginXxx(U u, ..., V v, ...));	 // equivalent to T Xxx(U u, ..., V v);

```

- Na invocação assíncrona, existem APIs diferentes para para lançar a operação assincrona (`BeginXxx`) e para obter o respectivo resultado (`EndXxx`).

- A API BeginXxx retorna assim que a operação assíncrona fica em execução autonomamente, e devolve um objecto (que implementa a interface IAsyncResult) que representa a operação assíncrona em curso, e que deverá ser usado na sincronização com a conclusão da operação assíncrona e para obter o respectivo resultado

- A API EndXxx que tem como argumento o objecto retornado por `BeginXxx` permite obter o resultado da operação assíncrona (resultado normal ou excepção). Pela sua semêntica, este método tem uma interface síncrona, isto é, bloqueia a _thread_ invocante se a operação assíncrona ainda não tiver terminado.   

- Para ser possível apenas invocar a API `EndXxx` com a garantia que a _thread_ invocante já não se blouqueia - porque o resultado já está disponível - e necessário a sincronização (_rendezvous_) com a conclusão da respectiva operação assíncrona, o que pode ser feito de duas formas:

	- Utilizando a funcionalidade da interface `IAsyncResult` implementada pelo objecto retornado pela API `BeginXxx` e usando técnicas de "polling": a _thread_ que se quer sincronizar com a conclusão da operação assíncrona **tem que tomar a iniciativa de**: (a) interrogar a propriedade `IAsyncResult.IsCompleted` para saber se a operação assíncrona já foi concluída; (b) bloquear-se no sincronizador devolvido pela propriedade `IAsyncResult.AsyncWaitHandle` especificando ou não um _timeout_, ou; (c) chamar directamente o método `EndXxx`;
	
	- Utilizando a técnica de _callback_/"_interrupt_" em que se especifica no método `BeginXxx` o método de _callback_ e, opcionalmente um objecto contendo informação de contexto, que será invocado pelo _runtime_ quando a operação assíncrona terminar. No método de _callback_ a chamada ao método `EndXxx`nunca bloqueia a _thread_ invocante. Neste caso não é necessário que nenhuma _thread_ empreenda qualquer actividade para saber que a operação terminou. A chamada ao método de _callback_ é realizada numa _worker thread_ gerida pela infraestruruta de suporte das operações assincronas.

- Por exemplo, no sistema operativo _Windows_ quando é invocada assíncronamente a API `ReadFile` especifica-se uma instância da estrutura `OVERLAPPED` que representa a operação assíncrona (funcionalidade equivalente à do objecto que implementa `IAsyncResult` no APM) -, é a _thread_ invocante faz a preparação do processamento da leitura, construindo um IRP para descrever completamente o pedido de leitura; depois, entrega o IRP ao respectivo _device driver_ que em conjunto com o respectivo _hardware_ executa autonomamente a leitura no respectivo dispositivo periférico subjacente; depois, a _thread_ invocante retorna da API `ReadFile` devolvendo a indicação de que a operação foi executada sincronamente (situação em que `ReadFile` devolve `TRUE`) ou se ficou pendente (situação em que `ReadFile` devolve `FALSE` e a API `GetLastError` devolve o código de erro `ERROR_IO_PENDING`, que não se trata própriamente de um código de error). No primeiro caso, os dados lidos do periférico estão disponíveis e podem ser processados de imediato. No segundo caso, é necessáro usar uma das formas disponíveis no _Windows_ para fazer o _rendezvous_ com a conclusão das operações de I/O assícronas - por exemplo usar a API `GetOverlappedResult` especificando a respectiva estrutura ` `OVERLAPPED` - para saber quando os resultados da leitura estão disponíveis. A API `GetOverlappedResult` implementa a funcionalidade que no modelo APM está disponível através da interface `IAsyncResult` e com o método `EndXxx`.

#### Conclusões

- Usando o _rendezvous_ com a conclusão das operações assíncronas - obtenção do resultado da operação assíncrona e respectivo processamento - pelo método _callback_ deixa de haver qualquer relação entre _threads_ e o código que cada uma delas executa; neste cenário, o código das aplicações é simplesmente executado por um conjunto de _worker threads_, que o _runtime_ pode gerir em _pool_.

- O modelo de execução assíncrono, que segue o padrão _Continutaion Passing Style_ (CPS), é baseado no lançamento de operações executadas assincronamente em conjunto com a especificação do código que processa o resultado das operações, designado frequentemente por continuações.

### Custos da Utilização de _Threads_

- Os principais custos associados à utilização de _threads_ são os seguintes:

	- Custo fixo proporcional ao número total de _threads_ em cada momento: memória não paginada para albergar uma ou mais estruturas de dados que representa a _thread_ no sistema operativo assim comoo _stack_ de modo _kernel_ (_Windows_ a 64-_bit_ são usadas 6 páginas de memória por _thread_- 24KB - em 64-_bit_); espaço de endereçamento virtual para o _stack_ de modo utlizador, que apenas sendo _commited_ há medida que as página virtuais vão sendo utilizadas, tem que ser reservado quando as _threads_ são criadas (no _Windows_, por omissão, a reserva de espaço de endereçamento para o _stack_ de modo utilizador é 1 MB).

	- Custo variável associado à actividade do _scheduler_ do sistema operativo e que é proporcioal ao excesso de _threads_ no estado _ready_, relativamente ao número de processadores do sistema.

### Número Óptimo de _Worker Threads_ num _Thread Pool_

- Tendo em consideração os custos da utilização de _threads_ anteriormente descritos, e tendo em consideração que os _work items_ agendados para execução no _thread pool_ podem bloquear as _worker threads_, o número de _worker threads_ utilizadas num _thread pool_ ser ajustado dinamicamente de acordo com os seguintes princípios:

	- Utilizar em cada momento o menor número de _threads_ possível (para optimizar o custo fixo), mas dispondo sempre do número de _threads_ suficiente para ser possível utilizar todos os processadores do sistema sempre que existe existe processamento pendente de execução. Como as _worker threads_ podem bloquear-se na execução dos _work items_, deverá haver sempre no estado _ready_ um número de _threads_ pelo menos igual ao número de processadores.
	
	- Para optimizar o custo variável o número de _worker threads_ no estado _ready_ em cada momento não deve exceder muito o número de processadores.
	

 
 
	 
	
	





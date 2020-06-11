# Aula 24 - Métodos Assíncronos no C# 5.0

___

## Sumário

- Problemas levantados quando se pretende escrever código assíncrono manualmente como base _tasks_ e continuações.

- Métodos assíncronos como forma de escrever código em métodos que executam assíncronamente usando uma estrutura semelhante à do código dos métodos que executam sincronamente.

- Palavras chave `async` e `await`; forma de execução; retorno de valores e excepções.

- Implementação de um _custom awaiter_ e demonstração da sequência de acções na suspensão e reatamento de um método assíncrono. 

- Padrão _fork/join_ usando métodos assíncronos e utilização dos _task combinators_ obtidos com os métodos `Task.WhenAny` e `Task.WhenAll`.

- Cópia de ficheiros usando métodos assíncronos com controlo do número de operações assíncronas de escrita pendentes.

### Bibliografia

- _Pro Asynchronous Programming with .NET_, Capítulo 7 

- _Aysync in C# 5.0_
	
### Qual é o Efeito  das Palavras Chave `async` e `await`?

- Provavelmente a coisa mais importante a perceber é que a palavra chave `async` não faz com que o nosso código execute assincronamente, e a palavra chave `await` não faz com que o nosso código espere.

- A palavra chave `async` aplicada a um método não influencia em nada o código gerado pelo compilador; esta palavras chave habilita a utilização da palavra chave `await` no código do método. Se a palavra chave `await` tivesse sido reservada quando foram inicialmente definidas as palavras chave do C# (na versão 1), não haveria necessidade de introduzir o marcador `async`, uma vez que `await` seria sempre uma palavra reservada da linguagem. O problema com a introdução de novas palavras chave numa linguagem é que já pode haver código que possa estar a usar a nova palavra chave como identificador e, por isso, não seria compilável com compilador de C# da versão 5.0. Assim, quando a palavra chave `async` é aplicada a um método o compilador é informado de que a palavra `await` dentro desse método é uma palavra chave da linguagem e não um identificador.

- Consideremos a seguinte operação assíncrona ao estilo TAP:

```C#
Task<decimal> CalculateTheMeaningOfLifeAsync() { ... }
```

- Em contraste com `async` a palavra chave `await` tem muitas consequências. Como foi dito anteriormente, `await` não realiza uma espera física. Por outras palavras,

```C#
decimal result = await CalculateMeaningOfLifeAsync();
```

- é completamente diferente de

```C#
decimal result = CalculateMeaningOfLifeAsync().Result;
```

- na medida em que esta última instrução resultaria no bloqueio da _thread_ invocante até que a tarefa assíncrona estivesse completa. Por outro lado, a _thread_ não pode prosseguir até ser conhecido o resultado da operação assíncrona; por outras palavras, **a _thread_ não pode continuar a execução**. O que o compilador implementa quando se usa `await` numa "espera" é algo que suspende a execução do método até que a operação assíncrona subjacente termine e remota a execução, mais tarde, quando a operação assíncrona for concluída. Isto é implementado usando continuações, pelo que **não existe nenhum bloqueio real de _threads_** quando se utiliza a palavra chave `await`. Todo o código que vem depois da instrução `await` forma uma continuação, que executa quando a operação assíncrona é concluída, da mesma forma como acontece quando se usa o método `Task.ContinueWith`. Sendo o compilador a construir a continuação por nós, é especificar código assíncrono com uma estrutura semelhante à do código sequencial, isto é, **preservar a noção de ordem de programa**..

- A instrução `await` consiste da palavra chave `await` aplicada uma expressão assíncrona a ser aguardada (_await expression_). O tipo desta expressão é tipicamente `Task` ou `Task<TResult>`; adiante, quando for discutida a mecânica do compilador com mais detalhe, veremos que a expressão pode produzir objectos de outros tipos, desde que sejam cumpridas certas convenções. Uma vez que o compilador agenda uma continuação sobre a _task_ que produz o resultado da operação assíncrona, a _thread_ corrente retorna simplesmente do método quando a _task_ associada à instrução `await` não completa sincronamente. Para o compilador emita código de métodos que retornam sem que tenha executado todas as suas instruções, os métodos assíncronos devem ter um dos seguintes tipos de retorno:
	 
	 - `void`
	 
	 - `Task`
	 
	 - `Task<T>`

- Para os métodos que retornam `void` não existe nenhum valor de retorno, pelo que o chamador não pode saber se a chamada foi totalmente concluída ou se apenas começou.  (Isto é uma coisa boa ou má? Mais adiante.) Nos casos em que e retornada uma `Task`, o chamador está ciente de que a operação pode não ter sido totalmente concluída e poderá observar a `Task` retornada para obter o estado final.

- Por exemplo, o método `CalculateTheMeaningOfLifeAsync` devolve uma `Task<decimal>`, o código que utiliza a palavra chave `await` é simplesmente escrito em termos de `decimal`. Assim um comportamento adicional da palavra chave `await` é fazer a coerção do resultado da expressão assíncrona e apresentá-lo da mesma forma como se o mesmo fosse obtido com uma chamada síncrona a um método. Para clarificar, deixamos aqui uma versão com dois passos do código apresentado anteriormente.
	
```C#
Task<decimal> calcTask = CalculateMeaningOfLifeAsync();
decimal result = await calcTask;
```

- A palavra chave `await` evita a necessidade de lidar directamente com a `Task` subjacente.

- Outra funcionalidade interessante subjacente à palavra chave `await` é o facto de, por omissão, manter o `SynchronizationContext` com que o método assícrono é chamado durante toda a execução do método. Para que elementos da GUI sejam actualizados com sucesso na continuação do método assícrono, a continuação necessita de ser executada na _thread_ de UI. Assim, em tempo de execução, o comportamento de `await` determina se o `SynchronizationContext.Current` é diferente de `null` e, em caso afirmativo, garante automaticamente que a continuação do método executa no mesmo `SynchronizationContext` - evitando a necessidade de explicitar directivas que determinem que a execução da continuação deve ser feita na _thread_ de UI.

- A parte final da história é o tratamento dos erros nos métodos assíncronos. Consideremos o seguinte método assíncrono:

```C#
private async void ButtonClick(object sender, RoutedEventArgs e) {
	try {
		decimal result = await CalculateMeaningOfLifeAsync();
		resultTable.Text = result.ToString();  
	} catch (MiddleLifeCrisisException error) {
		resultLabel.Text = error.Message;
	} 
} 
```

- Repare que no método acima estamos a tratar os erros usando um bloco _catch_ tal como acontece no código sequencial. O compilador, através da utilização de continuações, de novo, garante que a excepção é capturada e processada na _thread_ UI. Contudo, poderá ter pensado que o novo código baseado em `async`/`await` tem um _bug_. Afinal, se o método `CalculateMeaningOfLifeAsync` devolve uma _task_ que termina no estado _faulted_, seria de esperar o lançamento de `AggregateException`. Quando a _task_ subjacente a uma expressão `await` termina no estado _faulted_ (lançando obviamente uma instância de `AggegateException`), o código gerado pelo compilador lança exactamente a primeira excepção associada à instância de `AggregateException`; isto é feito com a intenção de replicar o mesmo modelo de programação, no tratamento de erros, que é usado em código síncrono.

- Resumindo: a palavra chave `async` apenas habilita a utilização da palavra chave `await`. A palavra chave `await` faz o seguinte:

	- Regista uma continuação para executar na conclusão da operação assíncrona;
		
	- Liberta a _thread_ corrente, isto é, retorna do método assíncrono;
		
	- Se o valor da propriedade `SynchronizationContext.Current` não for `null`, garante que todas as continuações são executadas nesse contexto de sincronização;
		
	- Se `SynchronizationContext.Current` é `null`, a continuação é agendada usando o _task scheduler_ corrente, que provavelmente será o _Default Task Scheduler_ e, por isso, a continuação executará numa _worker thread_ do _thread pool_;
	
	- Faz a coerção do resultado da operação assíncrona, seja ele um valor de retorno normal ou uma excepção.
	
- Por último, é importante realçar que embora o `async`/`await` seja muito usado na GUI com o utilizador, este mecanismo também é um ferramenta eficaz para cenários não baseados em interfaces GUI. Os métodos providenciam um modelo de programação conveniente para construir operações assíncronas compostas. Além disso, em oposição a esperar pela conclusão da operações assíncronas (usando técnicas de _polling_) são utilizadas continuações (técnica de _callback_) o que permitirá uma utilização optimizada da _worker threads_ do _thread pool_.


### Devolvendo Valores dos Métodos Assíncronos

- É expectável que os métodos `async` tenham pelo menos uma operação `await`, de modo a ser capaz de retornar sempre que for encontrada um _task_ não terminada. Se o método não tem nenhum valor de retorno, então o retorno antecipado não é um problema. Se, no entanto, o método retorna um valor, o retorno antecipado do método será um problema. Considere o seguinte método síncrono.

```C#
private static int CountWordsOnPage(string url) {
	using (var client = new WebClient()) {
		string page = client.DownloadString(url);
		return wordRegEx.Matches(page).Count;
	}
}
```

- Como a assinatura do método indica, não é possível acrescentar simplesmente a palavra chave `async` - é necessário alterar a assinatura do método para retornar `Task<int>` em vez de `int`. Isto informa o chamador que o método irá completar assincronamente e, portanto, para obter o respectivo resultado final, é necessário observar a `Task<int>`. A versão assíncrona deste método deverá ser definida como se mostra a seguir.

```C#
private static async Task<int> CountWordsOnPageAsync(string url) {
	using (var client = new WebClient()) {
		string page = await client.DownloadStringTaskAsync(url);
		return wordRegEx.Matches(page).Count;
	}
}
```

- O tipo do retorno mudou para `Task<int>` dando ao compilador a possibilidade de retornar do método antes deste executar completamente. Contudo, repare que a instrução `return` não necessitou de ser alterada; retorna ainda um `int` não `Task<int>`. O compilador toma em consideração estes aspectos por nós. O código cliente que utiliza este método poderá ser o seguinte:
	
```C#
CountWordsOnPageAsync("http://www.google.com")
	 .ContinueWith((antecedent) => Console.WriteLine($"--number of words: {antecedent.Result}"));
```

- Se tudo correr bem, o chamador irá obter o ver o número de palavras mostrado na consola. Contudo, o que acontece se for lançada uma excepção pelo método `CountWordsOnPageAsync`? Da mesma forma que o resultado sucesso é comunicado através da _task_, assim são também comunicadas as excepções não tratadas. A `Task` devolvida pelo método irá agora completar no estado _faulted_. Quando o chamador obsrevar o resultado da _task_, a excepção será relançada e o chamador terá noção da ocorrência do erro.

- O tratamento de excepções nos métodos assíncronos que retornam `void` como, por exemplo, o método `ButtonClick` mostrado atrás. Com o método devolve `void`, não existe nenhuma `Task` para o chamador observar. Neste caso, a excepção é simplesmente relançada usando o contexto de sincronização definido pelo valor da propriedade `SynchronizationContext.Current` quando o método foi chamado. No caso do contexto de sincronização ser o da _thread_ de UI, isto quase preservaria o comportamento da versão síncrona, pois entregaria a excepção à _thread_ de UI. Dizemos "quase", uma vez que envolvendo a chamada ao método assíncrono com um bloco `try`/`catch` nunca se veria a excepção se a mesma tivesse origim depois da execução do primeiro `await`. O único sítio que resta para tratar a excepção é o _top-level UI exception handler_. Se o valor da propriedade `SynchronizationContext.Current` for `null`, então a excepção é simplesmente relançada na _thread_ corrente e permitida a sua propagação no _stack_ na forma normal; se não for encontrado nenhum _exception handler_ que trate da excepção, o processo terminará abruptamente (comportamento por omissão quando uma _thread_ lança uma excepção que não é tratada).

- Assim, se um método assícncrono retorna uma `Task`, aplica-se o tratamento normal das excepções não tratadas lançadas pelas _tasks_. Se o método retorna `void`, a excepção será relançada sendo permitido que se propague no _stack_ de forma normal.

- Poderá argumentar que os projectistas dos métodos assíncronos do C# não deveriam suportar métodos do tipo `void` e que deviam ter insitido em que todos os métodos deviam retornar uma `Task` ou `Task<TResult>`, o que teria os seguintes dois efeitos: Xxxx

	  1. Torna o chamador ciente da assincronicidade do método;
	  
	  2. Garante que o chamador é sempre capaz de observar o resultado de uma invocação específica. A distância entre o bloco _catch_ da causa de um excepção dificulta frequentemente ou torna impossível produzir o nível correcto de recuperação. Os _handlers_ globais não têm normalmente a informação necessária para produzir uma recuperação efectiva e, assim, são frequentemente apenas capazes do fazer registo dos erros e deitar a aplicação abaixo graciosamente.

- A necessidade de suportar métodos `void` deriva do facto de permitir definir _event handlers_ de UI como método assíncronos - isto é, afinal, um das utilizações típicas deste mecanismo. Contudo, conidera-se uma boa prática para todos os métodos assíncronos que não sejam _event handlers_ devem retonar uma _task_, dando ao chamador a indicação de que a execução é assíncrona e disponibilizando as máximas oportunidades de tratar o resultado da operação assíncrona tão perto da invocação quanto possível.

### Devemos Sempre Continuar a Execução na _Thread_ de UI?

- Considere o seguinte código, que se destina a ser invocado pela _thread_ de UI.

```C#
async void LoadContent(object sender, RoutedEventArgs) {
	var client = new WebClient();
	
	string pageContent = await client.DownloadStringTaskAsync("http://www.msdn.com");
	
	pageContent = RemoveAdverts(pageContent);
	pageText.Text = pageContent;
}
```

- O conteúdo da página é descarregado assincronamente e, por isso, não bloqueia a _thread_ de UI. Quando é concluída a operação assíncrona, a execução deste método é reatada na _thread_ de UI para remover a publicidade presente na página. Obviamente, se o processamento necessário para remover a publicidade demorar muito tempo a executar irá haver um congelamento da UI. Para evitar este problema, podemos em alternativa, escrever:

```C#
async void LoadContent(object sender, RoutedEventArgs) {
	var client = new WebClient();
	
	string pageContent = await client.DownloadStringTaskAsync("http://www.msdn.com");
	
	pageContent = await Task.Run<string>(() => RemoveAdverts(pageContent));
	pageText.Text = pageContent;
}
```

- Agora temos uma versão que não congela a UI. Contudo, depois da página ter sido descarregada, acabamos por mobilizar a _thread_ de UI, ainda que seja por um pequeno período, para lançar uma nova _task_. Podemos evitar essa mobilização, se agendarmos uma continuação, usando o método `Task.ContinueWith`:

```C#
async void LoadContent(object sender, RoutedEventArgs) {
	var client = new WebClient();
	
	string pageContent = await client.DownloadStringTaskAsync("http://www.msdn.com")
							.ContinueWith(antecedent => RemoveAdverts(antecedent.Result));
	pageText.Text = pageContent;
}
```

- Agora, o descarregamento e remoção da publicidade da página serão executados numa _worker thread_ do _thread pool_, não sendo a _thread_ de UI mobilizada até que ambas as operações estejam concluídas. Isto é uma melhoria, mas é menos legível e mais difícil de escrever. Os métodos assíncronos permitem que estruturemos o código como se ele fosse executado sequencialmente; no código mostrado anteriormente, voltamos usar as antigas APIs baseadas em _tasks_ e em continuaões. A resposta para este problema é dupla. Primeiro, é possível configurar a operação `await` de modo a que a continuação da execução do método não continue na _thread_ de UI, mas numa _worker thread_ do _thread pool_. Para que isto aconteça, chama-se o método `Task.ConfigureAwait` especificando o valor de `false` no parâmetro `continueOnCapturedContext´, como se mostra a seguir:

```C#
async void LoadContent(object sender, RoutedEventArgs) {
	var client = new WebClient();
	
	string pageContent = await client.DownloadStringTaskAsync("http://www.msdn.com")
									.ConfigureAwait(continueOnCapturedContext: false);
	
	pageContent = RemoveAdverts(pageContent);
	pageText.Text = pageContent;
}
```

- Agora removemos a continuação, e o método `RemoveAdverts`executa agora numa _worker thread_ do _thread pool_. Parece tudo certo, mas de facto tudo correrá bem até chegar à última linha do método onde se actualiza a UI. Neste ponto, é obviamente necessário estar a executar na _thread_ de UI, o que não se verifica. Isto conduz-nos à segunda peça de refatoração para resolver o problema. O que é necessário fazer e tornar o descarregamento da página e a remoção da publicidade numa única _task_; depois, o método `LoadContext` faz simplesmente `await` sobre essa _task_ como se mostra a seguir.

```C#
async void LoadContent(object sender, RoutedEventArgs) {	
	pageText.Text = await LoadPageAndRemoveAdvertAsync("http://www.msdn.com");
}

async Task<string> LoadPageAndRemoveAdvertsAsync(string url) {
	var client = new WebClient();
	
	string pageContent = await client.DownloadStringTaskAsync(url)
								.ConfigureAwait(continueOnCapturedContext: false);
	
	return RemoveAdverts(pageContent);
}
````

- O método `LoadContent` executa 100% na _thread_ de UI, delegando no método `LoadPageAndRemoveAdvertsAsync` que após lançar a operação de descarregamento, executará numa _worker thread_ do _thread pool_. Uma vez completado o trabalho, a execução do método `LoadContent` é reatada na _thread_ de UI. Com esta última retatoração obteve-se um código mais limpo e eficiente.

- **Os métodos assíncronos do C# oferecem uma maneira conveniente e compor operações assíncronas, usando estilos de programação convencionais e simples**.


### `Task.Delay`

- O .NET _Framework_ 4.5 introduziu uma série de métodos na classe `Task` para auxiliar na escrita de código usando os métodos assíncronos. Vamos rever aqui alguns desses métodos e mostrar como os mesmos podem ser utilizados.

- Por exemplo, o seguinte código está a tentar realizar uma operação; se a mesma falha, ele recuará e tentará novamente até três vezes:

```C#
for (int nTry = 0; nTry < 3; nTry++) {
	try {
		AttemptOperation();
		break;
	} catch (OperationFailedException) {}
	Thread.Sleep(2000);
}
```

- Embora este código funcione, quando a execução chega à chamada ao método `Thread.Sleep`, a _thread_ corrente é bloqueada durante dois segundos. Enquanto a _thread_ está bloqueda não consome quaisquer recursos de CPU, mas, pelo facto de continuar viva, ainda consome recursos de memória. Numa aplicação _multithreaded_, o objecto deverá explorar a máxima concorrência utilizando o menor número possível de _threads_. Podemos resolver este problema não bloqueando a _thread_ corrente mas, em alternativa, usar `await` numa `Task` que seja considerada concluída após decorrer um determinado período de tempo. O propósito do método `Task.Delay` é exactamente fornecer este tipo de `Task`. Assim podemos refazer o código anterior para o seguinte:

```C#
for (int nTry = 0; nTry < 3; nTry++) {
	try {
		AttemptOperation();
		break;
	} catch (OperationFailedException) {}
	await Task.Delay(2000);
}
```

### `Task.WhenAll`

- O método `Task.WhenAll` cria uma _task_ que será considerada concluída quando tiverem concluído todas aas _tasks_ do conjunto especificada como argumento. Isto permite suspender a execução de um método assíncrona até que um conjunto de _tasks_ completem. Considere o seguinte código:

```C#
public static async Task DownloadDocumentsAsync(params Uri[] downloads) {
	var client = new WebClient();
	foreach (Uri uri in downloads) {
		string content = await client.DownLoadStringTaskAsync(uri);
		UpdateUI(content);
	}
}
```

- Executando este código na _thread_ de UI, não causará o congelamento da UI, na medida em que a execução do método é suspensa enquanto decorre o _download_, sendo a _thread_ de UI libertada. Contudo, esta não é a forma mais eficiente para fazer _downloads_ de múltiplas fontes, uma vez que estamos a fazer um _download_ de cada vez. É claro que é possível fazer _download_ de todos documentos em simultâneo.

- Usando o método `Task.WhenAll`, podemos criar antecipadamente todas as _tasks_ responsáveis pelos _downloads_ e depois, usando o método `Task.WhenAll`, esperar pela conclusão de uma única _task_; desta forma é possível fazer o _download_ dos documentos em paralelo, como se ilustra no seguinte excerto de código:

```C#
public static async Task DownloadDocumentsAsync(params Uri[] downloads) {
	List<Task<string>> downloadTasks = new List<Task<string>>();	
	foreach (Uri uri in downloads) { 	// fork operation
		var client = new WebClient();
		downloadTasks.add(client.DownLoadStringTaskAsync(uri));
	}
	await Task.WhenAll(downloadTasks);		// join using the when-all task-combinator
	// update the UI
	downloadTask.forEach(dt => UpdateUI(dt.Result));
}
```

- Este código irá produzir o resultado final mais rapidamente, mas só actualiza a UI quando todos os _downloads_ forem concluídos. Uma alternativa seria criar todas as _download tasks_ mantendo-as numa colecção. Depois, executar um ciclo que faça `await` em cada _task_ por iteração pela ordem das _tasks_ na colecção. Isto iria actualizar a UI à medida que os _downloads_ eram concluídos, se os mesmo completassem pela ordem com que foram iniciados. O código seria o seguinte.

```C#
public static async Task DownloadDocumentsAsync(params Uri[] downloads) {
	List<Task<string>> downloadTasks = new List<Task<string>>();	
	foreach (Uri uri in downloads) { 	// fork operation
		var client = new WebClient();
		downloadTasks.add(client.DownLoadStringTaskAsync(uri));
	}
	foreach (Task<string> downloadTask in downloadTasks)
		UpdateUi(await downloadTask);
}
```

- Contudo, se os _downloads_ não terminarem pela ordem com que são lançados, este código ainda não actualiza a UI assim que tem a respectiva informação disponível. 

#### Tratamento de Erros e o Método `Task.WhenAll`

- Embora seja mais simples ignorar as falhas, obviamente não o devemos fazer. O método `DownloadDocumentsAsync` pode possivelmente lançar múltiplas excepções devido à actividade de _download_. Como discutimos atrás, se usamos um simples bloco `try`/`catch` no método assíncrono envolvendo a instrução `await Task.WhenAll`, apenas será obtida a primeira excepção de um possivel conjunto de excepções, que estão agregadas na respectiva instância de `AggregateException`. Assim, ainda que seja conveniente aguardarmos directamente sobre a `Task` devolvida pelo método `Task.WhenAll`, é frequentemente necessário fazer esse processamento em dois passos, como se mostra no seguinte código.

```C#
Task allDownloads =  Task.WhenAll(downloadTasks);
try {
	await allDownloads;
	downloadTask.forEach(dt => UpdateUI(dt.Result));
} catch (Exception firstEx) {
	allDownloads.Exception.Handle(ex => {
		Console.WriteLine(ex.Message);		// handle each exception
		return true;
	});
}
```

- Em alternativa, se estivemos interessados em saber qual foi o _download_ responsável por uma dada excepção, então será necessáro iterar sobre a colecção de _tasks_ passadas para o método `Task.WhenAll`, perguntando a cada _task_ se falhou e, em caso afirmativo, qual foi a excepção que descreve a falha. O seguinte excerto de código mostra essa abordagem.

```C#
catch (Exception firstEx) {
	foreach (Task<string> downloadTask in downloadTasks) {
		if (downloadTask.IsFaulted) {
			Console.WriteLine($"Download {downloadTask}");
			downloadTask.Exception.Handle(ex => {
				Console.WriteLine($"\t{ex.Message}");		// handle the exception
				return true;
			});
		}
	}
}
```

### `Task.WhenAny`

- O método `Task.WhenAny` recebe como argumento uma colecção de _tasks_ e retorna uma _task_ que será considerada concluída quando qualquer for concluída uma das _tasks_ da colecção. Este método permite resolver o problema, que vimos anteriormente, de não ocorrer a actualização imediata da UI assim que terminam um _download_, se reescrevermos o método `DownloadDocumentsAsync` do seguite modo:

```C#
public static async Task DownloadDocumentsWhenAnyAsync(params Uri[] downloads) {
	List<Task<string>> downloadTasks = new List<Task<string>>();
	
	foreach (Uri uri in downloads) { 
		var client = new WebClient();
		downloadTasks.add(client.DownLoadStringTaskAsync(uri));
	}
	
	while (downloadTasks.Count > 0) {
		Task<string> downloadTask = await Task.WhenAny(downloadTasks);
		UpdateUI(downloadTask.Result);
		
		int nDownloadCompleted = downloadTasks.IndexOf(downloadTask);
		Console.WriteLine($"Download: {downloadTask}");
		downloadTasks.RemoveAt(nDownloadCompleted);
	}
}
```

- Este código agora produz o resultado desejado: as actualizações da UI acontecem quando é concluída cada uma das operações de _download_ assíncronas. Salinta-se que a expressão `await` não devolve uma `string`, mas sim uma instância do tipo `Task<string>`. Isto pode parecer estranho na medida em que até agora vimos que o `await` faz a coerção para o resultado da _tasks_, e de facto, aqui acontece isso mesmo - só que o método `Task.WhenAny` envolve uma `Task<string>` numa `Task`, pelo que o tipo do retorno do método `Task.WaitAny` é `Task<Task<string>>`. Isto permite ao chamador saber qual foi a _task_ concluída. Se fosse devolvido apenas o `string` não haveria forma de saber qual a _task_ que tinha sido responsável pelo retorno do método `Task.WhenAny`, tornando impossível chamadas repetidas ao método `Task.WhenAny`, como fizemos no exemplo de código anterior.
	
- O código do método apresentado acima tem alguns problemas. Podemos constatar que cada iteração do ciclo são realizadas duas tarefas:

	- A invocação do método `Task.WhenAny`, que tem como consequência o registo de uma continuação em cada uma das _tasks_ da colecção que ainda não estejam concluídas;
	
	- A invocação do método `downloadTask.IndexOf(downloadTask)`, que realiza uma pesquisa linear em toda a colecção, procurando determinar a _task_ concluída.
	
- Para ambas estas peças de código, o custo da execução aumenta à medida que o número de _tasks_ da colecção aumenta. Além disso, este custo é repetido após a conclusão de cada _task_ subsequente, embora seja verdade que esse custo vai sendo cada vez menor. Por cada chamada ao método `Task.WhenAny` é agendada uma continuação por cada uma das _tasks_ da colecção. Como este processo é repetido até que complete a última _task_, é fácil perceber que, no pior caso (nenhuma _task_ está concluída quando o método é invocado), o número total de agendamento de continuações quando a colecção inicial tem 5 _tasks_ será: 5 + 4 + 3 + 2 + 1. Deste ponto de vista, o método `Task.WhenAll` é mais eficiente, pois sendo apenas invocado uma vez agenda apenas uma continuação por cada uma das _tasks_ ainda não terminadas da colecção. 

- O método `Task.WhenAny` é, à primeira vista, a escolha óbvia para este tipo de utilização, mas é preciso ter cuidado porque este mecanismo não escala. Por isso, deve utilizar-se `Task.WhenAny` apenas quando tiver um pequeno número de _tasks_, ou quando apenas se pretende obter o resultado da primeira _task_ a concluir. O seguinte método interroga três _web servers_ para obter um resultado e apenas age sobre a primeira resposta.

```C#
public static async Task GetGooglePageAsync(string relativeUrl) {
	string[] hosts = {"google.co.uk", "google.com", "www.google.com.sg"};
	
	List<Task<string>> queryTasks = new List<Task<string>>();
	
	foreach (var host in hosts) {
		var urlBuilder = new UrlBuilder("http", host, 80, relativeUrl);
		var webClient = new WebClient();
		queryTasks.Add(webClient.DownloadStrintTaskAsync(urlBuilder.Uri)
				.ContinueWith<string>((downloadTask) = > {
					webClient.Dispose();
					return downloadTask.Result;	
				}));
	}
	return await Task.WaitAny(queryTasks).Result;
}
```

- O método `GetGooglePageAsync` devolve o resultado da primeira _task_ que for concluída, independentemente se ser concluída com sucesso ou com falha. Obviamente, seria preferível esperar até que um servidores responda com sucesso, ignorando as respostas com erro que possam ocorrer antes. Uma versão mais confiável do método `GetGooglePageAsync` sustituirá a chamada ao método `Task.WhenAny` pelo código que se apresenta a seguir.

```C#
var errors = new List<Exception>();
do {
	Task<string> completedTask = await Task.WhenAny(queryTasks);
	if (completedTask.Status == TaskStatus.RanToCompletion)
		return completedTask.Result;
	queryTasks.Remove(completedTask);
	errors.Add(completedTask.Exception);
} while (queryTasks.Count > 0);
throw new AggregateException(errors);
```

- Os métodos `Task.WhenAll`e `Task.WhenAny` são designados por _built-in task combinators_; a sua funcionalidade é básica, sendo frequente a necessidade de adicionar processamento adicional para os utilizar adequadamente nas nossas operações.

### Operações Assincronas Compostas Usando Múltiplos Métodos Assíncronos

- Quando se implementam operações assíncronas compostas por outras operações assíncronas, onde é desejável explorar todo o paralelismo potencial existente - isto é, só executar sequencialmente as operações que dependem de outras e executar em paralelo todas as operações que não têm dependências entre si -, nem sempre é fácil exprimir essas dependências usando apenas  a ordem de programa (definindo as as dependências directas) e os _task-combinatores_ `Task.WhenAll` e `Task.WhenAny`.

- Uma das estrutura de código fácil de expressar são os cenários onde fazemos _fork_ para executar múltiplas operações assíncronas em paralelo e depois fazer _join_ para combinar os resultados dessas operações e terminar a operação assíncrona composta ou continuar o processamento executando outras operações assíncronas. As operações assíncronas devem são lançadas sem usar a palavra chave `await`, armazenando numa colecção _tasks_ devolvidas pelos métodos que implememntam as operações assíncronas; depois, faz-se `await` sobre a _task_ devolvida pelo _task-combinator_ `Task.WhenAll` aplicado à colecção das _tasks_ que representam as operações assíncronas para suspender o método assíncrono até que todas as _tasks_ sejam concluídas. As seguir, executam-se outras operações síncronas ou assíncronas dependentes ou combinam-se simplesmente os resultados das operações assíncronas executadas em paralelo para produzir o resultado final da operação assíncrona composta. Atrás usámos esta estrutura de código no método `DownloadDocumentsAsync'.

- Outra estrutura de código que também é fácil de expressar são cenários onde fazemos _fork_ para executar especulativamente a mesma operação assíncrona em paralelo em vários servidores e depois aceitamos como resultado final o resultado da primeira operação assíncrona a terminar. Neste caso, as operações assíncronas são lançadas sem usar `await`, memorizando numa colecção as _tasks_ devolvidas pelo método que implementam a operação assíncronas depois, faz-se `await` sobre a _task_ devolvidas pelo _task-combinator_ `Task.WhenAny` aplicado à colecção das _tasks_. Este combinador devolve uma `Task<Task<TResult>>` cujo resultado é a primeira _tasks_ da colecção a terminar e que vamos usar para obter o resultado da operação assíncrona composta. Para não "abandonar" as operações assíncronas cujos resultados vamos ignorar, deve proceder-se do seguinte modo: (i) se as operações assícnronas forem canceláveis, proceder ao respectivo cancelamento; (ii) agendar uma continuação da colecção de _tasks_ "abandonadas" com `TashFactory.ContinueWhenAll`, para observar o resultado de todas as _tasks_, evitando assim o cenário das excepções não obervadas.

- Quando a estrutura do código tem que expressar a execução em paralelo de operações assíncronas que, por si só, também são operações assíncronas compostas, já não é fácil estruturar o código num único método assícrnono, usando os meios à disposição do programador (ordem de programa e _task-combinators_). Felizmente existe uma solução simples: a decomposição do processamento em no número de métodos assíncronos necessário para que o prcessamento global possa ser expresso com uma das duas estruturas que descrevemos anteriormente.

- Implementando o método `DoenloadDocumentsAsync` com esta abordagem seria:

```C#
private static async Task DownloadAndUpdateUI(Uri uri) {
	UpdateUI(await (new WebClient).DownLoadStringTaskAsync(uri));
}


public static async Task DownloadDocumentsAsync(params Uri[] downloads) {
	List<Task> downloadAndUpdateUITasks = new List<Task>();	
	foreach (Uri uri in downloads)
		downloadAndUpdateUITasks.add(DownloadAndUpdateUI(uri));
	await Task.WhenAll(downloadAndUpdateUiTasks);
}
```

- Como se pode ver a estrutura do código no método `DownloadDocumentsAsync` é um simples _fork/join_. Por outro lado, a estrutura do código do método `DownloadAndUpdateUI` expime uma sequência com duas operações: uma operação assícrona, que potencialmente suspende a execução do método, e outra síncrona executada quando o método for reatado após a conclusção do _download_.

- Recomenda-se a aplicação da seguinte regra empírica no desenho de operações assíncronas compostas usando métodos assíncronos: **quando a estrutura do código da operação assíncrona composta não pode ser expressa naturalmente, com base na ordem de programa e com a utilização dos _task-combinators_, decompôe-se a operação assíncrona composta no número de suboperações assíncronas necessárias para que todas elas tenham uma implementação natural com um único método assíncrono.**


## Mecânica dos Métodos Assíncronos

- Agora que já observámos a magia, está na altura de explicar a forma como essa magia é obtida. Vamos examinar como é que o compilador reescreve os métodos assíncronos para obter as constinuações. Aqui não temos a intençao de recriar completamente todo o detalhe, mas apenas mostrar o suficiente para dar uma ideia do que está a acontecer.

- Nesta análise vamos utilizar o seguinte código:

```C#
static void async TickTockAsync() {
	Console.WriteLine("Starting Clock");
	while (true) {
		Console.Write("Tick ");
		await Task.Delay(500);			// suspension point #1
		Console.WriteLine("Tock");
		await Task.Dealy(500);			// suspension point #2
	}
}
```

- De acordo com que já dissemos até aqui, sabemos que quando é chamadao o método `TickTockAsync`, a execução na _thread_ invocante decorre até que o haja retorno da chamada ao método `Task.Delay`, ponto em que o método assíncrono é suspenso e a _thread_ invocante retorna ao código chamador do método assícrono. Quando a _delay task_ completa a execução irá "mover-se" para a próxima peça de código, e o padrão repete-se para cada uma das expressões `await`. Acontece que os compiladores são bons na construção de máquinas de estados; tudo o que é necessário saber é qual a forma _standard_ para o compilador detectar quando deve acontecer uma transição de estado.

- O seguinte diagrama mostra a decomposição do método assíncrono `TickTockAsync`.

```
                                                  o
                                                  |
                                                  V
                              +------------------------------------------+
                              |                                          |
                              |   Console.WriteLine("Starting Clock");   |
                              |                                          |
                              +------------------------------------------+
                                                  |
                                                  v
                              +------------------------------------------+
                              |                                          |
             +--------------->|           Console.Write("Tick ");        |
             |                |            wait Task.Delay(500);         |
             |                |                                          |
             |                +------------------------------------------+
             |                                    |
             |                           Dealay Task Completed
             |                                    |
     Delay Task Completed                         V
             |                +------------------------------------------+
             |                |                                          |
             |                |           Console.WriteLine("Tock")      |
             |                |             await Task.Delay(500);       |
             |                |                                          |
             |                +------------------------------------------+
             |                                    |
             + -----------------------------------+
````

-  É uma suposição razoável admitir que o `await` só funciona com o tipo `Task`, uma vez que se trata do componente assíncrono básico no .NET _Framework_ 4.5, podendo o compilador utilizar o método `Task.ContinueWith` para agendar continuações. Contudo, o compilador de C# segue uma abordagem mais geral. A especificação da expressão a utilizar no lado direito da palavra chave `await` é que a mesma deve gerar um objecto que defina um método chamado `GetAwaiter`. Este método não tem parâmetros e pode devolver uma instância de um qualquer tipo, desde que esse tipo obedeça aos seguintes requisitos.

	- Define uma propriedade booleana chamada `IsCompleted`;
	
	- Define um método chamado `GetResult`, que não aceita parâmetros, e cujo tipo de retorno pode ser qualque coisa - este tipo será o tipo do valor da expressão `await`; e;
	
	- Implemente a interface `System.Runtime.CompilerServices.INotifyCompletion`, cuja definição se apresenta a seguir:

```C#
public interface INotifyCompletion {
	void OnCompleted(Action continuation);
}
````

- É assim o objecto _awaiter_ que providencia a funcionalidade de suporte de continuações através da chamada ao método `OnCompleted`. Este método é chamado especificando um _delegate_ que contém o código correspondente ao próximo estado do método assíncrono.

- As classes `Task` e `Task<TResult>` implementam um  método `GetAwaiter` que devolver um _awaiter_, que utiliza o tipo `Task` para suportar a funcionalidade requerida pelo objecto _awaiter_. Em certo sentido, podemos considerar que o objecto _awaiter_ é um adaptador. O que é preciso reter é que o objecto _awaiter_ deve ter tudo o que é necessário para determinar as continuações entre os vários estados.
	
- Agora que temos os blocos constituitivos para registar nas conclusões, que vão provocar as mudanças de estado na nossa máquina de estados. Podemos agora reescrever o método assíncrono `TickTockAsync` de forma semelhante aquilo que o compilador irá produzir a partir do código apresentado atrás.

```C#
static void TickTockAsync() {
	var stateMachine = new TickTockStateMachine();
	stateMachine.MoveNext();
}

public class TickTockStateMachine {
	private int state = 0;
	private TaskAwaiter awaiter;
	
	public void MoveNext() {
		switch(state) {
			case 0: goto firstState;
			case 1: goto secondState;
			case 2: goto thirdState;
		}
	  firstState:
	  	Console.WriteLine("Starting Clock");
		goto secondHalfState;
	  secondState:
	  	awaiter.GetResult();
		
	  secondHalfState:
	  	Console.Write("Tick ");
		awaiter = Task.Delay(500).GetAwaiter();
		if (!awaiter.IsCompleted) {
			state = 2;
			awaiter.OnCompleted(MoveNext);
			return;
		}
	  thirdState:
	  	awaiter.GetResult();
		Console.WriteLine("Tock");
		awaiter = Task.Delay(500).GetAwaiter();		
		if (!awaiter.IsCompleted) {
			state = 1;
			awaiter.OnCompleted(MoveNext);
			return;
		}
		goto secondState;	// infinite cycle
	}
}
```

- Este código deve ser auto-explicativo. O método `async` original é substituído pela criação de uma instância de uma máquina de estados que é iniciada com uma chamada ao método `MoveNext`. O método `MoveNext` é, de facto, o corpo do método assíncrono original, embora intercadado por um um bloco _switch/case_. O campo `state` é utilizado para determinar qual o código que deve ser executado pelo método `MoveNext` for chamado de novo. Quando cada peça de código atinge o ponto da operação `await` original, é necessáro orquestrar a transição para o próximo estado. Se o objecto _awaiter_ já estiver marcado como completado, a transição para o próximo estado é imediata; caso contrário, é registada uma continuação para chamar o próprio método e retorna. Qaundo o objecto _awaiter_ considera que a operação foi concluída, a continuação passada ao método `OnCompleted` é chamada e será executada a próxima peça de código que é determinada pelo valor do campo `state`. O processo repete-se até que a máquina de estados seja considerada concluída.


### _Costum Awaiter_

- Com o objectivo de ilustrar o que dissemos atrás e de observar a sequência de passos que ocorrem durante a execução de uma método assíncrono simples, vamos mostrar a implementação e utilização de um _costum awaiter_ que suspenda a execução do método assíncrono durante 3 segundos.

```C#
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

static class Logger {
	/**
	 * Shows the string on the console prefixed with the managed thread id
	 * of the current thread
	 */
	public static void Log(string msg) {
		Console.WriteLine($"[#{Thread.CurrentThread.ManagedThreadId}]: {msg}");
	}
}

/**
 * A custom awaiter that resumes the async method 3 seconds after
 * the call to the OnCompleted() method and produce a result of 42.
 */
class PauseForAWhileAwaiter : INotifyCompletion {
	private Task delayTask;
	
	public bool IsCompleted {
		get {
			bool result = delayTask != null ? delayTask.IsCompleted : false;
			//bool result = true;
			Logger.Log($"--IsCompleted.get() called, returns: {result}");
			return result;
		}
	}

	// INotifyCompletion
	public void OnCompleted(Action asyncContinuation) {
		int start = System.Environment.TickCount;
		Logger.Log("--OnCompleted() called, the async method will be suspended");

		// Start a delay task, and schedule a continuation that will be resume the async method
		delayTask = Task.Delay(3000).ContinueWith((_) => {
			Logger.Log($"--async method resumed, after {System.Environment.TickCount - start} ms");
			asyncContinuation();
		});
	}

	public int GetResult() {
		Logger.Log("--GetResult() called, returned 42");
		return 42;
	}
}

/**
 * A custom awaiter source that will be used as "awaiter expression".
 */
class PauseForAWhileAwaiterSource {
	public PauseForAWhileAwaiter GetAwaiter() {
		Logger.Log("--GetAwaiter() called");
		return new PauseForAWhileAwaiter();
	}
}
```

- Este _customer awaiter_ pode ser utilizado com o seguinte código:

```C#
public class CustomAwaiterDemo {

	/**
	 * Asynchronous method that uses the custom awaiter.
	 */
	private static async Task<int> PauseForAWhileAsync() {
		Logger.Log("--async method called");
		int result = await new PauseForAWhileAwaiterSource();
		Logger.Log($"--async method continues after the await expression, it will return {42}");
		return result;
	}
	
	public static void Main() {
		var asyncTask = PauseForAWhileAsync();
		Logger.Log("--async method returned");
		asyncTask.Wait();
		Logger.Log($"--async method returned {asyncTask.Result}");
	}
}
```

- Este código encontra-se no ficheiro [awaiter.cs](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/src/async-await/awaiter.cs). Se compilar e executar obterá a seguinte sequência de _logs_ na consola:

```
[#1]: --async method called
[#1]: --GetAwaiter() called
[#1]: --IsCompleted.get() called, returns: False
[#1]: --OnCompleted() called, the async method will be suspended
[#1]: --async method returned
[#4]: --async method resumed, after 3031 ms
[#4]: --GetResult() called, returned 42
[#4]: --async method continues after the await expression, it will return 42
[#1]: --async method returned 42
```

- O métdodo assíncrono `PauseForWhileAsync` é invocado na _thread_ primária (#1). Quando encontra a expressão `await` chama o método `GetAwaiter` sobre o resultado da expressão `new PauseForAWhileAwaiterSource()`, que é mostrado com a mensagem `--GetAwaiter() called`. A seguir, é interrogada a propriedade `IsCompleted` do _awaiter_ devolvido pelo método `GetAwaiter` (uma instância do tipo `PauseForAWhileAwaiter`). Como esta propriedade devolve `false`, o método assíncrona vai ser suspenso, pelo que é invocado o método `OnCompleted` do _awaiter_ para agendar a continuação que vai continuar a execução do método assíncrono quando este for reatado. Toda esta execução decorre na _thread_ primária (#1), isto é, a _thread_ invocante de um método assíncrono executa até ao primeiro ponto de suspensão, onde retorna ao código chamador. Após decorrerem pelo menos 3 segundos, o método assíncrono é reatado executando a continuação agendada anteriormente com o método `OnCompleted` numa _worker thread_ do _thread pool_ (#4). A seguir, é invocado o método `GetResult` do _awaiter_ para obter o resultado da _await expression_ (42) e o método assíncrono termina a execução terminando a _task_ subjacente com o resultado 42. Finalmente, a _thread_ primária retorna do método `Task.Wait` e mostra o resultado do método assíncrono (42).

- Se alterarmos a implementação da propriedade `PauseForAWhileAwaiter.IsCompleted` de modo a devolver `true`, veremos a seguinte seqência de mensagens na consola:

```
[#1]: --async method called
[#1]: --GetAwaiter() called
[#1]: --IsCompleted.get() called, returns: True
[#1]: --GetResult() called, returned 42
[#1]: --async method continues after the await expression, it will return 42
[#1]: --async method returned
[#1]: --async method returned 42
```

- Neste caso observamos que a execução do método assíncrono nunca foi suspensa, pelo que executa até ao fim na _thread_ primária (#1).

___






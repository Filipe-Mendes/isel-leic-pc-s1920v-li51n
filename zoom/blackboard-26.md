# Aula 26 - Implementação de Sincronizadores com Interfaces Assíncrona (II)

___

## Sumário

- Padrão de desenho para implementar sincronizadores com interfaces assíncrona e síncrona em .NET _Framework_ e em _Java_.



### Série de Exercícios 3: Notas sobre o Exercício 1

```C#
public CommException : Exception {
	public CommException(string message = "communication error") : base(message) {}
}

const int MIN_OPER_TIME = 100;
const int MAX_OPER_TIME = 1000;


static async Task<int> OperAsync(string argument, CancellationToken ctoken) {
	var rnd = new Random(Environment.TickCount);	
	try {
		await Task.Delay(rnd.Next(Min_OPER_TIME, MAX_OPER_TIME), ctoken);
		if (rnd.Next(0, 100) >= 50)
			throw new CommException();
		return argument.Length;
	} catch (OperationCanceledException) {
		Console.WriteLine("***delay cnaceled");
		throw;
	}
}

static async Taskint> OperRetryAsync(string argument, int maxRetries, CancellationTokenSource lcts) {
	--executar OperAsync maxRetry vezes passando lcts.Token;
	--se maxRetry was exceeded lcts.Cancel();
}


static async Task<int[]> ComputeAsync(string[] elems, int maxRetries, CancellationToken ctoken) {
	CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ctoken);
	
	-- call OperRetryAsync(elmes[.], maRetries, linkedCts);
	...
}
```

### Implementação de Sincronizadores com Interafce Assíncrona

- Todo o texto sobre este tópico encontra-se no ficheiro [blackboard-25.md](https://github.com/carlos-martins/isel-leic-pc-s1920v-li51n/blob/master/zoom/blackboard-25.md).

___




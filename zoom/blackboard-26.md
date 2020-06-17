# Aula 26 - Implementação de Sincronizadores com Interface Assíncrona

___

## Sumário

- Padrão de desenho para implementar sincronizadores com interface assíncrona em .NET _Framework_ e em _Java_.



### Série de Exercícios 3

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




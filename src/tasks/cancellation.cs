/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Task cancellation and task exceptions
 *
 * Carlos Martins, June 2020
 *
 **/

using System;
using System.Threading;
using System.Threading.Tasks;

public static class Cancellation {
	
	private static Task<int> LoopRandomAsync(CancellationToken ctoken) {
		
		return Task<int>.Run(() => {
			Random rnd = new Random(Environment.TickCount);
			int loopCount = rnd.Next(100);
			
			// 25% failures!
			if (loopCount > 75)
				throw new InvalidOperationException(loopCount.ToString() + " are too much loops!");
			
			Console.Write($"[{loopCount}]");
			
			for (int i = 0; i < loopCount; i++) {
				
				//ctoken.ThrowIfCancellationRequested();
				// or
				if (ctoken.IsCancellationRequested) {
					// do some necessary cleanup!
					throw new OperationCanceledException("LoopRandom task cancelled!", ctoken);
				}
				// show progress
				Console.Write('.');
				// yield processor for a random time between 10 and 100 ms 				
				Thread.Sleep(rnd.Next(10, 100));
			}
			return loopCount;
		}, ctoken);		// specify cancellation token
	}
	
	public static void Main() {
		// the source of cancellation
		CancellationTokenSource cts = new CancellationTokenSource(2500);	// cancel automatically after 2500 ms
		// task receives the underlying CancellationToken
		CancellationToken ctoken = cts.Token;
				
		var loopTask = LoopRandomAsync(ctoken);
		
		while (!loopTask.IsCompleted) {
			if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
				// cancel through CancellationTokenSource
				cts.Cancel();
			}
			Thread.Sleep(50);
		}
		// observe and process the success, the cancellation or the fault
		try {
			long result = loopTask.Result;
			Console.WriteLine($"\n-- Successful execution of {result} loop iterations");
		} catch (AggregateException ae) {
			try {
				ae.Handle((ex) => {
					if (ex is TaskCanceledException) {
						Console.WriteLine($"\n** The task was cancelled by user with: \"{ex.Message}\"");
						return true;
					}
					return false;
				});
			} catch (AggregateException ae2) {
				foreach (Exception ex in ae2.Flatten().InnerExceptions) {
					Console.WriteLine($"\n** Exception type: {ex.GetType().Name}: ; Message: {ex.Message}");
				}
			}
		}
	}
}

/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Task continuations
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.Threading;
using System.Threading.Tasks;

public static class Continuations {
	
	public static void Main() {
		CancellationTokenSource cts = new CancellationTokenSource();
		CancellationToken ct = cts.Token;
		
		var atask = Task<int>.Run(() => {
			Console.WriteLine("-- The task: starts");
			Random rnd = new Random(Environment.TickCount);
			int howManyTimes = rnd.Next(100);
			
			if (howManyTimes > 75) {
				throw new InvalidOperationException(
						String.Format($"{howManyTimes} is too many times to repeat the same thinking!"));
			}
			Console.Write($"[{howManyTimes}]");
			for (int i = 0; i < howManyTimes; i++) {
				
				// check for cancellation on each iteration 				
				ct.ThrowIfCancellationRequested();
				
				// think for a random time and prove I'm alive
				Thread.Sleep(rnd.Next(1, 10));

				// alive message
				Console.Write('.');
			}
			Console.WriteLine("\n<- The task: exits");
			return howManyTimes;
		}, ct);
		
		var whenRanToCompletion = atask.ContinueWith(antecedent => {
			Console.WriteLine($"\n-- Ran to completion continuation: antecedent's result: {antecedent.Result}");
		}, TaskContinuationOptions.OnlyOnRanToCompletion);
		
		var whenFaulted = atask.ContinueWith(_ => {
			Console.WriteLine("\n** Faulted continuation");
		}, TaskContinuationOptions.OnlyOnFaulted);
		
		var whenCanceled = atask.ContinueWith(_ => {
			Console.WriteLine("\n** Canceled continuation");
		}, TaskContinuationOptions.OnlyOnCanceled);

		var always= atask.ContinueWith(antecedent => {
			switch (antecedent.Status) {
				case TaskStatus.RanToCompletion:
					Console.WriteLine($"-- The task ran to completion, result: {antecedent.Result}");
					break;
				case TaskStatus.Canceled:
					Console.WriteLine("** The task was cancelled");
					break;
				case TaskStatus.Faulted:
					Console.WriteLine("** The task faulted, with {0}: {1}",
							 		  antecedent.Exception.InnerExceptions[0].GetType().Name,
									  antecedent.Exception.InnerExceptions[0].Message);
					break;
				default:
					Console.WriteLine($"** {antecedent.Status}: unexpected task state!");
					break;
			}
			//throw new Exception("Opps!");
		});
		
		// continuation combinator		
		var whenAny = Task.WhenAny(new Task[] { whenRanToCompletion, whenFaulted, whenCanceled});
		while (!whenAny.IsCompleted) {
			if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
				cts.Cancel();
			}
			Thread.Sleep(25);
		}
		
		// observe continuations exceptions
		try {
			Task.WaitAll(new Task[]{ whenAny, always });
			Console.WriteLine("-- Continuations completed successfully");
		} catch (AggregateException ae) {
			Console.WriteLine($"*** {ae.InnerExceptions[0].GetType().Name} : {ae.InnerExceptions[0].Message}");
		}
	}
}

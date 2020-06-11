using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Primes {
	
	/**
	 * Returns the number of prime numbers in a range.
	 * (Something that consumes processor time.)
	 */
	static int GetPrimesCount (int start, int count) {
		return ParallelEnumerable.Range (start, count).Count(
			n => Enumerable.Range (2, (int)Math.Sqrt(n) - 1).All (i => n % i > 0));
	}
	
	static void DisplayPrimesCounts() {		
		for (int i = 0; i < 10; i++)
			Console.WriteLine ("{0} primes between {1} and {2}",
		 	   					GetPrimesCount(i * 1000000 + 2, 1000000),
								i * 1000000,
								(i + 1) * 1000000 - 1);
	}
	
	static Task<int> GetPrimesCountAsync(int start, int count) {
		return Task.Run(() => ParallelEnumerable.Range (start, count).Count(
			n => Enumerable.Range (2, (int)Math.Sqrt(n) - 1).All (i => (n % i) > 0)));
		
	}
	
	// wrong consuming asynchronous method
	static void DisplayPrimeCountsAsyncWrong() {
		for (int i = 0; i < 10; i++) {
			var awaiter = GetPrimesCountAsync(i * 1000000 + 2, 1000000).GetAwaiter();
			awaiter.OnCompleted(() =>
				Console.WriteLine("{0} primes ...", awaiter.GetResult()));
		}
		Console.WriteLine("Done");	
	}
	
	// try to do something that works - recursive solution 
	static void DisplayPrimeCountsAsync2() {
		DisplayPrimeCountsFrom(0);
	}
	
	// recursive method
	static void DisplayPrimeCountsFrom(int i) {
		var awaiter = GetPrimesCountAsync(i * 1000000 + 2, 1000000).GetAwaiter();
		awaiter.OnCompleted(() => {
				Console.WriteLine($"{awaiter.GetResult()} primes from {i * 1000000} to {(i + 1) * 1000000 - 1}");
				if (++i < 10)
					DisplayPrimeCountsFrom(i);
				else
					Console.WriteLine("Done");
			}); 
	}
	
	/**
	 * Using a state machine and TaskCompletionSource<bool>
	 */
	class PrimesStateMachine {
		TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

		public Task Task { get { return tcs.Task; } }
		
		public void DisplayPrimeCountsFrom(int i) {
			var awaiter = GetPrimesCountAsync(i * 1000000 + 2, 1000000).GetAwaiter();
			awaiter.OnCompleted(() => {
					Console.WriteLine($"{awaiter.GetResult()} primes from {i * 1000000} and {(i + 1) * 1000000 - 1}");
					if (++i < 10)
						DisplayPrimeCountsFrom(i);
					else {
						Console.WriteLine("Done");
						tcs.SetResult(true);
					}
				}); 
		}
	}
	
	static Task DisplayPrimeCountsAsync3() {
		var machine = new PrimesStateMachine();
		machine.DisplayPrimeCountsFrom(0);
		return machine.Task;
	}

	
	static async void DisplayPrimesCount() {
		var result = await GetPrimesCountAsync(2, 1000000);
		Console.WriteLine(result);		
	}
	
	/**
	 * The expression upon which you await is typically a task; however, any object
	 * with a GetAwaiter method that returns an awaitable object (implementing
	 * INotifyCompletion.OnCompleted and with a appropriately typed GetResult method
	 * and a bool IsCompleted property will satisfy the compiler).
	 *
	 * Notice that our await expression evaluates to an int type; this is because
	 * the expression that we awaited was a Task<int> (whose
	 * GetAwaiter().GetResult() method returns an int).
	 */
	
	/**
	 * The real power of await expressions is that they can appear almost anywhere
	 * in code.
	 * Specifically, an await expression can appear in place of any expression
	 * (within an asynchronous method) except for inside a catch or finally block,
	 * lock expression, unsafe context or an executable’s entry point (main method).
	 */
	
	static async Task DisplayPrimeCountsAsync4() {
		for (int i = 0; i < 10; i++)
			Console.WriteLine($"{await GetPrimesCountAsync(i * 1000000 + 2, 1000000)} primes " +
							  $"from {i * 1000000} to {(i + 1) * 1000000 - 1}");
	}
	
	public static void Main() {
//		DisplayPrimesCounts();
//		DisplayPrimeCountsAsyncWrong();
//		DisplayPrimeCountsAsync2();
//		DisplayPrimeCountsAsync3().Wait();

		var runner = DisplayPrimeCountsAsync4();
		Console.WriteLine("--returned from async method. Wait until completion...");
		runner.Wait();

		Console.ReadLine();
	} 
}
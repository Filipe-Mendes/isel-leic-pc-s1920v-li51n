/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Processing task errors
 *
 * Carlos Martins, June 2020
 *
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program {

	static void Main() {
		
		// create a task that throws an exception
		var task = Task.Run(() => {
			Console.WriteLine("-- task created, delay for 2 seconds...");
			Thread.Sleep(2000);
			
			/*
			Console.WriteLine("*** task will throw an exception");
			throw new Exception("opps!");
			*/
		});
		try {
			task.Wait();		// wait for task completion, observing exceptions! (Task.WaitAll, Task.Result)
			Console.WriteLine("-- task terminated normally!");
		} catch (AggregateException ae) {
			foreach (Exception ex in ae.Flatten().InnerExceptions)
				Console.WriteLine($"--{ex.GetType().Name} : \"{ ex.Message}\"");
				
		}
		Console.WriteLine("-- main exiting...");
	}
}

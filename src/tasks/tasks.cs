/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Using TaskFactory.StartNew() and Task.Run() with asynchronous lambdas
 *
 * Carlos Martins, November 2019
 *
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program {

	// using non-async lambda expressions
	public static void _Main() {
		/**
		 * Hello, world task, using TaskFactory.StartNew and Task.Run factory methods.
		 */
		Task t = Task.Factory.StartNew((taskArg) => {
			Console.WriteLine("--I'm a {0} thread",
							  Thread.CurrentThread.IsThreadPoolThread ? "Thread Pool" : "Custom");
			Console.WriteLine($"Hello, world with TaskFactory.StartNew with \"{taskArg}\" argument!");
			Thread.Sleep(2000);
		}, "the-task-argument");
		Console.WriteLine("-- first task was created, wait until it completes");
		t.Wait();
		Console.WriteLine("-- first task terminated");
				
		// equivalent using Task.Run()
		t = Task.Run(() => { 
				Console.WriteLine("Hello, world with Task.Run!");
				Thread.Sleep(2000);
			});
		Console.WriteLine("-- second task was created, wait until it completes");
		while (!t.IsCompleted) {
			// do somethin else
			Thread.Sleep(100);
		}
		Console.WriteLine("-- second task terminated");
		
		/**
		 * task 42 using Task.Run
	     */
		Task<int> t42 = Task<int>.Run(() => 42);
		Console.WriteLine($"--task result is: {t42.Result}");
		
	}
	
	/**
	 * Take care with stated captured in the closures and asynchronous execution.
	 */
	public static void Main() {
		for (int i = 0; i < 10; i++) {
			Task.Run(() => Console.WriteLine(i));
		}
		Console.ReadLine();
		
		for (int i = 0; i < 10; i++) {
			int capturedI = i;
			Task.Run(() => Console.WriteLine(capturedI));
		}
		Console.ReadLine();
	}
	
	// using async lambda expressions
	public static void __Main() {
		
		/**
		 * differences between TaskFactory.StartNew() asnd Task.Run() when the task
		 * body is specified with an asynchronous delegate/lamdba
		 */
		
		// when using TaskFactory.StartNew() we must call Unwrap() explicity to get the inner task
		var t = Task.Factory.StartNew(async (arg) => {
			await Task.Delay(1000);
			Console.WriteLine($"TaskFactory.StartNew() created task with \"{arg}\" argument");
		}, "the-argument");
		t.Unwrap().Wait();
		
		// using Task.Run() we get already an unwraped task
		var tr = Task.Run(async () => {
			await Task.Delay(2000);
			Console.WriteLine("Task.Run() created task");
			return 42;
		}, CancellationToken.None);
		Console.WriteLine("task result: {0}", tr.Result);
		Console.WriteLine("main exiting...");
	}
}

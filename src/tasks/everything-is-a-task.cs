/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Everything a task:  creating a task that executes on a foreground Thread
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class EverythingATaskt {
	
	public static Task<T> StartForegroundTask<T>(Func<T> taskBody, CancellationToken ct) {
		// create a TaskCompletionSource to be bound to the foregroud task
		var tcs = new TaskCompletionSource<T>();
		
		// create a foreground thread, and start it to execute the task body
		var worker = new Thread(() => ExecuteForegroundTaskBody(taskBody, ct, tcs));
		worker.IsBackground = false; 
		worker.Start();
		
		// return a task that is bound to the life time of the foreground thread body
		return tcs.Task;
	}
	
	private static void ExecuteForegroundTaskBody<T>(Func<T> taskBody, CancellationToken ct,
			 										 TaskCompletionSource<T> tcs) {
		try {
			tcs.SetResult(taskBody());
		} catch (OperationCanceledException ocex) {
			// If the Task body ended with an OperationCancelledException, and the cancellation
			// is associated with the cancellation token for this Task, mark the Task as cancelled,
			// otherwise just set the exception on the Task.
			if (ct == ocex.CancellationToken) {
				tcs.SetCanceled();
			} else {
				tcs.SetException(ocex);
			}
		} catch (Exception error) {
			// Set the Task status to Faulted, and re-throw as part of an AggregateException
			tcs.SetException(error);
		}
	}

	// asynchronous delay
	static Task DelayAsync(int millis) {
		TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
		var timer = new Timer((_) => tcs.SetResult(true), null, millis, Timeout.Infinite);
		return tcs.Task;
	}

	public static void Main(string[] args) {
		CancellationTokenSource cts = new CancellationTokenSource();
		CancellationToken ct = cts.Token;
		
		Task fgTask = StartForegroundTask<int>(() => {
        		Console.WriteLine("--task running on the {0} thread #{1}...",
						      	  Thread.CurrentThread.IsBackground ? "background" : "foreground",
						      	  Thread.CurrentThread.ManagedThreadId);
				// sleep for a while
	        	Thread.Sleep(2000);
			
				// throw new Exception("Boom!!!");
				
				// check for cancellation
				ct.ThrowIfCancellationRequested();
				
				// return something
				return 42;
	    	}, ct).ContinueWith((t) => {
	        	Console.WriteLine("--continuation running on the {0} thread #{1}...",
							  	  Thread.CurrentThread.IsBackground ? "background" : "foreground",
							 	  Thread.CurrentThread.ManagedThreadId);
				try {
					Console.WriteLine("--task result is: {0}", t.Result);
				} catch (AggregateException ae) {
					Console.WriteLine("**Exception: {0}", ae.InnerExceptions[0].Message);
				}
			}, TaskContinuationOptions.RunContinuationsAsynchronously /* ExecuteSynchronously */);
		
		//cts.Cancel();
        fgTask.Wait();
		Console.WriteLine("--main completed");
		
		// use delay
		int start = Environment.TickCount;
		Console.WriteLine("--start 5 seconds delay");
		var delay = DelayAsync(5000).ContinueWith((_) => {
			Console.WriteLine($"--elapsed exactly {(Environment.TickCount - start) / 1000} seconds");
		});
		delay.Wait();
	}
}

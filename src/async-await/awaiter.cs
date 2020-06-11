/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Asynchronous methods : Custom Awaiter
 *
 * Carlos Martins, June 2020
 *
 **/

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
			//bool result = delayTask != null ? delayTask.IsCompleted : false;
			bool result = true;
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

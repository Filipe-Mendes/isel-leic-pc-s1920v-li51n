/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Implementing an assincronos delay using the type TaskCompletionSource<TResult>
 *
 * Carlos Martins, June 2020
 *
 **/

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Delay {
	
	public static Task DelayAsync(int millisDelay) {
		TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
		new Timer((_) => tcs.SetResult(null), null, millisDelay, Timeout.Infinite);
		return tcs.Task;
	}
	
	public static Task DelayAsync(int millisDelay, CancellationToken ctoken) {
		TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
		
		Timer timer = null;
		CancellationTokenRegistration ctokenRegistration;
		
		// if the cancellation token can be canceled, register a cancellation handler with it
		if (ctoken.CanBeCanceled) {
			ctokenRegistration = ctoken.Register(() => {
				if (tcs.TrySetCanceled(ctoken))
					timer?.Dispose();
			});
		} else
			ctokenRegistration = default(CancellationTokenRegistration);
		
		// start the timer
		timer = new Timer((_) => {
					if (tcs.TrySetResult(null)) {
						if (ctoken.CanBeCanceled)
							ctokenRegistration.Dispose();
					}
				}, null, millisDelay, Timeout.Infinite);
		return tcs.Task;
	}
	
	public static void Main() {
		CancellationTokenSource cts = new CancellationTokenSource(1800);
		Stopwatch sw = Stopwatch.StartNew();
		Task delay = DelayAsync(1500, cts.Token);
		Console.WriteLine($"--returned from DelayAsync() after {sw.ElapsedMilliseconds} ms");
		try {
			delay.Wait();
		} catch (AggregateException ae) {
			ae.Handle((ex) => {
				if (ex is OperationCanceledException) {
					Console.WriteLine("***the timer was cancelled");
					return true;
				}
				return false;
			});
		}
		Console.WriteLine($"--elapsed {sw.ElapsedMilliseconds} ms from the instant delay was set");
	}
}
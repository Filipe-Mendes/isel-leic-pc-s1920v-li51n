/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Asynchronous methods
 *
 * Carlos Martins, June 2020
 **/

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.IO;

/**
 * The expression upon which you await is typically a task; however, any object
 * with a GetAwaiter method that returns an "awaitable object", this is an object
 * implementing INotifyCompletion.OnCompleted(Action) method and with a appropriately typed
 * GetResult method and a bool IsCompleted property will satisfy the compiler.
 */

public class AsyncMethods {
	static void Log(String msg) {
		Console.WriteLine($"[#{Thread.CurrentThread.ManagedThreadId}]: {msg}");
	}

	/**
	 * Download a web page using explicitly tasks and continuations.
	 */
	private static Task<string> DownloadWebPageNotUsingAsync(string url) {
		Task<WebResponse> responseTask;
		try {
			responseTask = WebRequest.Create(url).GetResponseAsync();
		} catch (Exception ex) {
			// Asynchronous methods alway throw asynchronous exceptions.
			// So, wrap the exception in an AggregateException and throw it.
			throw new AggregateException(ex);
		}
		return responseTask.ContinueWith((_) => {
			try {
				using (var reader = new StreamReader(responseTask.Result.GetResponseStream())) {
					return reader.ReadToEnd();
				}				
			} catch (AggregateException ae) {
				// Throw the first exception wrapped by the AggregateException,
				// that is ae.InnerException that has the same value as
				// ae.InnerExceptions[0];
				throw ae.InnerException;
			}
		});
	}

	/**
	 * Download a web page using explicitly tasks and continuations
	 * Now using TaskCompletionSource<T>
	 */
	private static Task<string> DownloadWebPageNotUsingAsync2(string url) {
		// The TaskCompletionSource<> that controls the returned task
		TaskCompletionSource<String> tcs = new TaskCompletionSource<String>();
		Task<WebResponse> responseTask = null;
		try {
			responseTask = WebRequest.Create(url).GetResponseAsync();
		} catch (Exception ex) {
			// Throw the synchronous exception asynchronoiusly throug
			// the returned task.
			tcs.SetException(ex);
			return tcs.Task;
		}
		// This continuation sets the result of the task returned from this
		// method associated to an instance of TaskCompletionSource<String>. 
		responseTask.ContinueWith((_) => {
			try {
				using (var reader = new StreamReader(responseTask.Result.GetResponseStream())) {
					tcs.SetResult(reader.ReadToEnd());
				}				
			} catch (AggregateException ae) {
				// Throw the first exception wrapped by the AggregateException,
				// that is ae.InnerException that has the same value as
				// ae.InnerExceptions[0];
				
				tcs.SetException(ae.InnerException);
			}
		});
		return tcs.Task;
	}

	/**
	 *  Download a web page using an asynchronous method
	 */
	private static async Task<string> DownloadWebPageUsingAsync(string uri) {
		Log("--async method called");
		WebResponse response = await WebRequest.Create(uri).GetResponseAsync();
		Log("--async method resumed, count");
		using (var reader = new StreamReader(response.GetResponseStream())) {
				return reader.ReadToEnd();
		}
	}
	
	// Test code
	public static void Main() {
		String url = "http://google.com";
//		String url = "http://www.isel.pt";
		var sw = Stopwatch.StartNew();
		Task<String> download;
		try {
//			download = DownloadWebPageNotUsingAsync(url);
//			download = DownloadWebPageNotUsingAsync2(url);
			download = DownloadWebPageUsingAsync(url);
			Log($"-- async method returned after {sw.ElapsedMilliseconds} ms");
			download.Wait();
			sw.Stop();
			Log($"-- result available after {sw.ElapsedMilliseconds} ms; " +
							  $"response length: {download.Result.Length}");
		} catch (AggregateException ae) {
			Log($"***Asynchronous exception: \"{ae.InnerException.Message}\"");
		}
	}
}
	

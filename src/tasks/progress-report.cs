/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Reporting progress of a TAP asynchronous operation using IProgress<T> interface
 * and its off the self implementation with Progress<T>
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class ProgressReport {
	 
	// A TAP method

	public static Task<int> ProcessDirAsync(string dir, string pattern, CancellationToken ct,
		 									IProgress<int> progress) {
		return Task.Run(() => {
		    var csfiles = (new DirectoryInfo(dir)).GetFiles(pattern, SearchOption.AllDirectories);
			if (csfiles.Length == 0) {
				progress.Report(100);
				return 0;
			}
			int count = 0;
			var rnd = new Random(Environment.TickCount);
		    foreach (FileInfo file in csfiles) {
				
				// convenient point to check for cancellation
				ct.ThrowIfCancellationRequested();
				
				// here we process the file contents, taking some random time
				Console.WriteLine($"--[#{Thread.CurrentThread.ManagedThreadId}]name: \"{file.FullName}\", size: {file.Length}");

				// report progress
				progress.Report((++count * 100) / csfiles.Length);
				
				// take some time before processing the next file 
				Thread.Sleep(rnd.Next(1000));
				
				// throw an exception!
				/*
				if (count > 5)
					throw new Exception("Injected Exception");
				*/
			}
			return csfiles.Length;
		}, ct);
	}

	public static void Main(string[] args) {
		CancellationTokenSource  cts = new CancellationTokenSource();
		CancellationToken ctoken = cts.Token;

		//...
		Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]: main");
		
		// create the progress reporter specifying the report action
		Progress<int> progressReporter = new Progress<int>((percent) => {
			Console.WriteLine($"[#{Thread.CurrentThread.ManagedThreadId}]: {percent}%");			
		});

		// Start processing of the files in the specified directory
		Task<int> processing = ProcessDirAsync(args.Length == 0 ? "." : args[0], "*.cs", ctoken, progressReporter);
		
		while (!processing.IsCompleted) {
			if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
				cts.Cancel();
			}
			Thread.Sleep(50);
		}
		// observe and process success, cancellation or fault
		try {
			Console.WriteLine($"-- processing of {processing.Result} files was completed");
		} catch (AggregateException ae) {
			try {
				ae.Flatten().Handle((ex) => {
					if (ex is OperationCanceledException &&
					    ((OperationCanceledException)ex).CancellationToken == ctoken) {
						Console.WriteLine("*** the processing of files was cancelled!");
						return true;
					}
					return false;
				});
			} catch (AggregateException ae2) {
				foreach (Exception ex in ae2.Flatten().InnerExceptions) {
					Console.WriteLine($"**Exception type: {ex.GetType().Name}; Message: {ex.Message}");
				}
			}
		}
	}
}

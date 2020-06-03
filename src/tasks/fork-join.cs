/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Fork/Join using explicitly tasks and continuations
 *
 * Carlos Martins, November, 2019
 *
 **/

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.IO;

public static class AsyncForkAndJoin {
	
	// Asynchronous fork and join
	
	private static readonly long CHUNK_SIZE = 32 * 1024;
	
	private static Task<long> ProcessFileAsync(string fname) {

		List<Task<int>> tasks = new List<Task<int>>();

		var instream = new FileStream(fname, FileMode.Open, FileAccess.Read,
									  FileShare.Read, 1024 * 16,
									  FileOptions.Asynchronous);
		long length = instream.Length;
		
		// fork processing of file on several tasks
		for (int i = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), j = 0; i > 0; i--, j++) {
			byte[] buffer = new byte[CHUNK_SIZE];
			Console.WriteLine($"--iteration[{j}]");
			tasks.Add(
				instream.ReadAsync(buffer, 0, buffer.Length).ContinueWith((antecedent) => { /* process read data */
					Thread.Sleep(40);
					return antecedent.Result;
				}));
		}
		// join partial results into a final one
		return Task.Factory.ContinueWhenAll(tasks.ToArray(), (antecedents) => {
			long sum = 0;
	
			foreach (var antecedent in antecedents) {
				sum += antecedent.Result;
			}
			return sum;
		});
	}
	
	public static void Main() {
		var sw = Stopwatch.StartNew();
		var future = ProcessFileAsync(@"c:\Windows\System32\ntoskrnl.exe");
		Console.WriteLine($"--async method returned after {sw.ElapsedMilliseconds} ms");
		try {
			future.Wait();
			sw.Stop();
			Console.WriteLine($"--completed after {sw.ElapsedMilliseconds} ms; computed file length: {future.Result} bytes");			
		} catch (Exception ex) {
			Console.WriteLine($"***Exception: {ex.Message}");
		}
	}
}

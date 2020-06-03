/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Why task continuations
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/**
 * Why continuations
 */

public static class WhyContinuations {
	
	// Asynchronous fork and join
	
	private static readonly long CHUNK_SIZE = 32 * 1024;
	
	public static Task<long> ProcessFileAsync(string fname) {
		var instream = new FileStream(fname,
									  FileMode.Open, FileAccess.Read,
									  FileShare.None, (int)CHUNK_SIZE,
									  true);		
		
		List<Task<int>> tasks = new List<Task<int>>();
		long length = instream.Length;
		Console.WriteLine("--file length: {0}", length);
		
		// fork processing of file on several tasks
		for (int i = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), j = 0; i > 0; i--, j++) {
			//Console.WriteLine($"--read block {j}");
			byte[] buffer = new byte[CHUNK_SIZE];
			Task<int> readTask = instream.ReadAsync(buffer, 0, buffer.Length);
			Task<int> readContinuation = readTask.ContinueWith(
				(antecedent) => { /* process the read data */
					Thread.Sleep(50);
			 		return antecedent.Result;
				}
			);
			tasks.Add(readContinuation);
			// or
			/*
			tasks.Add(instream.ReadAsync(buffer, 0, buffer.Length).ContinueWith(
				(antecedent) => { // process the read data
					Thread.Sleep(50);
			 		return antecedent.Result;
				}
			));
			*/
		}
		
		// Join and compute global result from partial results
		return Task<long>.Factory.ContinueWhenAll(tasks.ToArray(), (antecedents) => {
			long result = 0;
			foreach (var t in antecedents) {
				result += t.Result;
			}
			Console.WriteLine("terminated: {0}", result);
			return result;
		});
	}
	
	public static void Main() {
		var sw = Stopwatch.StartNew();
		var future = ProcessFileAsync(@"c:\Windows\System32\ntoskrnl.exe");
		Console.WriteLine("--async method returned after {0} ms", sw.ElapsedMilliseconds);
		future.Wait();
		sw.Stop();
		Console.WriteLine("--result available after: {0} ms, file length: {1}", sw.ElapsedMilliseconds,
						  future.Result);
	}
}
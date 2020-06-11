/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Fork/Join pattern using C# asynchronous methods
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
	
	private const int CHUNK_SIZE = 256 * 1024;
	private const int PROC_TIME = 200;
	
	/**
	 * Sequential file processing
	 */
	
	private static async Task<long> SequentialProcessFileAsync(string fname) {

		var instream = new FileStream(fname, FileMode.Open, FileAccess.Read,
									  FileShare.Read, 1024 * 16,
									  FileOptions.Asynchronous);
		long length = instream.Length;
		long sum = 0;
		// Sequentially process chunks of data from the file
		for (int count = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), i = 0; i < count ; i++) {
			byte[] buffer = new byte[CHUNK_SIZE];
			Console.Write($"[#{i}]");
			int bytesRead = await instream.ReadAsync(buffer, 0, buffer.Length);
			// simulate processing time asynchronously
			await Task.Delay(PROC_TIME);
			sum += bytesRead;
		}
		return sum;
	}
	
	
	/**
	 * Parallel file processing using a normal lambda for continuation and
	 * a synchronous delay to simulate chunk processing time.
	 */
	private static async Task<long> ParallelProcessFileAsync(string fname) {

		List<Task<int>> tasks = new List<Task<int>>();

		var instream = new FileStream(fname, FileMode.Open, FileAccess.Read,
									  FileShare.Read, 1024 * 16,
									  FileOptions.Asynchronous);
		long length = instream.Length;
		
		// fork processing of file on several tasks
		for (int count = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), i = 0; i < count; i++) {
			byte[] buffer = new byte[CHUNK_SIZE];
			Console.Write($"[#{i}]");
			tasks.Add(
				instream.ReadAsync(buffer, 0, buffer.Length).ContinueWith(antecedent => {
					// simulate some processing time synchronously
					Thread.Sleep(PROC_TIME);
					return antecedent.Result;
				}));
		}
		
		/**
		 * Join forked tasks
		 * We can await individualy for each task termination, or use
		 * the Task.WhenAll task combinator.
		 */
		
		/*
		long sum = 0;
		foreach (var task in tasks)
			sum += await task;
		*/
		int[] results = await Task.WhenAll(tasks);
		long sum = 0;
		// compute the global result
		foreach (var partial in results)
			sum += partial;
		return sum;
	}

	/**
	 * Parallel file processing using an asynchronous lambda for continuation
	 * and asynchronous delay to simulate chunk processing time.
	 */
	private static async Task<long> ParallelProcessFile2Async(string fname) {

		List<Task<Task<int>>> tasks = new List<Task<Task<int>>>();

		var instream = new FileStream(fname, FileMode.Open, FileAccess.Read,
								  	  FileShare.Read, 1024 * 16,
								  	  FileOptions.Asynchronous);
		long length = instream.Length;
	
		// fork processing of file on several tasks
		for (int count = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), i = 0; i < count; i++) {
			byte[] buffer = new byte[CHUNK_SIZE];
			Console.Write($"[#{i}]");
			tasks.Add( instream.ReadAsync(buffer, 0, buffer.Length).ContinueWith(async (antecedent) => {
				// simulate asynchrnously some processing time
				await Task.Delay(PROC_TIME);
				return antecedent.Result;
			}));
		}

		/**
		 * Join forked tasks and compute global result
		 */
		

		Task<int>[] results = await Task.WhenAll(tasks);
		long sum = 0;
		foreach (var partial in results)
			sum += partial.Result;
		return sum;
	}

	/**
	 * Parallel file processing based on two asynchronous methods
	 * and asynchronous delay to simulate chunk processing time.
	 */

	/**
	 * Auxiliar async method that reads and processes a chunk of data
	 */
	private static async Task<int> ProcessFileChunkAsync(FileStream instream) {
		byte[] buffer = new byte[CHUNK_SIZE];
		int bytesRead = await instream.ReadAsync(buffer, 0, buffer.Length);
		await Task.Delay(PROC_TIME);
		return bytesRead;
	}

	private static async Task<long> ParallelProcessFile3Async(string fname) {

		List<Task<int>> tasks = new List<Task<int>>();

		var instream = new FileStream(fname, FileMode.Open, FileAccess.Read,
									  FileShare.Read, 1024 * 16,
									  FileOptions.Asynchronous);
		long length = instream.Length;
		
		// fork processing of file on several tasks
		for (int count = (int)((length + CHUNK_SIZE - 1) / CHUNK_SIZE), i = 0; i < count; i++) {
			Console.Write($"[#{i}]");
			tasks.Add(ProcessFileChunkAsync(instream));
		}

		/**
		 * Join forked tasks and compute global result
		 */

		int[] results = await Task.WhenAll(tasks);
		long sum = 0;
		foreach (var partial in results)
			sum += partial;
		return sum;
	}
	
	public static void Main() {
		
		/**
		 * Configure (or not) the thread pool to quickly create more worker
		 * threads than the number of processors, setting the minimum number
		 * of worker threads to a higher value.
		 */
		int worker, iocp;
		ThreadPool.GetMinThreads(out worker, out iocp);
		ThreadPool.SetMinThreads(worker + 40, iocp);
		
		var sw = Stopwatch.StartNew();
		Task<long> procFileTask;
		// Sequential file processing
//		procFileTask = SequentialProcessFileAsync(@"c:\Windows\System32\ntoskrnl.exe");

		// Parallel file processing using a normal lambda as continuation and a sync delay
		procFileTask = ParallelProcessFileAsync(@"c:\Windows\System32\ntoskrnl.exe");

		// Parallel file processing using an async lambda as continuation and an async delay
//		procFileTask = ParallelProcessFile2Async(@"c:\Windows\System32\ntoskrnl.exe");

//		// Parallel file processing using two async methods and an async delay
//		procFileTask = ParallelProcessFile3Async(@"c:\Windows\System32\ntoskrnl.exe");

		long retAfter = sw.ElapsedMilliseconds;
		try {
			procFileTask.Wait();
			sw.Stop();
			Console.WriteLine($"\n\n--async method returned after {retAfter} ms, " + 
			                  $"and completed after {sw.ElapsedMilliseconds} ms; " +
							  $"computed file length: {procFileTask.Result} bytes");			
		} catch (AggregateException ae) {
			Console.WriteLine($"\n***{ae.InnerException.GetType()}: \"{ae.InnerException.Message}\"");
		}
	}
}

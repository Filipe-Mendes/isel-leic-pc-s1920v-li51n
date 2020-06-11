/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Copy Files using the C# asynchronous methods
 *
 * Generate executable running command: name
 * Cleanup all generated files: nmake clean
 *
 * Carlos Martins, June 2020
 **/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

static class FileCopyAsync {

	const int BUFFER_SIZE = 4 * 1024;
	
	//
	// File copy using an asynchronous method without exploiting any parallelism between
	// reading from the source file and writing to the destination file.
	//
	static async Task<long> SequentialCopyAsync(Stream src, Stream dst) {
		long fileSize = 0;
		byte[] buffer = new byte[BUFFER_SIZE];
		int bytesRead;
		while ((bytesRead = await src.ReadAsync(buffer, 0, buffer.Length)) != 0) {
			//Console.Write($"[{bytesRead}]");
			await dst.WriteAsync(buffer, 0, bytesRead);
			fileSize += bytesRead;			
		}
		return fileSize;
	}

	//
	// File copy using---
	//
	public static async Task<long> ParallelCopyUnboundedAsync(Stream src, Stream dst) {
		List<Task> writeTasks = new List<Task>();
		long fileSize = 0;	
		do {
			byte[] buffer = new byte[BUFFER_SIZE];
			int bytesRead = await src.ReadAsync(buffer, 0, buffer.Length);
			Console.WriteLine($"bytesRead: {bytesRead}");
			if (bytesRead == 0)
				break;
			fileSize += bytesRead;						
			// start the write operation	
			writeTasks.Add(dst.WriteAsync(buffer, 0, bytesRead));
		} while (true);
		await Task.WhenAll(writeTasks);
		return fileSize;
	}	

	//
	// File copy using an asynchronous method copy a file to another exploiting
	// the parallelism between the read and the previous N (>= 1) write operation.
	//
	public static async Task<long> ParallelCopyBoundedAsync(Stream src, Stream dst) {
		const int MAX_ONGOING_WRITES = 50;
		long fileSize = 0;
		var ongoingWriteTasks = new HashSet<Task>();	
		do {
			byte[] buffer = new byte[BUFFER_SIZE];
			int bytesRead = await src.ReadAsync(buffer, 0, buffer.Length);
			if (bytesRead == 0) 
				break;
			fileSize += bytesRead;						
			// start the write operation	
			ongoingWriteTasks.Add(dst.WriteAsync(buffer, 0, bytesRead));
			
			// If there are already the maximum ongoing writes, wait until
			// at least one of them terminates
			if (ongoingWriteTasks.Count >= MAX_ONGOING_WRITES) {
				//remove all finished tasks from the set
				if (ongoingWriteTasks.RemoveWhere(t => t.IsCompleted) == 0) {
					// If none of the tasks on the set are complete, you must wait for
					// at least one task to finish.
					ongoingWriteTasks.Remove(await Task.WhenAny(ongoingWriteTasks));
				}
			}
		} while (true);
		if (ongoingWriteTasks.Count > 0)
			await Task.WhenAll(ongoingWriteTasks);
		return fileSize;
	}	
	
	public static void Main() {
		const bool AsyncMode = true;
		using ( FileStream src = new FileStream(@"c:\windows\system32\ntoskrnl.exe",
												FileMode.Open, FileAccess.Read,
												FileShare.None, BUFFER_SIZE, AsyncMode),		
						   dst = new FileStream(@".\\result.dat",
												FileMode.Create, FileAccess.Write,
												FileShare.None, BUFFER_SIZE, AsyncMode)) {
			try {								
				Stopwatch sw = Stopwatch.StartNew();
				Task<long> copyTask;
//				copyTask = SequentialCopyAsync(src, dst);
//				copyTask = ParallelCopyUnboundedAsync(src, dst);
				copyTask = ParallelCopyBoundedAsync(src, dst);
				long elapsedUntilReturn = sw.ElapsedMilliseconds;
				long copiedBytes = copyTask.Result;
				sw.Stop();
				Console.WriteLine($"copied {copiedBytes} bytes, elapsed until return: " +
								  $"{elapsedUntilReturn} ms; until result: {sw.ElapsedMilliseconds} ms");
			} catch (AggregateException ae) {
				Console.WriteLine($"***{ae.InnerException.GetType()}: {ae.InnerException.Message}");
			}
		}
	}
}

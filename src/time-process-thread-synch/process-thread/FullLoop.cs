/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Observation of the Windows's sheduling behavior
 *
 * To generate the executable, execute: csc FullLoop.cs
 *
 * Carlos Martins, March 2017
 *
 **/

using System;
using System.Threading;


class FullLoop {
	public static void Main() {

		// Create as many busy loop threads as processors.
		Console.WriteLine($"-- create {Environment.ProcessorCount} threads that never block, and start them...");
		Thread[] threads = new Thread[Environment.ProcessorCount];
		for (int i = 0; i < threads.Length; ++i) {
			threads[i] = new Thread(() => {
				try {
					do { } while (true);
				} catch {
					Console.WriteLine("--thread was aborted");
				}
			});
			threads[i].Start();
		}
		Console.ReadLine();
		
		// Abort each created threads and wait it terminates. 
		for (int i = 0; i < threads.Length; ++i) {
			threads[i].Abort();
			threads[i].Join();
		}
	}
}

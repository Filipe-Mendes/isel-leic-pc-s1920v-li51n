/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Single Resource implemented using .NET Implicit Monitors
 *
 * Carlos Martins, April 2017
 *
 **/

using System;
using System.Threading;

public class SingleResource {
	private bool busy;					// true when resource is busy
	private readonly object monitor;	// the monitor and the associated condition variable
	
	public SingleResource(bool busy = false) {
		monitor = new object();
		this.busy = busy; 
	}
	
	// acquire resource
	public void Acquire() {
		lock(monitor) {
			try {
				while (busy)
					Monitor.Wait(monitor);
			} catch (ThreadInterruptedException) {
				/**
				 * When a notification races with waiting thread's interruption
				 * the interrupted exception cam be thrown. So, if the resource
				 * is not busy we propagate the notification to notify other thread
				 * that cam be also blocked.
				 */
				if (!busy)
					Monitor.Pulse(monitor);
				throw;
			}
			busy = true;
		}
	}
	
	// release previously acquired resource
	public void Release() {
		lock(monitor) {
			busy = false;
			/**
			 * This notification can be lost if the waiting thread is also
			 * interrupted.
			 * This situation is fixed on the Acquire method.
			 */
			Monitor.Pulse(monitor);
		}
	}
	
	/**
	 * Test code
	 */

	// constants
	const int THREADS = 20;
	const int MIN_PAUSE = 1;
	const int MAX_PAUSE = 50 - MIN_PAUSE;
	
	// the threads
	static Thread[] threads = new Thread[THREADS];
	
	// control variables
	static int sharedTotal = 0;
	static int[] privatePartial = new int[THREADS];
	static volatile bool exit = false;
	
	// a random number generator per thread
	static ThreadLocal<Random> rnd =
				new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));

	// the single resource used as a lock
	static SingleResource _lock = new SingleResource();
		
	public static void Main() {
		for (int i = 0; i < THREADS; i++) {
			int li = i;
			threads[i] = new Thread(() => {
				Console.WriteLine("->#{0:D2}", li);
				do {
					_lock.Acquire();
					sharedTotal++;
					Thread.Yield();
					_lock.Release();
					// sleep for a while in 50% of loop cycles
					if (rnd.Value.Next(100) >= 50)
						Thread.Sleep(rnd.Value.Next(MAX_PAUSE) + MIN_PAUSE);
					if (++privatePartial[li] % 100 == 0)
						Console.Write("[#{0:D2}]", li);
				} while (!exit);
				Console.WriteLine("<-#{0:D2}", li);
			});
			threads[i].Start();
		}
		// run threads until <enter>
		Console.WriteLine("-- press <enter> to terminate...");
		Console.ReadLine();
		exit = true;
		Console.WriteLine("\n--wait until all threads terminate");
		// wait for each thread termination, and compute private total
		int privateTotal = 0;
		for (int i = 0; i < THREADS; i++) {
			threads[i].Join();
			privateTotal += privatePartial[i];
		}
		
		// display results
		Console.WriteLine("\n-- shared total: {0}, private total: {1}, diff: {2}",
			sharedTotal, privateTotal, privateTotal - sharedTotal);
	}
}

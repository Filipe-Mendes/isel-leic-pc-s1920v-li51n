/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  ManualResetEvent slim with optimized fast-paths.
 *
 *  Carlos Martins, May 2020
 *
 **/

using System;
using System.Threading;

public sealed class ManualResetEventSlim_ {
	private volatile bool signaled;		// true when the event is signaled
	private volatile int waiters;		// the current number of waiter threads - atomicity granted by monitor
	private int setVersion;				// the version of set operation - atomicty granted by monitor
	private readonly object monitor;
	
	// Constructor
	public ManualResetEventSlim_(bool initialState) {
		monitor = new object();
		signaled = initialState;
	}
	
	// return true when tha Wait must return
	private bool tryAcquire() { return signaled; }
	
	// set signaled to true and make it visible to all processors
	private void DoRelease() {
		signaled = true;
		/**
		 * In order to guarantee that this write is visible to all processors, before
		 * any subsequente read, notably the volatile read of "waiters" we must
		 * interpose a full-fence barrier.
		 */
		Interlocked.MemoryBarrier();
	}
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {
	
		// If the event is signalled, return true
		if (tryAcquire())
			return true;
		
		// the event is not signalled; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// if a time out was specified, get a time reference
		TimeoutHolder th  = new TimeoutHolder(timeout);
		
		lock(monitor) {
		
			// get the current setVersion and declare the current thread as a waiter.						
			int sv = setVersion;
			waiters++;
			
			/**
			 * before we read the "signaled" volatile variable, we need to make sure that the increment
			 * of *waiters* is visible to all processors.
			 * In .NET this means interpose a full-fence memory barrier.
			 */			
			Interlocked.MemoryBarrier();
			
			try {
				/**
			 	 * after declare this thread as waiter, we must recheck the "signaled" in order
			 	 * to capture a check that ocorred befor we increment the waiters.
			 	 */
				if (tryAcquire())
					return true;

				// loop until the event is signalled, the specified timeout expires or
				// the thread is interrupted.
				do {				
					// check if the wait timed out
					if ((timeout = th.Value) == 0)
						// the specified time out elapsed, so return failure
						return false;
				
					Monitor.Wait(monitor, timeout);
				} while (sv == setVersion);
				return true;
			} finally {
				// at the end, decrement the number of waiters
				waiters--;
			}
		}
	}
		
	// Set the event to the signalled state
	public void Set() {
		DoRelease();
		// after set the "signaled" to true and making sure that it is visble to all
		// processors, check if there are waiters
		if (waiters > 0) {		
			lock(monitor) {
				// We must recheck waiters after acquire the lock in order
				// to avoid unnecessary notifications
				if (waiters > 0) {
					setVersion++;
					Monitor.PulseAll(monitor);
				}
			}
		}
	}

	// Reset the event
	public void Reset() { signaled = false; }
}
			
public static class ManualResetEventSlim_Tests {
	
	private const int MIN_TIMEOUT = 1;
	private const int MAX_TIMEOUT = 500;
	private const int SETUP_TIME = 50;
	private const int DEVIATION_TIME = 20;
	private const int EXIT_TIME = 100;
	private const int THREADS = 10;

	/*
	 * Test normal wait
	 */

	private static bool TestWait() {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlim_ mrevs = new ManualResetEventSlim_(false);
		
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				string stid = string.Format("#{0}", tid);
				
				Console.WriteLine("--{0}, started...", stid);
				try {
					mrevs.Wait();
				} catch (ThreadInterruptedException) {
					Console.WriteLine("-->{0}, was interrupted while waiting!", stid);
				}
				try {
					Console.WriteLine("{0}, exiting...", stid);
				} catch (ThreadInterruptedException) {}
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep for a while before set the manual-reset event.
		//
		
		Thread.Sleep(SETUP_TIME);
		mrevs.Set();
		Thread.Sleep(EXIT_TIME);
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine("++#" + i + " is still alive so it will be interrupted");
				tthrs[i].Interrupt();
			}
		}

		//
		// Wait until all test threads have been exited.
		//
		
		for (int i = 0; i < THREADS; i++)
			tthrs[i].Join();
		return success;
	}
	
	//
	// Test timed wait.
	//
	  
	private static bool TestTimedWait() {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlim_ mrevs = new ManualResetEventSlim_(false);
				
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				string stid = string.Format("#{0}", tid);
				Random rnd = new Random(tid);
				
				Console.WriteLine("{0}, started...", stid);
				bool timedOut = false;
				try {
					timedOut = !mrevs.Wait(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT));
				} catch (ThreadInterruptedException) {
					Console.WriteLine("-->{0}, was interrupted while waiting!", stid);
				}
				try {
					Console.WriteLine("{0}, timed out = {1}", stid, timedOut);
					Console.WriteLine("{0}, exiting...", stid);
				} catch (ThreadInterruptedException) {}
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep ...
		//
		
		Thread.Sleep(MAX_TIMEOUT + DEVIATION_TIME);
		//Thread.Sleep(MIN_TIMEOUT - DEVIATION_TIME);
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine("++#" + i + " is still alive so it will be interrupted");
				tthrs[i].Interrupt();
			}
		}
		
		//
		// Wait until all test threads have been exited.
		//
		
		for (int i = 0; i < THREADS; i++)
			tthrs[i].Join();
		
		return success;
	}
	
	/*
	 * Test Set followed immediately by Reset
	 */

	private static bool TestSetFollowedByReset() {
		Thread[] tthrs = new Thread[THREADS];
		//ManualResetEventSlim mrevs = new ManualResetEventSlim(false);
		ManualResetEventSlim_ mrevs = new ManualResetEventSlim_(false);
		
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				string stid = string.Format("#{0}", tid);
				
				Console.WriteLine("--{0}, started...", stid);
				try {
					mrevs.Wait();
				} catch (ThreadInterruptedException) {
					Console.WriteLine("-->{0}, was interrupted while waiting!", stid);
				}
				try {
					Console.WriteLine("{0}, exiting...", stid);
				} catch (ThreadInterruptedException) {}
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep for a while before set the manual-reset event.
		//
		
		Thread.Sleep(SETUP_TIME);
		mrevs.Set();
		mrevs.Reset();
		Thread.Sleep(EXIT_TIME + 500);
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine("++#" + i + " is still alive so it will be interrupted");
				tthrs[i].Interrupt();
			}
		}

		//
		// Wait until all test threads have been exited.
		//
		
		for (int i = 0; i < THREADS; i++)
			tthrs[i].Join();
		return success;
	}
			
	//
	// Run manual-reset event slim tests.
	//
	
	public static void Main() {
		Console.WriteLine("\n>> Test Wait: {0}\n", TestWait() ? "passed" : "failed");
		Console.WriteLine("\n>> Test Timed Wait: {0}\n", TestTimedWait() ? "passed" : "failed");
		Console.WriteLine("\n>> Test Set Followed by Reset: {0}\n", TestSetFollowedByReset() ? "passed" : "failed");
	}
}

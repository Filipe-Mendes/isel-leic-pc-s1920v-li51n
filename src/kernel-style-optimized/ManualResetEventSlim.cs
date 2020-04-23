/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Manual reset event implementation "kernel style" and following a
 *  reasoning based on the idea of "generation".
 *
 *  Generate executable with: csc ManualResetEventSlim.cs TimeoutHolder.cs
 *
 *  Carlos Martins, April 2020
 *
 **/

using System;
using System.Collections.Generic;
using System.Threading;

/**
 * Applying the "kernel style" directly, that is, each thread has its own request.
 */

public class ManualResetEvenKernelStyletNaive {
	// the implict .NET monitor
	private readonly object monitor = new object();
	
	// The resquest object is just a "bool"
	private readonly LinkedList<bool> reqQueue = new LinkedList<bool>();
	
	// synchronization state: // true when the event is signaled
	private bool signalState;
	
	public ManualResetEvenKernelStyletNaive(bool initialState = false) { signalState = initialState; }
	
	public bool Wait(int timeout = Timeout.Infinite) {
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// enqueue a request on the request queue
			LinkedListNode<bool> requestNode = reqQueue.AddLast(false);
			 
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until our request is satisfied, the specified timeout expires
			// or the thread is interrupted.

			do {
				if ((timeout = th.Value) == 0) {
					
					// remove our request from "reqQueue"
					reqQueue.Remove(requestNode);
					return false;		// return failure: timeout expired
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// as this acquire operation has no side effects we can choose to
					// throw ThreadInterruptedException instead of giving the operation
					// completed successfully.
					// anyway, we must remove the request from the queue if it is stiil
					// inserted.
					if (!requestNode.Value)
						reqQueue.Remove(requestNode);
					throw;
				}
			} while (!requestNode.Value);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			signalState = true;
			if (reqQueue.Count > 0) {
				LinkedListNode<bool> request = reqQueue.First;
				do {
					request.Value = true;
				} while ((request = request.Next)!= null);
				reqQueue.Clear();
				Monitor.PulseAll(monitor);
			}
		}
	}
	
	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}

/**
 * An optimized version sharing the "request" object between acquirer threads.
 */

public class ManualResetEvenKernelStyleShareRequest {
	private readonly object monitor = new object();
	
	// this request object is shared by all waiters
	private class SharedRequest {
		internal waiters;
		internal bool done;
		
		internal SharedRequest() { waiters = 1; } 
	}
	
	// the pseudo-"request queue"
	
	private SharedRequest reqQueue = null; 

	private bool signalState;	// true when the event is signaled
	
	public ManualResetEvenKernelStyleShareRequest(bool initialState = false) {
		signalState = initialState;
	}
	
	/**
	 * Methods to manipulate the "simplified-request queue"
	 */

	// add a waiter to the queue
	private SharedRequest EnqueueWaiter() {
		if (reqQueue == null) 
			reqQueue = new SharedRequest();
		else
			reqQueue.waiters++;
		return reqQueue;
	}
	
	// remove a waiter froom the queue
	public void RemoveWaiter() {
		if (--reqQueue.waiters == 0)
			reqQueue = null;
	}
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// enqueue a request on the request queue
			SharedRequest request =  EnqueueWaiter();
			 
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until the event our request is satisfied, the specified timeout expires
			// or the thread is interrupted.

			do {
				if ((timeout = th.Value) == 0) {
					
					// remove our request from "waiters queue"
					RemoveWaiter();
					return false;		// return failure: timeout expired
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// as this acquire operation has no side effects we can choose to
					// throw ThreadInterruptedException instead of giving the operation
					// completed successfully.
					// anyway, we must remove the request from the queue if it is stiil
					// inserted.
					if (!request.done)
						RemoveWaiter();
					throw;
				}
			} while (!request.done);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			signalState = true;
			if (reqQueue != null) {
				reqQueue.done = true;
				reqQueue = null;
				Monitor.PulseAll(monitor);
			}
		}
	}
	
	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}

/**
 * An optimized implementation for void acquire operations, using
 * the notion of generation.
 * The sunchroniation state include a  generation counter that is
 * incremented whenever the event is signaled.
 * When a thread must wait because the event is reset, it gets a copy
 * of the current generation.
 * After a waiting thread is notifified, it checks if the generation
 * has evolved and, if so, it can conclude that the event has already
 * been signaled after being blocked.
 */

public class ManualResetEventSlimOptimized {
	// implicit .NET monitor
	private readonly object monitor = new object();
	// synchronization state
	private bool signalState;	
	// current state generation
	private int signalStateGeneration;
	
	public ManualResetEventSlimOptimized(bool initial = false) { signalState = initial; }
	
	// Wait until the event is signalled
	public bool Wait(int timeout = Timeout.Infinite) {	
		lock(monitor) {
			// If the event is already signalled, return true
			if (signalState)
				return true;
		
			// create an instance of TimeoutHolder to support timeout adjustments
			TimeoutHolder th = new TimeoutHolder(timeout);
		
			// loop until the event is signalled, the specified timeout expires or
			// the thread is interrupted.
			
			int arrivalGeneration = signalStateGeneration;
			do {
				if ((timeout = th.Value) == 0)
					return false;		// timeout expired
				Monitor.Wait(monitor, timeout);
			} while (arrivalGeneration == signalStateGeneration);
			return true;
		}
	}
	
	// Set the event to the signalled state
	public void Set() {
		lock(monitor) {
			if (!signalState) {
				signalState = true;
				signalStateGeneration++;
				Monitor.PulseAll(monitor);
			}
		}
	}

	// Reset the event
	public void Reset() {
		lock(monitor)
			signalState = false;
	}
}
			
public static class ManualResetEventSlimTests {
	
	private const int MIN_TIMEOUT = 30;
	private const int MAX_TIMEOUT = 500;
	private const int SETUP_TIME = 50;
	private const int DEVIATION_TIME = 20;
	private const int EXIT_TIME = 100;
	private const int THREADS = 20;

	/**
	 * Test normal wait
	 */
	private static bool TestWait() {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlimOptimized mrevs = new ManualResetEventSlimOptimized(false);	// our implementation
//		ManualResetEventSlim mrevs = new ManualResetEventSlim(false);	// BCL implementation
		
		Console.WriteLine("-->Test wait");
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				Console.WriteLine($"--{tid}, started...");
				try {
					mrevs.Wait();
				} catch (ThreadInterruptedException) {
					Console.WriteLine($"-->{tid} was interrupted while waiting!");
				}
				try {
					Console.WriteLine($"{tid}, exiting...");
				} catch (ThreadInterruptedException) {}
				/*
				if (tid == 7) 
					do {
						try {
							Thread.Sleep(0);
						} catch (ThreadInterruptedException) { break; }
					} while(true);
				*/
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep for a while before set the manual-reset event.
		//
		
		Thread.Sleep(SETUP_TIME);
		Console.WriteLine("-- set event");
		mrevs.Set();
		Thread.Sleep(EXIT_TIME);
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine($"++#{i} is still alive, so it will be interrupted");
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
	
	/**
	 * Test timed wait.
	 */
	private static bool TestTimedWait() {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlimOptimized mrevs = new ManualResetEventSlimOptimized(false);		// our implementation
//		ManualResetEventSlim mrevs = new ManualResetEventSlim(false);		// BCL implementation
					
		Console.WriteLine("-->Test timed wait");
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				Random rnd = new Random(tid);
				
				Console.WriteLine($"{tid}, started...");
				bool timedOut = false;
				try {
					timedOut = !mrevs.Wait(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT));
				} catch (ThreadInterruptedException) {
					Console.WriteLine($"-->{tid}, was interrupted while waiting!");
				}
				try {
					Console.WriteLine($"{tid}, timed out = {timedOut}");
					Console.WriteLine($"{tid}, exiting...");
				} catch (ThreadInterruptedException) {}
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep ...
		//
		
		Thread.Sleep(MAX_TIMEOUT + DEVIATION_TIME);		// all waiters must time out
		//Thread.Sleep(MIN_TIMEOUT - DEVIATION_TIME);		// none waiter must times out
		//Thread.Sleep((MIN_TIMEOUT + MAX_TIMEOUT) / 2);	// some waiters time out
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine($"++#{i} is still alive, so it will be interrupted");
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
	
	/**
	 * Test Set followed immediately by Reset
	 */
	private static bool TestSetFollowedByReset() {
		Thread[] tthrs = new Thread[THREADS];
        ManualResetEventSlimOptimized mrevs = new ManualResetEventSlimOptimized(false);		// our implementation
    	//ManualResetEventSlim mrevs = new ManualResetEventSlim(false);		// BCL implementation - fails!

        Console.WriteLine("-->Test set followed by reset");
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				Console.WriteLine($"--{tid}, started...");
				try {
                    mrevs.Wait();
                } catch (ThreadInterruptedException) {
					Console.WriteLine($"-->{tid} was interrupted while waiting!");
				}
				try {
					Console.WriteLine($"{tid}, exiting...");
				} catch (ThreadInterruptedException) {}
			});
			tthrs[i].Start();
		}
		
		//
		// Sleep for a while before set the manual-reset event.
		//
		
		Thread.Sleep(SETUP_TIME);
		mrevs.Set();
		//Thread.Sleep(20);
		mrevs.Reset();
		Thread.Sleep(EXIT_TIME + 500);
		bool success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].IsAlive) {
				success = false;
				Console.WriteLine($"++#{i} is still alive so it will be interrupted");
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
		Console.WriteLine("\n-->Test wait: {0}\n", TestWait() ? "passed" : "failed");
		Console.WriteLine("\n-->Test timed wait: {0}\n", TestTimedWait() ? "passed" : "failed");
		Console.WriteLine("\n-->Test set followed by reset: {0}\n", TestSetFollowedByReset() ? "passed" : "failed");
	}
}

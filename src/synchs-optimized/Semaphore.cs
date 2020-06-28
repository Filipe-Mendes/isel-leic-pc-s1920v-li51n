/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Semaphore with fast-path optimization
 * 
 *  Carlos Martins, May 2020
 *
 **/

using System;
using System.Threading;

public sealed class Semaphore_ {

	private volatile int permits;		// the available number of permits
	private volatile int waiters;		// the current number of waiters
	private readonly object monitor;
	 
	// Constructor
	public Semaphore_(int initialPermits = 0) {
		if (initialPermits < 0)
			throw new ArgumentException();
		monitor = new object();
		permits = initialPermits;
	}

	// Tries to acquire one permit
	private bool TryAcquire() {
		while (true) {
			int observedPermits = permits;	// volatile read
			if (observedPermits == 0)
				return false;
			if (Interlocked.CompareExchange(ref permits, observedPermits - 1, observedPermits) == observedPermits)
				return true;
		}
	}
	
	// Releases the specified number of permits
	private void DoRelease(int releases) {
		Interlocked.Add(ref permits, releases);	// inserts a full memory fence
	}
	
	// Acquire one permit from the semaphore
	public bool Acquire(int timeout = Timeout.Infinite) {
		// try to acquire one permit, if available
		if (TryAcquire())
			return true;
		
		// no permits available; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// if a time out was specified, get a time reference
		TimeoutHolder th = new TimeoutHolder(timeout);		
		lock(monitor) {
			
			// the current thread declares itself as a waiter..
			waiters++;
			/**
			 * The .NET Memory Model does not guarantees non-reordering of previous volatile write
			 *  of "waiters" with the next volatile read of "permits", so we must EXPLICITLY
			 *  insert a Full Fence to ensure that this algorithm works!
			 */
			Interlocked.MemoryBarrier();
			try {		
				do {
					// after increment waiters, we must recheck if acquire is possible!
					if (TryAcquire())
						return true;
					// check if the specified timeout expired
					if ((timeout = th.Value) == 0)
						return false;
					Monitor.Wait(monitor, timeout);
				} while (true);
			} catch (ThreadInterruptedException) {
				// if we were interrupted and there are permits available, we can have
				// been notified and interrupted.
				// so, we leave this method throwing ThreadInterruptException, but before
				// we regenerate the notification, if there are available permits
				//
				if (permits > 0) 
					Monitor.Pulse(monitor);
				throw; // re-throw thread interrupted exception
			} finally {
				// the current thread is no longer a waiter
				waiters--;
			}	
		}
	}
		
	// Release the specified number of permits
	public void Release(int releases) {
		DoRelease(releases);	// the underlying Interlocked.Add method inserts a full-fence barrier 
		if (waiters > 0) {	
			lock(monitor) {
				// we must recheck "waiters" after acquire the lock,
				// to avoid unnecessary notifications 
				if (waiters > 0) {
					if (waiters == 1 || releases == 1)
						Monitor.Pulse(monitor);		// only one thread can proceed execution
					else
						Monitor.PulseAll(monitor);	// more than one threads cam proceed execution
				}
			}
		}
	}

	// Release one permit
	public void Release() { Release(1); }


	/*
     * Test code.
	 */
	
	public static bool IsCurrentThreadInterrupted() {
		try {
			Thread.Sleep(0);
			return false;
		} catch (ThreadInterruptedException) {
			// re-assert interruption
			Thread.CurrentThread.Interrupt();
			return true;
		}
	}
					
	private static bool TestSemaphoreAsLock() {

		const int MIN_ACQUIRE_TIMEOUT = 5;
		const int MAX_ACQUIRE_TIMEOUT = 50;
		const int MAX_CRITICAL_SECTION_TIME = 5;
		const int JOIN_TIMEOUT = 50;		
		const int RUN_TIME = 10 * 1000;
		const int THREADS = 10;

		Thread[] tthrs = new Thread[THREADS];
		int[] privateCounters = new int[THREADS];
		int[] timeouts = new int[THREADS];
		int sharedCounter = 0;
		Semaphore_ lockSem = new Semaphore_(1);
		
		//
		// Create and start acquirer/releaser threads
		//
		
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() => {
				Random rnd = new Random(tid);
				Console.WriteLine($"-> #{tid:D2} starting...");			
				do {
					try {
						do {
							if (lockSem.Acquire(rnd.Next(MIN_ACQUIRE_TIMEOUT, MAX_ACQUIRE_TIMEOUT)))
								break;
							if (++timeouts[tid] % 100 == 0)
								Console.Write('.');
						} while (true);
						try {
							Interlocked.Increment(ref sharedCounter);
							if (++privateCounters[tid] % 10 == 0) {
								Console.Write($"[#{tid:D2}]");
							}
							Thread.Sleep(rnd.Next(MAX_CRITICAL_SECTION_TIME));
						} finally {
							lockSem.Release();							
						}
					} catch (ThreadInterruptedException) {
						/*
						if (tid == 0)
							do {} while (true);
						*/
						goto exit;
					}
				} while (!IsCurrentThreadInterrupted());
			exit:
				Console.WriteLine($"<- #{tid:00} exiting...");
			});
			tthrs[i].IsBackground = true;
			tthrs[i].Start();
		}

		// run the test threads for a while...
		Thread.Sleep(RUN_TIME);
		
		// Interrupt each test thread and wait for a while until it finished.
		int stillRunning = 0;
		for (int i = 0; i < THREADS; i++) {
			tthrs[i].Interrupt();
			if (!tthrs[i].Join(JOIN_TIMEOUT))
				stillRunning++;
		}
		
		if (stillRunning > 0) {
			Console.WriteLine("\n*** failure: {0} test thread(s) did not answer to interruption", stillRunning);
			return false;
		}
		
		// All threads finished - compute results
		
		Console.WriteLine("\nPrivate counters:\n");
		int sum = 0;
		for (int i = 0; i < THREADS; i++) {
			sum += privateCounters[i];
			if ((i % 5) == 0)
				Console.WriteLine();
			else
				Console.Write(' ');
			Console.Write($"[#{i:00}:{privateCounters[i],4}/{timeouts[i]}]");
		}
		Console.WriteLine($"\n--shared aquisitions: {sharedCounter}, private acquisitions: {sum}");
		return sum == sharedCounter;
	}

	private static bool TestSemaphoreInAProducerConsumerContext() {

		const int MAX_PRODUCE_TIME = 2;
		const int MAX_CONSUME_TIME = 3;
		const int RUN_TIME = 10 * 1000;
		const int JOIN_TIMEOUT = 50;
		const int PRODUCER_THREADS = 5;
		const int CONSUMER_THREADS = 10;
 
		Thread[] cthrs = new Thread[CONSUMER_THREADS];
		Thread[] pthrs = new Thread[PRODUCER_THREADS];
		int[] consumerCounters = new int[CONSUMER_THREADS];
		int[] producerCounters = new int[PRODUCER_THREADS];
		
		// Using our semaphore...
		Semaphore_ freeSem = new Semaphore_(1);
		Semaphore_ dataSem = new Semaphore_(0);
		
		// Create and start consumer threads.		
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			int tid = i;
			cthrs[i] = new Thread(() => {
				Random rnd = new Random(tid);
				do {
					try {
						dataSem.Acquire();
						try {
							if (++consumerCounters[tid] % 10 == 0)
								Console.Write($"[#c{tid:D2}]");
							Thread.Sleep(rnd.Next(MAX_CONSUME_TIME));
						} finally {
							freeSem.Release();
						}
					} catch (ThreadInterruptedException) {
						break;
					}
				} while (!IsCurrentThreadInterrupted());					
			});
			cthrs[i].IsBackground = true;
			cthrs[i].Priority = ThreadPriority.Highest;
			cthrs[i].Start();
		}
		
		// Create and start producer threads.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			int tid = i;
			pthrs[i] = new Thread(() => {				
				Random rnd = new Random(tid + CONSUMER_THREADS);
				do {
					try {
						freeSem.Acquire();
						try {
							if (++producerCounters[tid] % 10 == 0)
								Console.Write($"[#p{tid:D2}]");
							Thread.Sleep(rnd.Next(MAX_PRODUCE_TIME));	
						} finally {
							dataSem.Release();							
						}
					} catch (ThreadInterruptedException) {
						break;
					}
				} while (!IsCurrentThreadInterrupted());
			});
			pthrs[i].IsBackground = true;
			pthrs[i].Start();
		}
		
		// run the test for a while
		Thread.Sleep(RUN_TIME);
		
		// Interrupt each consumer thread and wait for a while until it finished.
		int stillRunning = 0;
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			cthrs[i].Interrupt();
			if (!cthrs[i].Join(JOIN_TIMEOUT))
				stillRunning++;
		}

		// Interrupt each producer thread and wait for a while until it finished.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			pthrs[i].Interrupt();
			if (!pthrs[i].Join(JOIN_TIMEOUT))
				stillRunning++;
		}
		
		if (stillRunning > 0) {
			Console.WriteLine($"\n*** failure: {stillRunning} test thread(s) did not answer to interruption");
			return false;
		}
		
		
		// Compute results
		
		Console.WriteLine("\nConsumer counters:");
		int consumptions = 0;
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			consumptions += consumerCounters[i];
			if (i != 0) {
				if (i % 4 == 0)
					Console.WriteLine();
				else
					Console.Write(' ');
			}
			Console.Write($"#c[{i:00}: {consumerCounters[i],4}]");
		}
		if (dataSem.TryAcquire())
			consumptions++;
		
		Console.WriteLine("\nProducer counters:");
		int productions = 0;
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			productions += producerCounters[i];
			if (i != 0) {
				if (i % 4 == 0)
					Console.WriteLine();
				else
					Console.Write(' ');
			}
			Console.Write($"[#p{i:00}: {producerCounters[i],4}]");
		}
		Console.WriteLine($"\n--productions: {productions}, consumptions: {consumptions}");
		return consumptions == productions;
	}
	
	public static void Main() {
		/*
		Console.WriteLine("\n-->Test semaphore as lock: {0}",
						  TestSemaphoreAsLock() ? "passed" : "failed");
		*/
		Console.WriteLine("\n-->Test semaphore in a producer/consumer context: {0}",
						  TestSemaphoreInAProducerConsumerContext() ? "passed" : "failed");
	
	}
}

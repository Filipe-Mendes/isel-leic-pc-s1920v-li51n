/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Java: Mutable shared data access synchronization
 *
 * Carlos Martins, March 2017
 *
 ***/

import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import java.util.concurrent.locks.*;

public class Locks {

	//
	// The several flavours of locks available.
	//

	private static final Object intrinsicLock = new Object();
	private static final ReentrantLock reentrantLock = new ReentrantLock();
	private static final ReentrantReadWriteLock readWriteLock = new ReentrantReadWriteLock();
	private static final Semaphore semaphoreLock = new Semaphore(1, false);
	private static final Semaphore fairSemaphoreLock = new Semaphore(1, true);
	
	//
	// The shared counter is implemented by an instance of AtomicInteger
	// in order to support atomic operations.
	//

	private static final AtomicInteger sharedCounter = new AtomicInteger();

	//
	// Unprotected increment.
	//
	
	private static void unprotectedIncrement() {
		sharedCounter.lazySet(sharedCounter.get() + 1);
		//sharedCounter.set(sharedCounter.get() + 1);
	}

	//
	// Increment the shared counter using several synchronization mechanisms.
	// 
	
	private static void atomicIncrement() {
		sharedCounter.incrementAndGet();
	}

	private static void atomicIncrementUsingCAS() {
		int repeatCount = 0;
		for (;;) {
			int c = sharedCounter.get();
			if (sharedCounter.compareAndSet(c, c + 1)) {
				/*
				if (repeatCount >= 5)
					System.out.print('.');
				*/
				return;
			}
			repeatCount++;
		}
	}

	private static void  intrinsicLockProtecetedIncrement() {	
		synchronized(intrinsicLock) {
			unprotectedIncrement();
		}
	}

	private static void  reentrantLockProtecetedIncrement() {
		reentrantLock.lock();
		unprotectedIncrement();
		reentrantLock.unlock();
	}

	private static void  readWriteLockProtecetedIncrement() {
		readWriteLock.writeLock().lock();
		unprotectedIncrement();
		readWriteLock.writeLock().unlock();
	}

	private static void  nonFairSemaphoreProtecetedIncrement() {
		semaphoreLock.acquireUninterruptibly();
		unprotectedIncrement();
		semaphoreLock.release();
	}

	private static void  fairSemaphoreProtecetedIncrement() {
		fairSemaphoreLock.acquireUninterruptibly();
		unprotectedIncrement();
		fairSemaphoreLock.release();
	}

	//
	// Test running flag.
	//

	private static volatile boolean running = true;

	//
	// The increment shared counter thread(s).
	//

	private static class IncrementThread extends Thread {
		private Runnable privateRun;
		
		public IncrementThread(Runnable pr ) {
			privateRun = pr;
		}
		
		public void run() {
			do {
		
				//
				// Choose the way to increment the shared counter.
				//

				//unprotectedIncrement();
				//atomicIncrement();
				//atomicIncrementUsingCAS();
				//intrinsicLockProtecetedIncrement();
				//reentrantLockProtecetedIncrement();
				//readWriteLockProtecetedIncrement();
				nonFairSemaphoreProtecetedIncrement();
				//fairSemaphoreProtecetedIncrement();

				//
				// Increment the private counter using the action delegate.
				//

				privateRun.run();
			} while(running);
		}
	}

	//
	// Array to store the private counters on the same cache line
	// (ARRAY_SIZE = 2) or in different cache lines (ARRAY_SIZE >= 16).
	//
	
	private static final int ARRAY_SIZE = 16;
	private static final int FIRST_IDX = 0;
	private static final int SECOND_IDX = ARRAY_SIZE - 1;
	private static final int[] privateCounters = new int[ARRAY_SIZE];
	
	//
	// The primary thread.
	//
	
	public static void main(String[] args) throws InterruptedException {
	
		//
		// Sets the primary thread priority to highest to ensure that,
		// when ready, it preempts one of the other threads.
		//
	
		Thread.currentThread().setPriority(Thread.MAX_PRIORITY);

		// Uncomment to prevent biased locking
		System.identityHashCode(intrinsicLock);
		
		//
		// Create the increment thread(s) and sleep for a while.
		//

		Thread thrinc1 = new IncrementThread(new Runnable() {
			public void run() {
				privateCounters[FIRST_IDX]++;
			}
		});
		thrinc1.start();
		
		/**
		Thread thrinc2 = new IncrementThread(new Runnable() {
			public void run() {
				privateCounters[SECOND_IDX]++;
			}
		});
		thrinc2.start();
		*/
		
				
		//
		// Run increment threads for 5 seconds.
		//
		
		System.out.print("++run increment thread(s) for 3 seconds...\n");
		
		long start = System.nanoTime();
		Thread.sleep(3000);
	
		//
		// Clear the running flag and synchronize with the termination
		// of the increment threads.
		//
	
		running = false;
		long elapsed = System.nanoTime() - start;
		
		thrinc1.join();
		//thrinc2.join();
	
		//
		// Show the results.
		//

		int privateSum = privateCounters[FIRST_IDX] + privateCounters[SECOND_IDX];
		int diff = privateSum - sharedCounter.get();
	
		System.out.println("\n--shared Counter: " + (sharedCounter.get() >> 10) +
						   " K, private counters: " + (privateSum  >> 10) +
						   " K, diff: " + diff + "\n--increment cost: " +
						   (long)(elapsed / sharedCounter.get()) + " ns");
	}
}

/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  ManualResetEventSlim with fast-path optimization
 * 
 *  Carlos Martins, May 2020
 *
 **/

import java.util.Random;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import java.util.concurrent.locks.*;
import java.io.IOException;

public final class ManualResetEventSlim {
	private volatile boolean signaled;
	private int setVersion;
	private volatile int waiters;
	private final Lock lock;
	private final Condition okToAwait;
		
	// Constructor
	public ManualResetEventSlim(boolean initialState) {
		lock = new ReentrantLock();
		okToAwait = lock.newCondition();
		signaled = initialState;
	}
	
	// try acquire
	private boolean tryAcquire() {
		return signaled;	// volatile read
	}
	
	// do release
	private void doRelease() {
		signaled = true;	// JMM guarantees that the volatile write o "signaled" is made
		// visible to all processors before any subsequente read or volatile ready
	}
	
	// Wait until the event is signalled
	public boolean await(long timeout, TimeUnit unit) throws InterruptedException {
	
		// If the event is signalled, return true
		if (tryAcquire())
			return true;
		
		// the event is not signalled; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// process timeout
		boolean timed = timeout > 0;
		long nanosTimeout = timed ? unit.toNanos(timeout) : 0L;

		lock.lock();	
		try {
		
			// get the current setVersion and declare the current thread as a waiter.			
			int sv = setVersion;
			waiters++;
			/**
			 * JMM guarantees that the volatile write in "waiters" is made visible to all
			 * peocessors before the next volatile read of "signaled".
			 */

			// loop until the event is signaled, the specified timeout expires or
			// the thread is interrupted.
			
			try {
				// after declared as waiter, the current thread must recheck if is
				// really necessary to block. 
				if (tryAcquire())
					return true;
				
				// loop until the event is signaled, the specified timeout expires or
				// the thread is interrupted.
				do {
					// check if the wait times out
					if (timed) {
						if (nanosTimeout <= 0L)
							// the specified time out elapsed, so return failure
							return false;
						nanosTimeout = okToAwait.awaitNanos(nanosTimeout);
					} else
						okToAwait.await();
				} while (sv == setVersion);
				return true;
			} finally {
				// at the end, decrement the number of waiters.
				waiters--;
			}
		} finally {
			lock.unlock();
		}
	}

	public boolean await(long millisTimeout)  throws InterruptedException {
		return await(millisTimeout, TimeUnit.MILLISECONDS);
	}
	
	public void await()  throws InterruptedException {
		await(-1L, TimeUnit.MILLISECONDS);
	}
	
	// Set the event to the signalled state
	public void set(){
		doRelease();
		/**
		 * JMM guarantees that the volatile write of "signaled" is made visible to all
		 * processors, before the volatile read of "waiters".
		 */

		// check if there are any waiters; if so, acquire the lock and notify them is necessary
		
		if (waiters > 0) {		
			lock.lock();
			try {
				// After acquire the lock, we must recheck waiters in order to avoid
				// unnecessary notifications
				if (waiters > 0) {
					setVersion++;	// grant that notified threads see that the event was set
					okToAwait.signalAll();
				}
			} finally {
				lock.unlock();
			}
		}
	}

	// Reset the event
	public void reset() { signaled = false; }
	

	/**
	 * Test Code
	 */
	
	private static final int MIN_TIMEOUT = 30;
	private static final int MAX_TIMEOUT = 500;
	private static final int SETUP_TIME = 50;
	private static final int DEVIATION_TIME = 20;
	private static final int EXIT_TIME = 100;
	private static final int THREADS = 10;

	/*
	 * Test normal wait
	 */		
	private static boolean testWait() throws InterruptedException {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlim mrevs = new ManualResetEventSlim(false);
		
		for (int i = 0; i < THREADS; i++) {
			final int tid = i;
			tthrs[i] = new Thread(() -> {				
				System.out.printf("-->%02d, started...%n", tid);
				try {
					mrevs.await();
				} catch (InterruptedException ie) {
					System.out.printf("=>#%02d was interrupted while waiting!%n", tid);
				}
				System.out.printf("<--%02d, exiting...%n", tid);
			});
			tthrs[i].start();
		}
		
		// Sleep for a while before set the manual-reset event.	
		Thread.sleep(SETUP_TIME);
		mrevs.set();
		Thread.sleep(EXIT_TIME);
		boolean success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].isAlive()) {
				success = false;
				System.out.printf("***#%d is still alive so it will be interrupted!%n", i);
				tthrs[i].interrupt();
			}
		}

		// Wait until all test threads have been exited.
		for (int i = 0; i < THREADS; i++)
			tthrs[i].join();
		return success;
	}
	
	/*
	 * Test timed wait.
	 */ 
	private static boolean testTimedWait() throws InterruptedException {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlim mrevs = new ManualResetEventSlim(false);
				
		for (int i = 0; i < THREADS; i++) {
			final int tid = i;
			tthrs[i] = new Thread(() -> {
				Random rnd = new Random(tid);
				
				System.out.printf("-->#%02d, started...%n", tid);
				boolean timedOut = false;
				try {
					timedOut = !mrevs.await(rnd.nextInt(MAX_TIMEOUT + MIN_TIMEOUT));
				} catch (InterruptedException ie) {
					System.out.printf("=>#%02d, was interrupted while waiting!%n", tid);
				}
				System.out.printf("--#%02d, %s%n", tid, timedOut ? "timed out" : "interrupted");
				System.out.printf("<--#%02d, exiting...%n", tid);
			});
			tthrs[i].start();
		}
		
		// Sleep ...
		
		Thread.sleep(MAX_TIMEOUT + DEVIATION_TIME);			// test succeeds!
		//Thread.sleep(MIN_TIMEOUT - DEVIATION_TIME);		// test fails!
		boolean success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].isAlive()) {
				success = false;
				System.out.printf("***#%02d is still alive so it will be interrupted%n", i);
				tthrs[i].interrupt();
			}
		}
		
		// Wait until all test threads have been exited.
		for (int i = 0; i < THREADS; i++)
			tthrs[i].join();
		
		return success;
	}
	
	/*
	 * Test Set followed immediately by Reset
	 */
	private static boolean testSetFollowedByReset() throws InterruptedException {
		Thread[] tthrs = new Thread[THREADS];
		ManualResetEventSlim mrevs = new ManualResetEventSlim(false);
		
		for (int i = 0; i < THREADS; i++) {
			final int tid = i;
			tthrs[i] = new Thread(() -> {
				System.out.printf("-->#%02d, started...%n", tid);
				try {
					mrevs.await();
				} catch (InterruptedException ie) {
					System.out.printf("=>#%02d, was interrupted while waiting!%n", tid);
				}
				System.out.printf("<--#%02d, exiting...%n", tid);
			});
			tthrs[i].start();
		}
		
		// Sleep for a while before set the manual-reset event.
		Thread.sleep(SETUP_TIME);
		mrevs.set();
		mrevs.reset();
		Thread.sleep(EXIT_TIME + 500);
		boolean success = true;
		for (int i = 0; i < THREADS; i++) {
			if (tthrs[i].isAlive()) {
				success = false;
				System.out.printf("***#%02d is still alive so it will be interrupted%n", i);
				tthrs[i].interrupt();
			}
		}

		// Wait until all test threads have been exited.
		for (int i = 0; i < THREADS; i++)
			tthrs[i].join();
		return success;
	}
	
	//
	// Run manual-reset event slim tests.
	//
	
	public static void main(String... args) throws InterruptedException {
		System.out.printf("%n>> Test Wait: %s%n", testWait() ? "passed" : "failed");
		System.out.printf("%n>> Test Timed Wait: %s\n", testTimedWait() ? "passed" : "failed");
		System.out.printf("%n>> Test Set Followed by Reset: %s\n", testSetFollowedByReset() ? "passed" : "failed");
	}
}

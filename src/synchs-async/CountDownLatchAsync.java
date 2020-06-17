/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  CountDownLatch with asynchronous and synchronous interfaces
 *
 *  Carlos Martins, June 2020
 *
 **/

import java.util.LinkedList;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

public class CountDownLatchAsync {
			
	// Type used to represent each asynchronous waiter
	private class AsyncWaiter extends CompletableFuture<Boolean> implements Runnable {
		private ScheduledFuture<?> timer;	// the timeout timer, if any
		boolean done;			// True when the async request is completed

		/**
		 * This is the timeout cancellation handler
		 */
		@Override
		public void run() {
			boolean complete = false;
			synchronized(theLock) {
				if (!done) {
					// Remove the request from the queue and mark it as done
					asyncWaiters.remove(this);
					complete = done = true;
				}
			}
			if (complete) {
				// Release resources and complete CF<> with false result.
				close();
				complete(false);
			}
		}

		/**
		 * Disposes the resources associated with the async acquire
		 */
		void close() {
			if (timer != null)
				timer.cancel(false);
		}
	}

	// The lock - we do not use the monitor functionality
	private final Object theLock = new Object();
	
	// The initial and current count
	private final int initialCount;
	private final AtomicInteger count;

	// The state of the latch	
	private volatile boolean open;		// volatile grants visibility w/o acquire/release the lock

	// The queue of async waitetrs
	private LinkedList<AsyncWaiter> asyncWaiters;

	//  Completed futures used to return true and false results
	private static final CompletableFuture<Boolean> trueFuture = CompletableFuture.completedFuture(true);
	private static final CompletableFuture<Boolean> falseFuture = CompletableFuture.completedFuture(false);;
	    
	/**
     * Constructor
     */
    public CountDownLatchAsync(int initialCount) {
		if (initialCount < 0)
			throw new IllegalArgumentException("initialCount");
		this.initialCount = initialCount;
		count = new AtomicInteger(initialCount);
		// If the count down latch is initialized as closed, initialize the wait list.
		if (count.get() > 0)
        	asyncWaiters = new LinkedList<AsyncWaiter>();
	}
	
    /**
	 * Asynchronous Task-based Asynchronous Pattern (TAP) interface.
	 */

    /**
	 * Wait asynchronously for the latch to open enabling, optionally, a timeout
	 * and/or cancellation.
	 */
    private CompletableFuture<Boolean> doAwaitAsync(boolean timed, long timeout, TimeUnit unit) {
		// We get the count value with a "volatile read", so the visibility is guaranteed
		if (count.get() == 0)
			return trueFuture;
		synchronized(theLock) {
			// After acquire the lock we must re-check the latch state, because
			// however it may have been opened by another thread.
			if (count.get() == 0)
				return trueFuture;

            // If the wait was specified as immediate, return failure
            if (timed && timeout == 0)
				return falseFuture;
			
			// Create a request node and insert it in the async waiters queue
			AsyncWaiter awaiter = new AsyncWaiter();
			asyncWaiters.addLast(awaiter);
		
			/**
			 * If a timeout was specified, start a timer.
			 * Since that all paths of code that cancel the timer execute on other
			 * thread that must aquire the lock, we has the guarantee that the field
			 * "acquirer.timer" is correctly set when the method AsyncAcquire.close()
			 * is called.
			 */
			if (timed)
				awaiter.timer = Delayer.delay(awaiter, timeout, unit);
			return awaiter;
		}
	}

	/**
	 * Wait until latch opens asynchronously unconditionally.
	 */
	public CompletableFuture<Boolean> awaitAsync() {
		return doAwaitAsync(false, 0L, null);
	}

	/**
	 * Wait until latch opens asynchronously enabling the timeout.
	 */
	public CompletableFuture<Boolean> awaitAsync(long timeout, TimeUnit unit) {
		return doAwaitAsync(true, timeout, unit);
	}

	/**
	 * Returns the latch state
	 */
	public boolean isOpen() { return open; }

    /**
	 *	Synchronous interface implemented using the asynchronous TAP interface.
	 */
	
	 /**
	 * Try to cancel an asynchronous request identified by its CF<>.
	 */
	boolean tryCancelAwaitAsync(CompletableFuture<Boolean> awaiterFuture) {
		AsyncWaiter awaiter = (awaiterFuture instanceof AsyncWaiter) ? (AsyncWaiter)awaiterFuture : null;
		boolean complete = false;
		if (awaiter == null)
			throw new IllegalArgumentException("awaiterFuture");
		synchronized(theLock) {		
			if (!awaiter.done) {
				// Remove the async request from the queue and mark it as completed 
				asyncWaiters.remove(awaiter);
				complete = awaiter.done = true;
			}
		}
		if (complete) {
			// Release resources and complete the CompletableFuture<Boolean>
			awaiter.close();
			awaiter.completeExceptionally(new CancellationException());
			return true;
		}
		return false;
	}

    /**
	 * Wait synchronously for the latch to open enabling, optionally,
	 * timeout and/or cancellation.
	 */
    private boolean doAwait(boolean timed, long timeout, TimeUnit unit) throws InterruptedException {
		CompletableFuture<Boolean> awaitFuture = doAwaitAsync(timed, timeout, unit); 
		try {
            return awaitFuture.get();
        } catch (InterruptedException ie) {
			// Try to cancel the async await
			if (tryCancelAwaitAsync(awaitFuture))
				throw ie;
			
			// Here, we known that the request was already completed.
			// Return the underlying result, filtering any possible interrupts.
			try {
				do {
					try {
						return awaitFuture.get();
					} catch (InterruptedException ie2) {
						// While waiting for result, we filter all interrupts
					} catch (Throwable ex2) {
						// We never get here, because we never complete the CF<> exceptionally.
					}
				} while (true);
            } finally {
				// Anyway, re-assert the interrupt
                Thread.currentThread().interrupt();
            }
        } catch (Throwable ex) {
			// We never get here, because we never complete the CF<> exceptionally.
		}
		return false;
	}

	/**
	 * Wait until latch opens synchronously unconditionally.
	 */
	public boolean await() throws InterruptedException {
		return doAwait(false, 0L, null);
	}

	/**
	 * Wait until latch opens synchronously enabling the timeout.
	 */
	public boolean await(long timeout, TimeUnit unit) throws InterruptedException {
		return doAwait(true, timeout, unit);
	}
	
	/**
	 * Registers one or more signals with the CountdownLatch, decrementing the
	 * value of CurrentCount by the specified amount.
	 */
	public boolean signal(int signalCount) {
		if (signalCount < 1)
			throw new IllegalArgumentException("signalCount");
		int c;
		do {
			c = count.get();
			if (c == 0 || signalCount > c)
				throw new  IllegalStateException();
		} while (!count.compareAndSet(c, c - signalCount));
		if (c > signalCount)
			return false;
		
		// The latch is now open, release all async waiters.
		// A list to hold temporarily the async waiters to complete later
		// after release the lock.

		// A list to hold temporarily the async waiters to complete
		// later without owning the lock.
		LinkedList<AsyncWaiter> completed = null;
		synchronized(theLock) {
			if (asyncWaiters.size() > 0)
				completed = asyncWaiters;
			asyncWaiters = null;
		}
		// Complete the tasks of the async waiters without owning the lock
		if (completed != null) {
			for (AsyncWaiter awaiter : completed) {
				awaiter.close();
				awaiter.complete(true);
			}
		}
		return true;
	}

	public boolean signal() { return signal(1); }

	/**
	 * Increments the CurrentCount by a specified value.
	 */
	public void addCount(int signalCount) {
		if (signalCount < 1)
			throw new IllegalArgumentException("signalCount");
		int c;
		do {
			c = count.get();
			if (c == 0 || c + signalCount < c)
				throw new IllegalStateException();
		} while (!count.compareAndSet(c, c + signalCount));
	}

	public void  addCount() { addCount(1); }

	/**
	 * Attempts to increment CurrentCount by a specified value.
	 */
	public boolean tryAddCount(int signalCount) {
		if (signalCount < 1)
			throw new IllegalArgumentException("signalCount");
		int c;
		do {
			c = count.get();
			if (c == 0)
				return false;
			if (c + signalCount < c)
				throw new IllegalStateException();
		} while (!count.compareAndSet(c, c + signalCount));
		return true;
	}

	public boolean tryAddCount() { return tryAddCount(1); }

	/**
	 * Gets the number of remaining signals required to open the latch.
	 */ 
	public int getCurrentCount() { return count.get(); }

	/**
	 * Gets the numbers of signals initially required to set the latch.
	 */
	public int getInitialCount() { return initialCount; }

	/**
	 * Indicates whether the count down latch's current count has reached zero.
	 */
	public boolean isSet() { return count.get() == 0; }

	/**
	 * Entry point to run tests
	 */

	public static void main(String[] args) throws InterruptedException {
		CountDownLatchAsyncTests.testWaitAsync();	}

}

/**
 * Test code
 */
class CountDownLatchAsyncTests {
	static final int SETUP_TIME = 50;
	static final int UNTIL_OPEN_TIME = 500;
	static final int THREAD_COUNT = 10;
	static final int EXIT_TIME = 100;
	static final int WAIT_ASYNC_TIMEOUT = 100;

	static void Log(String msg) {
		System.out.printf("[#%02d]: %s\n", Thread.currentThread().getId(), msg);
	}

	public static void testWaitAsync() throws InterruptedException {
		CountDownLatchAsync cdl = new CountDownLatchAsync(1);
		Thread[] waiters = new Thread[THREAD_COUNT];
		boolean timed = false;
		long timeout = WAIT_ASYNC_TIMEOUT /* + UNTIL_OPEN_TIME */;

		for (int i = 0; i < THREAD_COUNT; i++) {
			int li = i;

			waiters[i] = new Thread(() -> {
				Log(String.format("--[#%02d]: waiter thread started", li));
				if (li > 0)
					cdl.addCount();
				try {
					CompletableFuture<Boolean> awaitFuture;
					if (timed)
						awaitFuture = cdl.awaitAsync(timeout, TimeUnit.MILLISECONDS);
					else
						awaitFuture = cdl.awaitAsync();
					Log(String.format("--[#%02d]: returned from async await", li));
					try {
						if (awaitFuture.get())
							Log(String.format("--[#%02d]: count down latch opened", li));
						else
							Log(String.format("--[#%02d]: awaitAsync() timed out", li));
					} catch (ExecutionException ee) {
						// We never get here
					}
				} catch (InterruptedException ie) {
					Log(String.format("--[#%02d]: awaiter thread was interrupted"));
				}		
			});
			waiters[i].start();
		}

		Thread.sleep(SETUP_TIME + UNTIL_OPEN_TIME);

		for (int i = 0; i < THREAD_COUNT; i++)
			cdl.signal();
		
		Thread.sleep(EXIT_TIME);

		for (int i = 0; i < THREAD_COUNT; i++) {
			if (waiters[i].isAlive())
				waiters[i].interrupt();
			waiters[i].join();
		}
		Log("--test terminated");
	}
}



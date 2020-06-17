/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Boolean latch with asynchronous and synchronous interfaces
 *
 *  Carlos Martins, November 2019
 *
 **/

import java.util.LinkedList;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

/**
 * A boolean latch with asynchronous ans synchronous interfaces
 */
public class BooleanLatchAsync {
			
	// Type used to represent each asynchronous waiter
	private class AsyncWaiter extends CompletableFuture<Boolean> implements Runnable {
		private ScheduledFuture<?> timer;	// the timeout timer, if any
		private final AtomicBoolean lock = new AtomicBoolean(false);	// the lock

		/**
		 * This is the timeout cancellation handler
		 */
		@Override
		public void run() {
			if (tryLock()) {
				// We must acquire the lock in order to try to remove the
				// request from the queue
				synchronized(theLock) {
					asyncWaiters.remove(this);
				}
				// Release resources and complete CF<> with false result.
				close();
				complete(false);
			}
		}

		/**
		 * Tries to lock the waiter in order to satisfy or cancel it.
		 */
		boolean tryLock() {
			return !lock.get() && lock.compareAndSet(false, true);
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
    public BooleanLatchAsync(boolean open) {
		this.open = open;
		// If the latch is initialized as closed, initialize the wait list.
		if (!open)
        	asyncWaiters = new LinkedList<AsyncWaiter>();
    }

	public BooleanLatchAsync() { this(false); }
		

    /**
	 * Asynchronous Task-based Asynchronous Pattern (TAP) interface.
	 */

    /**
	 * Wait asynchronously for the latch to open enabling, optionally, a timeout
	 * and/or cancellation.
	 */
    private CompletableFuture<Boolean> doAwaitAsync(boolean timed, long timeout, TimeUnit unit) {
		// The "open" field is volatile, so the visibility is guaranteed
		if (open)
			return trueFuture;
		synchronized(theLock) {
			// After acquire the lock we must re-check the latch state, because
			// however it may have been opened by another thread.
			if (open)
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
			 * threads and must aquire the lock, we has the guarantee that the field
			 * "acquirer.timer" is correctly set when the method AsyncAcquire.close()
			 * is called.
			 */
			if (timed)
				awaiter.timer = Delayer.delay(awaiter, timeout, unit);
			return awaiter;
		}
	}
	
	/**
	 * Open the latch
	 */
	public void open() {
		// If the latch is already open return
		if (open)
			return;
		open = true;		// no more waits
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
		if (awaiter == null)
			throw new IllegalArgumentException("awaiterFuture");
		if (awaiter.tryLock()) {
			synchronized(theLock) {
				asyncWaiters.remove(awaiter);	// no operation if object is not in the list
			}
			// Release resources and complete the CF<>
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

	public static void main(String[] args) throws InterruptedException {
		BooleanLatchAsyncTests.testWaitAsync();
    }
}

/**
 * Test code
 */

class BooleanLatchAsyncTests {
	static final int SETUP_TIME = 50;
	static final int UNTIL_OPEN_TIME = 500;
	static final int THREAD_COUNT = 10;
	static final int EXIT_TIME = 100;
	static final int WAIT_ASYNC_TIMEOUT = 100;

	static void Log(String msg) {
		System.out.printf("[#%02d]: %s\n", Thread.currentThread().getId(), msg);
	}

	public static void testWaitAsync() throws InterruptedException {
		BooleanLatchAsync latch = new BooleanLatchAsync();
		Thread[] waiters = new Thread[THREAD_COUNT];
		boolean timed = false;
		long timeout = WAIT_ASYNC_TIMEOUT  /* UNTIL_OPEN_TIME */;

		for (int i = 0; i < THREAD_COUNT; i++) {
			int li = i;

			waiters[i] = new Thread(() -> {
				Log(String.format("--[#%02d]: waiter thread started", li));
				try {
					CompletableFuture<Boolean> awaitFuture;
					if (timed)
						awaitFuture = latch.awaitAsync(timeout, TimeUnit.MILLISECONDS);
					else
						awaitFuture = latch.awaitAsync();
					Log(String.format("--[#%02d]: returned from async await", li));
					try {
						if (awaitFuture.get())
							Log(String.format("--[#%02d]: latch opened", li));
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
		
		latch.open();

		Thread.sleep(EXIT_TIME);

		for (int i = 0; i < THREAD_COUNT; i++) {
			if (waiters[i].isAlive())
				waiters[i].interrupt();
			waiters[i].join();
		}
		Log("--test terminated");
	}
}



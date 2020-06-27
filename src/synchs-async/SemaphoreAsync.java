
/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Semaphore with asynchronous interface (based on CompletableFuture<>)
 *
 *  Carlos Martins, June 2020
 * 
 **/

import java.util.List;
import java.util.ArrayList;
import java.util.LinkedList;
import java.util.Random;
import java.io.IOException;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

/**
 * A semaphore with asynchronous and synchronous interfaces
 */
public class SemaphoreAsync {

	/**
	 * The type used to represent each pending async acquire
	 */
	private class AsyncAcquire extends CompletableFuture<Boolean> implements Runnable {
		final int acquires; 			// the number of the requested permits
		ScheduledFuture<?> timer;		// timeout's timer
		boolean done;					// true when the async request was completed or cancelled
		
		/**
		 * Construct a acquire request object
		 */
		AsyncAcquire(int acquires) {
			this.acquires = acquires;
		}

		/**
		 * This is the timeout cancellation handler
		 */
		 @Override
		public void run() {
			List<AsyncAcquire> satisfied = null;
			boolean complete = false;
			synchronized(theLock) {
				if (!done) {
					asyncAcquires.remove(this);
					complete = done = true;
					if (asyncAcquires.size() > 0 && permits >= asyncAcquires.peek().acquires)
						satisfied = satisfyPendingAsyncAcquires();
				}
			}
			if (complete) {
				// Complete previously satisfied requests
				completeSatisfiedAsyncAcquires(satisfied);
				// Complete this completable future with false, indicating timeout
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
	
	// The lock that synchronizes access to the shared mutable state
 	private final Object theLock;

	// The available number of permits	
	private int permits;

	// The maximum number of permits
	private final int maxPermits;

	// The queue of pending asynchronous acquires
	private final LinkedList<AsyncAcquire> asyncAcquires;

	//  Completed futures used to return true and false results and IllegalArgumentException
    private static final CompletableFuture<Boolean> trueFuture = CompletableFuture.completedFuture(true);
	private static final CompletableFuture<Boolean> falseFuture = CompletableFuture.completedFuture(false);
	private static final CompletableFuture<Boolean> illegalArgExceptionFuture =
			 		CompletableFuture.failedFuture(new IllegalArgumentException("acquires"));
	
	/**
     * Constructors
     */
    public SemaphoreAsync(int initial, int maximum) {
		// Validate arguments
		if (initial < 0 || initial > maximum)
			throw new IllegalArgumentException("initial");
		if (maximum <= 0)
			throw new IllegalArgumentException("maximum");
		// Initialize semaphore's fields
		this.theLock = new Object();
		this.maxPermits = maximum;
		this.permits = initial;
		this.asyncAcquires = new LinkedList<>();
	}
	
	public SemaphoreAsync(int initial) { this(initial, Integer.MAX_VALUE); }
	
	public SemaphoreAsync() { this(0, Integer.MAX_VALUE); }

	/**
	 * Auxiliary methods
	 */

	/**
	 *  Satisfy all pending async requests that can now acquire the desired permits.
	 *
	 * Note: This method is called when the current thread *does* own the lock.
	 */
	private List<AsyncAcquire> satisfyPendingAsyncAcquires() {
		List<AsyncAcquire> satisfied = null;
		while (asyncAcquires.size() > 0) {
			AsyncAcquire acquirer = asyncAcquires.peek();
			if (permits < acquirer.acquires)
				break;
			// Remove the request from the queue
			asyncAcquires.removeFirst();
			// Update state and set done
			permits -= acquirer.acquires;
			acquirer.done = true;
			if (satisfied == null)
				satisfied = new ArrayList<>(1);
			satisfied.add(acquirer);
		}
		return satisfied;
	}

	/**
	 * Complete the tasks associated to the satisfied requests.
	 *
	 * Note: This method is called when calling thread *does not* own the lock.
	 */
	private void completeSatisfiedAsyncAcquires(List<AsyncAcquire> toComplete) {
		if (toComplete != null) {
			for (AsyncAcquire acquirer : toComplete) {
				// release allocated resources and complte the underlying future
				acquirer.close();
				acquirer.complete(true);
			}
		}
	}
		
	/**
	 * Try to cancel an asynchronous request identified by the underlying
	 * completable future.
	 * 
	 * Note: This method is used to cancel asynchronous acquires and also to
	 *       implement the synchronous interface.
	 */
	public boolean tryCancelAcquireAsync(CompletableFuture<Boolean> acquireFuture) {
		AsyncAcquire acquirer = (acquireFuture instanceof AsyncAcquire) ? (AsyncAcquire)acquireFuture : null;
		List<AsyncAcquire> satisfied = null;
		boolean complete = false;
		if (acquirer == null)
			throw new IllegalArgumentException("acquireFuture");
		synchronized(theLock) {
			if (!acquirer.done) {
				asyncAcquires.remove(acquirer);	// no operation if object is not in the list
				complete = acquirer.done = true;
				// Check to see if now, we ca satisfy other pending async acquires
				if (asyncAcquires.size() > 0 && permits >= asyncAcquires.peek().acquires)
					satisfied = satisfyPendingAsyncAcquires();
			}
		}
		if (complete) {
			// Complete the CompletableFutures of satisfied requests
			completeSatisfiedAsyncAcquires(satisfied);
			
			// Dispose the timer resoure and complete completable future in cancelled state
			acquirer.close();
			acquirer.cancel(false);
			return true;
		}
		return false;
	}

	/**
	 * Asynchronous interface based on CompletableFuture<Boolean>.
	 */

	/**
	 * Base method to acquire asyncronously the specified number of permits
	 * enabling, optionally, the timeout.
	 */
	private CompletableFuture<Boolean> doAcquireAsync(int acquires, boolean timed, long timeout, TimeUnit unit) {
		// Validate  the argument acquires
		if (acquires < 1 || acquires > maxPermits)
			return illegalArgExceptionFuture;
				
		synchronized(theLock) {
			// If the queue is and there are sufficient permits, update the
			// permits a returna a CompletableFuture<> completed with true.
			if (asyncAcquires.size() == 0 && permits >= acquires) {
				permits -= acquires;
				return trueFuture;
			}
			// The current request must be pending, so we must check for immediate timeout
			if (timed && timeout == 0)
				return falseFuture;

			// Create an async acquire object and insert it in the pending queue
			AsyncAcquire acquirer = new AsyncAcquire(acquires);
			asyncAcquires.addLast(acquirer);

			/**
			 * If a timeout was specified, start a timer.
			 * Since that all paths of code that cancel the timer execute on other
			 * threads and must aquire the lock, we has the guarantee that the field
			 * "acquirer.timer" is correctly set when the method AsyncAcquire.close()
			 * is called.
			 */
			if (timed)
				acquirer.timer = Delayer.delay(acquirer, timeout, unit);
			return acquirer;
		}
	}

	/**
	 * Release the specified number of permits.
	 */
	public void release(int releases) {
		List<AsyncAcquire> satisfied = null;
		synchronized(theLock) {
			if (releases + permits < permits || permits + releases > maxPermits)
				throw new IllegalStateException("Exceeded the maximum number of permits");
			permits += releases;
			satisfied = satisfyPendingAsyncAcquires();
		}
		// After we release the lock do the cleanup and complete the released
		// CompletableFutures.
		// We do this after release the lock to prevent reentrancy when synchronous
		// continuations are executed.
		completeSatisfiedAsyncAcquires(satisfied);
	}
	
	/**
	 * Acquire multiple permits asynchronously unconditionally.
	 */
	public CompletableFuture<Boolean> acquireAsync(int acquires) {
		return doAcquireAsync(acquires, false, 0L, null);
	}

	/**
	 * Acquire multiple permit asynchronously enabling the timeout.
	 */
	public CompletableFuture<Boolean> acquireAsync(int acquires, long timeout, TimeUnit unit) {
		return doAcquireAsync(acquires, true, timeout, unit);
	}

	/**
	 * Acquire one permit asynchronously unconditionally.
	 */
	public CompletableFuture<Boolean> acquireAsync() {
		return doAcquireAsync(1, false, 0L, null);
	}

	/**
	 * Acquire one permit asynchronously enabling timeout.
	 */
	public CompletableFuture<Boolean> acquireAsync(long timeout, TimeUnit unit) {
		return doAcquireAsync(1, true, timeout, unit);
	}

	/**
	 * Tries to acquire one permit asynchronously
	 */
    public CompletableFuture<Boolean> tryAcquireAsync() {
		return doAcquireAsync(1, true, 0, null);
	}
	
	/**
	 * Release one permit
	 */
	public void release() { release(1);	}

    /**
	 *	Synchronous interface based on the asynchronous interface
	 */

	/**
	 * Acquire multiple permits synchronously enabling optionally the timeout.
	 */
	private boolean doAcquire(int acquires, boolean timed, long timeout, TimeUnit unit) 
							  throws InterruptedException {
		if (Thread.interrupted())
				throw new InterruptedException();
		 
		CompletableFuture<Boolean> acquireFuture = doAcquireAsync(acquires, timed, timeout, unit); 
		try {
			return acquireFuture.get();
		} catch (InterruptedException ie) {
			// Try to cancel the asynchronous acquire request
			if (tryCancelAcquireAsync(acquireFuture))
				throw ie;
			// The request was already completed or cancelled, so return
			// the proper result. When waiting for the result we must discard
			// any interrupt.
			try {
				do {
					try {
						return acquireFuture.get();
					} catch (InterruptedException ie2) {
						// Ignore further interrupts
					} catch (ExecutionException eex) {
						// Throw the IllegalArgumentException that caused the ExecutionException. 
						throw (IllegalArgumentException)eex.getCause();		
					}
				} while (true);
			} finally {
				// anyway, re-assert the interrupt
				Thread.currentThread().interrupt();
			}
		} catch (ExecutionException eex) {
			// Throw the IllegalArgumentException that caused the ExecutionException. 
			throw (IllegalArgumentException)eex.getCause();		
		}
	}

	/**
	 * Acquire multiple permits synchronously enabling the timeout.
	 */
	public boolean acquire(int acquires, long timeout, TimeUnit unit) throws InterruptedException {
		return doAcquire(acquires, true, timeout, unit);
	}

	/**
	 * Acquire multiple permits synchronously unconditionally.
	 */
	public boolean acquire(int acquires) throws InterruptedException {
		return doAcquire(acquires, false, 0L, null);
	}

	/**
	 * Acquire one permit synchronously enabling the timeout. 
	 */
	public boolean acquire(long timeout, TimeUnit unit) throws InterruptedException {
		return doAcquire(1, true, timeout, unit);
	}

	/**
	 * Acquire one permit synchronously unconditionally.
	 */
	public boolean acquire() throws InterruptedException {
		return doAcquire(1, false, 0L, TimeUnit.MILLISECONDS);
	}

	/**
	 * Run the tests
	 */

	public static void main(String... args) throws Throwable {
		SemaphoreAsyncTests.runTests();
	}
}

/**
 * A blocking queue with asynchonous and synchronous interfaces
 */
class BlockingQueueAsync<T> {
	private final ConcurrentLinkedQueue<T> data;
	private final SemaphoreAsync freeSlots, filledSlots;

	// Construct a blocking queue with the specified capacity
	public BlockingQueueAsync(int capacity) {
		data = new ConcurrentLinkedQueue<>();
		freeSlots = new SemaphoreAsync(capacity, capacity);
		filledSlots = new SemaphoreAsync(0, capacity);
	}

	// Construct a blocking queue with unbounded capacity
	public BlockingQueueAsync() { this(Integer.MAX_VALUE); }

	/**
	 * Asynchronous interface
	 */

	/**
	 * Put an item in the queue asynchronously enabling optionally the timeout.
	 */
	private CompletableFuture<Boolean> doPutAsync(final T item, boolean timed,
												  long timeout, TimeUnit unit) {
		CompletableFuture<Boolean> freeFuture = timed ? freeSlots.acquireAsync(timeout, unit)
												      : freeSlots.acquireAsync();
		return freeFuture.thenApply((result) -> {
			if (result) {
				data.add(item);
				filledSlots.release();
				return true;
			}
			return false;
		});
	}

	/**
	 * Put an item in the queue asynchronously enabling, optionally the timeout. 
	 */
	public CompletableFuture<Boolean> putAsync(final T item, long timeout, TimeUnit unit) {
		return doPutAsync(item, true, timeout, unit);
	}

	/**
	 * Put an item in the queue asynchronously unconditionally. 
     */
	public CompletableFuture<Boolean> putAsync(T item) {
		return doPutAsync(item, false, 0L, null);
	}

	/**
	 * Take an item from the queue asynchronously enabling, optionally, the timeout. 
	 */
	private CompletableFuture<T> doTakeAsync(boolean timed, long timeout, TimeUnit unit) {
		CompletableFuture<Boolean> filledFuture = timed ? filledSlots.acquireAsync(timeout, unit)
													    : filledSlots.acquireAsync();
		return filledFuture.thenApply((result) -> {
			if (result) {
				T item = data.poll();
				freeSlots.release();
				return item;
			}
			return null;
		});
	}

	/**
	 * Take an item from the queue asynchronously enabling the timeout. 
	 */
	public CompletableFuture<T> takeAsync(long timeout, TimeUnit unit) {
		return doTakeAsync(true, timeout, unit);
	}
	/**
	 * Take an item from the queue asynchronously unconditionally.
	 */
	public CompletableFuture<T> takeAsync() {
		return doTakeAsync(false, 0L, null);
	}

	/**
	 * Synchronous interface
	 */

	/**
	 * Put an item in the queue synchronously enabling, optionally, the timeout
	 */
	private boolean doPut(final T item, boolean timed, long timeout, TimeUnit unit)
						    throws InterruptedException, IllegalStateException {
		boolean acquired = true;
		if (timed)
			acquired = freeSlots.acquire(timeout, unit);
		else
			freeSlots.acquire();
		if (acquired) {
			data.add(item);
			filledSlots.release();
			return true;
		}
		return false;
	}

	/**
	 * Put an item in the queue synchronously enabling, optionally, the timeout.
	 */
	public boolean put(T item, long timeout, TimeUnit unit)
						 throws InterruptedException, IllegalStateException {
		return doPut(item, true, timeout, unit);
	}
	
	// Put an item in the queue synchronously unconditionally
	public boolean put(T item) throws InterruptedException, IllegalStateException {
		return doPut(item, false, 0L, null);
	}

	/**
	 * Take an item from the queue asynchronously enabling, optionally, the timeout.
	 */
	private T doTake(boolean timed, long timeout, TimeUnit unit)
					throws InterruptedException, IllegalStateException {
		boolean filled = true;
		if (timed)
			filled = filledSlots.acquire(timeout, unit);
		else
			filledSlots.acquire();
		if (filled) {
			T item = data.poll();
			freeSlots.release();
			return item;
		}
		return null;
	}

	/**
	 * Take an item from the queue asynchronously enabling the timeout.
	 */
	public T take(long timeout, TimeUnit unit) throws InterruptedException, IllegalStateException {
		return doTake(true, timeout, unit);
	}

	/**
	 * Take an item from the queue unconditionally.
	 */
	public T take() throws InterruptedException, IllegalStateException {
		return doTake(false, 0L, null);
	}

	// Returns the number of filled positions in the queue
	public int size() { return data.size(); }
}

/**
 * Test code
 */
class SemaphoreAsyncTests {

	/**
	 * Auxiliary methods
	 */

	// Sleep filtering InterruptedException
	static void sleep(long time, TimeUnit unit) {
		try {
			Thread.sleep(unit.toMillis(time));
		} catch (InterruptedException ie) {}
	}

	// Sleep filtering InterruptedException
	static void sleep(long timeMillis) { sleep(timeMillis, TimeUnit.MILLISECONDS); }

	// Test the semaphore as a mutual exclusion lock using synchronous acquires
	static int sharedCounter = 0;
	static boolean testSemaphoreAsLockSync() throws InterruptedException, IOException {
		final int SETUP_TIME = 50;
		final int RUN_TIME = 10 * 1000;
		final int THREADS = 50;
		final int MIN_TIMEOUT = 1;
		final int MAX_TIMEOUT = 5;

		Thread[] tthrs = new Thread[THREADS];
		int[] privateCounters = new int[THREADS];
		int[] timeouts = new int[THREADS];
		int issuedInterrupts = 0;
		int[] sensedInterrupts = new int[THREADS];
		AtomicBoolean exit = new AtomicBoolean(false);
		CountDownLatch start = new CountDownLatch(1);
		SemaphoreAsync lock = new SemaphoreAsync(1, 1);

		/**
		 * Create and start acquirer/releaser threads
		 */
		
		for (int i = 0; i < THREADS; i++) {
			final int tid = i;
			tthrs[i] = new Thread(() -> {
				Random rnd = new Random(Thread.currentThread().getId());
				try {
					start.await();
				} catch (InterruptedException ie) {}
				while (!exit.get()) {
					try {
                        if (!lock.acquire(rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT, TimeUnit.MILLISECONDS)) {
							timeouts[tid]++;
							continue;
						}
                    } catch (InterruptedException ie) {
						sensedInterrupts[tid]++;
						continue;
					} catch (Throwable error) {
						// This never happens
					}
					if (Thread.interrupted())
                      	sensedInterrupts[tid]++;
                    sharedCounter++;
					if (rnd.nextInt(100) < 95) {
						Thread.yield();
					} else {
						try {
							Thread.sleep(rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT);
						} catch (InterruptedException ie) {
							sensedInterrupts[tid]++;
						}							
					}
					// release the lock
					lock.release();
					if ((++privateCounters[tid] % 100) == 0)
						System.out.printf("[#%02d]", tid);
				}
				if (Thread.interrupted())
					sensedInterrupts[tid]++;
			});
			tthrs[i].start();
		}
		// sleep for a while and start tes
		Thread.sleep(SETUP_TIME);
		long startTime = System.currentTimeMillis();
		start.countDown();
		Random grnd = new Random(Thread.currentThread().getId());
		//...
		do {
			Thread.sleep(grnd.nextInt(50));
			/*
			if (THREADS > 1) {
				tthrs[grnd.nextInt(THREADS)].interrupt();
				issuedInterrupts++;
			}
			*/
			if (System.in.available() > 0) {
				System.in.read();
				break;
			}
		} while (System.currentTimeMillis() < startTime + RUN_TIME);
		long elapsedTime = System.currentTimeMillis() - startTime;
		// set exit flag
		exit.set(true);
		// Wait until all threads have been terminated.
		for (int i = 0; i < THREADS; i++)
			tthrs[i].join();
	
		// Compute final results
		
		System.out.println("\nPrivate counters:");
		int totalAcquisitons = 0, totalInterrupts = 0;
		for (int i = 0; i < THREADS; i++) {
           	totalAcquisitons += privateCounters[i];
           	totalInterrupts += sensedInterrupts[i];
           	if (i != 0 && (i % 2) == 0) {
				System.out.println();
			} else if (i != 0) {
				System.out.print(' ');
			}
			System.out.printf("[#%02d: %d/%d/%d]", i,
				 		privateCounters[i], timeouts[i], sensedInterrupts[i]);
		}
		System.out.printf("%n--shared/private: %d/%d%n", sharedCounter, totalAcquisitons);
		System.out.printf("--interrupts issuded/sensed: %d/%d%n", issuedInterrupts, totalInterrupts);
        long unitCost = (elapsedTime * 1000000L) / sharedCounter;
		
		System.out.printf("--time per acquisition/release: %d %s%n",
					 			unitCost >= 1000 ? unitCost / 1000 : unitCost,
					 			unitCost >= 1000 ? "us" : "ns");
		return totalAcquisitons == sharedCounter;
	}

	// test semaphore as a mutual exclusion lock using asynchronous acquires
	static boolean testSemaphoreAsLockAsync() throws Throwable {

		final int SETUP_TIME = 50;
		final int RUN_TIME = 10 * 1000;
		final int THREADS = 20;
		final int MIN_TIMEOUT = 0;
		final int MAX_TIMEOUT = 50;
		final int MIN_ACQUIRE_INTERVAL = 1;
		final int MAX_ACQUIRE_INTERVAL = 10;


		final Thread[] tthrs = new Thread[THREADS];
		final int[] privateCounters = new int[THREADS];
		final int[] timeouts = new int[THREADS];
		final AtomicBoolean exit = new AtomicBoolean(false);
		final CountDownLatch start = new CountDownLatch(1);
		final SemaphoreAsync lock = new SemaphoreAsync(1, 1);

		//
		// Create and start acquirer/releaser threads
		//
		
		for (int i = 0; i < THREADS; i++) {
			final int tid = i;
			tthrs[i] = new Thread(() -> {
				try {
					start.await();
				} catch (InterruptedException ie) {}
				Random rnd = new Random(tid);
				while (!exit.get()) {
					try {
						CompletableFuture<Boolean> acquireFuture =
								lock.acquireAsync(rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT, TimeUnit.MILLISECONDS);
						CompletableFuture<Boolean> doneFuture = acquireFuture.thenApplyAsync((result) ->{
							if (result) {
								sharedCounter++;
								if (rnd.nextInt(100) < 95)
									Thread.yield();
								else
									sleep(rnd.nextInt(MAX_ACQUIRE_INTERVAL) + MIN_ACQUIRE_INTERVAL);
								lock.release();
							} else {
								timeouts[tid]++;
							}
							return result;
						});
						if (!doneFuture.get()) {
							timeouts[tid]++;
							continue;
						}
					} catch (Throwable ex) {
							System.out.printf("*** Exception: %s%n", ex.getClass());
					}
					if (++privateCounters[tid] % 100 == 0)
						System.out.printf("[#%02d]", tid);
				}
			});
			tthrs[i].start();
		}
		
		Thread.sleep(SETUP_TIME);
		long startMillis = System.currentTimeMillis();
		start.countDown();
		// excute RUN_TIME or until a key is pressed
		do {
			sleep(20);
			if (System.in.available() > 0) {
				System.in.read();
				break;
			}
		} while (System.currentTimeMillis() < startMillis + RUN_TIME);
		long elapsedMillis = System.currentTimeMillis() - startMillis;
		exit.set(true);				
		// Wait until all threads have been terminated.
		for (int i = 0; i < THREADS; i++)
			tthrs[i].join();
		
		// Compute results
		
		System.out.println("\n\nPrivate counters:");
		int totalAcquisitions = 0;
		for (int i = 0; i < THREADS; i++) {
			totalAcquisitions += privateCounters[i];
			if (i != 0 && i % 2 == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[#%02d: %d/%d]", i, privateCounters[i], timeouts[i]);
		}
		System.out.println();
		System.out.printf("--total acquisitions/releases: %d/%d\n", totalAcquisitions, sharedCounter);
		long unitCost = (elapsedMillis * 1000000L) / sharedCounter;
		System.out.printf("--unit cost of acquire/release: %d %s%n",
							unitCost > 1000 ? unitCost / 1000 : unitCost,
							unitCost > 1000 ? "us" : "ns");
		return totalAcquisitions == sharedCounter;
	}

	// Use the semaphore in a producer/consumer context using a synchronous
	// blocking queue based on asynchronous semaphores

	static boolean testSemaphoreInAProducerConsumerContextSync() throws Exception {

		final int RUN_TIME = 10 * 1000;
		final int EXIT_TIME = 50;
		final int PRODUCER_THREADS = 20;
		final int CONSUMER_THREADS = 20;
		final int QUEUE_SIZE = 1; // (PRODUCER_THREADS / 2) + 1;
		final int MIN_TIMEOUT = 5;
		final int MAX_TIMEOUT = 100;
		final int MIN_PAUSE_INTERVAL = 10;
		final int MAX_PAUSE_INTERVAL = 100;
		final int PRODUCTION_ALIVE = 2500;
		final int CONSUMER_ALIVE = 2500;

		final Thread[] pthrs = new Thread[PRODUCER_THREADS];
		final Thread[] cthrs = new Thread[CONSUMER_THREADS];
		final int[] productions = new int[PRODUCER_THREADS];
		final int[] productionTimeouts = new int[PRODUCER_THREADS];
		final int[] consumptions = new int[CONSUMER_THREADS];
		final int[] consumptionTimeouts = new int[CONSUMER_THREADS];
		final AtomicBoolean exit = new AtomicBoolean(false);
		final BlockingQueueAsync<String> queue = new BlockingQueueAsync<String>(QUEUE_SIZE);

		// Create and start consumer threads.

		for (int i = 0; i < CONSUMER_THREADS; i++) {
			final int ctid = i;
			cthrs[i] = new Thread(() -> {
				Random rnd = new Random(ctid);
				while (!exit.get()) {
					try {
						String  take = queue.take(rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT, TimeUnit.MILLISECONDS);
						if (take == null) {
							consumptionTimeouts[ctid]++;
							continue;
						}
					} catch (InterruptedException ie) {
						break;
					} catch (IllegalStateException ise) {
						System.out.printf("***IllegalStateException at consumer: %s%n", ise.getMessage());
						continue;
					} catch (Throwable e) {
						System.out.printf("***Exception at consumer: %s%n", e.getClass());
						continue;
					}
					if (++consumptions[ctid] % CONSUMER_ALIVE == 0) {
						System.out.printf("[#c%02d]", ctid);
						try {
							Thread.sleep(rnd.nextInt(MAX_PAUSE_INTERVAL) + MIN_PAUSE_INTERVAL);
						} catch (InterruptedException ie) {
							break;
						}
					}
				}
			});
			cthrs[i].setPriority(Thread.MAX_PRIORITY);
			cthrs[i].start();
		}

		// Create and start producer threads.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			final int ptid = i;
			pthrs[i] = new Thread(() -> {
				Random rnd = new Random(ptid);
				while (!exit.get()) {
					String data = String.format("%d", rnd.nextInt(1000));
					try {
						boolean put = queue.put(data, rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT, TimeUnit.MILLISECONDS);
						if (!put) {
							productionTimeouts[ptid]++;
							continue;
						}
					} catch (InterruptedException ie) {
							break;
					} catch (IllegalStateException ise) {
						System.out.printf("***IllegalStateException at producer: %s%n", ise.getMessage());
						continue;
					} catch (Exception e) {
						System.out.printf("***Exception at producer: %s%n", e.getClass());
						continue;
					}
					if (++productions[ptid] % PRODUCTION_ALIVE == 0) {
						System.out.printf("[#p%02d]", ptid);
						try {
							Thread.sleep(rnd.nextInt(MAX_PAUSE_INTERVAL) + MIN_PAUSE_INTERVAL);
						} catch (InterruptedException ie) {
							break;
						}
					}
				}
			});
			pthrs[i].start();
		}
		// run the test for a while
		long endMillis = System.currentTimeMillis() + RUN_TIME;
		do {
			sleep(20);
			if (System.in.available() > 0) {
				System.in.read();
				break;
			}
		} while (System.currentTimeMillis() < endMillis);
		exit.set(true);
		sleep(EXIT_TIME);

		// Wait until all producer have been terminated.
		int totalProductions = 0;
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (pthrs[i].isAlive())
				pthrs[i].interrupt();
			pthrs[i].join();
			totalProductions += productions[i];
		}

		int totalConsumptions = 0;
		// Wait until all consumer have been terminated.
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (cthrs[i].isAlive())
				cthrs[i].interrupt();
			cthrs[i].join();
			totalConsumptions += consumptions[i];
		}

		// Display consumer results
		System.out.println("\n\nConsumer counters:");
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (i != 0 && i % 4 == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[#c%02d: %d/%d]", i, consumptions[i], consumptionTimeouts[i]);
		}

		// take into account not consumed productions
		totalConsumptions += queue.size();

		System.out.println("\n\nProducer counters:");
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (i != 0 && i % 4 == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[#p%02d: %d/%d]", i, productions[i], productionTimeouts[i]);
		}
		System.out.printf("%n--productions: %d, consumptions: %d", totalProductions, totalConsumptions);
		return totalConsumptions == totalProductions;
	}

	// Use the semaphore in a producer/consumer context using a asynchronous
	// blocking queue.

	static boolean testSemaphoreInAProducerConsumerContextAsync() throws Exception {

		final int RUN_TIME = 10 * 1000;
		final int EXIT_TIME = 50;
		final int PRODUCER_THREADS = 20;
		final int CONSUMER_THREADS = 20;
		final int QUEUE_SIZE = 1; // (PRODUCER_THREADS / 2) + 1;
		final int MIN_TIMEOUT = 1;
		final int MAX_TIMEOUT = 50;
		final int MIN_PAUSE_INTERVAL = 10;
		final int MAX_PAUSE_INTERVAL = 100;
		final int PRODUCTION_ALIVE = 2500;
		final int CONSUMER_ALIVE = 2500;

		final Thread[] pthrs = new Thread[PRODUCER_THREADS];
		final Thread[] cthrs = new Thread[CONSUMER_THREADS];
		final int[] productions = new int[PRODUCER_THREADS];
		final int[] productionTimeouts = new int[PRODUCER_THREADS];
		final int[] consumptions = new int[CONSUMER_THREADS];
		final int[] consumptionTimeouts = new int[CONSUMER_THREADS];
		final AtomicBoolean exit = new AtomicBoolean(false);
		final BlockingQueueAsync<String> queue = new BlockingQueueAsync<String>(QUEUE_SIZE);

		// Create and start consumer threads.

		for (int i = 0; i < CONSUMER_THREADS; i++) {
			final int ctid = i;
			cthrs[i] = new Thread(() -> {
				Random rnd = new Random(ctid);
				while (!exit.get()) {
					try {		
						CompletableFuture<String> take = queue.takeAsync(rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT, TimeUnit.MILLISECONDS);							
						if (take.get() == null) {
							consumptionTimeouts[ctid]++;
							continue;
						}
					} catch (InterruptedException ie) {
						break;
					} catch (ExecutionException ee) {
						System.out.printf("***ExecutionException at consumer: %s%n", ee.getMessage());
						continue;
					} catch (Exception e) {
						System.out.printf("***Exception at consumer: %s%n", e.getClass());
						continue;
					}
					if (++consumptions[ctid] % CONSUMER_ALIVE == 0) {
						System.out.printf("[#c%02d]", ctid);
						try {
							Thread.sleep(rnd.nextInt(MAX_PAUSE_INTERVAL) + MIN_PAUSE_INTERVAL);
						} catch (InterruptedException ie) {
							break;
						}
					}
				}
			});
			cthrs[i].setPriority(Thread.MAX_PRIORITY);
			cthrs[i].start();
		}

		// Create and start producer threads.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			final int ptid = i;
			pthrs[i] = new Thread(() -> {
				Random rnd = new Random(ptid);
				while (!exit.get()) {
					String data = String.format("%d", rnd.nextInt(1000));
					try {
							
						CompletableFuture<Boolean> put = queue.putAsync(data, rnd.nextInt(MAX_TIMEOUT) + MIN_TIMEOUT,
																		TimeUnit.MILLISECONDS);
						if (!put.get()) {
							productionTimeouts[ptid]++;
							continue;
						}
					} catch (InterruptedException ie) {
						break;
					} catch (ExecutionException ee) {
						System.out.printf("***ExecutionException at producer: %s%n", ee.getMessage());
						continue;
					} catch (Exception e) {
						System.out.printf("***Exception at producer: %s%n", e.getClass());
						continue;
					}
					if (++productions[ptid] % PRODUCTION_ALIVE == 0) {
						System.out.printf("[#p%02d]", ptid);
						try {
							Thread.sleep(rnd.nextInt(MAX_PAUSE_INTERVAL) + MIN_PAUSE_INTERVAL);
						} catch (InterruptedException ie) {
							break;
						}
					}
				}
			});
			pthrs[i].start();
		}
		// run the test for a while
		long endMillis = System.currentTimeMillis() + RUN_TIME;
		do {
			sleep(20);
			if (System.in.available() > 0) {
				System.in.read();
				break;
			}
		} while (System.currentTimeMillis() < endMillis);
		exit.set(true);
		sleep(EXIT_TIME);

		// Wait until all producer have been terminated.
		int totalProductions = 0;
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (pthrs[i].isAlive())
				pthrs[i].interrupt();
			pthrs[i].join();
			totalProductions += productions[i];
		}

		int totalConsumptions = 0;
		// Wait until all consumer have been terminated.
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (cthrs[i].isAlive())
				cthrs[i].interrupt();
			cthrs[i].join();
			totalConsumptions += consumptions[i];
		}

		// Display consumer results
		System.out.println("\n\nConsumer counters:");
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (i != 0 && i % 4 == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[#c%02d: %d/%d]", i, consumptions[i], consumptionTimeouts[i]);
		}

		// take into account not consumed productions
		totalConsumptions += queue.size();

		System.out.println("\n\nProducer counters:");
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (i != 0 && i % 4 == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[#p%02d: %d/%d]", i, productions[i], productionTimeouts[i]);
		}
		System.out.printf("%n--productions: %d, consumptions: %d", totalProductions, totalConsumptions);
		return totalConsumptions == totalProductions;
	}

	static void runTests() throws Throwable {
		/* */
		System.out.printf("\n-->test semaphore as lock using synchronous acquires: %s%n",
							SemaphoreAsyncTests.testSemaphoreAsLockSync() ? "passed" : "failed");
		/* */
		/* */
		System.out.printf("\n-->test semaphore as lock using asynchronous acquires: %s%n",
				SemaphoreAsyncTests.testSemaphoreAsLockAsync() ? "passed" : "failed");
		/* */
		/* */
		System.out.printf("\n-->test semaphore in a synchronous producer/consumer context: %s%n",
				SemaphoreAsyncTests.testSemaphoreInAProducerConsumerContextSync() ? "passed" : "failed");
		/* */
		/* */
		System.out.printf("\n-->test semaphore in a asynchronous producer/consumer context: %s%n",
				SemaphoreAsyncTests.testSemaphoreInAProducerConsumerContextAsync() ? "passed" : "failed");
		/* */
	}
}

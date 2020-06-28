/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Semaphore with fast-path optimization
 * 
 *  Carlos Martins, May 2020
 *
 **/

import java.util.Random;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.*;

public final class Semaphore {

	private final AtomicInteger permits;
	private volatile int waiters;
	private final Lock lock;
	private final Condition okToAcquire;
	 
	// Constructor
	public Semaphore(int initial) {
		if (initial < 0)
			throw new IllegalArgumentException();
		lock = new ReentrantLock();
		okToAcquire = lock.newCondition();
		permits = new AtomicInteger(initial);
	}
	
	public Semaphore() { this(0); }
	
	// tries to acquire one permit
	public boolean tryAcquire() {
		while (true) {
			int observedPermits = permits.get(); 
			if (observedPermits == 0)
				return false;
			if (permits.compareAndSet(observedPermits, observedPermits - 1))
				return true;
		}
	}
	
	// releases the specified number of permits
	private void doRelease(int releases) {
		permits.addAndGet(releases);
		// Java guarantees that this write is visible before any subsequent reads
	}
	
	// Acquire one permit from the semaphore
	public boolean acquire(long timeout, TimeUnit unit) throws InterruptedException {
		// try to acquire one permit, if available
		if (tryAcquire())
			return true;
		
		// no permits available; if a null time out was specified, return failure.
		if (timeout == 0)
			return false;

		// if a time out was specified, compute the timeout value in nanoseconds
		boolean timed = timeout > 0;
		long nanosTimeout = timed ? unit.toNanos(timeout) : 0L;
		
		lock.lock();
		try {
			
			// the current thread declares itself as a waiter..
			waiters++;
			/**
			 * Java: JMM guarantees non-ordering of previous volatile write of "waiters"
			 * with the next volatile read of "permits"
			 */
			try {		
				do {
					// after increment waiters, we must recheck if acquire is possible!
					if (tryAcquire())
						return true;
					// check if the specified timeout expired
					if (timed && nanosTimeout <= 0)
						return false;
					if (timed)
						nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
					else
						okToAcquire.await();
				} while (true);
			} finally {
				// the current thread is no longer a waiter
				waiters--;
			}	
		} finally {
			lock.unlock();
		}
	}
	
	public void acquire() throws InterruptedException {
		acquire(-1, TimeUnit.MILLISECONDS);
	}

	public boolean acquire(int timeoutMillis) throws InterruptedException {
		return acquire(timeoutMillis, TimeUnit.MILLISECONDS);
	}
	
	// Release the specified number of permits
	public void release(int releases) {
		doRelease(releases);	// this has volatile write semantics so, it is visible before read waiters
		if (waiters > 0) {	
			lock.lock();
			try  {
				// We must recheck waiters, after enter the monitor in order
				// to avoid unnecessary notifications 
				if (waiters > 0) {
					if (waiters == 1 || releases == 1)
						okToAcquire.signal(); // only one thread can proceed execution
					else
						okToAcquire.signalAll(); // more than only one thread can proceed  execution
				}
			} finally {
				lock.unlock();
			}
		}
	}

	// Release one permit
	public void release() { release(1); }


	/*
     * Test code.
	 */
			
	private static boolean testSemaphoreAsLock() throws InterruptedException {

		final int MIN_ACQUIRE_TIMEOUT = 5;
		final int MAX_ACQUIRE_TIMEOUT = 50;
		final int MIN_CRITICAL_SECTION_TIME = 0;
		final int MAX_CRITICAL_SECTION_TIME = 5;
		final int JOIN_TIMEOUT = 50;		
		final int RUN_TIME = 10 * 1000;
		final int THREADS = 10;

		Thread[] tthrs = new Thread[THREADS];
		int[] privateCounters = new int[THREADS];
		int[] timeouts = new int[THREADS];
		final AtomicInteger sharedCounter = new AtomicInteger();
		Semaphore lockSem = new Semaphore(1);
		
		//
		// Create and start acquirer/releaser threads
		//
		
		for (int i = 0; i < THREADS; i++) {
			int tid = i;
			tthrs[i] = new Thread(() -> {
				Random rnd = new Random(tid);
				System.out.printf("-> #%02d starting...%n", tid);			
				outerLoop: do {
					try {
						do {
							if (lockSem.acquire(rnd.nextInt(MAX_ACQUIRE_TIMEOUT) + MIN_ACQUIRE_TIMEOUT, TimeUnit.MILLISECONDS)) {
								break;
							}
							if (++timeouts[tid] % 1000 == 0)
								System.out.print('.');
						} while (true);
						try {
							sharedCounter.incrementAndGet();
							if (++privateCounters[tid] % 100 == 0) {
								System.out.printf("[#%d]", tid);
							}
							Thread.sleep(rnd.nextInt(MAX_CRITICAL_SECTION_TIME) + MIN_CRITICAL_SECTION_TIME);
						} finally {
							lockSem.release();							
						}
					} catch (InterruptedException ie) {
						/*
						if (tid == 0)
							do {} while (true);
						*/
						break outerLoop;
					}
				} while (!Thread.currentThread().isInterrupted());
				System.out.printf("<- #%02d exiting...%n", tid);
			});
			tthrs[i].setDaemon(true);
			tthrs[i].start();
		}

		// run the test threads for a while...
		Thread.sleep(RUN_TIME);
		
		// Interrupt each test thread and wait for a while until it finished.
		int stillRunning = 0;
		for (int i = 0; i < THREADS; i++) {
			tthrs[i].interrupt();
			tthrs[i].join(JOIN_TIMEOUT);
			if (tthrs[i].isAlive())
				stillRunning++;
		}
		
		if (stillRunning > 0) {
			System.out.printf("%n*** failure: %d test thread(s) did not answer to interruption%n", stillRunning);
			return false;
		}
		
		// All thread finished - compute results
		
		System.out.printf("%nPrivate counters:%n");
		int sum = 0;
		for (int i = 0; i < THREADS; i++) {
			sum += privateCounters[i];
			if (i != 0) {
				if ((i % 4) == 0)
					System.out.println();
				else
					System.out.print(' ');
			}
			System.out.printf("[#%02d: %4d/%d]", i, privateCounters[i], timeouts[i]);
		}
		System.out.printf("%n--shared aquisition: %d, private acquisitions: %d%n", sharedCounter.get(), sum);
		return sum == sharedCounter.get();
	}

	private static boolean testSemaphoreInAProducerConsumerContext() throws InterruptedException {

		final int MAX_PRODUCE_TIME = 5;
		final int MAX_CONSUME_TIME = 5;
		final int RUN_TIME = 10 * 1000;
		final int JOIN_TIMEOUT = 50;
		final int PRODUCER_THREADS = 20;
		final int CONSUMER_THREADS = 30;
 
		Thread[] cthrs = new Thread[CONSUMER_THREADS];
		Thread[] pthrs = new Thread[PRODUCER_THREADS];
		int[] consumerCounters = new int[CONSUMER_THREADS];
		int[] producerCounters = new int[PRODUCER_THREADS];
		
		// Using our semaphore...
		Semaphore freeSem = new Semaphore(1);
		Semaphore dataSem = new Semaphore(0);
		
		// or using the Java semaphore...
		//java.util.concurrent.Semaphore freeSem = new java.util.concurrent.Semaphore(1);
		//java.util.concurrent.Semaphore dataSem = new java.util.concurrent.Semaphore(0);
			
			
		// Create and start consumer threads.
		
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			final int tid = i;
			cthrs[i] = new Thread(() -> {
				Random rnd = new Random(tid);
				do {
					try {
						dataSem.acquire();
						try {
							if (++consumerCounters[tid] % 20 == 0)
								System.out.printf("[#c%d]", tid);
							Thread.sleep(rnd.nextInt(MAX_CONSUME_TIME));
						} finally {
							freeSem.release();
						}
					} catch (InterruptedException ie) {
						break;
					}
				} while (!Thread.currentThread().isInterrupted());					
			});
			cthrs[i].setDaemon(true);
			cthrs[i].setPriority(Thread.MAX_PRIORITY);
			cthrs[i].start();
		}
		
		// Create and start producer threads.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			final int tid = i;
			pthrs[i] = new Thread(() -> {				
				Random rnd = new Random(tid);
				do {
					try {
						freeSem.acquire();
						try {
							if (++producerCounters[tid] % 20 == 0)
								System.out.printf("[#p%d]", tid);
							Thread.sleep(rnd.nextInt(MAX_PRODUCE_TIME));	
						} finally {
							dataSem.release();							
						}
					} catch (InterruptedException ie) {
						break;
					}
				} while (!Thread.currentThread().isInterrupted());
			});
			pthrs[i].setDaemon(true);
			pthrs[i].start();
		}
		
		// run the test for a while
		Thread.sleep(RUN_TIME);
		
		// Interrupt each consumer thread and wait for a while until it finished.
		int stillRunning = 0;
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			cthrs[i].interrupt();
			cthrs[i].join(JOIN_TIMEOUT);
			if (cthrs[i].isAlive())
				stillRunning++;
		}

		// Interrupt each producer thread and wait for a while until it finished.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			pthrs[i].interrupt();
			pthrs[i].join(JOIN_TIMEOUT);
			if (pthrs[i].isAlive())
				stillRunning++;
		}
		
		if (stillRunning > 0) {
			System.out.printf("%n*** failure: %d test thread(s) did not answer to interruption%n", stillRunning);
			return false;
		}	
		
		// Compute results
		
		System.out.printf("%nConsumer counters:%n");
		int consumptions = 0;
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			consumptions += consumerCounters[i];
			if (i != 0) {
				if (i % 5 == 0)
					System.out.println();
				else
					System.out.print(' ');
			}
			System.out.printf("[#c%02d: %4d]", i, consumerCounters[i]);
		}
		if (dataSem.tryAcquire())
			consumptions++;
		
		System.out.printf("%nProducer counters:%n");
		int productions = 0;
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			productions += producerCounters[i];
			if (i != 0) {
				if (i % 5 == 0)
					System.out.println();
				else
					System.out.print(' ');
			}
			System.out.printf("[#p%02d: %4d]", i, producerCounters[i]);
		}
		System.out.printf("%n--productions: %d, consumptions: %d%n", productions, consumptions);
		return consumptions == productions;
	}
	
	public static void main(String[] args) throws InterruptedException {
		
		System.out.printf("%n-->Test semaphore as lock: %s%n",
						  testSemaphoreAsLock() ? "passed" : "failed");
		
		System.out.printf("%n-->Test semaphore in a producer/consumer context: %s%n",
						  testSemaphoreInAProducerConsumerContext() ? "passed" : "failed");
		
	}
}

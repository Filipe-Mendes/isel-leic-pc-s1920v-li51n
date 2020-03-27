/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Single Resource implemented using the Java's Explicit Monitors
 *
 * Carlos Martins, April 2017
 *
 **/

import java.util.concurrent.ThreadLocalRandom;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.ReentrantLock;
import java.io.IOException;

public class SingleResourceEx {
	private boolean busy;				// true when resource is busy
	private final Lock mlock;			// monitor's lock
	private final Condition nonBusy;	// non-busy condition variable
	
	public SingleResourceEx() { this(false); }

	public SingleResourceEx(boolean busy) {
		mlock = new ReentrantLock();
		nonBusy = mlock.newCondition();
		this.busy = busy; 
	}
		
	// acquire resource
	public void acquire() throws InterruptedException {
		mlock.lock();		// don't throw InterruptedException
		try {
			while (busy)
				nonBusy.await();
			busy = true;
		} finally {
			mlock.unlock();
		}
	}
	
	// release previously acquired resource
	public void release() {
		mlock.lock();
		try {
			busy = false;
			nonBusy.signal();	// Java does not loses notifications due to thread interruption
		} finally {
			mlock.unlock();
		}
	}
	
	/**
	 * Test code
	 */

	/*
	 * auxiliary methods
	 */
				
	static void sleepUninterruptibly(int timeMillis) {
		try {
			Thread.sleep(timeMillis);
		} catch (InterruptedException ie) {}
	}
	
	static void joinUninterruptibly(Thread jt) {
		do {
			try {
				jt.join();
				break;
			} catch (InterruptedException ie) {}
		} while (true);
	}

	// read a line from console
	static void readln() {
		try {
			do {
				System.in.read();
			} while (System.in.available() != 0);
		} catch (IOException ioex) {}
	}

	// constants
	static final int THREADS = 20;
	static final int MAX_PAUSE = 50;
	
	// the threads
	static Thread[] threads = new Thread[THREADS];
	
	// control variables
	static int sharedTotal = 0;
	static int[] privatePartial = new int[THREADS];
	static volatile boolean exit = false;
	
	// the single resource used as a lock
	static SingleResourceEx _lock = new SingleResourceEx();
	
	public static void main(String... args) {
		for (int i = 0; i < THREADS; i++) {
			final int li = i;
			threads[i] = new Thread(() -> {
				System.out.printf("->#%02d%n", li);
				do {
					boolean lockTaken = false;
					try {
						_lock.acquire();
						lockTaken = true;
						sharedTotal++;
						Thread.yield();
					} catch (InterruptedException ie) { // ignore interrupts
					} finally {
						if (lockTaken)
							_lock.release();
					}
					if (ThreadLocalRandom.current().nextInt(100) >= 50)					
						sleepUninterruptibly(ThreadLocalRandom.current().nextInt(MAX_PAUSE));					
					if (++privatePartial[li] % 100 == 0)
						System.out.printf("[#%02d]", li);						
				} while (!exit);
				System.out.printf("<-#%02d%n", li);
			});
			threads[i].start();
		}
		// run threads until <enter>
		System.out.println("-- press <enter> to terminate...");
		readln();
		System.out.printf("%n--wait until all threads terminate");
		exit = true;
		// wait for each thread termination, and compute private total
		int privateTotal = 0;
		for (int i = 0; i < THREADS; i++) {
			joinUninterruptibly(threads[i]);
			privateTotal += privatePartial[i];
		}
		
		// display results
		System.out.printf("\n-- shared total: %d, private total: %d, diff: %d%n",
			sharedTotal, privateTotal, privateTotal - sharedTotal);
	}
}
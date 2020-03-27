/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Single Resource implemented using Java's Implicit Monitors
 *
 * Carlos Martins, April 2017
 *
 **/

import java.util.concurrent.ThreadLocalRandom;
import java.io.IOException;

public class SingleResource {
	private final Object monitor;	// the monitor and the associated condition variable
	private boolean busy;			// true when resource is busy
	
	public SingleResource() { this(false); }

	public SingleResource(boolean busy) {
		monitor = new Object();
		this.busy = busy; 
	}
	
	// acquire resource
	public void acquire() throws InterruptedException {
		synchronized(monitor) {
			while (busy)
				monitor.wait();
			busy = true;
		}
	}
	
	// release previously acquired resource
	public void release() {
		synchronized(monitor) {
			busy = false;
			monitor.notify();	// Java does not loses notifications due to thread interruption
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
	static SingleResource _lock = new SingleResource();
	
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
					// sleep for a while in 50% of loop cycles
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
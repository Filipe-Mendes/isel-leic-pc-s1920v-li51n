/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Control synchronization in Java
 *
 * Carlos Martins, October 2016
 *
 ***/

/*
The action graph is as follows:

- Starts with A;
- After A finishes execution, can be executed B, C, D and E;
- After D and E finish execution, can be executed F;
- After B, C and F finish execution, can be executed G.

In this design:
- Thread T1 executes A, B and G;
- Thread T2 executes C;
- Thread T3 executes D and F.
- Thread T4 executes E.
*/

import java.util.Random;
import java.util.concurrent.*;
import java.util.concurrent.locks.*;

public class Graph {

	//
	// Synchronizers
	//
	// The CountDownLatch when initialized with 1 has a synchronization semantics
	// equivalent to a manual-reset event (without Reset operation).
	//

	private static final CountDownLatch t1_2_t2t3t4 = new CountDownLatch(1);
	private static final CountDownLatch t4_2_t3 = new CountDownLatch(1);
	private static final Semaphore t2t3_2_t1 = new Semaphore(0, false);

	//
	// The worker threads
	//

	private static Thread thr1, thr2, thr3, thr4;

	//
	// Constants used to generate random action durations.
	//

	private static final int MIN_DURATION = 50;
	private static final int MAX_DURATION = 2000;

	//
	// Execute the specified action.
	//
	
	private static void doAction(String actionName, Random rand) {
		boolean interrupted = false;
		
		System.out.print("\n-->" + actionName);
		for (;;)
			try {
				Thread.sleep(rand.nextInt(MAX_DURATION) + MIN_DURATION);
				break;
			} catch (InterruptedException ie) {
				interrupted = true;
			}
		System.out.print("\n<--" + actionName);
		
		// If the current thread was interrupted, re-assert the interruption.
		if (interrupted)
			Thread.currentThread().interrupt();
	}
	
	//
	// Await uninterruptibly on a CountDownLatch.
	//
	
	private static void awaitUninterruptibly(CountDownLatch cdl) {
		boolean interrupted = false;
		
		do {
			try {
				cdl.await();
				break;
			} catch (InterruptedException ie) {
				interrupted = true;
			}
		} while (true);
			
		// If the current thread was interrupted, re-assert the interruption.
		if (interrupted)
			Thread.currentThread().interrupt();
	}

	//
	// Join with a thread uninterruptibly.
	//
	
	private static void joinUninterruptibly(Thread t) {
		boolean interrupted = false;
		
		do {
			try {
				t.join();
				break;
			} catch (InterruptedException ie) {
				interrupted = true;
			}
		} while (true);
			
		// If the current thread was interrupted, re-assert the interruption.
		if (interrupted)
			Thread.currentThread().interrupt();
	}
	
	//
	// Thread T1
	//

	static class T1 implements Runnable {
		public void run() {
			Random rand = new Random(Thread.currentThread().getId() << 8);
		
			// Do action A
			doAction("A", rand);
	
			// Allow thread T2, T3 and T4 start executing.
			t1_2_t2t3t4.countDown();
	
			// Do action B
			doAction("B", rand);

			// Wait until T2 and T3 execute completely.

			t2t3_2_t1.acquireUninterruptibly(2);
	
			// Do action G
			doAction("G", rand);
		}
	}

	//
	// Thread T2
	//

	private static class T2 extends Thread {
		public void run() {
			Random rand = new Random(Thread.currentThread().getId() << 9);
	
			// Wait until T1 completes execution of action A
			awaitUninterruptibly(t1_2_t2t3t4);
	
			// Do action C
			doAction("C", rand);
			
			// Signals T1
			t2t3_2_t1.release();
		}
	}

	//
	// Thread T3
	//

	private static class T3 extends Thread {
		public void run() {
			Random rand = new Random(Thread.currentThread().getId() << 10);
	
			// Wait until t1 completes the action A
			awaitUninterruptibly(t1_2_t2t3t4);
	
			// Do action D
			doAction("D", rand);

			// Wait until the T4 completes execution of action E
			awaitUninterruptibly(t4_2_t3);
	
			// Do action F
			doAction("F", rand);
			
			// Signals T1 in order to allow execution of action G
			t2t3_2_t1.release();
		}
	}

	//
	// Thread T4
	//

	private static class T4 extends Thread {
		public void run() {
			Random rand = new Random(Thread.currentThread().getId() << 11);
		
			// Wait until T1 completes execution of action A
			awaitUninterruptibly(t1_2_t2t3t4);
	
			// Do action E
			doAction("E", rand);

			// Signal T3 in order to allw execution of action F	
			t4_2_t3.countDown();
		}
	}

	public static void main(String[] args) {

		// Create the executer thread objects.
		// T1 is executed by the primary thread.
		thr1 = Thread.currentThread();
		thr2 = new T2();
		thr3 = new T3();
		thr4 = new T4();
		
		System.out.print("++executing actions...");

		// Start the executer threads
		thr2.start();
		thr3.start();
		thr4.start();
		
		// T1 is executed by the primary thread.
		new T1().run();
		// T1 executes the last Action, so print newline
		System.out.println();
	
		//
		// Since all thread are foreground threads, the process
		// only exits when all threads have been exited.
		// 	
	}
}

/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Test thread creation in Java
 *
 * Carlos Martins, October 2016
 *
 ***/

import java.util.concurrent.*;
import java.io.IOException;


public class Threads {

	private static final int MAX_THREADS = 20000;

	// The event where all threads are blocked.

	private static CountDownLatch waitEvent = new CountDownLatch(1); 

	public static void main(String[] args) throws IOException, InterruptedException {
		int count = 0;
		Thread[] threads = new Thread[MAX_THREADS];

		for (;;) {
			try {
				//Thread t = new WaitForeverThread();
				
				/*
				 * or
				 */
				threads[count] = new Thread(() -> {
					//
					// Wait unconditionally until the waitEvent is set.
					//
			
					do {
						try {
							waitEvent.await();
							break;
						} catch (InterruptedException ie) {}
					} while (true);
				});
				
				//
				// Comment the next line in order to do not create the thread.
				//	
				threads[count].setDaemon(true);
				threads[count].start();
			} catch (Throwable ex) {
				System.out.println("\n*** " + ex);
				break;		
			}
			
			if (++count >= MAX_THREADS)
				break;
			else if ((count % 1000) == 0)
				System.out.println(count);
			else if (count % 20 == 0)
				System.out.print('+');
		}
		System.out.println("\n--created threads: " + count);
		System.out.println("--active threads: " + Thread.activeCount());
		System.out.print("---hit <enter> to set the wait event...");
		
		//
		// Wait for <enter> to signal the wait event.
		//
		
		System.in.read();		
		waitEvent.countDown();
		
		//
		// Since all threads were created as non-daemon threads, the
		// process termination only occurs after all threads have been
		// exited.
		//
		
		long start = System.currentTimeMillis();
		
		// synchronize with termination of all threads
		for (int i = 0; i < count; ++i)
			threads[i].join();
		System.out.printf("--wait time: %d ms%n", System.currentTimeMillis() - start);
	}
}

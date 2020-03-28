import java.util.concurrent.locks.*;

class ShuttingDownThread {
	private Thread thread;

	public ShuttingDownThread() {
		// start to worker thread
		thread = new Thread(() -> {
			System.out.println("-- thread started...");
			int cycle = 0;
			do {
				try {
					// do something that can block the thread
					Thread.sleep(1000);
					System.out.print(++cycle);

				} catch (InterruptedException ie) {
					System.out.println("\n-- thread interrupted...");
					break;
				}
			} while (!Thread.interrupted());
			// execute graceful shutdown
			System.out.println("-- thread exiting...");
		});
	}
	
	// start thread...
	public void start() {
		thread.start();
	}

	// other methods

	// shuts down the worker thread ignoring interrupts
	public void shutdown() {
		boolean interrupted = false;
		thread.interrupt();
		do {
			try {
				thread.join();
				break;
			} catch (InterruptedException ie) {
				interrupted = true;
			}
		} while (true);
		if (interrupted)
			Thread.currentThread().interrupt();
	}
}

class UseInterruptToCancel {
	public static void execute() throws InterruptedException {
		ShuttingDownThread thread = new ShuttingDownThread();
		thread.start();
		Thread.sleep(5000);
		thread.shutdown();
		System.out.println("-- main exiting...");
	}
}

class InterruptWhileWaitingOnImplicitMonitor {
	public static void execute() throws InterruptedException {
		Object monitor = new Object();
		Thread t = new Thread(() -> {
			synchronized(monitor) {
				try {
					monitor.wait();
					System.out.println("-- thread was notified!");
				} catch (InterruptedException ie) {
					System.out.println("*** thread was interrupted while waiting!");
					return;
				}
				System.out.println("-- Thread.interrupted(): " + Thread.interrupted());
			}
		});
		t.start();
		Thread.sleep(100);
		synchronized(monitor) {
			monitor.notify();
			t.interrupt();
		}
		t.join();
		System.out.println("-- main exiting...");
	}
}

class InterruptWhileWaitingOnExplicitMonitor {
	public static void execute() throws InterruptedException {
		Lock monitor = new ReentrantLock();
		Condition cv = monitor.newCondition();
		Thread t = new Thread(() -> {
			monitor.lock();
			try {
				try {
					cv.await();
					System.out.println("-- thread was notified!");
				} catch (InterruptedException ie) {
					System.out.println("*** thread was interrupted while waiting!");
				}
			} finally {
				monitor.unlock();
			}
			System.out.println("-- Thread.interrupted(): " + Thread.interrupted());
		});
		t.start();
		Thread.sleep(100);
		
		monitor.lock();
		try {
			cv.signal();
			t.interrupt();
		} finally {
			monitor.unlock();
		}
		t.join();
		System.out.println("-- main exiting...");
	}
}

public class UseInterrupt {
	public static void main(String... args) throws InterruptedException {
		UseInterruptToCancel.execute();
		//InterruptWhileWaitingOnImplicitMonitor.execute();
		//InterruptWhileWaitingOnExplicitMonitor.execute();
	}
}


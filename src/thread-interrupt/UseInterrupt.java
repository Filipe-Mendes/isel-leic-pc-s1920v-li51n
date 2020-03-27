using System;
using System.Threading;

class ShuttingDownThread {
	private volatile bool shuttingDown;
	private Thread thread;

	public ShuttingDownThread() {
		// create a worker thread
		thread = new Thread(() => {
			Console.WriteLine("-- thread started...");
			int cycle = 0;
			do {
				try {
					// do something that can block the thread
					Thread.Sleep(1000);
					Console.Write($"{++cycle} ");

				} catch (ThreadInterruptedException) {
					Console.WriteLine("\n-- thread interrupted...");
					break;
				}
			} while (!shuttingDown);
			// execute graceful shutdown
			Console.WriteLine("-- thread exiting...");
		});
	}
	
	// start thread...
	public void Start() {
		thread.Start();
	}

	// other methods

	// shuts down the worker thread ignoring interrupts
	public void Shutdown() {
		bool interrupted = false;
		shuttingDown = true;
		thread.Interrupt();
		do {
			try {
				thread.Join();
				break;
			} catch (ThreadInterruptedException) {
				interrupted = true;
			}
		} while (true);
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
}

class UseInterruptToCancel {
	public static void Execute() {
		ShuttingDownThread thread = new ShuttingDownThread();
		thread.Start();
		Thread.Sleep(10000);
		thread.Shutdown();
		Console.WriteLine("-- main exiting...");
	}
}

class InterruptWhileWaitingOnImplicitMonitor {
	public static void Execute()  {
		Object monitor = new Object();
		Thread t = new Thread(() => {
			lock(monitor) {
				try {
					Monitor.Wait(monitor);
					Console.WriteLine("-- thread was notified!");
				} catch (ThreadInterruptedException) {
					Console.WriteLine("*** thread was interrupted while waiting!");
				}
				try {
					Thread.Sleep(0);
					Console.WriteLine("-- Thread.Interrupted: false");
					
				} catch (ThreadInterruptedException) {
					Console.WriteLine("-- Thread.Interrupted: true");
				}
			}
		});
		t.Start();
		Thread.Sleep(10000);
		lock(monitor) {
			Monitor.Pulse(monitor);
			t.Interrupt();
		}
		t.Join();
		Console.WriteLine("-- main exiting...");
	}
}

public class UseInterrupt {
	public static void Main() {
		//UseInterruptToCancel.Execute();
		InterruptWhileWaitingOnImplicitMonitor.Execute();
	}
}





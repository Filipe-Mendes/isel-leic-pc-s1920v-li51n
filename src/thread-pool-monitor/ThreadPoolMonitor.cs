/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Program to monitor worker thread injection in .NET ThreadPool.
 *
 * Carlos Martins, May 2020
 *
 **/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class ThreadPoolMonitor {

	// Class that reports worker thread creation, reuse, and retirement.

	internal class WorkerThreadReport {
		
		// Static fields whose access is protected by the _lock's lock.

		private static readonly object _lock = new object();
		private static int lastCreationTime = Environment.TickCount;
		private static int createdThreads;
		private static readonly List<WorkerThreadReport> reports = new List<WorkerThreadReport>();
		
		// Thread local that holds the report for each worker thread.
		internal static ThreadLocal<WorkerThreadReport> report =
						new ThreadLocal<WorkerThreadReport>(() => new WorkerThreadReport());


		// Instance fields used by each worker thread.
		private readonly Thread theThread;
		private readonly int theThreadId;
		private int timeOfLastUse;
		private int exitTime;

		internal WorkerThreadReport() {
			theThread = Thread.CurrentThread;
			theThreadId = theThread.ManagedThreadId;
			int order, injectionDelay, now;
			lock (_lock) {
				timeOfLastUse = now = Environment.TickCount;
				injectionDelay = now - lastCreationTime;
				lastCreationTime = now;
				order = ++createdThreads;
				reports.Add(this);
			}
			Console.WriteLine("--> injected the {0}-th worker #{1}, after {2} ms",
					 		  order, theThreadId, injectionDelay);
		}

		// Register or update a report for the current thread.
		internal static void RegisterWorker() {
			report.Value.timeOfLastUse = Environment.TickCount;
		}
		
		// Returns the number of created threads
		internal static int CreatedThreads {
			get { lock (_lock) return createdThreads; }
		}

		// Returns the number of active threads
		internal static int ActiveThreads {
			get { lock (_lock) return reports.Count; }
		}
		
		// Displays the alive worker threads
		internal static void ShowThreads() {
			lock (_lock) {
				if (reports.Count == 0)
					Console.WriteLine("-- no worker are threads alive");
				else
					Console.Write("-- {0} worker threads are alive:", reports.Count);
				foreach(WorkerThreadReport r in reports) {
					Console.Write(" #{0}", r.theThreadId);
				}
				Console.WriteLine();
			}
		}
		
		// Thread that monitors the worker thread's exit.
		private static void ExitMonitorThreadBody() {
			int rcount;
			do {
				List<WorkerThreadReport> exited = null;
				lock (_lock) {
					rcount = reports.Count;
					for (int i = 0; i < reports.Count;) {
						WorkerThreadReport r = reports[i];
						if (!r.theThread.IsAlive) {
							reports.RemoveAt(i);
							if (exited == null) {
								exited = new List<WorkerThreadReport>();
							}
							r.exitTime = Environment.TickCount;
							exited.Add(r);
						} else
							i++;
					}
				}
				if (exited != null) {
					foreach(WorkerThreadReport r in exited) {
						Console.WriteLine("--worker #{0} exited after {1} s of inactivity",
							r.theThreadId, (r.exitTime - r.timeOfLastUse) / 1000);
					}
				}
				
				// sleep for a while.
				try {
					Thread.Sleep(50);
				} catch (ThreadInterruptedException) {
					return;
				}
			} while (true);
		}

		// The exit thread
		private static Thread exitThread;
		
		// Static constructor: start the exit monitor thread.
		static WorkerThreadReport() {
			exitThread = new Thread(ExitMonitorThreadBody);
			exitThread.Start();
		}
		
		// shutdown thread report
		internal static void ShutdownWorkerThreadReport() {
			exitThread.Interrupt();
			exitThread.Join();
		} 
	}
	
	private static int ACTION_COUNT = 35;
	private static int REPEAT_FOR = 500;
	
	static void Main(String[] args) {	
		int minWorker, minIocp, maxWorker, maxIocp;
				
		if (args.Length != 1 || !(args[0].Equals("-cpu") || args[0].Equals("-io"))) {
			Console.WriteLine("usage: ThreadPoolMonitor [-cpu | -io]");
			return;
		}
		
		bool cpuBoundWorkload = args[0].Equals("-cpu");
		if (cpuBoundWorkload)
        	Console.WriteLine("--Monitor the .NET Framework's Thread Pool using a CPU-bound workload");	
		else
			Console.WriteLine("--Monitor the .NET Framework's Thread Pool using a I/O-bound workload");
		
		ThreadPool.GetMinThreads(out minWorker, out minIocp);
		ThreadPool.GetMaxThreads(out maxWorker, out maxIocp);
		
		//ThreadPool.SetMinThreads(2 * Environment.ProcessorCount, minIocp);
		Console.WriteLine("--processors: {0}; min/max workers: {1}/{2}; min/max iocps: {3}/{4}",
				 		  Environment.ProcessorCount, minWorker, maxWorker, minIocp, maxIocp );
		
	 	Console.Write("--hit <enter> to start, and then <enter> again to terminate...");
		Console.ReadLine();		
	
		for (int i = 0; i < ACTION_COUNT; i++) {
			
			ThreadPool.QueueUserWorkItem((targ) => {
                WorkerThreadReport.RegisterWorker();
                int tid = Thread.CurrentThread.ManagedThreadId;
				Console.WriteLine($"-->Action({targ:D2}, #{tid:D2})");
				for (int n = 0; n < REPEAT_FOR; n++) {
					WorkerThreadReport.RegisterWorker();
					if (cpuBoundWorkload)
                    	Thread.SpinWait(2500000);		// CPU-bound workload
					else
						Thread.Sleep(50);				// I/O-bound workload
				}
				Console.WriteLine($"<--Action({targ:D2}, #{tid:00})");
			}, i);
		}
		int delay = 50;
		do {
			int till = Environment.TickCount + delay;
			do {
				if (Console.KeyAvailable) {
					goto Exit;
				}
				Thread.Sleep(15);
			} while (Environment.TickCount < till);
			delay += 50;
			
			//
			// Comment the next statement to allow worker thread retirement!
			//
			/*
			ThreadPool.QueueUserWorkItem(_ => {
				WorkerThreadReport.RegisterWorker();
				Console.WriteLine("ExtraAction() --><-- on worker thread #{0}", Thread.CurrentThread.ManagedThreadId);
			});
			*/
			
		} while (true);
	Exit:
		Console.WriteLine($"-- {WorkerThreadReport.CreatedThreads} worker threads were injected");
		WorkerThreadReport.ShowThreads();
		WorkerThreadReport.ShutdownWorkerThreadReport();
	}
}

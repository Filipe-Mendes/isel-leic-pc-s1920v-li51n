/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Monitorization of a process' execution .NET
 *
 * To generate the executable, execute: csc ProcessMonitor.cs
 *
 * Carlos Martins, March 2017
 *
 ***/

using System;
using System.Threading;
using System.Diagnostics;

class ProcessMonitor {
	public static void Main() {
		int sample = 0;
		Process process = null;
		try {
			// Start a process to execute the notepad.exe program.
            process = Process.Start("notepad.exe", ".\\ProcessMonitor.cs");
			
            // Display the process statistics until the user closes the program. 
			do {
				if (!process.HasExited) {
					// Refresh the current process property values.
					process.Refresh();
					Console.WriteLine();
					
					// Display current process statistics.
					Console.WriteLine("{0}[{1}] -", process.ToString(), ++sample);
					Console.WriteLine("-------------------------------------");
					Console.WriteLine("  physical memory usage: {0} KB", process.WorkingSet64 / 1024);
					Console.WriteLine("  base priority: {0}", process.BasePriority);
					Console.WriteLine("  priority class: {0}", process.PriorityClass);
					Console.WriteLine("  user-mode processor time: {0}", process.UserProcessorTime);
					Console.WriteLine("  kernel-mode processor time: {0}", process.PrivilegedProcessorTime);
					Console.WriteLine("  total processor time: {0}", process.TotalProcessorTime);
					Console.WriteLine("  pagedSystemMemorySize64: {0} KB", process.PagedSystemMemorySize64 / 1024);
					Console.WriteLine("  pagedMemorySize64: {0} KB", process.PagedMemorySize64 / 1024);
					
					//Console.WriteLine("Threads: {0}", process.Threads.Count);
					
					Console.WriteLine("Threads({0}): ", process.Threads.Count);
					foreach(ProcessThread pt in process.Threads) {
						Console.Write(" 0x{0:x16} - {1}:{2}", pt.StartAddress.ToInt64(), pt.Id, pt.ThreadState);
					}
					Console.WriteLine();
				}
			} while (!process.WaitForExit(5000));
			
			Console.WriteLine();
            Console.WriteLine("process exit code: {0}", process.ExitCode);

			// Display peak memory statistics for the process.
			Console.WriteLine("peak physical memory usage: {0} KB", process.PeakWorkingSet64/ 1024);
			Console.WriteLine("peak paged memory usage: {0} KB", process.PeakPagedMemorySize64 / 1024);
			Console.WriteLine("peak virtual memory usage: {0} KB", process.PeakVirtualMemorySize64 / 1024);
		} finally {
			if (process != null) {
				process.Close();
			}
		}
	}
}



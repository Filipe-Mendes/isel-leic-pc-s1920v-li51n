/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Teste Process type in .NET
 *
 * Carlos Martins, October 2016
 *
 ***/

using System;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;


class TestProcess {

	public static void Main(String[] args) {
	
		// Create a process to execute the program wich image is contained in the notepad.exe file.
		Process p = Process.Start("notepad.exe", ".\\TestProcess.cs");
		//Process p = Process.Start("loop", "0");
		// Set the process' priority class to High.
		p.PriorityClass = ProcessPriorityClass.High;
		
		Console.WriteLine("process name: {0}, priority class: {1}",
						   p.ProcessName, p.PriorityClass);

		/*
		// Wait until <return> to terminate the creates process.
		Console.ReadLine();
		p.Kill();
		*/
		
		// Wait until the process terminate and show the processor times and exit code.
		p.WaitForExit();
		Console.WriteLine("processor times: k:{0}/u:{1}, exit code: {2}",
						   p.PrivilegedProcessorTime.Milliseconds,
						   p.UserProcessorTime.Milliseconds,
						   p.ExitCode);
	}
}

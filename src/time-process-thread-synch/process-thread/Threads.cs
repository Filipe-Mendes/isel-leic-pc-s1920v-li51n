/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Test thread creation and exit using the .NET framework
 *
 * Carlos Martins, October 2016
 *
 ***/

using System;
using System.Threading;

static public class Threads {

	private static uint MAX_THREADS = 20000;

	private static void Main()
	{
		Thread[] threads = new Thread[MAX_THREADS];

		// The event where all of the created threads will block.
		ManualResetEventSlim waitEvent = new ManualResetEventSlim(false); 
		int count;
		for (count = 0; count < MAX_THREADS; ++count) {
			try {
				(threads[count] = new Thread(() => waitEvent.Wait())).Start();
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
				break;
			}
			
			if (count != 0 && (count % 1000) == 0)
				Console.WriteLine("{0}", count);
			else if (count != 0 && count % 20 == 0)
				Console.Write('+');
		}
		Console.WriteLine("{0}", count);
		Console.Write("--hit <enter> to set the wait event...");
		Console.ReadLine();
		waitEvent.Set();
		int start = Environment.TickCount;
		// synchronize with termination of all threads	
		for (int i = 0; i < MAX_THREADS; ++i)
			threads[i].Join();
		Console.WriteLine("--wait time: {0} ms", Environment.TickCount - start);
	}
}

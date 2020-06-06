/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Ignoring task exceptions
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.Threading;
using System.Threading.Tasks;


public class IgnoringErrors {
	
	public static void Main() {
		Console.WriteLine("CLR version: {0}", Environment.Version);
		
		
		TaskScheduler.UnobservedTaskException +=
			(sender, e) => {
				Console.WriteLine($"***exception sender type: {sender.GetType()}");
				
				foreach (Exception ex in e.Exception.InnerExceptions)
					Console.WriteLine($"**unobserved exception {ex.GetType().Name}: {ex.Message}");

				// comment the next line to do not observe the exception
				e.SetObserved();
			};
		for (int i = 0; i < 10; i++) {
			int li = i;
			Task.Run(() => {
				Console.WriteLine($"...task #{li} started...");
				throw(new Exception(String.Format($"Boom[{li}]")));
			});
		}
		
		var objects = new object[10000000];
		for (int i = 0; !Console.KeyAvailable; i %= objects.Length)
			objects[i] = new object[i];
		Console.WriteLine("--done!");
	}
}

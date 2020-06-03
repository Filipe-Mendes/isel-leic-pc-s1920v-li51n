/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Why child tasks
 *
 * Carlos Martins, November 2019
 *
 **/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;


public static class WhyChildTasks {
	
	public static Task ProcessDirectoryAsync(string dataDirectory, CancellationToken ct) {
		// we need to use TaskFactory.StartNew() because Task.Run() prohibits attach-to-parent
		return Task.Factory.StartNew(() => {
		    foreach (FileInfo file in new DirectoryInfo(dataDirectory).GetFiles("*.cs")) {
		        Task.Factory.StartNew(_ => {
					// convenient point to check for cancellation
					//ct.ThrowIfCancellationRequested();
					// here we process the file contents
					Console.WriteLine("--> {0}", file.FullName);
					Thread.Sleep((new Random(Environment.TickCount)).Next(500, 5000));
					Console.WriteLine("<-- {0}", file.FullName);
				}, ct, TaskCreationOptions.AttachedToParent);
			}
		});
	}
	
	public static void Main() {
		
		// Parent only terminates when all the attached-to-parent childs terminate
		ProcessDirectoryAsync(".", CancellationToken.None).Wait();
		Console.WriteLine("--done!");
	}
}

using System;
using System.Threading;

public class NoVisibility {
	private static int number;
	private static bool ready;
	
	private static void ReaderThreadBody() {
		Console.WriteLine($"ready: {ready}");
		while (!ready)
			;
		Console.WriteLine(number);
	}
	
	public static void Main() {
		new Thread(ReaderThreadBody).Start();
		Thread.Sleep(100);
		number = 42;
		ready = true;
	}
}

		
			


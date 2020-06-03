/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Returning data from a task
 *
 * Generate executable with: csc returning-data.cs -r:System.Numerics.dll
 *
 * Carlos Martins, November 2019
 *
 */

using System;
using System.Numerics;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program {
	
	// computes n!
	private static BigInteger Factorial(BigInteger n) {
		BigInteger fact = 1;
		long ln = (long)n;
		for (long i = 1; i < ln; i++)
			fact = fact * i;
		return fact;
	}
	public static void Main() {

		// Some complicated calculation!
		BigInteger n = 49000;
		BigInteger r = 600;
		
		// Execute sequentially
		Stopwatch sw = Stopwatch.StartNew();
		BigInteger part1 = Factorial(n);
		BigInteger part2 = Factorial(n - r);
		BigInteger part3 = Factorial(r);
		BigInteger chances = part1 / (part2 * part3);
		Console.WriteLine($"\nchances: {chances}");
		Console.WriteLine($"sequential computation time: {sw.ElapsedMilliseconds}");

		// Execute with potential parallelism
		sw.Restart();
		Task<BigInteger> part1Task = Task.Run(() => Factorial(n));
		Task<BigInteger> part2Task = Task.Factory.StartNew(() => Factorial(n - r));
		Task<BigInteger> part3Task = Task.Run(() => Factorial(r));
		chances = part1Task.Result / (part2Task.Result * part3Task.Result);
		Console.WriteLine($"\nchances: {chances}");
		Console.WriteLine($"parallel computation time: {sw.ElapsedMilliseconds}");	
	}
}

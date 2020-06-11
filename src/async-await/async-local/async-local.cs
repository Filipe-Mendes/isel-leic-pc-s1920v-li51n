using System;
using System.Threading;
using System.Threading.Tasks;

class Example {
    static AsyncLocal<string> _asyncLocal = new AsyncLocal<string>();
    static ThreadLocal<string> _threadLocal = new ThreadLocal<string>();

    static async Task AsyncMethodA() {
		
		/**
		 * Start multiple async method calls, with different AsyncLocal values.
         * We also set ThreadLocal values, to demonstrate how the two mechanisms differ.
		 */
		
        _asyncLocal.Value = "V_1";
        _threadLocal.Value = "V_1";
        var t1 = AsyncMethodB("V_1");

        _asyncLocal.Value = "V_2";
        _threadLocal.Value = "V_2";
        var t2 = AsyncMethodB("V_2");

        // Await both calls
        await Task.WhenAll(new Task[] { t1, t2});
     }

    static async Task AsyncMethodB(string expectedValue) {
        Console.WriteLine("-->AsyncMethodB");
        Console.WriteLine("Expected: '{0}', AsyncLocal: '{1}', ThreadLocal: '{2}'", 
                          expectedValue, _asyncLocal.Value, _threadLocal.Value);
        await Task.Delay(100);
        Console.WriteLine("<--AsyncMethodB");
        Console.WriteLine("Expected: '{0}', got: '{1}', ThreadLocal: '{2}'", 
                          expectedValue, _asyncLocal.Value, _threadLocal.Value);
    }
	
	/**
	 *  Program entry point
	 */
	
    public static void Main(string[] args) {
        AsyncMethodA().Wait();
    }
}
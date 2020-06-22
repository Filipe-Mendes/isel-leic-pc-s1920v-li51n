/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * A TCP multithreaded server based on TAP interfaces and C# asynchronous methods.
 * To build this client as a .NET Core project:
 *
 *   1. Set the directory where the file server.cs resides as the current directory 
 *   2. To create the project file (<current-dir-name>.csproj) execute : dotnet new console<enter>
 *   3. Remove the file "Program.cs"
 *   4. To build the executable execute: dotnet build<enter>
 *   5. To run the executable execute: dotnet run<enter> 
 *
 *
 * Carlos Martins, June, 2020
 **/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

/**
 * A Tcp multithreaded echo server, using TAP interfaces and C# asynchronous methods.
 */
public class TcpMultiThreadedTapEchoServer {
	private const int SERVER_PORT = 13000;
	private const int BUFFER_SIZE = 1024;
	private const int MIN_SERVICE_TIME = 50;
	private const int MAX_SERVICE_TIME = 500;

	// The maximum number of simultaneous connections allowed.
	private const int MAX_SIMULTANEOUS_CONNECTIONS = 10;

	// Constants used when we poll for server idle.
	private const int WAIT_FOR_IDLE_TIME = 10000;
	private const int POLLING_INTERVAL = WAIT_FOR_IDLE_TIME / 100;

	// A random generator private to each thread.
	private ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));
	
	// The server socket
	private TcpListener server;
	
	// Number of processed requests.
	private volatile int requestCount = 0;
	
	// Construct the server
	public TcpMultiThreadedTapEchoServer() {
    	// Create a listen socket bind to the server port.
    	server = new TcpListener(IPAddress.Loopback, SERVER_PORT);
		
		// Start listen from server socket
		server.Start();
	}

	/**
	 * Serves the connection represented by the specified TcpClient socket,
	 * using an asynchronous method.
	 * If we need to cancel the processing of already accepted connectins, we
	 * must propagate the received CancellationToken.
	*/
	private async Task ServeConnectionAsync(TcpClient connection,
											CancellationToken cToken = default(CancellationToken)) { 
		using (connection) {
			try {
				// Get a stream for reading and writing through the socket.
				NetworkStream stream = connection.GetStream();
				byte[] requestBuffer = new byte[BUFFER_SIZE];
				
				// Receive the request (we know that it is smaller than 1024 bytes);
				int bytesRead = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
				
				Stopwatch sw = Stopwatch.StartNew();

				// Convert the request to ASCII and display it.
				string request = Encoding.ASCII.GetString(requestBuffer, 0, bytesRead);
				Console.WriteLine($"-->[{request}]");
				
				/**
				 * Simulate asynchornously a random service time, and send response to the client
				 */
				await Task.Delay(random.Value.Next(MIN_SERVICE_TIME, MAX_SERVICE_TIME), cToken);
				
				string response = request.ToUpper();
				Console.WriteLine($"<--[{response}({sw.ElapsedMilliseconds} ms)]");
				byte[] responseBuffer = Encoding.ASCII.GetBytes(response);
				await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
				// Increment the number of processed requests.
				Interlocked.Increment(ref requestCount);
			} catch (Exception ex) {
				Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	/**
 	 * Asynchronous method that listens for connections.
	 * This method limits, by design, the maximum number of simultaneous connections.
	 */
	public async Task ListenAsync(CancellationToken cToken) {
		var startedTasks = new HashSet<Task>();
		do {
			try {
				var connection = await server.AcceptTcpClientAsync();
				
				/**
				 * Add the task returned by the ProcessConnectionAsync method
				 * to the thread hash set.
				 */
				startedTasks.Add(ServeConnectionAsync(connection, cToken));
				
				/**
				 * If the defined threshold was reached, remove completed tasks; if none can
				 * be removed, wait until one of the active tasks complete its processing.
				 */
				if (startedTasks.Count >= MAX_SIMULTANEOUS_CONNECTIONS) {
					if (startedTasks.RemoveWhere(task => task.IsCompleted) == 0)
						startedTasks.Remove(await Task.WhenAny(startedTasks));
				}
			} catch (ObjectDisposedException) {
				// benign exception - occurs when when stop accepting connections
			} catch (Exception ex) {
				Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
			}
		} while (!cToken.IsCancellationRequested);
        
		/**
		 * When implementing a remote shutdown that waits for all accepted client
		 * request be completed, we must terminate this method when there is only
		 * um task to terminate, that is, the "shutdown task" itself.
		 * The shutdown task must "await" until this method terminates, and then
		 * send the response to its client.
		 */
		
		/*
		do {
			// Remove all the already completed tasks
			startedTasks.RemoveWhere(task => task.IsCompleted);
            
			// When there is only one task active (the "shutdown task"),
			// finish this method, in order to complete the "listenTask"
			if (startedTasks.Count <= 1)
				break;
			await Task.WhenAny(startedTasks);
		} while (true);
		*/
        
		/**
	 	 * Before return, wait for completion of processing of all accepted connections.
		 */	
		if (startedTasks.Count > 0)
        	await Task.WhenAll(startedTasks);
	}
	
	/**
	 * Start shudown and wait for termination.
	 */
	private async Task  ShutdownAndWaitTerminationAsync(Task listenTask, CancellationTokenSource cts) {
		/**
		 * Before close the listener socket, try to process the connections already
		 * accepted by the operating system's sockets layer.
		 * Since that it is possible that we never see no connections pending, due
		 * to an uninterrupted arrival of connection requests, we poll only for
		 * a limited amount of time.
		 */	
		for (int i = 0; i < WAIT_FOR_IDLE_TIME; i += POLLING_INTERVAL) {
			if (!server.Pending())
				break;
			await Task.Delay(POLLING_INTERVAL);
		}
		
		/**
		 * Stop accepting new connection requests
		 */
		server.Stop();
		
		/**
		 * Set the cancellation token to the canceled state, and wait until all of the
		 * previously accepted connections are processed.
		 */	
		cts.Cancel();	
		await listenTask;	/* await listenTask */
	}
	
	/**
	 * Server entry point
	 */
	public static async Task Main() {
		TcpMultiThreadedTapEchoServer echoServer = new TcpMultiThreadedTapEchoServer();
		
		// The cancellation token source used to shutdonw the server.
		CancellationTokenSource cts = new CancellationTokenSource();
		
		/**
		 * Start listen and processing requests.
		 * This is an asynchronous method that returns on the first await that will blocks.
		*/
		Task listenTask = echoServer.ListenAsync(cts.Token);
		
		/**
		 * Wait an <enter> press from the console to terminate the server.
		 */
		Console.WriteLine("--Hit <enter> to exit the server...");
		await Console.In.ReadLineAsync();
		
		/**
		 * Shutdown the server and wait until all processing terminates
		 */
		await echoServer.ShutdownAndWaitTerminationAsync(listenTask, cts);
			
		// Display the number of requests processed
		Console.WriteLine($"--{echoServer.requestCount} requests where processed");
	}
}


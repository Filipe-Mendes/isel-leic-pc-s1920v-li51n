/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Boolean latch with asynchronous and synchronous interfaces
 *
 *  Carlos Martins, June 2020
 *
 **/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

public class BooleanLatchAsync {
			
	// Type used to represent each asynchronous waiter
	private class AsyncWaiter: TaskCompletionSource<bool> {
		internal readonly CancellationToken cToken;
		internal CancellationTokenRegistration cTokenRegistration;
		internal Timer timer;
		internal bool done;		// True when the async wait is completed 	
		
		internal AsyncWaiter(CancellationToken cToken) : base() {
			this.cToken = cToken;
		}

		/**
		* Disposes resources associated with this async waiter.
		 *
		 * Note: when this method is called we are sure that the fields "timera"
		 *       and "cTokenRegistration" are correctly affected
		 */
		internal void Dispose(bool canceling) {
			// The CancellationTokenRegistration is disposed off after the cancellation
			// handler is called.
			if (!canceling && cToken.CanBeCanceled)
				cTokenRegistration.Dispose();
			timer?.Dispose();
		}
	}

	// The lock - we do not use the monitor functionality
	private readonly object theLock = new object();
	
	// The state of the latch	
	private volatile bool open;		// volatile grants visibility w/o acquire/release the lock

	// The queue of async waitetrs
	private LinkedList<AsyncWaiter> asyncWaiters;

	// Pre-initialized delegates that are used as cancellation handlers
    private readonly Action<object> cancellationHandler;
	private readonly TimerCallback timeoutHandler;

    // Pre-initialized completed task to return "true" and "false" results.
    private static readonly Task<bool> trueTask = Task.FromResult<bool>(true);
	private static readonly Task<bool> falseTask = Task.FromResult<bool>(false);
    
	/**
     * Constructor
     */
    public BooleanLatchAsync(bool open = false) {
		this.open = open;
		if (!open) {
	    	// If the latch is initialized as closed, initialize its fields
        	cancellationHandler = new Action<object>(CancellationHandler);
        	timeoutHandler = new TimerCallback(TimeoutHandler);
        	asyncWaiters = new LinkedList<AsyncWaiter>();
		}
    }

	/**
	 * Auxiliary methods
	 */

    /**
	 * Complete the tasks associated to the completed async waiters.
	 *  Note: This method is called when calling thread does not own the lock.
	 */
    private void CompleteAsyncWaiters(LinkedList<AsyncWaiter> completed) {
        if (completed != null) {
            foreach (AsyncWaiter awaiter in completed) {
                awaiter.Dispose(false);
                awaiter.SetResult(true);
            }
        }
    }

	/**
	 * Common code when cancelling an async waiter.
	 */
    private AsyncWaiter CancelCommon(object _awaiterNode) {
		LinkedListNode<AsyncWaiter> awaiterNode = (LinkedListNode<AsyncWaiter>)_awaiterNode;
		AsyncAwaiter awaiter = awaiterNode.Value;
		lock(theLock) {
			if (!awaiter.done) {
				// Remove the async waiter from the waiters list, and mark it as completed
				asyncWaiters.Remove(awaiterNode);
				awaiter.done= true;
				return awaiter;
			}
		}
		return null;
	}

    /**
	 * Cancel an async waiter due to cancellation.
	 */
    private void CancellationHandler(object awaiterNode) {
		AsyncWaiter awaiter;
		if ((awaiter = CancelCommon(awaiterNode)) != null) {
			// The CancellationTokenRegistration is disposed off after the cancellation
			// handler is called. Release resources owned by the waiter.
			awaiter.Dispose(true);

            // Complete the TaskCompletionSource<> to the Canceled state
            awaiter.SetCanceled();
        }
	}

	/**
	 * Cancels a request due to timeout.
	 */
	private void TimeoutHandler(object awaiterNode) {
		AsyncWaiter awaiter;
		if ((awaiter = CancelCommon(awaiterNode)) != null) {
			// Release the resources associated to the async waiter.
			awaiter.Dispose(false);

            // complete the TaskCompletionSource with RunToCompletion state, Result = false
            awaiter.SetResult(false);
        }
	}
		
	/**
	 * Try to cancel an asynchronous request identified by its task.
	 */
	public bool TryCancelWaitAsync(Task<bool> awaiterTask) {
        AsyncWaiter awaiter = null;
        lock(theLock) {
			foreach (AsyncWaiter _awaiter in asyncWaiters) {
				if (_awaiter.Task == awaiterTask) {
					awaiter = _awaiter;
					asyncWaiters.Remove(awaiter);
					awaiter.done = true;
					break;
				}
			}
		}
		// Complete cancelled async wait with TaskCanceledException
		if (awaiter != null) {
			// Release the resources associated with the async waiter, and
			// complete the TaskCompletionSource with TaskCanceledException.
            awaiter.Dispose(false);
            awaiter.SetCanceled();
        }
		return awaiter != null;
	}

    /**
	 * Asynchronous Task-based Asynchronous Pattern (TAP) interface.
	 */

    /**
	 * Wait asynchronously for the latch to open enabling, optionally, a timeout
	 * and/or cancellation.
	 */
    public Task<bool> WaitAsync(int timeout = Timeout.Infinite,
							    CancellationToken cToken = default(CancellationToken)) {
		// The "open" field is volatile, so the visibility is guaranteed
		if (open)
			return trueTask;
		lock(theLock) {
			// After acquire the lock we must re-check the latch state, because
			// however it may have been opened by another thread.
			if (open)
				return trueTask;

            // If the wait was specified as immediate, return failure
            if (timeout == 0)
				return falseTask;
			
			// If a cancellation was already requested return a task in the Canceled state
			if (cToken.IsCancellationRequested)
				return Task.FromCanceled<bool>(cToken);
						
			// Create a request node and insert it in requests queue
			AsyncWaiter awaiter = new AsyncWaiter(cToken);
			LinkedListNode<AsyncWaiter> awaiterNode = asyncWaiters.AddLast(awaiter);
		
			/**
			 * Activate the specified cancelers owning the lock.
			 */
			
			/**
			 * Since the timeout handler, that runs on a thread pool's worker thread,
			 * acquires the lock before access the "request.timer" and "request.cTokenRegistration"
			 * these assignements will be visible to timer handler.
			 */
			if (timeout != Timeout.Infinite)
				awaiter.timer = new Timer(timeoutHandler, awaiterNode, timeout, Timeout.Infinite);
			
			/**
			 * If the cancellation token is already in the canceled state, the cancellation
			 * delegate will run immediately and synchronously, which *causes no damage* because
			 * this processing is terminal and the implicit locks can be acquired recursively.
			 */
			if (cToken.CanBeCanceled)
            	awaiter.cTokenRegistration = cToken.Register(cancellationHandler, awaiterNode);
	
			// Return the Task<bool> that represents the async wait
			return awaiter.Task;
		}
    }

	/**
	 * Opens the latch
	 */
	public void Open() {
		// If the latch is already open return
		if (open)
			return;
		open = true;		// no more waits

        // A list to hold temporarily the async waiters to complete later
	    LinkedList<AsyncWaiter> toComplete = null;
		lock(theLock) {
			if (asyncWaiters.Count > 0)
				toComplete = asyncWaiters;
			asyncWaiters = null;
		}
		// Complete the tasks of the async waiters without owning the lock
		CompleteAsyncWaiters(toComplete);
	}

	/**
	 * Returns the latch state
	 */
	public bool IsOpen { get { return open; } }

    /**
	 *	Synchronous interface implemented using the asynchronous TAP interface.
	 */

    /**
	 * Wait synchronously for the latch to open enabling, optionally,
	 * timeout and/or cancellation.
	 */
    public bool Wait(int timeout = Timeout.Infinite,
					 CancellationToken cToken = default(CancellationToken)) {
		Task<bool> waitTask = WaitAsync(timeout, cToken); 
		try {
            return waitTask.Result;
        } catch (ThreadInterruptedException) {
			// Try to cancel the asynchronous request
			if (TryCancelWaitAsync(waitTask))
				throw;
			// We known that the request was already completed or cancelled, return the
			// underlying result. When waiting here, we must ignore interrupts.
			try {
				do {
					try {
						return waitTask.Result;
					} catch (ThreadInterruptedException) {
						// Here, we ignore all interrupts
					} catch (AggregateException ae) {
                		throw ae.InnerException;
					}
				} while (true);
            } finally {
				// Anyway, re-assert the interrupt
                Thread.CurrentThread.Interrupt();
            }
        } catch (AggregateException ae) {
            throw ae.InnerException;
        }
	}
}

/**
 * Test code
 */

internal class BooleanLatchTests {
	const int SETUP_TIME = 50;
	const int UNTIL_OPEN_TIME = 500;
	const int THREAD_COUNT = 10;
	const int EXIT_TIME = 100;
	const int WAIT_ASYNC_TIMEOUT = 100;


	internal static void Log(String msg) {
		Console.WriteLine($"[#{Thread.CurrentThread.ManagedThreadId:D2}]: {msg}");
	}

	private static void TestWaitAsync() {
		BooleanLatchAsync latch = new BooleanLatchAsync();
		CancellationTokenSource cts = new CancellationTokenSource();
		Thread[] waiters = new Thread[THREAD_COUNT];
		int timeout = Timeout.Infinite;
		//int timeout = WAIT_ASYNC_TIMEOUT;

		for (int i = 0; i < THREAD_COUNT; i++) {
			int li = i;

			waiters[i] = new Thread(() => {
				Log($"--[#{li}:D2]: waiter thread started");
				try {
					var waitTask = latch.WaitAsync(timeout: timeout, cToken: cts.Token);
					Log($"--[#{li:D2}]: returned from async wait");
					if (waitTask.Result)
						Log($"--[#{li:D2}]: latch opened");
					else
						Log($"--[#{li:D2}]: WaitAsync() timed out");
				} catch (AggregateException ae) {
					Log($"***[#{li:D2}]: {ae.InnerException.GetType()}: {ae.InnerException.Message}");
				} catch (ThreadInterruptedException) {
					Log($"***[#{li:D2}]: thread was interrupted");
				}
				Log($"--[#{li:D2}]: waiter thread exiting");
			});
			waiters[i].Start();

		}

		Thread.Sleep(SETUP_TIME + UNTIL_OPEN_TIME);
		Console.ReadLine();
		
		latch.Open();
		//cts.Cancel();

		Thread.Sleep(EXIT_TIME);

		for (int i = 0; i < THREAD_COUNT; i++) {
			if (waiters[i].IsAlive)
				waiters[i].Interrupt();
			waiters[i].Join();
		}
		Log("--test terminated");
	}

    static void Main() {
		TestWaitAsync();
    }
}



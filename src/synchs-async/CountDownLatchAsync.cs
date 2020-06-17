/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Count down latch with asynchronous and synchronous interfaces
 *
 *  Carlos Martins, June 2020
 *
 **/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

public class CountDownLatchAsync {

		// Type used to represent each asynchronous waiter
	private class AsyncWaiter: TaskCompletionSource<bool> {
		internal readonly CancellationToken cToken;
		internal CancellationTokenRegistration cTokenRegistration;
		internal Timer timer;
		internal bool done; 		// True when the async request is completed
		
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

	// The initial and current count
	private readonly int initialCount;
	private volatile int count;

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
	public CountDownLatchAsync(int initialCount = 1) {
		if (initialCount < 0)
			throw new ArgumentOutOfRangeException("initialCount");
		this.count = this.initialCount = initialCount;
		if (count > 0) {
	    	// If the latch is initialized as closed, initialize its fields
        	cancellationHandler = new Action<object>((awaiterNode) => WaitCancellationHandler(awaiterNode, true));
        	timeoutHandler = new TimerCallback((awaiterNode) => WaitCancellationHandler(awaiterNode, false));
        	asyncWaiters = new LinkedList<AsyncWaiter>();
		}
    }

	/**
	 * Auxiliary methods
	 */

	/**
	 * Common code when cancelling an async waiter.
	 */
    private void WaitCancellationHandler(object _awaiterNode, bool canceling) {
		LinkedListNode<AsyncWaiter> awaiterNode = (LinkedListNode<AsyncWaiter>)_awaiterNode;
		AsyncWaiter awaiter = awaiterNode.Value;
		bool complete = false;
		lock(theLock) {
			if (!awaiter.done) {
				asyncWaiters.Remove(awaiterNode);
				complete = awaiter.done = true;
			}
		}
		if (complete) {
			awaiter.Dispose(canceling);
			if (canceling)
				awaiter.SetCanceled();
			else
				awaiter.SetResult(false);
		}
	}

    /**
	 * Asynchronous Task-based Asynchronous Pattern (TAP) interface.
	 */

    /**
	 * Wait asynchronously for the count down latch to open enabling, optionally,
	 * a timeout and/or cancellation.
	 */
    public Task<bool> WaitAsync(int timeout = Timeout.Infinite,
							    CancellationToken cToken = default(CancellationToken)) {
		// The "count" field is volatile, so the visibility is guaranteed
		if (count == 0)
			return trueTask;
		
		lock(theLock) {
			// After acquire the lock we must re-check the count down latch state,
			// because however it may have been opened by another thread.
			if (count == 0)
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
	
			// Return the Task<bool> that represents the asynchronous operation
			return awaiter.Task;
		}
    }

	/**
	 *	Synchronous interface implemented using the asynchronous TAP interface.
	 */

	/**
	 * Try to cancel an asynchronous request identified by its task.
	 */
	private bool CancelWaitByTask(Task<bool> awaiterTask) {
        AsyncWaiter awaiter = null;
        lock(theLock) {
			foreach (AsyncWaiter _awaiter in asyncWaiters) {
				if (_awaiter.Task == awaiterTask) {
					awaiter = _awaiter;
					asyncWaiters.Remove(_awaiter);
					awaiter.done = true;
					break;
				}
			}
		}
		if (awaiter != null) {
			// Dispose resources and complete async wait with TaskCanceledException
            awaiter.Dispose(false);
            awaiter.SetCanceled();
        }
		return awaiter != null;
	}


    /**
	 * Wait synchronously for the count down latch to open enabling, optionally,
	 * timeout and/or cancellation.
	 */
    public bool Wait(int timeout = Timeout.Infinite,
					 CancellationToken cToken = default(CancellationToken)) {
		Task<bool> waitTask = WaitAsync(timeout, cToken); 
		try {
            return waitTask.Result;
        } catch (ThreadInterruptedException) {
			// Try to cancel the asynchronous request
			if (CancelWaitByTask(waitTask))
				throw;
			// We known that the request was already completed or cancelled,
			// return the underlying result
			try {
				do {
					try {
						return waitTask.Result;
					} catch (ThreadInterruptedException) {
						// ignore interrupts while we wait for the task's result
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
	 * Registers one or more signals with the CountdownLatch, decrementing the
	 * value of CurrentCount by the specified amount.
	 */
	public bool Signal(int signalCount = 1) {
		if (signalCount < 1)
			throw new ArgumentOutOfRangeException("signalCount");
		int c;
		do {
			c = count;
			if (c == 0 || signalCount > c)
				throw new InvalidOperationException();
		} while (Interlocked.CompareExchange(ref count, c - signalCount, c) != c);
		if (c > signalCount)
			return false;
		
		// The latch is now open, release all async waiters.
		// A list to hold temporarily the async waiters to complete later
		// after release the lock.
	    LinkedList<AsyncWaiter> toComplete = null;
		lock(theLock) {
			if (asyncWaiters.Count > 0)
				toComplete = asyncWaiters;
			asyncWaiters = null;
		}
		CompleteAsyncWaiters(toComplete);
		return true;
	}

	/**
	 * Increments the CurrentCount by a specified value.
	 */
	public void AddCount(int signalCount = 1) {
		if (signalCount < 1)
			throw new ArgumentOutOfRangeException("signalCount");
		int c;
		if (signalCount <= 0)
			throw new ArgumentOutOfRangeException("signalCount");
		do {
			c = count;
			if (c == 0 || c + signalCount < c)
				throw new InvalidOperationException();
		} while (Interlocked.CompareExchange(ref count, c + signalCount, c) != c);
	}

	/**
	 * Attempts to increment CurrentCount by a specified value.
	 */
	public bool TryAddCount(int signalCount = 1) {
		int c;
		do {
			c = count;
			if (c == 0)
				return false;
			if (c + signalCount < c)
				throw new InvalidOperationException();
		} while (Interlocked.CompareExchange(ref count, c + signalCount, c) != c);
		return true;
	}

	/**
	 * Gets the number of remaining signals required to open the latch.
	 */ 
	public int CurrentCount { get { return count; } }

	/**
	 * Gets the numbers of signals initially required to set the latch.
	 */
	public int InitialCount { get { return initialCount; } }

	/**
	 * Indicates whether the count down latch's current count has reached zero.
	 */
	public bool IsSet {
		get { return count == 0; }
	}
}

/**
 * Test code
 */

internal class CountDownLatchAsyncTests {
	const int SETUP_TIME = 50;
	const int UNTIL_OPEN_TIME = 500;
	const int THREAD_COUNT = 10;
	const int EXIT_TIME = 100;
	const int WAIT_ASYNC_TIMEOUT = 2000;


	internal static void Log(String msg) {
		Console.WriteLine($"[#{Thread.CurrentThread.ManagedThreadId:D2}]: {msg}");
	}

	private static void TestWaitAsync() {
		CountDownLatchAsync cdl = new CountDownLatchAsync(1);
		CancellationTokenSource cts = new CancellationTokenSource();
		Thread[] waiters = new Thread[THREAD_COUNT];
		//int timeout = Timeout.Infinite;
		int timeout = WAIT_ASYNC_TIMEOUT;

		for (int i = 0; i < THREAD_COUNT; i++) {
			int li = i;

			waiters[i] = new Thread(() => {
				Log($"--[#{li}:D2]: waiter thread started");
				if (li > 0)
					cdl.TryAddCount(1);
				try {
					var waitTask = cdl.WaitAsync(timeout: timeout, cToken: cts.Token);
					Log($"--[#{li:D2}]: returned from async wait");
					if (waitTask.Result)
						Log($"--[#{li:D2}]: count down latch opened");
					else
						Log($"--[#{li:D2}]: WaitAsync() timed out");
				} catch (AggregateException ae) {
					Log($"***[#{li:D2}]: {ae.InnerException.GetType().Name}: {ae.InnerException.Message}");
				} catch (ThreadInterruptedException) {
					Log($"***[#{li:D2}]: thread was interrupted");
				}
				Log($"--[#{li:D2}]: waiter thread exiting");
			});
			waiters[i].Start();
		}

		Thread.Sleep(SETUP_TIME + UNTIL_OPEN_TIME);
		Log("--press <enter> to signal");
		Console.ReadLine();
		//cts.Cancel();
		for (int i = 0; i < THREAD_COUNT; i++)
			cdl.Signal();

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



/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Blocking queue with asynchronous and synchronous interface (.NET)
 *
 *  Carlos Martins, June 2020
 *
 **/

/**
 * Comment the next line use .NET BCL queue
 */
//#define USE_OUR_QUEUE

/**
 * Comment/Uncomment to select tests
 */
//#define SYNC_INTERFACE_TEST	
//#define ASYNC_INTERFACE_TEST		
#define COMPUTE_THROUGHPUT		

/*
 * Uncomment to run the test continously until <enter>,
 * otherwise the test runs for 10 seconds.
 */
//#define RUN_CONTINOUSLY		

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

/**
 * BlockingQueue with synchronous and asynchronous interfaces.
 */
public class BlockingQueueAsync<T> where T: class {

	/**
	 * The base type used with the types that represent the async requests.
	 */
	private class AsyncRequest<V> : TaskCompletionSource<V> {
		internal readonly CancellationToken cToken;
		internal CancellationTokenRegistration cTokenRegistration;
		internal Timer timer;
		internal bool done;     // true when the async request is completed or canceled
		
		// initialize a waiter for a request
		internal AsyncRequest(CancellationToken cToken) {
			this.cToken = cToken;
		}
		/**
		 * Disposes resources associated with this async request.
		 *
		 * Note: when this method is called we are sure that the fields "timer"
		 *       and "cTokenRegistration" are correctly affected
		 */
		internal void Dispose(bool canceling = false) {
			// The CancellationTokenRegistration is disposed off after the cancellation
			// handler is called.
			if (!canceling && cToken.CanBeCanceled)
				cTokenRegistration.Dispose();
			timer?.Dispose();
		}
    }

	/**
	 * The type used to represent an asynchronous take request.
	 */
	private class AsyncTake: AsyncRequest<T> {
		internal T receivedMessage;

		// initialize a waiter for an async take request
		internal AsyncTake(CancellationToken cToken) : base (cToken) {}
	}

	/**
	 * The type used to represent an asynchronous put request.
	 */
	private class AsyncPut : AsyncRequest<bool> {
		internal readonly T sentMessage;
        
		internal AsyncPut(T sentMessage, CancellationToken cToken) : base (cToken) {
			this.sentMessage = sentMessage;
		}
	}
	
	// The global lock
	private readonly object theLock = new object();

	// The queue capacity
	private readonly uint capacity;
	private readonly uint mask;
	
	// Available messages and put and get indexes
	private readonly T[] messageRoom;
	private uint takeIdx, putIdx;
	
	// The state of the queue
	private const int OPERATING = 0, COMPLETING = 1, COMPLETED = 2;
	static volatile int state;

	// The queue of pending take requests
	private readonly LinkedList<AsyncTake> asyncTakes;

	// The queue of pending put requests
	private readonly LinkedList<AsyncPut> asyncPuts;

	/**
	 * Delegates with cancellation handlers for asynchrounous requests 
	 */
	private readonly Action<object> takeCancellationHandler;
	private readonly TimerCallback takeTimeoutHandler;
	private readonly Action<object> putCancellationHandler;
	private readonly TimerCallback putTimeoutHandler;

	/**
	 * Completed task used to return null (failed Take) and success or
	 * failure on Put.
	 */
	private static readonly Task<T> nullTask = Task.FromResult<T>(null);
	private static readonly Task<bool> falseTask = Task.FromResult<bool>(false);
	private static readonly Task<bool> trueTask = Task.FromResult<bool>(true);

	/**
	 * Compute the next power of 2
	 */
	private static uint NextPowerOfTwo(uint value)  {
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return ++value;
	}
	
	private const int MAX_QUEUE_SIZE = 128 * 1024;
	
	/**
	 * Constructor
	 */
	public BlockingQueueAsync(uint capacity = MAX_QUEUE_SIZE) {
		if (capacity == 0 || capacity > MAX_QUEUE_SIZE)
			throw new ArgumentOutOfRangeException("_capacity");

		// Initialize the list of messages and a lists of pending messages
		this.capacity = NextPowerOfTwo(capacity);
		mask = capacity - 1;
		messageRoom = new T[this.capacity];
		putIdx = takeIdx = 0;
		state = OPERATING;
		asyncTakes = new LinkedList<AsyncTake>();
		asyncPuts = new LinkedList<AsyncPut>();
		
		/**
		 * Construct the delegates to describe cancellation handlers
		 */
		takeCancellationHandler = new Action<object>((takeNode) => TakeCancellationHandler(takeNode, true));
		takeTimeoutHandler = new TimerCallback((takeNode) => TakeCancellationHandler(takeNode, false));
		putCancellationHandler = new Action<object>((putNode) => PutCancellationHandler(putNode, true));
		putTimeoutHandler = new TimerCallback((putNode) => PutCancellationHandler(putNode, false));
	}
	
	/**
	 * Cancellation handlers
	 */

	/**
	 * Try to cancel an async take request.
	 */
	private void TakeCancellationHandler(object _takeNode, bool canceling) {
		LinkedListNode<AsyncTake> takeNode = (LinkedListNode<AsyncTake>)_takeNode;
		AsyncTake take = null;
		// Acquire the lock to access the shared mutable state
		lock(theLock) {
			/**
			 * Here, the async take request can be completed or canceled
			 */
			if (!takeNode.Value.done) {
				// Remove the async take from the queue and mark it as canceled
				asyncTakes.Remove(takeNode);
				take = takeNode.Value;
				take.done = true;
			}
		}
		if (take != null) {
            // Dispose resources associated with this async take request.
            take.Dispose(canceling);

            // Complete the underlying task properly.
            if (canceling)
                take.SetCanceled();
            else
                take.SetResult(null);
        }
	}
	
	/**
	 * Try to cancel an async put request.
	 */
     private void PutCancellationHandler(object _putNode, bool canceling) {
        LinkedListNode<AsyncPut> putNode = (LinkedListNode<AsyncPut>)_putNode;
        AsyncPut put = null;
		lock(theLock) {
			if (!putNode.Value.done) {
				asyncPuts.Remove(putNode);
				put = putNode.Value;
				put.done = true;
			}
		}
		if (put != null) {
			// Dispose resources associated with this async put request.
			put.Dispose(canceling);
			
			// Complete the underlying task properly.
			if (canceling)
				put.SetCanceled();
			else
				put.SetResult(false);
		}
	}
    
	/**
	 * Signals end-of-stream
	 */
	public void Complete() {
		List<AsyncTake> completed = null;
		lock(theLock) {
			if (state != OPERATING)
				return;
			if (putIdx - takeIdx > 0) {
				state = COMPLETING;
				return;
			}
			state = COMPLETED;
			if (asyncTakes.Count == 0) 
				return;
			completed = new List<AsyncTake>(asyncTakes.Count);
			foreach (AsyncTake take in asyncTakes)
				completed.Add(take);
            asyncTakes.Clear();
        }
		// Complete tasks in the faulted state, after release the lock
        foreach (AsyncTake take in completed) {
            take.Dispose();
            take.SetException(new InvalidOperationException());   // end-of-stream exception
        }
    }

    /**
     * Signals end-of-stream.
     */
    public void CompleteAdding() { Complete(); }

    /**
     * Returns trie if the method Complete was already called.
     */
    public bool IsCompleted {
        get { return state != OPERATING; }	// state is declared as volatile
    }

    /**
	 * Asynchronous TAP interface
	 */

    /**
     * Take a message from the queue asynchronously enabling, optionally,
     * timeout and/or cancellation.
     */
    public Task<T> TakeAsync(int timeout = Timeout.Infinite,
							 CancellationToken cToken = default(CancellationToken)) {
		Task<T> takeTask = null;
		AsyncPut put = null;
		lock(theLock) {
			if (putIdx - takeIdx > 0) {
				// Immediate take
				takeTask = Task.FromResult<T>(messageRoom[takeIdx++ & mask]);

				// Try to satisfy a pending async put request
				if (asyncPuts.Count > 0) {
					put = asyncPuts.First.Value;
					asyncPuts.RemoveFirst();
					messageRoom[putIdx++ & mask] = put.sentMessage;
					put.done = true;
				}
				
				/**
				 * If the queue is in the COMPLETING state, the message queue is empty and
				 * we released the last pending put, then transition the queue to the
				 * COMPLETED state.
				 */
				if (putIdx == takeIdx && state == COMPLETING && asyncPuts.Count == 0)
				state = COMPLETED;
			} else {
				// If the queue was already completed or an immediate take was spedified
				// return failure.
				if (state != OPERATING)
					throw new InvalidOperationException();
				if (timeout == 0)
					return nullTask;
				
				// If a cancellation was requested return a task in the Canceled state
				if (cToken.IsCancellationRequested)
					return Task.FromCanceled<T>(cToken);
						
				// Create a waiter node and insert it in the wait queue
				AsyncTake take = new AsyncTake(cToken);
				LinkedListNode<AsyncTake> takeNode = asyncTakes.AddLast(take);
		
				/**
                 * Activate the specified cancellers owning the lock.
				 * Since the timeout handler acquires the lock before use the "take.timer" and
				 * "take.cTokenRegistration" the assignements will be visible.
                 */
				if (timeout != Timeout.Infinite)
					take.timer = new Timer(takeTimeoutHandler, takeNode, timeout, Timeout.Infinite);
			
				/**
                 * If the cancellation token is already in the cancelled state, the cancellation
                 * will run immediately and synchronously, which causes no damage because the
                 * implicit locks can be acquired recursively and this is a terminal processing.
                 */
				if (cToken.CanBeCanceled)
            		take.cTokenRegistration = cToken.Register(takeCancellationHandler, takeNode);
	
				// Set the result task that represents the asynchronous operation
				takeTask = take.Task;
			}
		}
		// If we released any putter, cancel its cancellers and complete its task.
		if (put != null) {
			put.Dispose();
			put.SetResult(true);
		}
		return takeTask;
    }

    /**
     * Put a message on the queue asynchronously enabling, optionally,
     * a timeout and/or cancellation.
     */
     public Task<bool> PutAsync(T sentMessage, int timeout = Timeout.Infinite,
	 						    CancellationToken cToken = default(CancellationToken)) {
		Task<bool> putTask = null;
		AsyncTake take = null;
		lock (theLock) {
			if (state != OPERATING)
			throw new InvalidOperationException();
			if ((putIdx - takeIdx) < capacity) {
				// Do an immediate put
				messageRoom[putIdx++ & mask] = sentMessage;
				putTask = trueTask;
				
				// Try to satisfy a pending async take request.
				if (asyncTakes.Count > 0) {
					take = asyncTakes.First.Value;
					asyncTakes.RemoveFirst();
					take.receivedMessage = messageRoom[takeIdx++ & mask];
					take.done = true;
				}
			} else {
				// The current thread must block, so we check for immediate cancelers
				if (timeout == 0)
					return falseTask;
				
				// If a cancellation was requested return a task in the Canceled state
				if (cToken.IsCancellationRequested)
					return Task.FromCanceled<bool>(cToken);
				
				// Create a waiter node and insert it in the wait queue
				AsyncPut put = new AsyncPut(sentMessage, cToken);
				LinkedListNode<AsyncPut> putNode = asyncPuts.AddLast(put);

    			/**
				 * Activate the specified cancelers owning the lock.
				 * Since the timeout handler acquires the lock before use the "put.timer" and
				 * "put.cTokenRegistration" the assignements will be visible.
                 */
				if (timeout != Timeout.Infinite)
					put.timer = new Timer(putTimeoutHandler, putNode, timeout, Timeout.Infinite);

 				/**
				 * If the cancellation token is already in the canceled state, the cancellation
				 * will run immediately and synchronously, which causes no damage because the
				 * implicit locks can be acquired recursively and this is a terminal processing.
				 */
				if (cToken.CanBeCanceled)
					put.cTokenRegistration = cToken.Register(putCancellationHandler, putNode);
				
				// Set the result task that represents the asynchronous operation
				putTask = put.Task;
			}
		}
		// If we released any putter, cancel its cancellers and complete its tasks.
		if (take != null) {
			take.Dispose();
			take.SetResult(take.receivedMessage);
		}
		return putTask;
	}

    /**
	 *	Synchronous interface based on asynchronous TAP interface
	 */

   /**
    * Try to cancel an asynchronous take represented by its task.
    */
    private bool CancelTakeByTask(Task<T> takeTask) {
        AsyncTake take = null;
        lock(theLock) {
			foreach (AsyncTake _take in asyncTakes) {
				if (_take.Task == takeTask) {
					take = _take;
					asyncTakes.Remove(_take);
					take.done = true;
					break;
				}
			}
		}
		if (take != null) {
			take.Dispose();
			take.SetCanceled();
		}
		return take != null;
	}

	/**
	 * Take a message from the queue synchronously enabling, optionally,
	 * timeout and/or cancellation.
	 */
	public T Take(int timeout = Timeout.Infinite,
				  CancellationToken cToken = default(CancellationToken)) {
		Task<T> takeTask = TakeAsync(timeout, cToken); 
		try {
			return takeTask.Result;
		} catch (ThreadInterruptedException) {
			// Try to cancel the asynchronous request
			if (CancelTakeByTask(takeTask))
				throw;
			
			// The request was already completed or cancelled, return the
			// underlying result. We ignore any later interrupts.
			try {
				do {
					try {
						return takeTask.Result;
					} catch (ThreadInterruptedException) {
						// Ignore additional interrupts
					} catch (AggregateException ae) {
						throw ae.InnerException;
					}
				} while (true);
			} finally {
				// Anyway re-assert the first interrupt
				Thread.CurrentThread.Interrupt();
			}
		} catch (AggregateException ae) {
			throw ae.InnerException;
		}
	}
  
	/**
	 * Try to cancel an asynchronous put represented by its task.
	 */
	private bool CancelPutByTask(Task<bool> putTask) {
		AsyncPut put = null;
		lock(theLock) {
			foreach (AsyncPut _put in asyncPuts) {
				if (_put.Task == putTask) {
					put = _put;
					asyncPuts.Remove(_put);
					break;
				}
			}
		}
		if (put != null) {
			put.Dispose();
			put.SetCanceled();
		}
		return put != null;
	}

	/**
	 * Put a message into queue synchronously enabling, optionally
	 * timeout and/or cancellation.
	 */
	public bool Put(T sentMessage, int timeout = Timeout.Infinite,
					CancellationToken cToken = default(CancellationToken)) {
		Task<bool> putTask = PutAsync(sentMessage, timeout, cToken);
		try {
			return putTask.Result;
		} catch (ThreadInterruptedException) {
			// Try to cancel the asynchronous put request
            if (CancelPutByTask(putTask))
				// If the put was canceled, throw interrupted exception
				throw;
			
			// The request was already completed or cancelled, return the
			// underlying result. Here we ignore later interrupts.
			try {
				do {
					try {
						return putTask.Result;
					} catch (ThreadInterruptedException) {
						// We ignore additional interrupts
					} catch (AggregateException ae) {
						throw ae.InnerException;
					}
				} while (true);
			} finally {
				// Anyway re-assert the interrupt
				Thread.CurrentThread.Interrupt();
			}
		} catch (AggregateException ae) {
			throw ae.InnerException;
		}
	}

	/**
	 * Add a message to the queue enabling, optionally,
	 * timeout and/or cancellation.
	*/
	public void Add(T message, CancellationToken cToken = default(CancellationToken)) {
		Put(message, Timeout.Infinite, cToken);
	}

	/**
	 * Return the number of items in the queue.
	 */
	public int Count {
		get { lock(theLock) return (int)(takeIdx - putIdx); }
	}
}

/**
 * Test code
 */

internal class BlockingQueueAsyncTests {

	// Test the blocking queue using the synchron//ous interface	
	private static bool TestSyncInterface() {

#if (!RUN_CONTINOUSLY)
		const int RUN_TIME = 10 * 1000;
#endif
		const int EXIT_TIME = 50;
		const int PRODUCER_THREADS = 10;
		const int CONSUMER_THREADS = 20;
		const int QUEUE_SIZE = (PRODUCER_THREADS / 2) + 1;
		const int MIN_TIMEOUT = 1;
		const int MAX_TIMEOUT = 50;
		const int MIN_CANCEL_INTERVAL = 50;
		const int MAX_CANCEL_INTERVAL = 100;		
		const int MIN_PAUSE_INTERVAL = 10;
		const int MAX_PAUSE_INTERVAL = 100;
		const int PRODUCTION_ALIVE = 5000;
		const int CONSUMER_ALIVE = 5000;
		
		Thread[] pthrs = new Thread[PRODUCER_THREADS];
		Thread[] cthrs = new Thread[CONSUMER_THREADS];
		int[] productions = new int[PRODUCER_THREADS];
        int[] productionTimeouts = new int[PRODUCER_THREADS];
        int[] productionCancellations = new int[PRODUCER_THREADS];
        int[] consumptions = new int[CONSUMER_THREADS];
		int[] consumptionTimeouts = new int[CONSUMER_THREADS];
		int[] consumptionCancellations = new int[CONSUMER_THREADS];
		
		bool exit = false;
        BlockingQueueAsync<string> queue = new BlockingQueueAsync<string>(QUEUE_SIZE);

		// Create and start consumer threads.
		
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			int ctid = i;
			cthrs[i] = new Thread(() => {
				Random rnd = new Random(ctid);
				CancellationTokenSource cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL)); 
				do {
					do {
						try {
							if (queue.Take(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT), cts.Token) != null) {
								consumptions[ctid]++;
								break;
							} else
								consumptionTimeouts[ctid]++;
						} catch (OperationCanceledException) {
							consumptionCancellations[ctid]++;
							cts.Dispose();
					 		cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
						} catch (ThreadInterruptedException) {
							break;
                    	} catch (Exception e) {
                        	Console.WriteLine($"***Exception: {e.GetType()}: {e.Message}");
                        	break;
						}
                    } while (true);
					if (consumptions[ctid] % CONSUMER_ALIVE == 0) {
						Console.Write($"[#c{ctid:D2}]");
						try {
							Thread.Sleep(rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL));
						} catch (ThreadInterruptedException) {
							break;
						}
					}
				} while (!Volatile.Read(ref exit));
			});
			cthrs[i].Priority = ThreadPriority.Highest;
			cthrs[i].Start();
		}
		
		// Create and start producer threads.
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			int ptid = i;
			pthrs[i] = new Thread(() => {
				Random rnd = new Random(ptid);
				CancellationTokenSource cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL)); 
				do {
                    do {
                        try {
                            if (queue.Put(rnd.Next().ToString(), rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT),
                                          cts.Token)) {
                                productions[ptid]++;
                                break;
                            } else
                                productionTimeouts[ptid]++;
                        } catch (OperationCanceledException) {
                            productionCancellations[ptid]++;
                            cts.Dispose();
                            cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                        } catch (ThreadInterruptedException) {
                            break;
                        } catch (Exception e) {
                            Console.WriteLine($"***Exception: {e.GetType()}: {e.Message}");
                            break;
                        }
                    } while (true);
					if (productions[ptid] % PRODUCTION_ALIVE == 0) {
						Console.Write($"[#p{ptid:D2}]");
						try {
							Thread.Sleep(rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL));
						} catch (ThreadInterruptedException) {
							break;
						}
					} else
						Thread.Yield();
				} while (!Volatile.Read(ref exit));
			});
			pthrs[i].Start();
		}
		
		// run the test for a while
		long startTime = Environment.TickCount;
		do {
			Thread.Sleep(50);
			if (Console.KeyAvailable) {
				Console.Read();
				break;
			}
#if RUN_CONTINOUSLY
		} while (true);
#else
		} while (Environment.TickCount - startTime < RUN_TIME);
#endif		
		Volatile.Write(ref exit, true);
		Thread.Sleep(EXIT_TIME);
		
		// Wait until all producer have been terminated.
		int sumProductions = 0;
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (pthrs[i].IsAlive)
				pthrs[i].Interrupt();
			pthrs[i].Join();
			sumProductions += productions[i];
		}

		int sumConsumptions = 0;
		// Wait until all consumer have been terminated.
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (cthrs[i].IsAlive) {
				cthrs[i].Interrupt();
			}
			cthrs[i].Join();
			sumConsumptions += consumptions[i];
		}
		
		// Display consumer results
		Console.WriteLine("\nConsumer counters:");
		for (int i = 0; i < CONSUMER_THREADS; i++) {
			if (i != 0 && i % 2 == 0) {
				Console.WriteLine();
			} else if (i != 0) {
				Console.Write(' ');
			}
			Console.Write("[#c{0:D2}: {1}/{2}/{3}]", i, consumptions[i], consumptionTimeouts[i],
							consumptionCancellations[i]);
		}
		
		// consider not consumed productions
		sumConsumptions += queue.Count;
		
		Console.WriteLine("\nProducer counters:");
		for (int i = 0; i < PRODUCER_THREADS; i++) {
			if (i != 0 && i % 2 == 0) {
				Console.WriteLine();
			} else if (i != 0){
				Console.Write(' ');
			}
			Console.Write($"[#p{i:D2}: {productions[i]}/{productionTimeouts[i]}/{productionCancellations[i]}]");
		}
		Console.WriteLine($"\n--productions: {sumProductions}, consumptions: {sumConsumptions}");
		return sumConsumptions == sumProductions;
	}

    // Test the blocking queue using the asynchronous interface	
    private static bool TestAsyncInterface() {

#if (!RUN_CONTINOUSLY)
        const int RUN_TIME = 10 * 1000;
#endif
        const int EXIT_TIME = 50;
        const int PRODUCER_THREADS = 10;
        const int CONSUMER_THREADS = 20;
        const int QUEUE_SIZE = (PRODUCER_THREADS / 2) + 1;
        const int MIN_TIMEOUT = 1;
        const int MAX_TIMEOUT = 50;
        const int MIN_CANCEL_INTERVAL = 50;
        const int MAX_CANCEL_INTERVAL = 100;
        const int MIN_PAUSE_INTERVAL = 10;
        const int MAX_PAUSE_INTERVAL = 100;
        const int PRODUCTION_ALIVE = 1000;
        const int CONSUMER_ALIVE = 1000;

        Thread[] pthrs = new Thread[PRODUCER_THREADS];
        Thread[] cthrs = new Thread[CONSUMER_THREADS];
        int[] productions = new int[PRODUCER_THREADS];
        int[] productionTimeouts = new int[PRODUCER_THREADS];
        int[] productionCancellations = new int[PRODUCER_THREADS];
        int[] consumptions = new int[CONSUMER_THREADS];
        int[] consumptionTimeouts = new int[CONSUMER_THREADS];
        int[] consumptionCancellations = new int[CONSUMER_THREADS];

        bool exit = false;
        BlockingQueueAsync<string> queue = new BlockingQueueAsync<string>(QUEUE_SIZE);
        //BlockingQueueAsync<string> queue = new BlockingQueueAsync<string>();

        // Create and start consumer threads.
        for (int i = 0; i < CONSUMER_THREADS; i++) {
            int ctid = i;
            cthrs[i] = new Thread(() => {
                Random rnd = new Random(ctid);
                CancellationTokenSource cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                while (!Volatile.Read(ref exit)) {
                    try {
                        if (queue.TakeAsync(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT), cts.Token).Result == null) {
                            consumptionTimeouts[ctid]++;
                            continue;
                        }
                     } catch (AggregateException ae) {
                        if (ae.InnerException is TaskCanceledException) {
                            consumptionCancellations[ctid]++;
                            cts.Dispose();
                            cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                        } else {
                            Console.WriteLine($"***Exception: {ae.InnerException.GetType()}: {ae.InnerException.Message}");
                        }
                        continue;
                    } catch (ThreadInterruptedException) {
                            break;
                    }
                    if (++consumptions[ctid] % CONSUMER_ALIVE == 0) {
                        Console.Write($"[#c{ctid:D2}]");
                        try {
                            Thread.Sleep(rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL));
                        } catch (ThreadInterruptedException) {
                            break;
                        }
                    }
                }
            });
            cthrs[i].Priority = ThreadPriority.Highest;
            cthrs[i].Start();
        }

        // Create and start producer threads.
        for (int i = 0; i < PRODUCER_THREADS; i++)
        {
            int ptid = i;
            pthrs[i] = new Thread(() => {
                Random rnd = new Random(ptid);
                CancellationTokenSource cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                do {
                    do {
                        try {
                            var putTask = queue.PutAsync(rnd.Next().ToString(), rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT), cts.Token);
                            if (putTask.Result) {
                                productions[ptid]++;
                                break;
                            }
                            else
                                productionTimeouts[ptid]++;
                        }
                        catch (AggregateException ae) {
                            if (ae.InnerException is TaskCanceledException) {
                                productionCancellations[ptid]++;
                                cts.Dispose();
                                cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                            } else {
                                Console.WriteLine($"***Exception: {ae.InnerException.GetType()}: { ae.InnerException.Message}");
                                break;
                            }
                        }
                        catch (ThreadInterruptedException) {
                            break;
                        }
                    } while (true);
                    if (productions[ptid] % PRODUCTION_ALIVE == 0) {
                        Console.Write($"[#p{ptid:D2}]");
                        try {
                            Thread.Sleep(rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL));
                        } catch (ThreadInterruptedException) {
                            break;
                        }
                    }
                    else
                        Thread.Yield();
                } while (!Volatile.Read(ref exit));
            });
            pthrs[i].Start();
        }

        // run the test for a while
        long startTime = Environment.TickCount;
        do {
            Thread.Sleep(50);
            if (Console.KeyAvailable) {
                Console.Read();
                break;
            }
#if RUN_CONTINOUSLY
        } while (true);
#else
        } while (Environment.TickCount - startTime < RUN_TIME);
#endif
        Volatile.Write(ref exit, true);
        Thread.Sleep(EXIT_TIME);

        // Wait until all producer have been terminated.
        int sumProductions = 0;
        for (int i = 0; i < PRODUCER_THREADS; i++)
        {
            if (pthrs[i].IsAlive)
                pthrs[i].Interrupt();
            pthrs[i].Join();
            sumProductions += productions[i];
        }

        int sumConsumptions = 0;
        // Wait until all consumer have been terminated.
        for (int i = 0; i < CONSUMER_THREADS; i++)
        {
            if (cthrs[i].IsAlive)
                cthrs[i].Interrupt();
            cthrs[i].Join();
            sumConsumptions += consumptions[i];
        }

        // Display consumer results
        Console.WriteLine("\nConsumer counters:");
        for (int i = 0; i < CONSUMER_THREADS; i++)
        {
            if (i != 0 && i % 2 == 0)
                Console.WriteLine();
            else if (i != 0)
                Console.Write(' ');
            Console.Write($"[#c{i:D2}: {consumptions[i]}/{consumptionTimeouts[i]}/{consumptionCancellations[i]}]");
        }

        // consider not consumed productions
        Console.WriteLine($"\n--to consume: {queue.Count}");

        sumConsumptions += queue.Count;

        Console.WriteLine("\nProducer counters:");
        for (int i = 0; i < PRODUCER_THREADS; i++) {
            if (i != 0 && i % 2 == 0)
                Console.WriteLine();
            else if (i != 0)
                Console.Write(' ');
            Console.Write($"[#p{i:D2}: {productions[i]}/{productionTimeouts[i]}/{productionCancellations[i]}]");
        }
        Console.WriteLine($"\n--productions: {sumProductions}, consumptions: {sumConsumptions}");
        return sumConsumptions == sumProductions;
    }



    // Test the blocking queue using the asynchronous interface	
    private static int ComputeThroughput() {

#if (!RUN_CONTINOUSLY)
        const int RUN_TIME = 10 * 1000;
#endif
        const int QUEUE_SIZE = 64 * 1024;

        Thread producer, consumer;
        int productions = 0, consumptions = 0;
#if (USE_OUR_QUEUE)
        BlockingQueueAsync<string> queue = new BlockingQueueAsync<string>(QUEUE_SIZE);
#else
        BlockingCollection<string> queue = new BlockingCollection<string>(QUEUE_SIZE);
#endif
        Console.Write("--testing...");
        CancellationTokenSource cts = new CancellationTokenSource();
        // Create the consumer and producer threads
        consumer = new Thread(() => {
            do {
                try {
#if (USE_OUR_QUEUE)
                    string data = queue.Take(cToken: cts.Token);
#else
                    string data = queue.Take(cts.Token);
#endif
                    consumptions++;
                } catch (OperationCanceledException) {
                    Console.WriteLine("***Take canceled!");
                } catch (InvalidOperationException) {
                    break;
                }
            } while (true);
            //Console.WriteLine("\n--consumer exiting...");
        });
        consumer.Priority = ThreadPriority.Highest;
        producer = new Thread(() => {
            do {
                try {
                    queue.Add("data", cts.Token);
                    productions++;
                } catch (InvalidOperationException) {
                    break;
                } catch (OperationCanceledException) {
                }
            } while (true);
            //Console.WriteLine("\n--producer exiting...");
        });
        // start threads
        consumer.Start();
        producer.Start();
        Stopwatch sw = Stopwatch.StartNew();
        // run the test for a while
        long startTime = Environment.TickCount;
        do {
            Thread.Sleep(100);
            //Console.Write('.');
            if (Console.KeyAvailable) {
                Console.Read();
                break;
            }
#if (RUN_CONTINOUSLY)
        } while (true);
#else
		} while ((Environment.TickCount - startTime) < RUN_TIME);
#endif
        Console.WriteLine("\n--complete adding");
        queue.CompleteAdding();
        // Wait until the producer and consumer have been terminated.
        producer.Join();
        consumer.Join();
        sw.Stop();
        consumptions += queue.Count;
        //Console.WriteLine($"consumptinos: {consumptions}; productions: {productions}");
        long unitCost = (sw.ElapsedMilliseconds * 1000000) / consumptions;
        Console.WriteLine($"--unit cost: {unitCost} ns");
        return productions;
    }
    static void Main() {

#if SYNC_INTERFACE_TEST
		
		Console.WriteLine("\n-->test blocking queue using the synchronous interface: {0}",
						  TestSyncInterface() ? "passed" : "failed");
#endif

#if ASYNC_INTERFACE_TEST
		
		Console.WriteLine("\n-->test blocking queue using the asynchronous interface: {0}",
						  TestAsyncInterface() ? "passed" : "failed");
#endif
#if COMPUTE_THROUGHPUT

        Console.WriteLine($"-->test thoughput: {ComputeThroughput()} productions/consumptions");
#endif

    }
}



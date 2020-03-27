/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Pseudo-code for implementing a generic synchronizer using using
 *  the "kernel style" and the .NET Framework.
 *
 *  Realizations for the case of semaphore and message queue synchronizers.
 *
 *  Carlos Martins, October 2018
 */

#define EXCLUDE_PSEUDO_CODE

using System;
using System.Threading;
using System.Collections.Generic;

/**
  * Generic classes used for arguments and results
  */

#if (!EXCLUDE_PSEUDO_CODE)

public class SynchState {}
public class InitializeArgs {}
public class AcquireArgs {}
public class AcquireResult {}
public class ReleaseArgs {}

#endif

/**
 * This class implements a generic synchronizer using pseudo-code to express
 * data access and control synchronization (not considering the availability
 * of monitors).
 *
 * NOTE: This code does not provide for the possibility of a thread blocked being
 *       interrupted (see implementation based on implicit .NET monitors below).
 */

#if (!EXCLUDE_PSEUDO_CODE)

public class GenericSynchronizerKernelStylePseudoCode {
    // lock used to synchronize access to mutable shared state
    private readonly Lock _lock = new Lock();

    // queue where block threads are (also protected by "_lock") needed to
    // implement control synchronization inherent to acquire/release semantics.
    private readonly WaitQueue waitQueue = new WaitQueue();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and the status (not done/done)
	private class Request {
        readonly AcquireArgs acquireArgs;	// acquire arguments
		AcquireResult acquireResult;	    // acquire result
		bool done;						    // true when the acquire is done

		Request(AcquireArgs args) { acquireArgs = args; }
	}
	
	// queue of pending acquire requests
    private readonly LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// synchonization state
	private SynchState syncState;
	
	// initializes the synchronizer
	public GenericSynchronizerKernelStyle(InitialArgs initiallArgs) {
		initialize "syncState" according to information specified in initialArgs;
	}
	
	// returns true if synchronization state allows an immediate acquire on behalf
    // of the thread that is at the head of request queue, or on behalf of current
    // thread if the request queue is empty.
	private bool CanAcquire(AcquireArgs acquireArgs) {
		returns true if "syncState" allows an immediate acquire according to "acquireArgs";
	}
	
	// executes the processing associated with a successful acquire, and returns
	// the proper acquire result, if any.
	private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
		update "state" according to "acquireArgs" after a successful acquire;
		return "the-proper-acquire-result";
	}
	
	// update synchronization state due to a release operation according to "releaseArgs".
	private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
		update "state" according to "releaseArgs";
	}
	
	// generic acquire operation
	public AcquireResult Acquire(AcquireArgs acquireArgs) {
		_lock.Acquire();
        try {
		    if (reqQueue.Count == 0 && CanAcquire(acquireArgs))
			    return AcquireSideEffect(acquireArgs);
		    Request request = new Request(acquireArgs);
		    reqQueue.AddLast(request);	// enqueue the "request" at the end of the request queue
		    do {
			    enqueue current thread on the "waitQueue" sensible to posterior wakeups done by "_lock" owners;
			    int depth = _lock.ReleaseAll();
			    block current thread until it is waked up by a releaser thread;
			    _lock.ReAcquire(depth);
		    } while (!request.done);
            // the requested acquire operation completed successfully
            return request.acquireResult;
        } finally {
            _lock.Release();
        }
	}

    // perform as many pending acquires as possible
    private void PerformPossibleAcquires() {
        while (reqQueue.Count > 0) {
            Request request = reqQueue.First.Value;
            if (!CanAcquire(request.acquireArgs))
                break;
            // remove request from queue and satisfy it
            reqQueue.RemoveFirst();
            request.result = AcquireSideEffect(request.acquireArgs);
            request.done = true;
        }
        wake up at least all blocked threads whose acquires have been met;
    } 
	
	// generic release operations
    public void Release(ReleaseArgs releaseArgs) {
		_lock.Acquire();
        try {
		    UpdateStateOnRelease(releaseArgs);
            PerformPossibleAcquires();
        } finally {
		    _lock.Release();
        }
	}
}
#endif

/**
 * The generic synchronizer based on an *implicit .NET monitor*, with
 * support for timeout on the acquire operation.
 *
 * Notes:
 *  1. This implementation takes obviously into account the possible interruption
 *     of the threads blocked on the condition variables of the monitor; 
 *  2. The code structure is slightly changed due to the possibility of cancellation of
 *     the acquire operations due to timeout or interruption.
 */

#if (!EXCLUDE_PSEUDO_CODE)

public class GenericSynchronizerKernelStyleImplicitMonitor {
    // implicit .NET monitor provides synchronization of the access to the shared
    // mutable state, supports the control synchronization inherent to the
    // acquire/release semantics.
    private readonly Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private class Request {
        internal readonly AcquireArgs acquireArgs;   // acquire arguments
        internal AcquireResult acquireResult;        // acquire result
        internal bool done;                          // true when the acquire is done

        internal Request(AcquireArgs args) { acquireArgs = args; }
    }
    
    // queue of pending acquire requests
    private readonly LinkedList<Request> reqQueue = new LinkedList<Request>();
    
	// synchonization state
    private SynchState syncState;

    public GenericSynchronizerKernelStyleImplicitMonitor(InitializeArgs initialArgs) {
        initialize "syncState" according to information specified in initialArgs;
    }

    // returns true if the synchronization state allows the acquire on behalf of the
    // thread that is at the head of the queue or the current thread if the queue is empty.
    private bool CanAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" allows an immediate acquire according to "acquireArgs";
    }

    // returns true if the state of synchronization allows the acquire on behalf of
    // the thread that is at the head of the queue.
    private bool CurrentSynchStateAllowsAquire() {
        returns true if the current synchronization state allows acquire(s);
    }

    // executes the processing associated with a successful acquire and
    // returns the proper acquire result (if any)
    private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
        update "state" according to "acquireArgs" after a successful acquire;
        return "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation according to "releaseArgs".
    private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
        // update "state" according to "releaseArgs";
    }

	// generic acquire operation; returns false when it times out
    public bool Acquire(AcquireArgs acquireArgs, out AcquireResult result, int timeout = Timeout.Infinite) {
        lock(monitor) {
        	if (reqQueue.Count == 0 && CanAcquire(acquireArgs)) {
            	result = AcquireSideEffect(acquireArgs);
				return true;
			}
        	Request request = new Request(acquireArgs);
        	reqQueue.AddLast(request);  // enqueue "request" at the end of the request queue
            TimeoutHolder th = new TimeoutHolder(timeout);
            do {
				if ((timeout = th.Value) == 0) {
                    // the timeout limit has expired - here we are sure that the acquire resquest
                    // is still pending. So, we remove the request from the queue and return failure.
                    reqQueue.Remove(request);
                    
                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAquire())
                        PerformPossibleAcquires();

					result = default(AcquireResult);
					return false;
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
                    // the thread may be interrupted when the requested acquire operation
					// is already performed, in which case you can no longer give up
					if (request.done) {
                        // re-assert the interrupt and return normally, indicating to the
						// caller that the operation was successfully completed
                    	Thread.CurrentThread.Interrupt();
						break;
					}
					// remove the request from the queue and throw ThreadInterruptedException
					reqQueue.Remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAquire())
                        PerformPossibleAcquires();

                    throw;      // ThreadInterruptedException
                }
        	} while (!request.done);
            // the request acquire operation completed successfully
            result = request.acquireResult;
			return true;
		}
    }
    // perform as many acquires as possible
    private void PerformPossibleAcquires() {
        bool notify = false;
        while (reqQueue.Count > 0) {
            Request request = reqQueue.First.Value;
            if (!CanAcquire(request.acquireArgs))
                break;
            // remove request from queue and satisfy it
            reqQueue.RemoveFirst();
            request.acquireResult = AcquireSideEffect(request.acquireArgs);
            request.done = true;
            notify = true;
        }
        if (notify) {
            // even if we release only one thread, we do not know in what position it is
            // in the condition variable queue, so it is necessary to notify all blocked
            // threads to make sure that the target thread(s) are notified.
            Monitor.PulseAll(monitor);
        }
    }

    // generic release operation
    public void Release(ReleaseArgs releaseArgs) {
        lock(monitor) {
        	UpdateStateOnRelease(releaseArgs);
            PerformPossibleAcquires();
		}
    }
}
#endif

/**
 * The generic synchronizer based on an *implicit-extended .NET monitor*
 * (implemented by the class MonitorEx), with support for timeout on the acquire
 * operation.
 *
 * Note: In addition to the notes of the previous implementation, this implementation
 *       uses thread-specific notification.
 */

#if (!EXCLUDE_PSEUDO_CODE)

public class GenericSynchronizerKernelStyleImplicitExtendedMonitorSpecificNotifications {
    // implicit .NET monitor provides synchronization of the access to the shared
    // mutable state, supports the control synchronization inherent to the
    // acquire/release semantics.
    // Other implicit .NET monitors associated to the instances of the Request
    // type will be used as condition variables of the monitor represented by "monitor". 
    private readonly Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private class Request {
        internal readonly AcquireArgs acquireArgs;   // acquire arguments
        internal AcquireResult acquireResult;        // acquire result
        internal bool done;                          // true when the acquire is done

        internal Request(AcquireArgs args) { acquireArgs = args; }
    }

    // queue of pending acquire requests
    private readonly LinkedList<Request> reqQueue = new LinkedList<Request>();

    // synchonization state
    private SynchState syncState;

    public GenericSynchronizerKernelStyleImplicitExtendedMonitorSpecificNotifications(InitializeArgs initialArgs) {
        initialize "syncState" according to information specified in initialArgs;
    }

    // returns true if the synchronization state allows the acquire on behalf of the
    // thread that is at the head of the queue or the current thread if the queue is empty.
    private bool CanAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" allows an immediate acquire according to "acquireArgs";
    }
    // returns true if the state of synchronization allows the acquire on behalf of
    // the thread that is at the head of the request queue.
    private bool CurrentSynchStateAllowsAquire() {
        returns thrue if the current synchronization state allows acquire(s);
    }

    // executes the processing associated with a successful acquire and
    // returns the proper acquire result (if any)
    private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
        update "state" according to "acquireArgs" after a successful acquire;
        return "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation according to "releaseArgs".
    private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
        // update "state" according to "releaseArgs";
    }

    // generic acquire operation; returns false when it times out
    public bool Acquire(AcquireArgs acquireArgs, out AcquireResult result, int timeout = Timeout.Infinite) {
        lock (monitor) {
            if (reqQueue.Count == 0 && CanAcquire(acquireArgs)) {
                result = AcquireSideEffect(acquireArgs);
                return true;
            }
            Request request = new Request(acquireArgs);
            reqQueue.AddLast(request);  // enqueue "request" at the end of the request queue
            TimeoutHolder th = new TimeoutHolder(timeout);
            do {
                if ((timeout = th.Value) == 0) {
                    // the timeout limit has expired - here we are sure that the acquire resquest
                    // is still pending. So, we remove the request from the queue and return failure.
                    reqQueue.Remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAquire())
                        PerformPossibleAcquires();

                    result = default(AcquireResult);
                    return false;
                }
                try {
                    MonitorEx.Wait(monitor, request, timeout);  // *wait on a private condition var*
                } catch (ThreadInterruptedException) {
                    // the thread may be interrupted when the requested acquire operation
                    // is already performed, in which case you can no longer give up
                    if (request.done) {
                        // re-assert the interrupt and return normally, indicating to the
                        // caller that the operation was successfully completed
                        Thread.CurrentThread.Interrupt();
                        break;
                    }
                    // remove the request from the queue and throw ThreadInterruptedException
                    reqQueue.Remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAquire())
                        PerformPossibleAcquires();

                    throw;      // ThreadInterruptedException
                }
            } while (!request.done);
            // the request acquire operation completed successfully
            result = request.acquireResult;
            return true;
        }
    }
    
    // perform as many acquires as possible
    private void PerformPossibleAcquires() {
        while (reqQueue.Count > 0) {
            Request request = reqQueue.First.Value;
            if (!CanAcquire(request.acquireArgs))
                break;
            // remove request from queue and satisfy it
            reqQueue.RemoveFirst();
            request.acquireResult = AcquireSideEffect(request.acquireArgs);
            request.done = true;
            MonitorEx.Pulse(monitor, request);      // *specific notification*
        }
    }

    // generic release operation
    public void Release(ReleaseArgs releaseArgs) {
        lock (monitor) {
            UpdateStateOnRelease(releaseArgs);
            PerformPossibleAcquires();
        }
    }
}
#endif

/**
 * Semaphore following the kernel style, using a *implicit .NET monitor*, with
 * support for timeout on the acquire operation.
 */

public class SemaphoreKernelStyleImplicitMonitor {
    // monitor provides synchronization of the access to the shared mutable state,
    // supports the control synchronization inherent to acquire/release semantics.
    private Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private class Request {
		internal int acquires;		// acquire args: requested permits
		internal bool done;			// true when done

		internal Request(int acqs) { acquires = acqs; }
	}
	// queue of pending acquire requests
	private LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// synchronization state
	private int permits;
	
	public SemaphoreKernelStyleImplicitMonitor(int initial) {
		if (initial > 0)
			permits = initial;
    }
	
	// if there are sufficient permits, return true; false otherwise.
	private bool CanAcquire(int acquires) { return permits >= acquires; }

	// if there are threads in the queue, return whether the number of available
    // permits is sufficient to satisfy the request of the thread that
    // is at the head of the queue
    private bool CurrentSynchStateAllowsAcquire() {
        return reqQueue.Count > 0 && permits >= reqQueue.First.Value.acquires;
    } 

	// after acquire deduct the permissions granted
	private void AcquireSideEffect(int acquires) { permits -= acquires; }

    // update the available permits in accordance with the permits released
 	private void UpdateStateOnRelease(int releases) { permits += releases; }

    // acquires the specified number of permits; returns false if times out
    public bool Acquire(int acquires, int timeout = Timeout.Infinite) {
        lock(monitor) {
            if (reqQueue.Count == 0 && CanAcquire(acquires)) {
                AcquireSideEffect(acquires);
                return true;
            }
            Request request = new Request(acquires);
            reqQueue.AddLast(request);  // enqueue "request" at the end of  "reqQueue"
            TimeoutHolder th = new TimeoutHolder(timeout);
            do {
                if ((timeout = th.Value) <= 0) {
                    reqQueue.Remove(request);

                    // after remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAcquire())
                        PerformPossibleAcquires();

                    return false;
                }
                try {
                    Monitor.Wait(monitor, timeout);
                } catch (ThreadInterruptedException) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.CurrentThread.Interrupt();
						return true;
                    }
                    reqQueue.Remove(request);

                    // after remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAcquire())
                        PerformPossibleAcquires();

                    throw;      // ThreadInterruptedException
                }
            } while (!request.done);
            // the request was satisfied
            return true;
        }
    }

    // perform the possible pending acquires
    private void PerformPossibleAcquires() {
        bool notify = false;
        while (reqQueue.Count > 0) {
            Request request = reqQueue.First.Value;
            if (!CanAcquire(request.acquires))
                break;
            // remove request from queue and satisfy it 
            reqQueue.RemoveFirst(request);
            AcquireSideEffect(request.acquires);
            request.done = true;
            notify = true;
        }
        if (notify) {
            // even if we release only one thread, we do not know its position of the queue
            // of the condition variable, so it is necessary to notify all blocked threads,
            // to make sure that the thread(s) in question is notified.
            Monitor.PulseAll(monitor);
        }
    }

    // release the specified number of permits
    public void Release(int releases) {
        lock(monitor) {
            UpdateStateOnRelease(releases);
            PerformPossibleAcquires();
        }
    }
}

/**
 * Semaphore following the kernel style, using an *implicit-extended .NET monitor*,
 * with support for timeout on the acquire operation.
 * 
 * NOTE: This implementation uses specific thread notifications.
 */

public class SemaphoreKernelStyleImplicitExtendedMonitorSpecificNotifications {
    // extended .NET monitor provides synchronization of the access to the shared
    // mutable state, supports the control synchronization inherent to acquire/release
    // semantics.
    // Using th extension implemented by the MonitorEx class, other implicit .NET
    // monitors, associated to the Request objects, will be used as condition variables
    // subordinate to the monitor represented by "monitor". 
    private Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private class Request {
        internal int acquires;      // requested permits
        internal bool done;         // true when done

        internal Request(int acqs) { acquires = acqs; }
    }

    // queue of pending acquire requests
    private LinkedList<Request> reqQueue = new LinkedList<Request>();

    // synchronization state
    private int permits;

    public SemaphoreKernelStyleImplicitExtendedMonitorSpecificNotifications(int initial) {
        if (initial > 0)
            permits = initial;
    }

    // if there are sufficient permits, return true; false otherwise.
    private bool CanAcquire(int acquires) { return permits >= acquires; }

    // if there are threads in the queue, return whether the number of available
    // permits is sufficient to satisfy the request of the thread that
    // is at the head of the queue
    private bool CurrentSynchStateAllowsAcquire() {
        return reqQueue.Count > 0 && permits >= reqQueue.First.Value.acquires;
    }

    // after acquire deduct the permissions granted
    private void AcquireSideEffect(int acquires) { permits -= acquires; }

    // update the available permits in accordance with the permits released
    private void UpdateStateOnRelease(int releases) { permits += releases; }

    // acquires the specified number of permits; returns false if times out
    public bool Acquire(int acquires, int timeout = Timeout.Infinite) {
        lock (monitor) {
            if (reqQueue.Count == 0 && CanAcquire(acquires)) {
                AcquireSideEffect(acquires);
                return true;
            }
            Request request = new Request(acquires);
            reqQueue.AddLast(request);  // enqueue "request" at the end of  "reqQueue"
            TimeoutHolder th = new TimeoutHolder(timeout);
            do {
                if ((timeout = th.Value) <= 0) {
                    reqQueue.Remove(request);

                    // after remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAcquire())
                        PerformPossibleAcquires();

                    return false;
                }
                try {
                    MonitorEx.Wait(monitor, request, timeout);  // *wait on a private condition variable*
                } catch (ThreadInterruptedException) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.CurrentThread.Interrupt();
                        return true;
                    }
                    reqQueue.Remove(request);

                    // after remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization state allows now to satisfy other queued
                    // acquire(s).
                    if (CurrentSynchStateAllowsAcquire())
                        PerformPossibleAcquires();

                    throw;      // ThreadInterruptedException
                }
            } while (!request.done);
            // the request was completed
            return true;
        }
    }

    // perform the possible pending acquires
    private void PerformPossibleAcquires() {
        while (reqQueue.Count > 0) {
            Request request = reqQueue.First.Value;
            if (!CanAcquire(request.acquires))
                break;
            // remove request from queue and satisfy it
            reqQueue.RemoveFirst();
            AcquireSideEffect(request.acquires);
            request.done = true;
            MonitorEx.Pulse(monitor, request);      // *specific thread notification*
        }
    }

    // release the specified number of permits
    public void Release(int releases) {
        lock (monitor) {
            UpdateStateOnRelease(releases);
            PerformPossibleAcquires();
        }
    }
}

/**
 * Message queue following the kernel style, using an *implicit .NET monitors*, with
 * support for timeout on the receive operation.
 *
 * Note: in this synchronizer when a thread gives up the acquire operation due to timeout
 *       or interruption, no other acquire can be satisfied, so the code that addresses
 *       this situation was not included.
 */
public class MessageQueueKernelStyleImplicitMonitor<T> {
    // implicit .NET monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private Object monitor = new Object();

    // the instance of this type used to hold a receive request
    private class Request {
		internal T receivedMsg;	// received message
		internal bool done;		// true when done
	}
    
    // queue of pending receive requests
    private LinkedList<Request> reqQueue = new LinkedList<Request>();
    
	// synchronization state: list of messages pending for reception
    private LinkedList<T> pendingMessages = new LinkedList<T>();

    // initialize the message queue
	public MessageQueueKernelStyleImplicitMonitor() { }

    // returns true if there is an pending message, which means that receive
	// can succeed immediately
    private bool CanReceive() { return pendingMessages.Count > 0; }

    // when a message is received, it must be removed from the pending message list
    private T ReceiveSideEffect() {
        T receivedMessage = pendingMessages.First.Value;
        pendingMessages.RemoveFirst();
        return receivedMessage;
    }

    // add the sent message to the pending messages list
    private void UpdateStateOnSend(T sentMessage) { pendingMessages.AddLast(sentMessage); }

    // receive the next message from the queue
    public bool Receive(out T receivedMsg, int timeout = Timeout.Infinite) {
        lock (monitor) {
            if (reqQueue.Count == 0 && CanReceive()) {
                receivedMsg = ReceiveSideEffect();
                return true;
            }
            TimeoutHolder th = new TimeoutHolder(timeout);
            Request request = new Request();
            reqQueue.AddLast(request);  // enqueue "request" at the end of  "reqQueue"
            do {
                if ((timeout = th.Value) == 0) {
                    // the specified time limit has expired.
					// Here we know that our request was not satisfied.
                    reqQueue.Remove(request);
                    receivedMsg = default(T);
                    return false;
                }
                try {
                    Monitor.Wait(monitor, timeout);
                } catch (ThreadInterruptedException) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.CurrentThread.Interrupt();
                        break;
                    }
                    // remove the request from queue and throw ThreadInterruptedException
                    reqQueue.Remove(request);
                    throw;
                }
            } while (!request.done);
            // the request was satisfied, retrieve the received message
            receivedMsg = request.receivedMsg;
            return true;
        }
    }

	// send a message to the queue
    public void Send(T sentMsg) {
        lock(monitor) {
            UpdateStateOnSend(sentMsg);
            if (reqQueue.Count > 0) {
                Request request = reqQueue.First.Value;
                // remove the request from the queue and satisfy it
                reqQueue.RemoveFirst();
                request.receivedMsg = ReceiveSideEffect();
                request.done = true;
                
				// even if we release only one thread, we do not know its position of the queue
                // of the condition variable, so it is necessary to notify all blocked threads,
                // to make sure that the thread in question is notified.
                Monitor.PulseAll(monitor);
            }
        }
    }

    // send a message to the queue (optimized)
    public void SendOptimized(T sentMsg) {
        lock (monitor) {
            if (reqQueue.Count > 0) {
                // deliver the message directly to a blocked thread
                Request request = reqQueue.First.Value;
                reqQueue.RemoveFirst();
                request.receivedMsg = sentMsg;
                request.done = true;

                // even if we release only one thread, we do not know its position of the queue
                // of the condition variable, so it is necessary to notify all blocked threads,
                // to make sure that the thread in question is notified.
                Monitor.PulseAll(monitor);
            } else {
                // no receiving thread, so the message is left in the respective queue
                UpdateStateDueToSend(sentMsg);
            }
        }
    }
}

/**
 * Message queue following the kernel style, using an *implicit-extended .NET monitor*,
 * with support for timeout on the receive operation.
 * 
 * Notes:
 *   1. This implementation uses specific thread notifications.
 *   2. In this synchronizer when a thread gives up the acquire operation due to timeout
 *      or interruption, no other acquire can be satisfied, so the code that addresses
 *      this situation was not included.
 */

public class MessageQueueKernelStyleImplicitExtendedMonitorSpecificNotifications<T> {
    // extended .NET monitor provides synchronization of the access to the shared
    // mutable state, supports the control synchronization inherent to acquire/release
    // semantics.
    // Using th extension implemented by the MonitorEx class, other implicit .NET
    // monitors, associated to the Request objects, will be used as condition variables
    // subordinate to the monitor represented by "monitor". 
    private Object monitor = new Object();

    // the instance of this type used to hold a receive request
    private class Request {
        internal T receivedMsg; // received message
        internal bool done;     // true when done
    }

    // queue of pending receive requests
    private LinkedList<Request> reqQueue = new LinkedList<Request>();

    // synchronization state: list of messages pending for reception
    private LinkedList<T> pendingMessages = new LinkedList<T>();

    // initialize the message queue
    public MessageQueueKernelStyleImplicitExtendedMonitorSpecificNotifications() { }

    // returns true if there is an pending message, which means that receive
    // can succeed immediately
    private bool CanReceive() { return pendingMessages.Count > 0; }

    // when a message is received, it must be removed from the pending message list
    private T ReceiveSideEffect() {
        T receivedMessage = pendingMessages.First.Value;
        pendingMessages.RemoveFirst();
        return receivedMessage;
    }

    // add the sent message to the pending messages list
    private void UpdateStateOnSend(T sentMessage) { pendingMessages.AddLast(sentMessage); }

    // receive the next message from the queue
    public bool Receive(out T receivedMsg, int timeout = Timeout.Infinite) {
        lock (monitor) {
            if (reqQueue.Count == 0 && CanReceive()) {
                receivedMsg = ReceiveSideEffect();
                return true;
            }
            TimeoutHolder th = new TimeoutHolder(timeout);
            Request request = new Request();
            reqQueue.AddLast(request);  // enqueue "request" at the end of  "reqQueue"
            do {
                if ((timeout = th.Value) == 0) {
                    // the specified time limit has expired.
                    // Here we know that our request was not met.
                    reqQueue.Remove(request);
                    receivedMsg = default(T);
                    return false;
                }
                try {
                    MonitorEx.Wait(monitor, request, timeout);  //block on private condition variable
                } catch (ThreadInterruptedException) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.CurrentThread.Interrupt();
                        break;
                    }
                    reqQueue.Remove(request);
                    throw;
                }
            } while (!request.done);
            receivedMsg = request.receivedMsg;
            return true;
        }
    }

    // send a message to the queue
    public void Send(T sentMsg) {
        lock (monitor) {
            UpdateStateOnSend(sentMsg);
            if (reqQueue.Count > 0) {
                Request request = reqQueue.First.Value;
                reqQueue.Remove(request);
                request.receivedMsg = ReceiveSideEffect();
                request.done = true;
                // notify waiting thread on its private conditions variable
                MonitorEx.Pulse(monitor, request);
            }
        }
    }

    // send a message to the queue (optimized)
    public void SendOptimized(T sentMsg) {
        lock (monitor) {
            if (reqQueue.Count > 0) {
                // deliver the message directly to a blocked thread
                Request request = reqQueue.First.Value;
                reqQueue.Remove(request);
                request.receivedMsg = sentMsg;
                request.done = true;
                // notify waiting thread on its private conditions variable
                MonitorEx.Pulse(monitor, request);
            } else {
                // no receiving thread, so the message is left in the respective queue
                UpdateStateDueToSend(sentMsg);
            }
        }
    }
}

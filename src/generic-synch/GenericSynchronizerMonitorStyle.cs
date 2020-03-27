/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Pseudo-code for implementing a generic synchronizer using the
 *  "Lampson & Redell monitor style" and .NET.
 *
 *  Realizations for the case of semaphore and message queue synchronizers.
 *
 *  Carlos Martins, October 2018
 */

#define EXCLUDE_PSEUDO_CODE

using System;
using System.Threading;
using System.Collections.Generic;

#if (!EXCLUDE_PSEUDO_CODE)

/**
  * Generic classes used for synchronization state, initialization
  * arguments, acquire arguments, acquire result and release arguments
  */
  
public class SynchState {}
public class InitializationArgs {}
public class AcquireArgs {}
public class AcquireResult {}
public class ReleaseArgs {}

#endif

/**
 * This class implements a generic synchronizer and is expressed in pseudo-code
 * to express data access and control synchronization (without considering the
 * availability of the monitor implementation).
 */

#if (!EXCLUDE_PSEUDO_CODE)

public class GenericSynchronizerMonitorStylePseudoCode {
	// lock that synchronizes the access to the synchronizer's mutable
	// shared state
	private Lock _lock = new Lock();

    // queue where blocked threads are present (also protected by "lock")
	// needed to implement control synchronization inherent to acquire/release
	// semantics.
    private WaitQueue waitQueue = new WaitQueue();
	
	// synchronization state
	private SynchState synchState;
	
	// initialize the synchronizer
	public GenericSynchronizerMonitorStyle(InitializeArgs initialArgs) {
		initialize "synchState" according to information specified by "initializationArgs";
	}
	
	// check if synchronization state allows an immediate acquire
	private bool CanAcquire(AcquireArgs acquireArgs) {
		returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
	}
	
	// executes the side effect processing related to a successful acquire 
	private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
		update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result"
	}
	
	// update synchronization state due to a release operation
	private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
		update "syncState" according to "releaseArgs";
	}
	
	// do the acquire operation
	public AcquireResult Acquire(AcquireArgs acquireArgs) {
		_lock.Acquire();
		try {
			while (!CanAcquire(acquireArgs)) {
				enqueue the current thread on the "waitQueue" sensible to posterior wakeups;
				int depth = _lock.ReleaseAll();
				block current thread until it is waked up by a releaser thread;
				_lock.ReAcquire(depth);
			}
			return AcquireSideEffect(acquireArgs);
		} finally {
			_lock.Release();
		}
	}
	
	// do the release operation
	public void Release(ReleaseArgs releaseArgs) {
		_lock.Acquire();
		try {
			UpdateStateOnRelease(releaseArgs);
			wakeup all the blocked threads that can have its acquire satisfied with
			the new value of "syncState";
		} finally {
			_lock.Release();
		}
	}
}

#endif

/**
 * Generic synchronizer pseudo-code based on an *implicit .NET monitor*, with
 * support for timeout on the acquire operation.
 */

 #if (!EXCLUDE_PSEUDO_CODE)

public class GenericSynchronizerMonitorStyleImplicitMonitor {
    // implicit .NET monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private readonly object monitor = new Object();
    
	// synchronization state
	private SynchState synchState;

    // initialize the synchronizer
	public GenericSynchronizerMonitorStyleImplicitMonitor(InitializeArgs initialArgs) {
        initialize "synchState" according to information specified by "initialArgs";
    }

    // returns true if synchronization state allows an immediate acquire
    private bool CanAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
    }

    // executes the processing associated with a successful acquire 
    private AcquireResult AcquireSideEffect(AcquireArgs acquireArgs) {
        update "synchState" according to "acquireArgs" after a successful acquire;
        returns "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation
    private void UpdateStateOnRelease(ReleaseArgs releaseArgs) {
        update "syncState" according to "releaseArgs";
    }

    public bool Acquire(AcquireArgs acquireArgs, out AcquireResult result, int timeout = Timeout.Infinite) {
        lock(monitor) {
			if (CanAcquire(acquireArgs)) {
				result = AcquireSideEffect(acquireArgs);
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0) {
					result = default(AcquireResult);
					return false;
				}
				try {
                    Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
                    // if notification was made with Monitor.Pulse, the single notification
					// may be lost if the blocking of the notified thread was interrupted,
					// so, if an acquire is possible, we notify another blocked thread, if any.
					if (CanAcquire(acquireArgs))
						Monitor.Pulse(monitor);
					throw;
				}
			} while (!CanAcquire(acquireArgs));
        	result = AcquireSideEffect(acquireArgs);
			return true;
		}
    }

    public void Release(ReleaseArgs releaseArgs) {
		lock(monitor) {
        	UpdateStateOnRelease(releaseArgs);
			Monitor.PulseAll(monitor);	/* or Monitor.Pulse if only one thread can have success in its acquire */
		}
    }
}

#endif

/**
 * Semaphore following the monitor style, using an *implicit .NET monitor*, with
 * support for timeout on the acquire operation.
 */
public class SemaphoreMonitorStyleImplicitMonitor {
    // implicit .NET monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private readonly Object monitor = new Object();
	
	// synchronization state
	private int permits;
	
	// initialize the semaphore
	public SemaphoreMonitorStyleImplicitMonitor(int initial) {
		if (initial > 0)
			permits = initial;
	}
	
	// if the pending permits e equal or greater to the request,
	// we can acquire immediately
	private bool CanAcquire(int acquires) { return permits >= acquires; }
	
	// deduce the acquired permits
	private void AcquireSideEffect(int acquires) { permits -= acquires; }
	
	// take into account the released permits
	private void UpdateStateOnRelease(int releases) { permits += releases; }
	
	public bool Acquire(int acquires, int timeout) {
		lock(monitor) {
			if (CanAcquire(acquires)) {
                AcquireSideEffect(acquires);
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0)
					return false;
				Monitor.Wait(monitor, timeout);
			} while (!CanAcquire(acquires));
			AcquireSideEffect(acquires);
			return true;
		}
	}

	public void Release(int releases) {
		lock(monitor) {
			UpdateStateOnRelease(releases);
			Monitor.PulseAll(monitor);	/* the release can satisfy multiple acquires */
		}
	}
}

/**
 * Message queue following the monitor style, using a *implicit .NET monitor*,
 * with timeout support on the receive acquire operation.
 */
public class MessageQueueMonitorStyleImplicitMonitor<T> {
    // implicit .NET monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private readonly Object monitor = new Object();
	
	// list of pending messages
 	private readonly LinkedList<T> pendingMessages = new LinkedList<T>();

    // initializes the message queue
	public MessageQueueMonitorStyleImplicitMonitor() {}

    // if there is an pending message, receive cam succeed immediately
    private bool CanReceive() { return pendingMessages.Count > 0; }

    // when a message is received, it must be removed from the pending message list
	private T ReceiveSideEffect() {
		T receivedMessage = pendingMessages.First.Value;
		pendingMessages.RemoveFirst();
		return receivedMessage;
    }

    // add the sent message to the pending messages list
    private void UpdateStateOnSend(T sentMessage) { pendingMessages.AddLast(sentMessage); }

    // receive the nest message
	public bool Receive(out T receivedMessage, int timeout = Timeout.Infinite) {
        lock(monitor) {
			if (CanReceive()) {
				receivedMessage = ReceiveSideEffect();
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(timeout);
			do {
				if ((timeout = th.Value) == 0) {
					receivedMessage = default(T);
					return false;
				}
				try {
					Monitor.Wait(monitor, timeout);
				} catch (ThreadInterruptedException) {
					// .NET monitors can ignore notifications when there is a race between
					// the notification and the interrupt. So, we must regenerate the notification
					// if there are pending messages, in order to propagate the notification to
					// another blocked thread
					if (CanAcquire())
						Monitor.Pulse(monitor);
					throw;
				}
			} while (!CanReceive());
        	receivedMessage = ReceiveSideEffect();
			return true;
		}
    }

    // send a message to the queue
	public void Send(T sentMessage) {
        lock (monitor) {
        	UpdateStateOnSend(sentMessage);
			Monitor.Pulse(monitor);	/* only one thread cam receive the sent message */
		}
    }
}

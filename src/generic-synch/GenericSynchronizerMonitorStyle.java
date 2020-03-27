
/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Pseudo-code for implementing a generic synchronizer using the
 *  "Lampson & Redell monitor style" and Java.
 *
 *  Realizations for the case of semaphore and message queue synchronizers.
 *
 *  Note: To compile the java code contained in this file it will be necessary
 *        to comment the excerpts that contain pseudo-code.
 *
 *  Carlos Martins, October 2018
 */

import java.util.LinkedList;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.*;

/**
 * Generic classes used to express state, arguments, and results
 */

class SynchState {}
class InitializeArgs {}
class AcquireArgs {}
class ReleaseArgs {}
class AcquireResult {}

/**
 * This class implements a generic synchronizer using pseudo-code to express
 * data access and control synchronization (without considering the availability
 * of monitors).
 */

///***
class GenericSynchronizerMonitorStylePseudoCode {
	// lock that synchronizes the access to the mutable shared state
	private Lock lock = new Lock();
	
	// queue where block threads are (also protected by "lock") needed to
	// implement control synchronization inherent to acquire/release semantics.
	private WaitQueue waitQueue = new WaitQueue();
	
	// synchronization state
	private SynchState synchState;
	
	// initialize the synchronizer
	public GenericSynchronizerMonitorStylePseudoCode(InitialState initialState) {
		initialize "synchState" according to information specified by "initialState";
	}
	
	// test if synchronization state allows an immediate acquire
	private boolean canAcquire(AcquireArgs acquireArgs) {
		returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
	}
	
	// executes the processing associated with a successful acquire 
	private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
		update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result"
	}
	
	// update synchronization state due to a release operation
	private void updateStateOnRelease(ReleaseArgs releaseArgs) {
		update "syncState" according to "releaseArgs";
	}
	
	public AcquireResult acquire(AcquireArgs acquireArgs) {
		lock.acquire();
		try {
			while (!canAcquire(acquireArgs)) {
				enqueue current thread on the "waitQueue" sensible to posterior wakeups;
				int depth = lock.releaseAll();
				block current thread until it is waked up by a releaser thread;
				lock.reAcquire(depth);
			}
			return acquireSideEffect(acquireArgs);
		} finally {
			lock.release();
		}
	}
	
	public void release(ReleaseArgs releaseArgs) {
		lock.acquire();
		try {
			updateStateOnRelease(releaseArgs);
			wakeup all the blocked threads that can have its acquire satisfied with the new value of "syncState";
		} finally {
			lock.Release();
		}
	}
}
//**/

/**
 * Generic synchronizer pseudo-code based on an *implicit Java monitor*, with
 * support for timeout on the acquire operation.
 */

///***
class GenericSynchronizerMonitorStyleImplicitMonitor {
	// implicit Java monitor provides synchronization of the access to the shared
	// mutable state, supports the control synchronization inherent to
	// acquire/release semantics.
	private final Object monitor = new Object();
    
	// synchronization state
	private SynchState synchState;

    // initialize the synchronizer
	public GenericSynchronizerMonitorStyleImplicitMonitor(InitializeArgs initialState) {
        initialize "synchState" according to information specified by "initialState";
    }

    // returns true if synchronization state allows an immediate acquire
    private boolean canAcquire(AcquireArgs acquireArgs) {
       returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
    }

    // executes the processing associated with a successful acquire 
    private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation
    private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        // update "syncState" according to "releaseArgs";
    }

	// the generic acquire operation; returns null when it times out
    public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout) throws InterruptedException {
        synchronized(monitor) {
			if (canAcquire(acquireArgs))
				return acquireSideEffect(acquireArgs);
			TimeoutHolder th = new TimeoutHolder(millisTimeout);
			do {
				if (th.isTimed()) {
					if ((millisTimeout = th.value()) <= 0)
						return null;		// timeout
					monitor.wait(millisTimeout);
				} else
					monitor.wait();
			} while (!canAcquire(acquireArgs));
			// successful acquire
        	return  acquireSideEffect(acquireArgs);
		}
    }

	// the generic release operation
    public void release(ReleaseArgs releaseArgs) {
		synchronized(monitor) {
        	updateStateOnRelease(releaseArgs);
			monitor.notifyAll();
			// or monitor.notify() if only one thread can have success in its acquire
		}
    }
}
//**/

/**
 * Generic synchronizer pseudo-code based on an *explicit Java monitor*, with
 * support for timeout on the acquire operation.
 */

///***
class GenericSynchronizerMonitorStyleExplicitMonitor {
	// explicit Java monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Lock lock = new ReentrantLock();
	private final Condition okToAcquire = lock.newCondition(); 
	
	// synchronization state
	private SynchState synchState;

	// initialize the synchronizer
	public GenericSynchronizerMonitorStyleExplicitMonitor(InitializeArgs initialState) {
        initialize "synchState" according to information specified by "initialState";
    }

	// returns true if synchronization state allows an immediate acquire
	private boolean canAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" satisfies an immediate acquire according to "acquireArgs";
    }

	// executes the processing associated with a successful acquire
	private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "synchState" according to "acquireArgs" after a successful acquire;
		returns "the-proper-acquire-result";

    }

	// update synchronization state due to a release operation
	private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        // update "syncState" according to "releaseArgs";
    }

	// generic acquire operation; returns null when it times out
	public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout) throws InterruptedException {
		lock.lock();
		try {
			if (canAcquire(acquireArgs))
				return acquireSideEffect(acquireArgs);
			boolean isTimed = millisTimeout >= 0;
			long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
			do {
				if (isTimed) {
					if (nanosTimeout <= 0)
						return null; // timeout
					nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
				} else
					okToAcquire.await();
			} while (!canAcquire(acquireArgs));
			// successful acquire
			return acquireSideEffect(acquireArgs);
		} finally {
			lock.unlock();
		}
	}

	// generic release operation 
	public void release(ReleaseArgs releaseArgs) {
		lock.lock();
		try {
			updateStateOnRelease(releaseArgs);
			okToAcquire.signalAll();
			// or okToAcquire.signal() if only one thread can have success in its acquire
		} finally {
			lock.unlock();
		}
	}
}
//**/

/**
 * Semaphore following the monitor style, using an *implicit Java monitor*, with
 * support for timeout on the acquire operation.
 */

class SemaphoreMonitorStyleImplictMonitor {
	// implicit Java monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Object monitor = new Object();
	
	// synchronization state
	private int permits;

	// initialize the semaphore
	public SemaphoreMonitorStyleImplictMonitor(int initial) {
		if (initial > 0)
			permits = initial;
	}

	// if the pending permits e equal or greater to the request,
	// we can acquire immediately
	private boolean canAcquire(int acquires) { return permits >= acquires; }

	// deduce the acquired permits
	private void acquireSideEffect(int acquires) { permits -= acquires; }

	// take into account the released permits
	private void updateStateOnRelease(int releases) { permits += releases; }

	// acquires the specified number of permits
	public boolean acquire(int acquires, long millisTimeout) throws InterruptedException {
		synchronized(monitor) {
			if (canAcquire(acquires)) {
				acquireSideEffect(acquires);
				return true;
			}
			TimeoutHolder th = new TimeoutHolder(millisTimeout);
			do {
				if (th.isTimed()) {
					if ((millisTimeout = th.value()) <= 0)
						return false; // timeout
					monitor.wait(millisTimeout);
				} else
					monitor.wait();
			} while (!canAcquire(acquires));
			// successful acquire
			acquireSideEffect(acquires);
			return true;
		}
	}

	// releases the specified number of permits
	public void release(int releases) {
		synchronized(monitor) {
			updateStateOnRelease(releases);
			monitor.notifyAll();
		}
	}
}

/**
 * Semaphore following the monitor style, using an *explicit Java monitors*,
 * with timeout support on acquire operation.
 */

 class SemaphoreMonitorStyleExplicitMonitor {
	// explicit Java monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Lock lock = new ReentrantLock();
	private final Condition okToAcquire = lock.newCondition();

	// synchronization state
	private int permits;
	
	// initialize the semaphore
	public SemaphoreMonitorStyleExplicitMonitor(int initial) {
		if (initial > 0)
			permits = initial;
	}
	
	// if the pending permits e equal or greater to the request,
	// we can acquire immediately
	private boolean canAcquire(int acquires) { return permits >= acquires; }
	
	// deduce the acquired permits
	private void acquireSideEffect(int acquires) { permits -= acquires; }
	
	// take into account the released permits
	private void updateStateOnRelease(int releases) { permits += releases; }
	
	// acquires the specified number of permits
	public boolean acquire(int acquires, long millisTimeout) throws InterruptedException {
		lock.lock();
		try {
			if (canAcquire(acquires)) {
                acquireSideEffect(acquires);
				return true;
			}
			boolean isTimed = millisTimeout > 0;
			long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
			do {
				if (isTimed) {
					if (nanosTimeout <= 0)
						return false;
					nanosTimeout = okToAcquire.awaitNanos(nanosTimeout);
				} else
					okToAcquire.await();
			} while (!canAcquire(acquires));
			acquireSideEffect(acquires);
			return true;
		} finally {
			lock.unlock();
		}
	}

	// releases the specified number of permits
	public void release(int releases) {
		lock.lock();
		try {
			updateStateOnRelease(releases);
			okToAcquire.signalAll();	// this release can satisfy multiple acquires
		} finally {
			lock.unlock();
		}
	}
}

/**
 * Message queue following the monitor style, using a *Java implicit monitors*,
 * with timeout support on the receive acquire operation.
 */
 
class MessageQueueMonitorStyleImplicitMonitor<T> {
 	// implicit Java monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Object monitor = new Object();
	
	// list of messages pending for reception
 	private final LinkedList<T> pendingMessages = new LinkedList<T>();

    // initializes the message queue
	public MessageQueueMonitorStyleImplicitMonitor() {}

    // if there is an pending message, receive cam succeed immediately
    private boolean canReceive() { return pendingMessages.size() > 0; }

    // when a message is received, it must be removed from the pending message list
	private T receiveSideEffect() { return pendingMessages.poll(); }

    // add the sent message to the pending messages list
    private void updateStateOnSend(T sentMessage) { pendingMessages.addLast(sentMessage); }

    // receive the next message; returns null when it times out
	public T receive(long millisTimeout) throws InterruptedException {
        synchronized(monitor) {
			if (canReceive())
				return receiveSideEffect();
			TimeoutHolder th = new TimeoutHolder(millisTimeout);
			do {
				if (th.isTimed()) {
					if ((millisTimeout = th.value()) <= 0)
						return null;	// timeout
					monitor.wait(millisTimeout);
				} else
					monitor.wait(millisTimeout);
			} while (!canReceive());
        	return receiveSideEffect();
		}
    }

    // send a message to the queue
	public void send(T sentMessage) {
        synchronized(monitor) {
        	updateStateOnSend(sentMessage);
			monitor.notify();	/* only one thread cam receive the sent message */
		}
    }
}

/**
 * Message queue following the monitor style, using an *explicit Java monitor*,
 * with timeout support on the receive operation.
 */

class MessageQueueMonitorStyleExplicitMonitor<T> {
	// explicit Java monitor that suports the synchronzation of shared data access
	// and supports also the control synchronization.
	private final Lock lock = new ReentrantLock();
	private final Condition okToReceive = lock.newCondition();

	// synchronization state: list of messages pending for reception
	private LinkedList<T> pendingMessages = new LinkedList<T>();

	// initializes the message queue
	public MessageQueueMonitorStyleExplicitMonitor() {}

	// if there is an pending message, receive cam succeed immediately
	private boolean canReceive() { return pendingMessages.size() > 0; }

	// when a message is received, it must be removed from the pending message list
	private T receiveSideEffect() { return pendingMessages.poll(); }

	// add the sent message to the pending messages list
	private void updateStateOnSend(T sentMessage) { pendingMessages.addLast(sentMessage); }

	// receive the next message; returns null when it times out
	public T receive(long millisTimeout) throws InterruptedException {
		lock.lock();
		try {
			if (canReceive())
				return receiveSideEffect();
			boolean isTimed = millisTimeout > 0;
			long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
			do {
				if (isTimed) {
					if (nanosTimeout <= 0)
						return null; // timeout
					nanosTimeout = okToReceive.awaitNanos(nanosTimeout);
				} else
					okToReceive.await();
			} while (!canReceive());
			return receiveSideEffect();
		} finally {
			lock.unlock();
		}
	}
	
	// send a message to the queue
	public void send(T sentMessage) {
		lock.lock();
		try {
			updateStateOnSend(sentMessage);
			okToReceive.signal(); 	/* only one thread cam receive the sent message */
		} finally {
			lock.unlock();
		}
	}
}

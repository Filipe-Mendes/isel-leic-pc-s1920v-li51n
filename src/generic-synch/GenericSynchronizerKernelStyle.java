
/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Pseudo-code for implementing a generic synchronizer using using
 *  the "kernel style" and Java.
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
 * Generic classes used to express synchronization state, arguments, and results
 */

class SynchState {}
class InitializeArgs {}
class AcquireArgs {}
class ReleaseArgs {}
class AcquireResult {}

/**
 * This class implements a generic synchronizer using pseudo-code to express
 * data access and control synchronization (not considering the availability
 * of monitors).
 *
 * NOTE: This code does not provide for the possibility of a thread blocked being
 *       interrupted (see implementation based on implicit .NET monitors below).
 */
///***
 public class GenericSynchronizerKernelStylePseudoCode {
    // lock used to synchronize access to mutable shared state
    private Lock _lock = new Lock();
    
    // queue where block threads are (also protected by "_lock") needed to
    // implement control synchronization inherent to acquire/release semantics.

    private WaitQueue wqueue = new WaitQueue();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
	private static class Request {
		AcquireArgs acquireArgs;		// acquire arguments
		AcquireResult acquireResult;	// acquire result
		boolean done;					// true when the acquire is done

		Request(AcquireArgs args) { acquireArgs = args; }
	}
	
	// queue of pending acquire requests
	private LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// synchonization state
	private SynchState syncState;
	
	// initializes the synchronizer
	public GenericSynchronizerKernelStyle(InitializeArgs initiallArgs) {
		initialize "syncState" according to information specified in initialArgs;
	}
	
	// returns true if synchronization state allows an immediate acquire
	private boolean canAcquire(AcquireArgs acquireArgs) {
		returns true if "syncState" allows an immediate acquire according to "acquireArgs";
	}
	
	// executes the processing associated with a successful acquire and
	// returns the proper acquire result (if any)
	private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
		update "state" according to "acquireArgs" after a successful acquire;
		return "the-proper-acquire-result";
	}
	
	// update synchronization state due to a release operation
	private void updateStateOnRelease(ReleaseArgs releaseArgs) {
		update "state" according to "releaseArgs";
	}
	
	// acquire operation
	public AcquireResult acquire(AcquireArgs acquireArgs) {
        _lock.acquire();
        try {
		    if (reqQueue.size() == 0 && canAcquire(acquireArgs))
			    return acquireSideEffect(acquireArgs);
		    Request request = new Request(acquireArgs);
		    reqQueue.addLast(request);	// enqueue "request" at the end of the request queue
		    do {
			    enqueue current thread on the "waitQueue" sensible to posterior wakeups done by "_lock" owners;
			    int depth = _lock.releaseAll();
			    block current thread until it is waked up by a releaser threads;
			    _lock.reAcquire(depth);
	    	} while (!request.done);
            // the request acquire operation completed successfully
		    return request.acquireResult;
        } finally {
            _lock.release();
        }
    }
    
    // generic release operation
	public void release(ReleaseArgs releaseArgs) {
        _lock.acquire();
        try {
		    updateStateOnRelease(releaseArgs);
		    while (reqQueue.size() > 0) {
			    Request request = reqQueue.peek();
			    if (!canAcquire(request.acquireArgs))
				    break;
			    reqQueue.removeFirst();
			    request.acquireResult = acquireSideEffect(request.acquireArgs);
			    request.done = true;
		    }
            wake up at least all blocked threads whose acquires have been met by this release
        } finally {
            _lock.release();
        }
	}
}
//**/

 /**
 * The generic synchronizer based on an *implicit Java monitor*, with support
 * for timeout on the acquire operation.
 *
 * Notes:
 *  1. This implementation takes obviously into account the possible interruption of
 *     the threads blocked on the condition variables of the monitor;
 *  2. The code structure is slightly changed due to the possibility of cancellation
 *     of the acquire operations due to timeout or interruption.
 */

 ///***
class GenericSynchronizerKernelStyleImplicitMonitor {
    // implicit Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Object monitor = new Object();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), result (if any) and status (not done/done)
    private static class Request {
        final AcquireArgs acquireArgs;  // acquire arguments
        AcquireResult acquireResult;    // acquire result
        boolean done;                      // true when the acquire is done

        Request(AcquireArgs args) { acquireArgs = args; }
    }
    
    // queue of pending acquire requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();
    
	// synchonization state
    private SynchState syncState;

    public GenericSynchronizerKernelStyleImplicitMonitor(InitializeArgs initiallArgs) {
        initialize "syncState" according to information specified in initialArgs;
    }

    // returns true if the synchronization state allows the acquire on behalf of the
    // thread that is at the head of the queue or the current thread if the queue is empty.
    private boolean canAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" allows an immediate acquire according to "acquireArgs";
    }

    // returns true if the state of synchronization allows the acquire on behalf of
    // the thread that is at the head of the queue.
    private boolean currentSynchStateAllowsAquire() {
        returns thrue if the current synchronization state allows acquire(s);
    }

    // executes the processing associated with a successful acquire and
    // returns the proper acquire result (if any)
    private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "state" according to "acquireArgs" after a successful acquire;
        return "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation according to "releaseArgs".
    private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        update "state" according to "releaseArgs";
    }

	// generic acquire operation; returns null when it times out
    public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout) throws InterruptedException {
        synchronized(monitor) {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();

        	if (reqQueue.size() == 0 && canAcquire(acquireArgs))
            	return acquireSideEffect(acquireArgs);
        	Request request = new Request(acquireArgs);
        	reqQueue.addLast(request);  // enqueue "request" at the end of the request queue
            TimeoutHolder th = new TimeoutHolder(millisTimeout);
        	do {
                try {
                    if (th.isTimed()) {
				        if ((millisTimeout = th.value()) <= 0) {
                            // the timeout limit has expired - here we are sure that the
					        // acquire resquest is still pending. So, we remove the request
					        // from the queue and return failure
                            reqQueue.remove(request);
                            // After remove the request of the current thread from queue, *it is possible*
                            // that the current synhcronization allows now to satisfy another queued
                            // acquires.
                            if (currentSynchStateAllowsAquire())
                                performPossibleAcquires();

					        return null;
                        }
                        monitor.wait(millisTimeout);
                    } else
                        monitor.wait();
				} catch (InterruptedException ie) {
                    // the thread may be interrupted when the requested acquire operation
					// is already performed, in which case you can no longer give up
					if (request.done) {
                        // re-assert the interrupt and return normally, indicating to the
						// caller that the operation was successfully completed
                    	Thread.currentThread().interrupt();
						break;
					}
					// remove the request from the queue and throw ThreadInterruptedException
                    reqQueue.remove(request);
                    
                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization allows now to satisfy another queued
                    // acquires.
                    if (currentSynchStateAllowsAquire())
                        performPossibleAcquires();

					throw ie;
				}
        	} while (!request.done);
            // the request acquire operation completed successfully
            return request.acquireResult;
		}
    }

    // perform as many acquires as possible
    private void performPossibleAcquires() {
        boolean notify = false;
        while(reqQueue.size()>0) {
            Request request = reqQueue.peek();
            if (!canAcquire(request.acquireArgs))
                break;
            reqQueue.removeFirst();
            request.acquireResult = acquireSideEffect(request.acquireArgs);
            request.done = true;
            notify = true;
        }
        if(notify) {
            // even if we release only one thread, we do not know what position it is
            // in the condition variable queue, so it is necessary to notify all blocked
            // threads to make sure that the target thread(s) are notified.
            monitor.notifyAll();
        }
    }
    
    // generic release operation
    public void release(ReleaseArgs releaseArgs) {
        synchronized(monitor) {
            updateStateOnRelease(releaseArgs);
            performPossibleAcquires();
		}
    }
}
//**/

/**
 * The generic synchronizer based on an *explicit Java monitor*
 * (implemented by the class MonitorEx), with support for timeout on the acquire
 * operation.
 *
 * Note: In addition to the notes of the previous implementation, this
 * implementation uses thread-specific notification.
 */

///***
class GenericSynchronizerKernelStyleExplicitMonitorSpecificNotifications {
    // explicit Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Lock lock = new ReentrantLock();

    // the instances of this type describe an acquire request, namely
    // their arguments (if any), the condition variable where the acquirer
    // thread is waiting, the result (if any) and status (not done/done) of
    // the operation.
    private static class Request {
        final AcquireArgs acquireArgs;  // acquire arguments
        AcquireResult acquireResult;    // acquire result
        boolean done; // true when the acquire is done
        final Condition okToAcquire;    // condition variable of monitor "lock" where the thread is blocked

        Request(AcquireArgs args, Condition okToAcq) {
            acquireArgs = args;
            this.okToAcquire = okToAcq;
        }
    }

    // queue of pending acquire requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();

    // synchonization state
    private SynchState syncState;

    public GenericSynchronizerKernelStyleExplicitMonitorSpecificNotifications(InitializeArgs initiallArgs) {
        initialize "syncState" according to information specified in initialArgs;
    }

    // returns true if the synchronization state allows the acquire on behalf of the
    // thread that is at the head of the queue or the current thread if the queue is empty.
    private boolean canAcquire(AcquireArgs acquireArgs) {
        returns true if "syncState" allows an immediate acquire according to "acquireArgs";
    }

    // returns true if the state of synchronization allows the acquire on behalf of
    // the thread that is at the head of the queue.
    private boolean currentSynchStateAllowsAquire() {
        returns thrue if the current synchronization state allows acquire(s);
    }

    // executes the processing associated with a successful acquire and
    // returns the proper acquire result (if any)
    private AcquireResult acquireSideEffect(AcquireArgs acquireArgs) {
        update "state" according to "acquireArgs" after a successful acquire;
        return "the-proper-acquire-result";
    }

    // update synchronization state due to a release operation according to "releaseArgs".
    private void updateStateOnRelease(ReleaseArgs releaseArgs) {
        update "state" according to "releaseArgs";
    }

    // generic acquire operation; returns null when it times out
    public AcquireResult acquire(AcquireArgs acquireArgs, long millisTimeout) throws InterruptedException {
        lock.lock();
        try {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();

            if (reqQueue.size() == 0 && canAcquire(acquireArgs))
                return acquireSideEffect(acquireArgs);
            Request request = new Request(acquireArgs, lock.newCondition());
            reqQueue.addLast(request); // enqueue "request" at the end of the request queue
            // handle timeout
            boolean isTimed = millisTimeout >= 0;
            long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0;
            do {
                try {
                    if (isTimed) {
                        if (nanosTimeout <= 0) {
                            // the timeout limit has expired - here we are sure that the
                            // acquire resquest is still pending. So, we remove the request
                            // from the queue and return failure
                            reqQueue.remove(request);

                            // After remove the request of the current thread from queue, *it is possible*
                            // that the current synhcronization allows now to satisfy another queued
                            // acquires.
                            if (currentSynchStateAllowsAquire())
                                performPossibleAcquires();

                            return null;
                        }
                        nanosTimeout = request.okToAcquire.awaitNanos(nanosTimeout);
                    } else
                        request.okToAcquire.await();
                } catch (InterruptedException ie) {
                    // the thread may be interrupted when the requested acquire operation
                    // is already performed, in which case you can no longer give up
                    if (request.done) {
                        // re-assert the interrupt and return normally, indicating to the
                        // caller that the operation was successfully completed
                        Thread.currentThread().interrupt();
                        break;
                    }
                    // remove the request from the queue and throw ThreadInterruptedException
                    reqQueue.remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization allows now to satisfy another queued
                    // acquires.
                    if (CurrentSynchStateAllowsAquire())
                        PerformPossibleAcquires();
                    throw ie;
                }
            } while (!request.done);
            // the request acquire operation completed successfully
            return request.acquireResult;
        } finally {
            lock.unlock();
        }
    }

    // perform as many acquires as possible
    private void performPossibleAcquires() {
        while (reqQueue.size() > 0) {
            Request request = reqQueue.peek();
            if (!canAcquire(request.acquireArgs))
                break;
            reqQueue.remove(request);
            request.acquireResult = acquireSideEffect(request.acquireArgs);
            request.done = true;
            
            // *specif notification* - the acquirer thread is blocked on its private condition variable
            request.okToAcquire.signal();
        }
    }

    // generic release operation
    public void release(ReleaseArgs releaseArgs) {
        lock.lock();
        try {
            updateStateOnRelease(releaseArgs);
            performPossibleAcquires();
        } finally {
            lock.unlock();
        }
    }
}
//**/

/**
 * Semaphore following the kernel style, using an *implicit Java monitor*, with
 * support for timeout on the acquire operation.
 */

class SemaphoreKernelStyleImplicitMonitor {
    // implicit Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Object monitor = new Object();
	
	// the request object
	private static class Request {
		final int acquires;     // requested permits
		boolean done;              // true when done

		Request(int acqs) { acquires = acqs; }
	}
	// the queue of pending acquire requests
	private final LinkedList<Request> reqQueue = new LinkedList<Request>();
	
	// the synchronization state
	private int permits;
	
	public SemaphoreKernelStyleImplicitMonitor(int initial) {
		if (initial > 0)
			permits = initial;
	}
	
	// if there are sufficient permits, return true; false otherwise.
    private boolean canAcquire(int acquires) { return permits >= acquires; }
    
    // if there are threads in the queue, return whether the number of available
    // permits is sufficient to satisfy the request of the thread that
    // is at the head of the queue
    private boolean currentSynchStateAllowsAcquire() {
        return reqQueue.size() > 0 && permits >= reqQueue.peek().acquires;
    }
	
	// after acquire deduct the permissions granted
	private void acquireSideEffect(int acquires) { permits -= acquires; }
	
	// update the available permits in accordance with the permits released
	private void updateStateOnRelease(int releases) { permits += releases; }

    // acquires the specified number of permits; return false when it times out
    public boolean acquire(int acquires, long millisTimeout)  throws InterruptedException {
        synchronized(monitor) {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();

            if (reqQueue.size() == 0 && canAcquire(acquires)) {
                acquireSideEffect(acquires);
                return true;
            }
            Request request = new Request(acquires);
            reqQueue.addLast(request);  // enqueue "request" at the end of  "reqQueue"
            TimeoutHolder th = new TimeoutHolder(millisTimeout);
            do {
                try {
                    if (th.isTimed()) {
                        if ((millisTimeout = th.value()) <= 0) {
                            // the specified time limit has expired
                            reqQueue.remove(request);

                            // After remove the request of the current thread from queue, *it is possible*
                            // that the current synhcronization allows now to satisfy another queued
                            // acquires.
                            if (currentSynchStateAllowsAcquire())
                                performPossibleAcquires();

                            return false;
                        }
                        monitor.wait(millisTimeout);
                    } else
                        monitor.wait();
                } catch (InterruptedException ie) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.currentThread().interrupt();
						return true;
                    }
                    reqQueue.remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization allows now to satisfy another queued
                    // acquires.
                    if (currentSynchStateAllowsAcquire())
                        performPossibleAcquires();
                    throw ie;
                }
            } while (!request.done);
            return true;
        }
    }

    // perform the possible pending acquires
    private void performPossibleAcquires() {
        boolean notify = false;
        while (reqQueue.size() > 0) {
            Request request = reqQueue.peek();
            if (!canAcquire(request.acquires))
                break;
            reqQueue.removeFirst();
            acquireSideEffect(request.acquires);
            request.done = true;
            notify = true;
        }
        if (notify) {
            // even if we release only one thread, we do not know its position of the queue
            // of the condition variable, so it is necessary to notify all blocked threads,
            // to make sure that the thread(s) in question is notified.
            monitor.notifyAll();
        }
    }

    //releases the specified number of permits
    public void release(int releases) {
        synchronized(monitor) {
            updateStateOnRelease(releases);
            performPossibleAcquires();
        }
    }
}

/**
 * Semaphore following the kernel style, using an *explicit Java monitor*, with
 * support for timeout on the acquire operation.
 * 
 * NOTE: This implementation uses specific thread notifications.
 */

class SemaphoreKernelStyleExplicitMonitorSpecificNotifications {
    // explicit Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Lock lock = new ReentrantLock();

    // the request object
    private static class Request {
        final int acquires;             // requested permits
        final Condition okToAcquire;    // the condition variable of "lock" monitor where the thread is
        boolean done; // true when done

        Request(int acqs, Condition okToAcq) {
            acquires = acqs;
            okToAcquire = okToAcq;
        }
    }

    // the queue of pending acquire requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();

    // the synchronization state
    private int permits;

    // initialize the semaphore
    public SemaphoreKernelStyleExplicitMonitorSpecificNotifications(int initial) {
        if (initial > 0)
            permits = initial;
    }

    // if there are sufficient permits, return true; false otherwise.
    private boolean canAcquire(int acquires) { return permits >= acquires; }

    // if there are threads in the queue, return whether the number of available
    // permits is sufficient to satisfy the request of the thread that
    // is at the head of the queue
    private boolean currentSynchStateAllowsAcquire() {
        return reqQueue.size() > 0 && permits >= reqQueue.peek().acquires;
    }

    // after acquire deduct the permissions granted
    private void acquireSideEffect(int acquires) { permits -= acquires; }

    // update the available permits in accordance with the permits released
    private void updateStateOnRelease(int releases) { permits += releases; }

    // acquires the specified number of permits; return false when it times out
    public boolean acquire(int acquires, long millisTimeout) throws InterruptedException {
        lock.lock();
        try {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();
        
            if (reqQueue.size() == 0 && canAcquire(acquires)) {
                acquireSideEffect(acquires);
                return true;
            }
            Request request = new Request(acquires, lock.newCondition());
            reqQueue.addLast(request); // enqueue "request" at the end of "reqQueue"
            boolean isTimed = millisTimeout > 0L;
            long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
            do {
                try {
                    if (isTimed) {
                        if (nanosTimeout <= 0L) {
                            // the specified time limit has expired
                            reqQueue.remove(request);
                            
                            // After remove the request of the current thread from queue, *it is possible*
                            // that the current synhcronization allows now to satisfy another queued
                            // acquires.
                            if (currentSynchStateAllowsAcquire())
                                performPossibleAcquires();

                            return false;
                        }
                        nanosTimeout = request.okToAcquire.awaitNanos(nanosTimeout);
                    } else
                        request.okToAcquire.await();
                } catch (InterruptedException ie) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.currentThread().interrupt();
                        return true;
                    }
                    reqQueue.remove(request);

                    // After remove the request of the current thread from queue, *it is possible*
                    // that the current synhcronization allows now to satisfy another queued
                    // acquires.
                    if (currentSynchStateAllowsAcquire())
                        performPossibleAcquires();

                    throw ie;
                }
            } while (!request.done);
            return true;
        } finally {
            lock.unlock();
        }
    }

    // perform the possible pending acquires
    private void performPossibleAcquires() {
        while (reqQueue.size() > 0) {
            Request request = reqQueue.peek();
            if (!canAcquire(request.acquires))
                break;
            reqQueue.removeFirst();
            acquireSideEffect(request.acquires);
            request.done = true;
            // *specific notification*: notify the thread on its private condition variable
            request.okToAcquire.signal();
        }
    }

    // releases the specified number of permits
    public void release(int releases) {
        lock.lock();
        try {
            updateStateOnRelease(releases);
            performPossibleAcquires();
        } finally {
            lock.unlock();
        }
    }
}

/**
 * Message queue following the kernel style, using an *implicit Java monitors*,
 * with support for timeout on the receive operation.
 *
 * Note: in this synchronizer when a thread gives up the acquire operation due
 * to timeout or interruption, no other acquire can be satisfied, so the code
 * that addresses this situation was not included.
 */

class MessageQueueKernelStyleImplicitMonitor<T> {
    // implicit Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Object monitor = new Object();

    // the type used to hold a receive request
    private class Request {
		T receivedMsg;	    // received message
		boolean done;		// true when done
	}
    
    // queue of pending receive requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();
    
	// synchronization state: list of messages pending for reception
    private final LinkedList<T> pendingMessages = new LinkedList<T>();

    // initialize the message queue
	public MessageQueueKernelStyleImplicitMonitor() { }

    // returns true if there is an pending message, which means that receive
	// can succeed immediately
    private boolean canReceive() { return pendingMessages.size() > 0; }

    // when a message is received, it must be removed from the pending message list
    private T receiveSideEffect() { return pendingMessages.removeFirst(); }

    // add the sent message to the pending messages list
    private void updateStateOnSend(T sentMessage) {
        pendingMessages.addLast(sentMessage);
    }

    // receive the next message from the queue; returns null when it times out
    public T receive(long millisTimeout) throws InterruptedException {
        synchronized(monitor) {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();
            
            if (reqQueue.size() == 0 && canReceive())
                return receiveSideEffect();
            Request request = new Request();
            reqQueue.addLast(request);          // enqueue the "request" at the end of "reqQueue"
            TimeoutHolder th = new TimeoutHolder(millisTimeout);
            do {
                try {
                    if (th.isTimed()) {
                        if ((millisTimeout = th.value()) <= 0) {
                            // the specified time limit has expired.
					        // Here we know that our request was not met.
                            reqQueue.remove(request);
                            return null;
                        }
                        monitor.wait(millisTimeout);
                    } else
                        monitor.wait();
                } catch (InterruptedException ie) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw 
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.currentThread().interrupt();
                        break;
                    }
                    reqQueue.remove(request);
                    throw ie;
                }
            } while (!request.done);
            return request.receivedMsg;
        }
    }

	// send a message to the queue
    public void Send(T sentMsg) {
        synchronized(monitor) {
            updateStateOnSend(sentMsg);
            if (reqQueue.size() > 0) {
                // here we know that the queue has one message, so we do not call "canAcquire"
                Request request = reqQueue.poll();
                request.receivedMsg = receiveSideEffect();
                request.done = true;
                
				// even if we release only one thread, we do not know its position of the queue
                // of the condition variable, so it is necessary to notify all blocked threads,
                // to make sure that the thread in question is notified.
                monitor.notifyAll();
            }
        }
    }

    // send a message to the queue (optimized)
    public void SendOptimized(T sentMsg) {
        synchronized(monitor) {
            // try to deliver the message directly to a blocked thread
            if (reqQueue.size() > 0) {
                Request request = reqQueue.poll();
                request.receivedMsg = sentMsg;
                request.done = true;

                // even if we release only one thread, we do not know its position of the queue
                // of the condition variable, so it is necessary to notify all blocked threads,
                // to make sure that the thread in question is notified.
                monitor.notifyAll();
            } else {
                // no receiving thread, so the message is left in the respective queue
                updateStateOnSend(sentMsg);
            }
        }
    }
}

/**
 * Message queue following the kernel style, using an *explicit Java monitor*,
 * with support for timeout on the receive operation.
 * 
 * NOTE: This implementation uses specific thread notifications.
 */

class MessageQueueKernelStyleExplicitMonitorSpecificNotification<T> {
    // explic Java monitor that suports the synchronzation of shared data access
    // and supports also the control synchronization.
    private final Lock lock = new ReentrantLock();

    // the type used to hold a receive request
    private class Request {
        final Condition okToReceive;
        T receivedMsg; // received message
        boolean done; // true when done

        Request(Condition okToRec) { okToReceive = okToRec; }
    }

    // queue of pending receive requests
    private final LinkedList<Request> reqQueue = new LinkedList<Request>();

    // synchronization state: list of messages pending for reception
    private final LinkedList<T> pendingMessages = new LinkedList<T>();

    // initialize the message queue
    public MessageQueueKernelStyleExplicitMonitorSpecificNotification() {}

    // returns true if there is an pending message, which means that receive
    // can succeed immediately
    private boolean canReceive() { return pendingMessages.size() > 0; }

    // when a message is received, it must be removed from the pending message list
    private T receiveSideEffect() { return pendingMessages.removeFirst(); }

    // add the sent message to the pending messages list
    private void updateStateOnSend(T sentMessage) { pendingMessages.addLast(sentMessage); }

    // receive the next message from the queue; returns null when it times out
    public T receive(long millisTimeout) throws InterruptedException {
        lock.lock();
        try {
            // if the was previously interrupted, throw the appropriate exception
            if (Thread.interrupted())
                throw new InterruptedException();

                if (reqQueue.size() == 0 && canReceive())
                return receiveSideEffect();
            Request request = new Request(lock.newCondition());
            reqQueue.addLast(request); // enqueue the "request" at the end of request queue
            boolean isTimed = millisTimeout > 0;
            long nanosTimeout = isTimed ? TimeUnit.MILLISECONDS.toNanos(millisTimeout) : 0L;
            do {
                try {
                    if (isTimed) {
                        if (nanosTimeout <= 0L) {
                            // the specified time limit has expired.
                            // Here we know that our request was not met.
                            reqQueue.remove(request);
                            return null;
                        }
                        nanosTimeout = request.okToReceive.awaitNanos(nanosTimeout);
                    } else
                        request.okToReceive.await();
                } catch (InterruptedException ie) {
                    // if the acquire operation was already done, re-assert interrupt
                    // and return normally; else remove request from queue and throw
                    // ThreadInterruptedException.
                    if (request.done) {
                        Thread.currentThread().interrupt();
                        break;
                    }
                    reqQueue.remove(request);
                    throw ie;
                }
            } while (!request.done);
            return request.receivedMsg;
        } finally {
            lock.unlock();
        }
    }

    // send a message to the queue
    public void Send(T sentMsg) {
        lock.lock();
        try {
            updateStateOnSend(sentMsg);
            if (reqQueue.size() > 0) {
                // here we know that the queue has one message, so we do not call "canAcquire"
                Request request = reqQueue.poll();
                request.receivedMsg = receiveSideEffect();
                request.done = true;
                // notify the receiver thread on its private condition varaiable.
                request.okToReceive.signal();
            }
        } finally {
            lock.unlock();
        }
    }

    // send a message to the queue (optimized)
    public void SendOptimized(T sentMsg) {
        lock.lock();
        try {
            // try to deliver the message directly to a blocked thread
            if (reqQueue.size() > 0) {
                Request request = reqQueue.poll();
                request.receivedMsg = sentMsg;
                request.done = true;
                // notify the receiver thread on its private condition varaiable.
                request.okToReceive.signal();
            } else {
                // no receiving thread, so the message is left in the respective queue
                updateStateOnSend(sentMsg);
            }
        } finally {
            lock.unlock();
        }
    }
}

public class GenericSynchronizerKernelStyle {
    public static void main(String... args) {}
}

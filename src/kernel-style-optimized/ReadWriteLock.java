/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Rreadwritelock with the semantics proposed by hoare and implemented using the
 *  code pattern presented in the Example 4 ("kernel style") in
 *  "Sincronização com Monitores na CLI e na Infra-estrutura Java".
 *
 *  Compile with: javac ReadWriteLock.java
 *  Execute with: java ReadWriteLock
 *
 *  Carlos Martins, April 2018
 *
 **/

import java.util.Random;
import java.util.LinkedList;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.ReentrantLock;
import java.util.concurrent.atomic.AtomicInteger;

/*
 * Read/Writer lock with Hoare's semantics in order to prevent
 * readers and writers starvation.
 */

final class ReadWriteLock_ {
	private final Lock mlock;			// the monitor's lock
	private final Condition okToRead;	// condition variable where readers are blocked
	private final Condition okToWrite;	// condition variable where writers are blocked
	private int state = 0;				// -1 means writing, 0 means free, > 0 means reading (# of readers)
	
	private static class Request {
		public boolean done;			// true when the request is satisfied
	}
	
	// We use a queue for waiting readers and a queue for waiting writers.
	// For each queue node holds an object with a boolean fields that says if
	// the requested access was already granted or not.
	private final LinkedList<Request> rdq;
	private final LinkedList<Request> wrq;

	// Constructor.
	public ReadWriteLock_() {
		mlock = new ReentrantLock();
		okToRead = mlock.newCondition();
		okToWrite = mlock.newCondition();
		rdq = new LinkedList<Request>();
		wrq = new LinkedList<Request>();
	}

	// Acquire the lock for read (shared) access
	public void lockRead() throws InterruptedException {
		mlock.lock();
		try {
			// if there isn’t blocked writers and the resource isn’t being written, grant
			// read access immediately
			if (wrq.size() == 0 && state >= 0) {
				state++;
				return;
			}
			
			// otherwise, create a request object and enqueue it
			Request rdreq = new Request();
			rdq.addLast(rdreq);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					okToRead.await();
				} catch (InterruptedException ie) {
					// if the requested shared access was granted, we must re-assert interrupt
					// exception, and return normally.
					if (rdreq.done) {
						Thread.currentThread().interrupt();
						return;
					}
					// otherwise, we remove the request from the queue and re-throw the exception
					rdq.remove(rdreq);
					throw ie;
				}
				// if shared access was granted then return; otherwise, re-wait
			} while (!rdreq.done);
		} finally {
			mlock.unlock();
		}
	}
	
	// auxiliary method: grant access to all waiting readers
	private boolean grantAccessToWaitingReaders() {
		if (rdq.size() > 0) {
			state += rdq.size();		// account with all new active readers
			do {
				rdq.element().done = true;	// mark read request as granted
				rdq.remove();			// remove the first element of the queue
			} while (rdq.size() > 0);
			okToRead.signalAll();		// notify all waiting readers
			return true;
		}
		return false;
	}

	// auxiliary method: grant access to the first waiting writer
	private void grantAccessToAWaitingWriter() {
		if (wrq.size() > 0) {
			wrq.element().done = true;	// mark write request as granted
			state = -1;					// exclusive lock was taken	
			wrq.remove();				// remove the first element of the queue
			okToWrite.signalAll(); 		// notify waiting writers to ensure that the released writer
										// is notified.
		}
	}
	
	// Acquire the lock for write (exclusive) access
	public void lockWrite() throws InterruptedException {
		mlock.lock();
		try {
			// if the lokc isn’t held for read nor for writing, grant the access immediately
			if (state == 0) {
				state = -1;
				return;
			}
			// create and enqueue a request for exclusive access
			Request wrreq = new Request();
			wrq.addLast(wrreq);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					okToWrite.await();
				} catch (InterruptedException ie) {
					// if exclusive access was granted, then we re-assert exception, and return normally
					if (wrreq.done) {
						Thread.currentThread().interrupt();
						return;
					}
					// othwewise, remove the request from the queue, and return throwing the exception.
					wrq.remove(wrreq);
					
					// when a waiting writer gives up, we must grant shared access to all
					// waiting readers that has been blocked by this waiting writer
					if (wrq.size() == 0 && rdq.size() > 0 && state >= 0)
						grantAccessToWaitingReaders();
					throw ie;
				}
				// if the request was granted return, else re-wait
			} while (!wrreq.done);
		} finally {
			mlock.unlock();
		}
	}
	
	// Release read (shared) lock
	public void unlockRead() {
		mlock.lock();
		try {
			// decrement the number of active readers
			// if this is the last active reader, and there is at least a blocked writer, grant access
			// to the writer that is at front of queue
			if (--state == 0 && wrq.size() > 0)
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}

	// Release the write (exclusive) lock
	public void unlockWrite() {
		mlock.lock();
		try {
			state = 0;
			if (!grantAccessToWaitingReaders())
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}
}

/**
 * Read/Writer lock with Hoare's semantics in order to prevent readers and
 * writers starvation.
 *
 * Note: This implementation has two optimizations: (1) it simplifies the queue
 * where readers are blocked by taking advantage of the fact that all blocked
 * readers are unlocked at the same time; (2) uses specific notifications of
 * blocked threads (all readers are locked in the same condition variable while
 * writers are locked in private condition variables).
 */

final class ReadWriteLockOptimized {
	private final Lock mlock; 			// the monitor's lock
	private final Condition okToRead;	// condition variable where readers are blocked
	private int state = 0; // -1 when writing, 0 when free, > 0 when reading (# of readers)
	
	// All waiting readers share the same request, because
	// because the respective request is guaranteed in group
	private static class LockReadRequest {
		int waiters = 1; 			// created by the first waiting reader
		boolean done;				// set to true when the request is satisfied
	}

	// request object used for writers
	private static class LockWriteRequest {
		final Condition okToWrite;	// conditon ver where the writer is waiting
		boolean done; 				// set true when the request is satisfied

		LockWriteRequest(Condition oktow) { okToWrite = oktow; }
	}
	// We use a queue for waiting readers and a queue for waiting writers.
	// For each queue node holds an object with a boolean fields that says if
	// the requested access was already granted or not.
	private LockReadRequest readReqQueue;	// null when queue is empty
	private final LinkedList<LockWriteRequest> writeReqQueue;

	// Constructor.
	public ReadWriteLockOptimized() {
		mlock = new ReentrantLock();
		okToRead = mlock.newCondition();
		readReqQueue = null;
		writeReqQueue = new LinkedList<LockWriteRequest>();
	}

	/*
	 * Methods that implement the lock read request queue
	 */

	private LockReadRequest enqueueReader() {
		if (readReqQueue == null)
			readReqQueue = new LockReadRequest();
		else
			readReqQueue.waiters++;
		return readReqQueue;
	}

	private void removeReader() {
		if (readReqQueue != null)
			readReqQueue.waiters--;
		else
			System.out.println("Ooops!!!");
	}

	private int waitingReaders() {
		return readReqQueue != null ? readReqQueue.waiters : 0;
	}

	private void clearReaderQueue() { readReqQueue = null; }

	// Acquire the lock for read (shared) access
	public void lockRead() throws InterruptedException {
		mlock.lock();
		try {
			// if there isn’t blocked writers and the resource isn’t being written, grant
			// read access immediately
			if (writeReqQueue.size() == 0 && state >= 0) {
				state++;
				return;
			}

			// otherwise, create a request object and enqueue it
			LockReadRequest request = enqueueReader();
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					okToRead.await();
				} catch (InterruptedException ie) {
					// if the requested shared access was granted, we must re-assert interrupt
					// exception, and return normally.
					if (request.done) {
						Thread.currentThread().interrupt();
						break;
					}
					// otherwise, we remove the request from the queue and re-throw the exception
					removeReader();
					throw ie;
				}
				// if shared access was granted then return; otherwise, re-wait
			} while (!request.done);
		} finally {
			mlock.unlock();
		}
	}

	// auxiliary method: grant access to all waiting readers
	private boolean grantAccessToWaitingReaders() {
		if (waitingReaders() > 0) {
			state += waitingReaders(); 	// account with all new active readers
			readReqQueue.done = true;
			clearReaderQueue();
			okToRead.signalAll(); 		// notify all waiting readers
			return true;
		}
		return false;
	}

	// auxiliary method: grant access to the first waiting writer
	private void grantAccessToAWaitingWriter() {
		if (writeReqQueue.size() > 0) {
			LockWriteRequest request = writeReqQueue.poll();	// remove the first element of the queue
			request.done = true;	// mark write request as granted
			state = -1; 			// exclusive lock was taken
			request.okToWrite.signal(); // notify waiting writer at its private condition variable
		}
	}

	// Acquire the lock for write (exclusive) access
	public void lockWrite() throws InterruptedException {
		mlock.lock();
		try {
			// if the lokc isn’t held for read nor for writing, grant the access immediately
			if (state == 0) {
				state = -1;
				return;
			}
			// create and enqueue a request for exclusive access
			LockWriteRequest request = new LockWriteRequest(mlock.newCondition());
			writeReqQueue.addLast(request);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					request.okToWrite.await();
				} catch (InterruptedException ie) {
					// if exclusive access was granted, then we re-assert exception, and return
					// normally
					if (request.done) {
						Thread.currentThread().interrupt();
						break;
					}
					// othwewise, remove the request from the queue, and return throwing the
					// exception.
					writeReqQueue.remove(request);

					// when a waiting writer gives up, we must grant shared access to all
					// waiting readers that has been blocked by this waiting writer
					if (writeReqQueue.size() == 0 && waitingReaders() > 0 && state >= 0)
						grantAccessToWaitingReaders();
					throw ie;
				}
				// if the request was granted return, else re-wait
			} while (!request.done);
		} finally {
			mlock.unlock();
		}
	}

	// Release read (shared) lock
	public void unlockRead() {
		mlock.lock();
		try {
			// decrement the number of active readers
			// if this is the last active reader, and there is at least a blocked writer,
			// grant access
			// to the writer that is at front of queue
			if (--state == 0 && writeReqQueue.size() > 0)
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}

	// Release the write (exclusive) lock
	public void unlockWrite() {
		mlock.lock();
		try {
			state = 0;		// mark lock as free
			if (!grantAccessToWaitingReaders())
				grantAccessToAWaitingWriter();
		} finally {
			mlock.unlock();
		}
	}
}

public class ReadWriteLock {
	
	// needed volatiles
	static volatile boolean running = true;
	static volatile int sharedWriteCounter = 0;
	
	private static boolean testReadWriteLock() throws InterruptedException {

		final int RUN_TIME = 5 * 1000;
		final int EXIT_TIME = 50;		
		final int READER_THREADS = 50;
		final int WRITER_THREADS = 25;
		final int MIN_BACKOFF = 0;
		final int MAX_BACKOFF = 1;
 
		Thread[] readers = new Thread[READER_THREADS];
		Thread[] writers = new Thread[WRITER_THREADS];
		AtomicInteger sharedReadCounter = new AtomicInteger(0);
		int[] readCounters = new int[READER_THREADS];
		int[] writeCounters = new int[WRITER_THREADS];

		// the read/write lock
		//ReadWriteLock_ rwlock = new ReadWriteLock_();
		ReadWriteLockOptimized rwlock = new ReadWriteLockOptimized();
		
		System.out.println("\n--> test read/write lock");
		// Create and start reader threads.
		for (int i = 0; i < READER_THREADS; i++) {
			final int tid = i;
			readers[i] = new Thread(() -> {
				Random rnd = new Random(tid);
				do {
					try {
						rwlock.lockRead();
					} catch (InterruptedException ie) {
						break;
					}
					Thread.yield();
					sharedReadCounter.incrementAndGet();
					rwlock.unlockRead();
					if ((++readCounters[tid] % 1000) == 0) {
						System.out.printf("[r#%02d]", tid);
					} else {
						try {
							if (MAX_BACKOFF > 0)
								Thread.sleep(rnd.nextInt(MAX_BACKOFF) + MIN_BACKOFF);
						} catch (InterruptedException ie) {
							break;
						}
					}
				} while (running);					
			});
			readers[i].start();
		}
		
		// Create and start writer threads.
		for (int i = 0; i < WRITER_THREADS; i++) {
			final int tid = i;
			writers[i] = new Thread(() -> {
				Random rnd = new Random(tid);
				do {
					try {
						rwlock.lockWrite();
					} catch (InterruptedException ie) {
						break;
					}
					Thread.yield();
					sharedWriteCounter++;
					rwlock.unlockWrite();
					if ((++writeCounters[tid] % 250) == 0) {
						System.out.printf("[w#%02d]", tid);
					} else {
						try {
							if (MAX_BACKOFF > 0)
								Thread.sleep(rnd.nextInt(MAX_BACKOFF) + MIN_BACKOFF);
						} catch (InterruptedException ie) {
							break;
						}
					}
				} while (running);					
			});
			writers[i].start();
		}
		
		// run the test for a while
		Thread.sleep(RUN_TIME);
		running = false;
		Thread.sleep(EXIT_TIME);
		
		// wait until all writer threads have been terminated.
		for (int i = 0; i < WRITER_THREADS; i++) {
			if (writers[i].isAlive())
				writers[i].interrupt();
			writers[i].join();
		}

		// wait until all reader threads have been terminated.
		for (int i = 0; i < READER_THREADS; i++) {
			if (readers[i].isAlive())
				readers[i].interrupt();
			readers[i].join();
		}
		
		// compute results
		System.out.printf("%n%nReader counters:%n");
		int reads = 0;
		for (int i = 0; i < READER_THREADS; i++) {
			reads += readCounters[i];
			if (i != 0 && (i % 5) == 0)
				System.out.println();
			else if (i != 0)
				System.out.print(' ');
			System.out.printf("[r#%02d: %4d]", i, readCounters[i]);
		}
		
		System.out.println("\n\nWriter counters:");
		int writes = 0;
		for (int i = 0; i < WRITER_THREADS; i++) {
			writes += writeCounters[i];
			if (i != 0 && (i % 5) == 0) {
				System.out.println();
			} else if (i != 0){
				System.out.print(' ');
			}
			System.out.printf("[w#%02d: %4d]", i, writeCounters[i]);
		}
		System.out.printf("%n%n--private/shared reads: %d/%d, private/shared writes: %d/%d%n",
				 			reads, sharedReadCounter.get(), writes, sharedWriteCounter);
		return reads == sharedReadCounter.get() &&
			   writes == sharedWriteCounter;
	}
	
	
	public static void main(String... args) throws InterruptedException {
		System.out.printf("-->test read/write lock: %s%n",
							testReadWriteLock() ? "passed" : "failed");
	}
}



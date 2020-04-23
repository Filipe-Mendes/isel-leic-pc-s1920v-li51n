/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Rreadwritelock with the semantics proposed by hoare and implemented using the
 *  code pattern presented in the Example 4 ("kernel style"/ "delegation of execution
 *  style") in "Sincronização com Monitores na CLI e na Infra-estrutura Java".
 *
 *  Generate executable with: csc ReadWriteLock.cs
 *
 *  Carlos Martins, April 2020
 *
 **/

using System;
using System.Threading;
using System.Collections.Generic;

/*
 * Read/Writer lock with a naive implementation where readers can prevent
 * access to writers.
 */

internal sealed class ReadWriteLockStarveWriters {
    private readonly object monitor = new object(); // the implicit monitor
    private int readers = 0;        // current number of readers
    private bool writing = false;   // true when writing

    // Constructor.
    public ReadWriteLockStarveWriters() { }

    // Acquire the lock for read (shared) access
    public void LockRead() {
        lock (monitor) {
            while (writing)
                Monitor.Wait(monitor);
            readers++;
        }
    }

    // Acquire the lock for write (exclusive) access
    public void LockWrite() {
        lock (monitor) {
            while (writing || readers > 0)
                Monitor.Wait(monitor);
            writing = true;
        }
    }

    // Release read (shared) lock
    public void UnlockRead() {
        lock (monitor) {
            if (--readers == 0)
                Monitor.PulseAll(monitor);
        }
    }

    // Release the write (exclusive) lock
    public void UnlockWrite() {
        lock (monitor) {
            writing = false;
            Monitor.PulseAll(monitor);
        }
    }
}

/*
 * Read/Writer lock with a naive implementation where writers can prevent
 * access to readers.
 */

internal sealed class ReadWriteLockStarveReaders {
    private readonly object monitor = new object(); // the implicit monitor
    private int readers = 0;        // current number of readers
    private bool writing = false;   // true when writing
    private int waitingWriters = 0; // the number of waiting writers	

    // Constructor.
    public ReadWriteLockStarveReaders() { }

    // Acquire the lock for read (shared) access
    public void LockRead() {
        lock (monitor) {
            while (writing || waitingWriters > 0)
                Monitor.Wait(monitor);
            readers++;
        }
    }

    // Acquire the lock for write (exclusive) access
    public void LockWrite() {
        lock (monitor) {
            if (readers == 0 && !writing) {
                writing = true;
                return;
            }
            waitingWriters++;
            try {
                do {
                    Monitor.Wait(monitor);
                } while (readers > 0 || writing);
                writing = true;
            } finally {
                waitingWriters--;
            }
        }
    }

    // Release read (shared) lock
    public void UnlockRead() {
        lock (monitor) {
            if (--readers == 0)
                Monitor.PulseAll(monitor);
        }
    }

    // Release the write (exclusive) lock
    public void UnlockWrite() {
        lock (monitor) {
            writing = false;
            Monitor.PulseAll(monitor);
        }
    }
}

/**
 * Read/Writer lock with Hoare's semantics in order to prevent
 * readers and writers starvation, with the following criteria:
 *  - Waiting writers prevent the access of new readers;
 *  - When a writer releases its write lock, all waiting readers
 *    acquire a read lock.
 */

internal sealed class ReadWriteLock {
	// the implicit .NET monitor
	private readonly object monitor = new object();

	// synchronization state
	private int state = 0;		// -1 when writing; 0 when free and > 0 when reading
	
	// The request object for lock read e lock write has only the "done" field.
	// So we implement the request queues with a LinkedList<bool>.
	private readonly LinkedList<bool> readReqQueue = new LinkedList<bool>();
	private readonly LinkedList<bool> writeReqQueue = new LinkedList<bool>();

	// Constructor.
	public ReadWriteLock() { }

	// Acquire the lock for read (shared) access
	public void LockRead() {
		lock(monitor) {
			// if there isn’t blocked writers and the resource isn’t being written, grant
			// read access immediately
			if (writeReqQueue.Count == 0 && state >= 0) {
				state++;	// account for a new read lock
				return;
			}
			
			// otherwise, enqueue a read access request
			LinkedListNode<bool> requestNode = readReqQueue.AddLast(false);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					Monitor.Wait(monitor);
				} catch (ThreadInterruptedException) {
					// if the requested shared access was granted, we must re-assert interrupt
					// exception, and return normally.
					if (requestNode.Value) {
						Thread.CurrentThread.Interrupt();
						break;
					}
					// otherwise, we remove the request from the queue and re-throw the exception
					readReqQueue.Remove(requestNode);
					throw;
				}
				// if shared access was granted then return; otherwise, re-wait
			} while (!requestNode.Value);
		}
	}
	
	// auxiliary method: grant access to all waiting readers
	private bool GrantAccessToWaitingReaders() {
		if (readReqQueue.Count > 0) {
			state += readReqQueue.Count;		// account with all new active readers
			do {
				readReqQueue.First.Value = true; 	// mark read request as granted
				readReqQueue.RemoveFirst();			// remove request from the queue	
			} while (readReqQueue.Count > 0);
			Monitor.PulseAll(monitor);	// notify all waiting readers (and writers)
			return true;
		}
		return false;
	}

	// auxiliary method: grant access to the first waiting writer
	private void GrantAccessToAWaitingWriter() {
		if (writeReqQueue.Count > 0) {
            writeReqQueue.First.Value = true;	// mark write request as granted
			state = -1;             			// exclusive lock was taken	
            writeReqQueue.RemoveFirst();
			Monitor.PulseAll(monitor); 	// notify all waiting writers (and readers)
		}
	}
	
	// Acquire the lock for write (exclusive) access
	public void LockWrite() {
		lock(monitor) {
			// if the lokc isn’t held for read nor for writing, grant the access immediately
			if (state == 0) {
				state = -1;
				return;
			}
			// enqueue a request for exclusive access
			LinkedListNode<bool> requestNode = writeReqQueue.AddLast(false);
			// wait until request is granted, or the thread gives up due to interruption
			do {
				try {
					Monitor.Wait(monitor);
				} catch (ThreadInterruptedException) {
					// if exclusive access was granted, then we re-assert exception, and return normally
					if (requestNode.Value) {
						Thread.CurrentThread.Interrupt();
						break;
					}
					// othwewise, remove the request from the queue, and return throwing the exception.
					writeReqQueue.Remove(requestNode);
					
					// when a waiting writer gives up, we must grant shared access to all
					// waiting readers that has been blocked by this waiting writer
					// this is the same as the CurrentSynchStateAllowAcquire() predicate
					if (writeReqQueue.Count == 0 && readReqQueue.Count > 0 && state >= 0)
						GrantAccessToWaitingReaders();

					throw;
				}
				// if the request was granted return, else re-wait
			} while (!requestNode.Value);
		}
	}
	
	// Release read (shared) lock
	public void UnlockRead() {
		lock(monitor) {
			// decrement the number of active readers
			// if this is the last active reader, and there is at least a blocked writer, grant access
			// to the writer that is at front of queue
			if (--state == 0 && writeReqQueue.Count > 0)
				GrantAccessToAWaitingWriter();
		}
	}

	// Release the write (exclusive) lock
	public void UnlockWrite() {
		lock(monitor) {
			state = 0;
			if (!GrantAccessToWaitingReaders())
				GrantAccessToAWaitingWriter();
		}
	}
}

/**
 * Read/Writer lock with Hoare's semantics in order to prevent readers
 * and writers starvation.
 *
 * Note: This implementation has two optimizations: (1) it simplifies the queue
 *       where readers are blocked by taking advantage of the fact that all blocked
 *       readers are unblocked at the same time; (2) uses specific notifications of
 *       blocked threads (all readers are blocked in the same condition variable 
 *       while writers are blocked in private condition variables).
 */

internal sealed class ReadWriteLockOptimized {

    // All waiting readers share the same request, because
    // because the respective request is guaranteed in group 
    private class LockReadRequest {
		internal int waiters = 1;	// created by the first waiting reader
		internal bool done;
	}

	// LockWriteRequest has only the field "done", so the request queue
	// for lock write requests is implemented by a LinkedList<bool>.

	// implicit .NET monitor
    private readonly object monitor = new object(); // reader's condition variable

	// lock synchronization state
    private int state = 0;        // -1 when writing; 0 when free; > 0 when reading

    // For readers we implement the queue sharing the same request object.
	private LockReadRequest readReqQueue;

	// For writers the request queue holds only a boolean for each waiting writer.
	private readonly LinkedList<bool> writeReqQueue = new LinkedList<bool>();

    // Constructor.
    public ReadWriteLockOptimized() { }

	/*
	 * Methods that implement the lock read request queue
	 */
	private LockReadRequest EnqueueReader() {
		if (readReqQueue == null)
			readReqQueue = new LockReadRequest();
		else
			readReqQueue.waiters++;
		return readReqQueue;
	}

    private void RemoveReader() { readReqQueue.waiters--; }

	private int WaitingReaders() {
		return readReqQueue != null ? readReqQueue.waiters : 0;
	}

    private void ClearReaderQueue() { readReqQueue = null; }

    // Acquire the lock for read (shared) access
    public void LockRead() {
        lock (monitor) {
            // if there isn’t blocked writers and the resource isn’t being written, grant
            // read access immediately
            if (writeReqQueue.Count == 0 && state >= 0) {
                state++;	// add one reader
                return;
            }

            // otherwise, enqueue a read access request

            LockReadRequest request  = EnqueueReader();
            // wait until request is granted, or the thread gives up due to interruption
            do {
                try {
					MonitorEx.Wait(monitor, monitor);	// eq a Monitor.Wait(monitor);
                } catch (ThreadInterruptedException) {
                    // if the requested shared access was granted, we must re-assert interrupt
                    // exception, and return normally.
                    if (request.done) {
                        Thread.CurrentThread.Interrupt();
                        break;
                    }
                    // otherwise, we remove the request from the queue and re-throw the exception
                    RemoveReader();
                    throw;
                }
                // if shared access was granted then return; otherwise, re-wait
            } while (!request.done);
        }
    }

    // auxiliary method: grant access to all waiting readers
    private bool GrantAccessToWaitingReaders() {
        if (WaitingReaders() > 0) {
            readReqQueue.done = true;	// mark all lock read requests as done
            state += WaitingReaders();	// account with all new active readers
            ClearReaderQueue();
            MonitorEx.PulseAll(monitor, monitor);  // notify waiting readers
            return true;
        }
        return false;
    }

    // auxiliary method: grant access to the first waiting writer
    private void GrantAccessToAWaitingWriter() {
        if (writeReqQueue.Count > 0) {
			LinkedListNode<bool> reqNode = writeReqQueue.First;
			reqNode.Value = true;		// set request.done to true
            state = -1;             	// exclusive lock was taken	
            writeReqQueue.RemoveFirst();
            MonitorEx.Pulse(monitor, reqNode);  // notify the waiting writer
        }
    }

    // Acquire the lock for write (exclusive) access
    public void LockWrite() {
        lock (monitor) {
            // if the lock isn’t held for read nor for writing, grant the access immediately
            if (state == 0 ) {
                state = -1;			// set state indicating exclusive lock held
                return;
            }
            // enqueue a request for exclusive access
            LinkedListNode<bool> requestNode = writeReqQueue.AddLast(false);
            // wait until request is granted, or the thread gives up due to interruption
            do {
                try {
                    MonitorEx.Wait(monitor, requestNode);
                } catch (ThreadInterruptedException) {
                    // if exclusive access was granted, then we re-assert exception, and return normally
                    if (requestNode.Value) {
                        Thread.CurrentThread.Interrupt();
                        break;
                    }
                    // othwewise, remove the request from the queue, and return throwing the exception.
                    writeReqQueue.Remove(requestNode);

                    // when a waiting writer gives up, we must grant shared access to all
                    // waiting readers that has been blocked by this waiting writer
                    if (writeReqQueue.Count == 0 && WaitingReaders() > 0 && state >= 0)
                        GrantAccessToWaitingReaders();
                    throw;
                }
                // if the request was granted return, else re-wait
            } while (!requestNode.Value);
        }
    }

    // Release read (shared) lock
    public void UnlockRead() {
        lock (monitor) {
			// decrement the number of active readers
            // if this is the last active reader, and there is at least a blocked writer,
			// grant access to the writer that is at front of queue
            if (--state == 0 && writeReqQueue.Count > 0)
                GrantAccessToAWaitingWriter();
        }
    }

    // Release the write (exclusive) lock
    public void UnlockWrite() {
        lock (monitor) {
            state = 0;		// no readers no writer
            if (!GrantAccessToWaitingReaders())
                GrantAccessToAWaitingWriter();
        }
    }
}

public class ReadWriteLockTest {
	
	private static bool TestReadWriteLock() {

		const int RUN_TIME = 5 * 1000;
		const int EXIT_TIME = 50;		
		const int READER_THREADS = 50;
		const int WRITER_THREADS = 25;
		const int MIN_BACKOFF = 0;
		const int MAX_BACKOFF = 10;
 
		Thread[] readers = new Thread[READER_THREADS];
		Thread[] writers = new Thread[WRITER_THREADS];
		int sharedWriteCounter = 0, sharedReadCounter = 0;
		int[] readCounters = new int[READER_THREADS];
		int[] writeCounters = new int[WRITER_THREADS];

        // the read/write lock
        //ReadWriteLockStarveWriters rwlock = new ReadWriteLockStarveWriters();
        //ReadWriteLockStarveReaders rwlock = new ReadWriteLockStarveReaders();
        //ReadWriteLock rwlock = new ReadWriteLock();
        ReadWriteLockOptimized rwlock = new ReadWriteLockOptimized();
		
        bool running = true;
		Console.WriteLine("\n--> test read/write lock");
		// Create and start reader threads.
		for (int i = 0; i < READER_THREADS; i++) {
			int tid = i;
			readers[i] = new Thread(() => {
				Random rnd = new Random(tid);
				do {
					try {
						rwlock.LockRead();
					} catch (ThreadInterruptedException) {
						break;
					}
					Thread.Yield();
					Interlocked.Increment(ref sharedReadCounter);
					rwlock.UnlockRead();
					if ((++readCounters[tid] % 1000) == 0) {
						Console.Write($"[r#{tid:D2}]");
						try {
							if (MAX_BACKOFF > 0)
								Thread.Sleep(rnd.Next(MIN_BACKOFF, MAX_BACKOFF));
						} catch (ThreadInterruptedException) {
							break;
						}
					}
				} while (Volatile.Read(ref running));					
			});
			readers[i].Start();
		}
		
		// Create and start writer threads.
		for (int i = 0; i < WRITER_THREADS; i++) {
			int tid = i;
			writers[i] = new Thread(() => {
				Random rnd = new Random(tid);
				do {
					try {
						rwlock.LockWrite();
					} catch (ThreadInterruptedException) {
						break;
					}
					Thread.Yield();
					sharedWriteCounter++;
					rwlock.UnlockWrite();
					if ((++writeCounters[tid] % 250) == 0) {
						Console.Write($"[w#{tid:D2}]");
						try {
							if (MAX_BACKOFF > 0)
								Thread.Sleep(rnd.Next(MIN_BACKOFF, MAX_BACKOFF));
						} catch (ThreadInterruptedException) {
							break;
						}
					}
				} while (Volatile.Read(ref running));					
			});
			writers[i].Start();
		}
		
		// run the test for a while
		Thread.Sleep(RUN_TIME);
		Volatile.Write(ref running, false);
		Thread.Sleep(EXIT_TIME);
		
		// wait until all writer threads have been terminated.
		for (int i = 0; i < WRITER_THREADS; i++) {
			if (writers[i].IsAlive)
				writers[i].Interrupt();
			writers[i].Join();
		}

		// wait until all reader threads have been terminated.
		for (int i = 0; i < READER_THREADS; i++) {
			if (readers[i].IsAlive)
				readers[i].Interrupt();
			readers[i].Join();
		}
		
		// compute results
		Console.WriteLine("\n\nReader counters:");
		int reads = 0;
		for (int i = 0; i < READER_THREADS; i++) {
			reads += readCounters[i];
			if (i != 0 && (i % 5) == 0)
				Console.WriteLine();
			else if (i != 0)
				Console.Write(' ');
			Console.Write($"[r#{i:D2}: {readCounters[i],4}]");
		}
		
		Console.WriteLine("\n\nWriter counters:");
		int writes = 0;
		for (int i = 0; i < WRITER_THREADS; i++) {
			writes += writeCounters[i];
			if (i != 0 && (i % 5) == 0) {
				Console.WriteLine();
			} else if (i != 0){
				Console.Write(' ');
			}
			Console.Write($"[w#{i:D2}: {writeCounters[i],4}]");
		}
		Console.WriteLine($"\n\n--private/shared reads: {reads}/{sharedReadCounter}, private/shared writes: {writes}/{sharedWriteCounter}");
		return reads == sharedReadCounter && writes == sharedWriteCounter;
	}
	
	
	public static void Main() {
		Console.WriteLine("-->test read/write lock: {0}",
							TestReadWriteLock() ? "passed" : "failed");
	}
}



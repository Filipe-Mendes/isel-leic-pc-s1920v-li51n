/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  A Bounded Message Queue with synchronous and asynchronous interface
 *
 *  Carlos Martins, June 2020
 *
 **/

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

/**
 * A bounded message queue with asynchronous and synchronous interfaces.
 */
public class MessageQueueAsync<T> where T: class {
  	private readonly ConcurrentQueue<T> queue;
	private readonly SemaphoreAsync freeSlots, filledSlots;
	
	/**
	 * Constructor 
	 */
	public MessageQueueAsync(int capacity = int.MaxValue) {
		queue = new ConcurrentQueue<T>();
		freeSlots = new SemaphoreAsync(capacity, capacity);
		filledSlots = new SemaphoreAsync(0, capacity);
	}

	/**
	 * Put a data item in the queue asynchronously enabling, optionally,
	 * timeout and/or cancellation.
	 */
	 public async Task<bool> PutAsync(T item, int timeout = Timeout.Infinite,
									 CancellationToken cToken = default(CancellationToken)) {
		if (!await freeSlots.WaitAsync(timeout: timeout, cToken: cToken))
			return false;       // timed out
		// we got a free slot, add the item to queue and update filled slots semaphore
		queue.Enqueue(item);
		filledSlots.Release();
		return true;
	}
	
	/**
	 * Put an item in the queue synchronously enabling, optionally,
	 * timeout and/or cancellation.
	 */
	 public bool Put(T item, int timeout = Timeout.Infinite,
		 		    CancellationToken cToken = default(CancellationToken)) {
		if (!freeSlots.Wait(1, timeout, cToken))
			return false;
		// We got a free slot, add the item to the queue and update filles slots semaphore
		queue.Enqueue(item);
		filledSlots.Release();
		return true;
	}
	
	/**
	 * Take an item from the queue asynchronously enabling, optionally,
	 * timeout and/or cancellation.
	 */
	 public async Task<T> TakeAsync(int timeout = Timeout.Infinite,
								    CancellationToken cToken = default(CancellationToken)) {
		if (!await filledSlots.WaitAsync(timeout: timeout, cToken: cToken))
			return null;	// timed out
		// Get the item from the queue and update free slots semaphore
		T item;
		queue.TryDequeue(out item);	// we know that there is at least a data item
		freeSlots.Release();
		return item;
	}

	/**
     * Take an item from the queue synchronously enabling, optionally,
	 * timeout and/or cancellation.
	 */
	 public T Take(int timeout = Timeout.Infinite,
			      CancellationToken cToken = default(CancellationToken)) {
		if (!filledSlots.Wait(1, timeout: timeout, cToken: cToken))
			return null;	// timed out
		// get the item from the queue and update free slots semaphore
		T item;
		queue.TryDequeue(out item);	// we know that there is at least a data item
		freeSlots.Release();
		return item;
	}

	/**
	 * Returns the number of filled positions in the queue
	 */
	public int Count { get { return queue.Count; } }
}

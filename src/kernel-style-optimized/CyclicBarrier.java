/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Cyclic barrier using the "delegation of execution style" following a reasoning
 *  based on the idea of generation.
 *
 *  Note: This implementation is similar to that used in jdk 9.
 *
 *  Compile with: javac CyclicBarrier.java
 *  Execute with: java CyclicBarrier
 *
 *  Carlos Martins, April 2018
 *
 */

import java.util.Random;
import java.util.List;
import java.util.ArrayList;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.BrokenBarrierException;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.ReentrantLock;


// The Cyclic Barrier
public final class CyclicBarrier {
	/**
	 * Each use of the barrier is represented as a generation instance.
	 * The generation changes whenever the barrier is tripped, or is reset.
	 * There can be many generations associated with threads using the barrier -
	 * due to the non-deterministic way the lock may be allocated to waiting
	 * threads - but only one of these can be active at a time (the one to which
	 * "count" applies) and all the rest are either broken or tripped.
	 * There need not be an active generation if there has been a break
	 * but no subsequent reset.
	 */
	private static class Generation {
		boolean broken;                 // initially false
	}
	
    private final Lock mlock;				// The monitor lock for guarding barrier entry
    private final Condition tripcv;			// Condition to wait on until tripped */
    private final int parties;				// The number of parties
    private final Runnable barrierCommand;	// The command to run when tripped
    private Generation generation;			// The current generation	

	
	/**
	 * Number of parties still waiting. Counts down from parties to 0 on each generation.
	 * It is reset to parties on each new generation or when broken.
	 */
    private int count;
	
	/**
	 * Updates state on barrier trip and wakes up everyone.
	 * Called only while holding lock.
	 */
	private void nextGeneration() {
		tripcv.signalAll();		// signal completion of last generation
		count = parties;		// set up next generation
		generation = new Generation();
	}

    /**
     * Sets current barrier generation as broken and wakes up everyone.
     * Called only while holding lock.
     */
    private void breakBarrier() {
        generation.broken = true;
        count = parties;
        tripcv.signalAll();
    }

    /**
     * Main barrier code, covering the various policies.
     */
	private int dowait(boolean timed, long nanos)
						throws InterruptedException, BrokenBarrierException, TimeoutException {
		mlock.lock();
		try {
			Generation g = generation;
            if (g.broken)
				throw new BrokenBarrierException();
            
			if (Thread.interrupted()) {
                breakBarrier();
                throw new InterruptedException();
            }
			
			int index = --count;
			if (index == 0) {					// tripped
				boolean ranAction = false;
                try {
                    if (barrierCommand != null)
                        barrierCommand.run();
                    ranAction = true;
                    nextGeneration();
                    return 0;
                } finally {
                    if (!ranAction)
                        breakBarrier();
                }
            }
			
            // loop until tripped, broken, interrupted, or timed out
            do {
				try {
					if (timed) {
						if (nanos <= 0L) {
							breakBarrier();
							throw new TimeoutException();
						}
                        nanos = tripcv.awaitNanos(nanos);						
					} else
                        tripcv.await();
                } catch (InterruptedException ie) {
                    if (g == generation && !g.broken) {
						breakBarrier();
                        throw ie;
                    } else {
                        // We're about to finish waiting even if we had not been interrupted,
						// so this interrupt is deemed to "belong" to subsequent execution.
                        Thread.currentThread().interrupt();
                    }
                }
                if (g.broken)
					throw new BrokenBarrierException();
			} while (g == generation);
			return index;
		} finally {
			mlock.unlock();
        }
    }

    /**
     * Creates a new CyclicBarrier that will trip when the given number of parties
	 * (threads) are waiting upon it, and which will execute the given barrier action
	 * when the barrier is tripped, performed by the last thread entering the barrier.
     */
	public CyclicBarrier(int parties, Runnable barrierAction) {
        if (parties <= 0)
			throw new IllegalArgumentException();
        this.parties = this.count = parties;
        this.barrierCommand = barrierAction;
		this.mlock = new ReentrantLock();
	    this.tripcv = mlock.newCondition();
		this.generation = new Generation();			// first genetarion	
    }

    public CyclicBarrier(int parties) { this(parties, null); }

    /**
     * Returns the number of parties required to trip this barrier.
     */
     public int getParties() {  return parties; }

    /**
     * Waits until all parties have invoked await on this barrier.
     *
     * If the current thread is not the last to arrive then it is
     * disabled for thread scheduling purposes and lies dormant until
     * one of the following things happens:
     * 
     * . The last thread arrives; or
     * . Some other thread interrupts the current thread; or
     * . Some other thread interrupts one of the other waiting threads; or
     * . Some other thread times out while waiting for barrier; or
     * . Some other thread invokes reset on this barrier.
     *
     *
     * If the current thread:
     * 
     * . is interrupted while waiting then InterruptedException is thrown and the
	 *   current thread's interrupted status is cleared.
     *
     * If the barrier is reset while any thread is waiting, or if the barrier is broken when
     * await is invoked, or while any thread is waiting, then BrokenBarrierException} is thrown.
     *
     * If any thread is interrupted} while waiting, then all other waiting threads will throw
     * BrokenBarrierException and the barrier is placed in the brokenvstate.
     *
     * If the current thread is the last thread to arrive, and a non-null barrier action was
	 * supplied in the constructor, then the current thread runs the action before allowing
	 * the other threads to continue.
     * If an exception occurs during the barrier action then that exception
     * will be propagated in the current thread and the barrier is placed in
     * the broken state.
     *
     * - return the arrival index of the current thread, where index
     *         getParties() - 1 indicates the first
     *         to arrive and zero indicates the last to arrive
     * - throws InterruptedException if the current thread was interrupted
     *         while waiting
     * -throws BrokenBarrierException if another thread was interrupted or timed out
     *		   while the current thread wasvwaiting, or the barrier was reset, or the
	 *         barrier was broken when await was called, or the barrier
     *         action (if present) failed due to an exception
	 *
	 *  The version with timeout, throws TimeoutException if the specified timeout elapses.
     *  In this case the barrier will be broken.
	 *
     */
    
	public int await() throws InterruptedException, BrokenBarrierException {
        try {
            return dowait(false, 0L);
        } catch (TimeoutException toe) {
            throw new Error(toe); // cannot happen
        }
    }

	public int await(long timeout, TimeUnit unit)
			throws InterruptedException, BrokenBarrierException, TimeoutException {
		boolean timed = timeout >= 0L; 
	    return dowait(timed, timed ? unit.toNanos(timeout) : 0L);
	}
	
    /**
     * Resets the barrier to its initial state.  If any parties are currently waiting
	 * at the barrier, they will return with a BrokenBarrierException. Note that resets
	 * after a breakage has occurred for other reasons can be complicated to carry out;
	 * threads need to re-synchronize in some other way, and choose one to perform the reset.
	 * It may be preferable to instead create a new barrier for subsequent use.
     */
    
	public void reset() {
        mlock.lock();
        try {
            breakBarrier();   // break the current generation
            nextGeneration(); // start a new generation
        } finally {
            mlock.unlock();
        }
    }

    /**
     * Returns the number of parties currently waiting at the barrier.
     * This method is primarily useful for debugging and assertions.
     */

    public int getNumberWaiting() {
        mlock.lock();
        try {
            return parties - count;
        } finally {
            mlock.unlock();
        }
    }
	
	/**
	 *  Test code
	 */
		
	private static class Solver {
		private static final int MIN_PROC_TIME = 100;
		private static final int MAX_PROC_TIME = 5000;
		private final int N;
		private final float[][] data;
		private final CyclicBarrier barrier;
		
		class Worker implements Runnable {
			private final int row;
			private final Random random;
			
			Worker(int row) {
				this.row = row;
				this.random = new Random(row);
			}
			
			public void run() {
				try {
					for (int i = 0; i < data[row].length; i++)
						data[row][i] += 1;
					// sleep a random time between MIN_TIME and MAX_TIME 
					Thread.sleep(random.nextInt(MAX_PROC_TIME) + MIN_PROC_TIME);
					System.out.printf("[%02d]", row);				
					barrier.await();
				} catch (InterruptedException ie) {
					System.out.println("*** worker thread was interrupted!");
				} catch (BrokenBarrierException ex) {
					System.out.println("*** the barrier was broken!");
				}
			}
		}
		
		public Solver(float[][] matrix) {
			data = matrix;
			N = matrix.length;
			Runnable barrierAction = () -> {
				System.out.println("\n\nAfter solved:");
				for (int r = 0; r < data.length; r++) {
					for (int c = 0; c < data[r].length; c++)
						System.out.printf("%6.1f ", data[r][c]);
					System.out.println();
				}
			};
			barrier = new CyclicBarrier(N, barrierAction);
		}
		
		public void start() throws InterruptedException {
			List<Thread> threads = new ArrayList<>(N);
			for (int i = 0; i < N; i++) {
				Thread thread = new Thread(new Worker(i));
				threads.add(thread);
				thread.start();
			}
			// wait until done
			for (Thread thread : threads)
				thread.join();
		}
	}
	
	public static void main(String... args) throws InterruptedException {
		final int ROWS = 30, COLUMNS = 10;
		float[][] matrix = new float[ROWS][COLUMNS];
		
		// fill the matrix with known values
		System.out.println("Initial matrix:");
		for (int r = 0; r < matrix.length; r++) {
			for (int c = 0; c < matrix[r].length; c++) {
				matrix[r][c] = r * COLUMNS + c;
				System.out.printf("%6.1f ", matrix[r][c]);
			}
			System.out.println();
		}
		Solver solver = new Solver(matrix);
		solver.start();
	} 		
}
	

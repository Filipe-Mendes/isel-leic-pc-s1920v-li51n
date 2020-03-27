/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Extension to the System.Threading.Monitor class in order to support Lampson and
 * Redell monitors supporting multiple condition variables.
 *
 * NOTE: This implementation has an importante limitation. It does not support waiting
 *       on condition variables by threads that have acquired the lock of the monitor
 *       more than once. (When this situation happens the monitor's lock is not completely
 *		 released in the Wait method, and as a consequence no other thread can enter the
 *       monitor.)
 *
 * Carlos Martins, September 2019
 *
 **/

using System;
using System.Threading;

public static class MonitorEx {
	
	/**
	 * Acquire an object's lock ignoring interrupts.
	 * Through its out parameter this method informs the caller if the current
	 * thread was interrupted while it was trying to acquire the lock.
	 */
	public static void EnterUninterruptibly(object mlock, out bool interrupted) {
		interrupted = false;
		do {
			try {
				Monitor.Enter(mlock);
				break;
			} catch (ThreadInterruptedException) {
				interrupted = true;
			}
		} while (true);
	}
	
	/**
	 * This method waits on a specific condition of a multi-condition monitor.
	 *
	 * This method assumes that it was called with "mlock" locked and the condition's lock
	 * unlocked.
	 * On return, the same conditions are meet: "mlock" locked and the condition's
	 * lock unlocked.
	 */
	
	public static void Wait(object mlock, object condition, int timeout = Timeout.Infinite) {
		// if the mlock and condition are the same object, we just call Monitor.Wait on "mlock"
		if (mlock == condition) {
			Monitor.Wait(mlock, timeout);
			return;
		}
		
		/**
		 * If the "mlock" and "condition" are different objects, we need to release the
		 * "mlock"'s lock before wait on condition's monitor.
		 *
		 * First, we acquire lock on "condition" object before release the lock on "mlock",
		 * to prevent the possible loss of notifications.
		 * If a ThreadInterruptException is thrown, we rethrow the exception with the "mlock"
		 * locked. We considerer this case as the exception was thrown by the
		 * the method Monitor.Wait(condition).
		 */
		
		// acquire the condition's lock
		Monitor.Enter(condition);
		// release the mlock's lock and wait on condition’s monitor condition
		Monitor.Exit(mlock);
		try {
			// wait on the condition monitor
			Monitor.Wait(condition, timeout);
		} finally {
			// release the condition’s lock
			Monitor.Exit(condition);
			
			// re-acquire the mlock's lock uninterruptibly
			bool interrupted;
			EnterUninterruptibly(mlock, out interrupted);
			// if the thread was interrupted while trying to acquire the mlock, we consider that
			// it was interrupted when in the wait state, so, we throw  ThreadInterruptedException.
			if (interrupted)
				throw new ThreadInterruptedException();
		}
	}
		
	/**
	 * This method notifies one thread that called Wait using the same mlock
	 * and condition objects.
	 *
	 * This method must be called with the "mlock"'s lock held, and returns under
	 * the same conditions.
	 */
	public static void Pulse(object mlock, object condition) {
		// If mlock and condition refers to the same object, we just call Monitor.Pulse on mlock.
		if (mlock == condition) {
			Monitor.Pulse(mlock);
			return;
		}
		/**
		 * If mlock and condition refer to different objects, in order to call Monitor.Pulse
		 * on condition we need to acquire condition object's lock.
		 * We must acquire the condition's lock ignoring ThreadInterruptedException,
		 * because the notify methods should not throw ThreadInterruptException.
		 */
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		// Notify the condition object and leave the corresponding monitor.
		Monitor.Pulse(condition);
		Monitor.Exit(condition);
		/**
		 * If the current thread was interrupted, we re-assert the interrupt, so the
		 * exception will be raised on the next call to a method that implies
		 * a "managed wait".
		 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
	
	/**
	 * This method notifies all threads that called Wait using the same mlock
	 * and condition objects.
	 *
	 * This method is called with the mlock's lock held, and returns under the
	 * same conditions.
	 */
	public static void PulseAll(object mlock, object condition) {
		// If mlock and condition refer to the same object, we just call Monitor.PulseAll on "mlock".
		if (mlock == condition) {
			Monitor.PulseAll(mlock);
			return;
		}
		/**
		 * If mlock and condition refer to different objects, in order to call Monitor.PulseAll
		 * on condition, we need to hold the condition's lock.
		 * We must acquire the condition's lock ignoring ThreadInterruptedException,
		 * because the notify methods should not throw ThreadInterruptException.
		 */
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		
		// Notify all threads waiting on the condition and leave the condition object's monitor
		Monitor.PulseAll(condition);
		Monitor.Exit(condition);
		/**
		 * In case of interrupt, we re-assert the interrupt, so the exception can be raised
		 * on call to a method that implies a "managed wait":
		 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
}

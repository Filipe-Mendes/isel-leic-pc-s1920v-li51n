/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Extension to the System.Threading.Monitor class in order to support Lampson and Redell
 * monitors with an arbitrary number of condition variables.
 *
 * NOTE: This implemetation has an importante limitation. It does not support waiting on condition
 * variables by threads than entered "the monitor" more than once (we this happens the monitor's lock
 * is not completely released by the wait method).
 *
 * Carlos Martins, April 2019
 *
 ***/

using System;
using System.Threading;

public static class MonitorEx {
	
	/**
	 * Acquire a monitor's lock ignoring possible interrupts.
	 * Through its out "interrrupted" parameter this method informs the caller if the
	 * current thread was interrupted while it was trying to acquire the monitor's lock.
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
	 * This method waits on the specified condition of a multi-condition monitor.
	 *
	 * This method is called with "monitor" locked and the condition's lock unlocked.
	 * On return, the same conditions are meet: "monitor" locked and the condition's lock unlocked.
	 */
	
	public static void Wait(object monitor, object condition, int timeout = Timeout.Infinite) {
		// if the monitor and condition are the same object, we just call Monitor.Wait on "monitor"
		if (monitor == condition) {
			Monitor.Wait(monitor, timeout);
			return;
		}
		
		/**
		 * if the monitor and condition are different objects, we need to release the monitor's
		 * lock before wait on the condition variable of the condition monitor.
		 *
		 * first, we need to enter the "condition's implicit monitor" before release the monitor's
		 * lock, in order to prevent the loss of notifications.
		 * if a ThreadInterruptException is thrown, we must return the exception with the monitor's
		 * lock locked. We considerer this case as the exception was thrown by the method
		 * Monitor.Wait(condition).
		 */
		
		// acquire the condition monitor's lock
		Monitor.Enter(condition);
		// release the monitor's lock; from here onwards it is possible to notify the condition,
		// but because the condition monitor's only will be released when the waiter thread
		// enters the Monitor.wait method, no notifications would be lost.
		Monitor.Exit(monitor);
		try {
			// wait on the condition monitor's condition variable
			Monitor.Wait(condition, timeout);
		} finally {
			// release the condition monitor’s lock
			Monitor.Exit(condition);
			
			// re-acquire the monitor's lock uninterruptibly
			bool interrupted;
			EnterUninterruptibly(monitor, out interrupted);
			// if the thread was interrupted while trying to acquire the monitor's lock, we consider
			// that it was interrupted when in the waiting on the condition variable, so we throw
			//  ThreadInterruptedException.
			if (interrupted)
				throw new ThreadInterruptedException();
		}
	}
		
	/**
	 * This method notifies one thread that called MonitorEx.Wait using the same monitor
	 * and condition variable objects.
	 *
	 * This method is called with the monitor's lock held, and returns under the same
	 * conditions.
	 */
	public static void Pulse(object monitor, object condition) {
		// if monitor and condition refers to the same object, we just call Monitor.Pulse on monitor.
		if (monitor == condition) {
			Monitor.Pulse(monitor);
			return;
		}
		
		/**
		 * If monitor and condition refer to different objects, in order to call Monitor.Pulse on
		 * condition we need to acquire condition monitor's lock.
		 * We must acquire the condition monitor's lock filtering ThreadInterruptedException,
		 * because this method is not used for wait purposes, so it must not throw that exception.
		 */
		
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		
		// notify the condition variable of the condition monitor and leave the corresponding monitor.
		Monitor.Pulse(condition);
		Monitor.Exit(condition);
		
		/*
		 * if the current thread was interrupted when acquiring the condition monitor's lock,
		 * we re-assert the interruption, so the exception will be raised on the next call to
		 * a managed wait operation.
		 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}

	/**
 	 * This method notifies all threads that called MonitorEx.Wait using the same monitor
 	 * and condition variable objects.
 	 *
 	 * This method is called with the monitor's lock held, and returns under the same
 	 * conditions.
 	 */
	public static void PulseAll(object monitor, object condition) {
		// if monitor and condition refers to the same object, we just call Monitor.PulseAll on monitor.
		if (monitor == condition) {
			Monitor.PulseAll(monitor);
			return;
		}
	
		/**
	 	 * If monitor and condition refer to different objects, in order to call Monitor.PulseAll on
	 	 * condition we need to acquire condition monitor's lock.
	 	 * We must acquire the condition monitor's lock filtering ThreadInterruptedException,
	 	 * because this method is not used for wait purposes, so it must not throw that exception.
	 	 */
	
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
	
		// notify the condition variable of the condition monitor and leave the corresponding monitor.
		Monitor.PulseAll(condition);
		Monitor.Exit(condition);
	
		/*
	 	 * if the current thread was interrupted when acquiring the condition monitor's lock,
	 	 * we re-assert the interruption, so the exception will be raised on the next call to
	 	 * a managed wait operation.
	 	 */
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
}

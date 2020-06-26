/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Singleton delay scheduler, used only to implement the timers used to
 * cancel synchronous operations due to timeout.
 *
 * Carlos Martins, June 2020
 * 
 */

import java.util.concurrent.*;

/**
 * This class supports one-shot timers
 */
public final class Delayer {
	
	/**
	 * Thread factory used to create the daemon worker thread that
	 * the timer's callbacks
	 */
    private static final class DaemonThreadFactory implements ThreadFactory {
        public Thread newThread(Runnable runnable) {
            Thread worker = new Thread(runnable);
            worker.setDaemon(true);
            worker.setName("AsyncDelayScheduler");
            return worker;
        }
    }
	
	// The scheduled thread pool executor
    private static final ScheduledThreadPoolExecutor delayer;
    
	// Static initializer
    static {
        (delayer = new ScheduledThreadPoolExecutor(1, new DaemonThreadFactory())).
                            setRemoveOnCancelPolicy(true);
    }

	/**
	 * Starts a timer sthat fires after the specified delay
	 */
    public static ScheduledFuture<?> delay(Runnable command, long delay, TimeUnit unit) {
        return delayer.schedule(command, delay, unit);
    }
}


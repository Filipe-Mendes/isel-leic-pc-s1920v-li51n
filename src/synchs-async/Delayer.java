/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Singleton delay scheduler, used only to implement timeouts for
 * asynchronous operations.
 *
 * Carlos Martins, June 2020
 * 
 */

import java.util.concurrent.*;

public final class Delayer {
    public static ScheduledFuture<?> delay(Runnable command, long delay, TimeUnit unit) {
        return delayer.schedule(command, delay, unit);
    }

    static final class DaemonThreadFactory implements ThreadFactory {
        public Thread newThread(Runnable runnable) {
            Thread worker = new Thread(runnable);
            worker.setDaemon(true);
            worker.setName("AsyncDelayScheduler");
            return worker;
        }
    }

    static final ScheduledThreadPoolExecutor delayer;
    
    static {
        (delayer = new ScheduledThreadPoolExecutor(1, new DaemonThreadFactory())).
                            setRemoveOnCancelPolicy(true);
    }
}

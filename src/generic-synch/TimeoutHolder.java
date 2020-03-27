
/***
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Auxiliary class used to processe timeout when using Lampson and Redell monitors.
 *  
 *  In order to standardize with the written code for .NET, we consider that a
 *  negative value of timeout corresponds to the wait without time limit, a value
 *  of zero corresponde to no wait, with the remaining positive values being an
 *  effective timeout in the specified units.
 * 
 *  Carlos Martins, October 2018
 *
 ***/

import java.util.concurrent.TimeUnit;

public class TimeoutHolder {
	private final long deadline;		// -1L when timeout is infinite
	
	public TimeoutHolder(long millis) {
		deadline = millis >= 0L ? System.currentTimeMillis() + millis: -1L;
	}
	
	public TimeoutHolder(long time, TimeUnit unit) {
		deadline = time >= 0L ? System.currentTimeMillis() + unit.toMillis(time) : -1L;
	}
	
	// returns true if timeout exists
	boolean isTimed() { return deadline >= 0L; }
	
	public long value() {
		if (deadline == -1L)
			return Long.MAX_VALUE;	// ensure that timeout does not expires!
		long remainder = deadline - System.currentTimeMillis();
		return remainder > 0L ? remainder : 0L;
	}	
}

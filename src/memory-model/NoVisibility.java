public class NoVisibility {
	private static int number;
	private static boolean ready;	// when true, validates number

	private static class ReaderThread extends Thread {
		public void run() {
			System.out.println("ready: " + ready);
			while (!ready)
				;
			System.out.println(number);		// 42?
			System.out.println("--other thread exiting...");
		}
	}	

    public static void main(String... args) throws InterruptedException {	
        new ReaderThread().start();	
		Thread.sleep(100);	// allow ReaderThread to start before set shared data
	    number = 42;
	    ready = true;
		System.out.println("--main exiting...");
	}
		
	/*

	//using locks
    public static void main(String... args) throws Exception {
		final Object lock = new Object();
		
        new Thread(() -> {
			do {
				synchronized(lock) {
        			if (ready) {
						System.out.println(number);
						break;
					}
				}
			} while (true);
        }).start();
		
		number = 42;
		Thread.sleep(500);
		synchronized(lock) {
        	ready = true;
		}
		System.out.println("main exiting...");
	}
		*/
}

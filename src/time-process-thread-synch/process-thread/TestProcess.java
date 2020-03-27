/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Test Process type in Java
 *
 * Carlos Martins, October 2016
 *
 ***/

/*
 * Launch a process execution notepad++ .\\TestProcess.java, and wait until it terminates.
 */

import java.io.IOException;

public class TestProcess {

	public static void main(String[] args) throws InterruptedException, IOException {
		ProcessBuilder pb = new ProcessBuilder("notepad++.exe", ".\\TestProcess.java");
		Process p = pb.start();
		// wait until process terminate, and display its exit code.
		p.waitFor();
		System.out.println("exit code: " + p.exitValue());
	}
}

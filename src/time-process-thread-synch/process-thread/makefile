##
# makefile to generate executables present on this folder
#
# To generate executables: nmake <enter>
# To clean genetated artifacts: nmake clean <enter>
##

all: TestProcess.exe TestProcess.class ProcessMonitor.exe Threads.exe Threads.class \
	UseInterrupt.exe UseInterrupt.class FullLoop.exe SuspendThread.exe
	
TestProcess.exe : TestProcess.cs
	csc /out:TestProcess.exe TestProcess.cs

TestProcess.class : TestProcess.java
	javac TestProcess.java
	
ProcessMonitor.exe : ProcessMonitor.cs
	csc /out:ProcessMonitor.exe ProcessMonitor.cs
	
Threads.exe : Threads.cs
	csc /out:Threads.exe Threads.cs
	
Threads.class : Threads.java
	javac Threads.java
	
UseInterrupt.exe : UseInterrupt.cs
	csc /out:UseInterrupt.exe UseInterrupt.cs
	
UseInterrupt.class : UseInterrupt.java
	javac UseInterrupt.java
	
FullLoop.exe : FullLoop.cs
	csc /out:FullLoop.exe FullLoop.cs
	
SuspendThread.exe : SuspendThread.c
	cl /Zi /FeSuspendThread.exe SuspendThread.c
	
clean:
	@del *.obj *.pdb *.ilk *.exe *.class 2> NUL
	

 
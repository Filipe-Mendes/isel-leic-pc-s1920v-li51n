# commands to build and clean programs in this directory using NMAKE

all: cancellation.exe continuations.exe everything-is-a-task.exe \
	 fork-join.exe ignoring-errors.exe progress-report.exe \
	 tasks.exe why-child-tasks.exe why-continuations.exe \
	 returning-data.exe
	
cancellation.exe: cancellation.cs
	csc /debug+ /out:cancellation.exe cancellation.cs

continuations.exe: continuations.cs
	csc /debug+ /out:continuations.exe continuations.cs
	
everything-is-a-task.exe: everything-is-a-task.cs
	csc /debug+ /out:everything-is-a-task.exe everything-is-a-task.cs
	
fork-join.exe: fork-join.cs
	csc /debug+ /out:fork-join.exe fork-join.cs

ignoring-errors.exe: ignoring-errors.cs
	csc /debug+ /out:ignoring-errors.exe ignoring-errors.cs

progress-report.exe: progress-report.cs
	csc /debug+ /out:progress-report.exe progress-report.cs

tasks.exe: tasks.cs
	csc /debug+ /out:tasks.exe tasks.cs

why-child-tasks.exe: why-child-tasks.cs
	csc /debug+ /out:why-child-tasks.exe why-child-tasks.cs

why-continuations.exe: why-continuations.cs
	csc /debug+ /out:why-continuations.exe why-continuations.cs

returning-data.exe: returning-data.cs
	csc /debug+ /out:returning-data.exe returning-data.cs -r:System.Numerics.dll

clean:
	@del *.exe *.pdb 2>NUL
	



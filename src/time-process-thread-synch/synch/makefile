##
# Build unmanaged and managed executables in this folder
# To generate executables: nmake <enter>
# To clean generated artifacts: nmake clean <enter>
##

all: _locks.exe _graph.exe Locks.exe Graph.exe Locks.class Graph.class

# unmanaged locks and graph
_locks.exe: locks.c
	cl /Zi /Fe_locks.exe locks.c

_graph.exe: graph.c
	cl /Zi /Fe_graph.exe graph.c

Locks.exe : Locks.cs
	csc /out:Locks.exe Locks.cs

Graph.exe : Graph.cs
	csc /out:Graph.exe Graph.cs

Locks.class : Locks.java
	javac Locks.java

Graph.class : Graph.java
	javac Graph.java
	
clean:
	@del *.obj *.exe *.pdb *.ilk *.class 2> NUL
	
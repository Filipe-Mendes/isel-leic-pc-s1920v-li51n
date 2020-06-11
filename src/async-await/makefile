# Commands to build and clean programs in this directory using NMAKE

all: async.exe fork-join.exe file-copy-async.exe
	
async.exe : async.cs
	csc /debug+ /out:async.exe async.cs

fork-join.exe : fork-join-async.cs
	csc /debug+ /out:fork-join-async.exe fork-join-async.cs

file-copy-async.exe : file-copy-async.cs
	csc /debug+ /out:file-copy-async.exe file-copy-async.cs

clean:
	@del *.exe *.pdb *.dat 2>NUL
	
	
	
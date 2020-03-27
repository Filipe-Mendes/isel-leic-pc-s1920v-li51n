##
# Makefile to generate executable on this folder
#
# To generate the executable: nmake <enter>
# To clean the generated artifacts: nmake clean <enter>
##

TimeMeasure.exe : TimeMeasure.c
	cl /FeTimeMeasure.exe /Zi TimeMeasure.c
	
clean:
	@del *.obj *.exe *.pdb *.ilk 2> NUL
	
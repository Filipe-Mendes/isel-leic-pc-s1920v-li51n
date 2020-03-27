/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Test thread suspension/resumption in Windows with deadlock
 *
 * To generate the executable, execute: cl SuspendThread.c
 *
 * Carlos Martins, March 2017
 *
 ***/

#include <windows.h>
#include <stdio.h>

#define NDEBUG
#include <assert.h>

// Comment this line to do not inject 
#define INJECT_DEADLOCK


VOID
PrintError(__in PCHAR Prefix, __in ULONG ErrorCode)
{
	LPVOID ErrorMsgBuf;
	
	if (FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER |
                      FORMAT_MESSAGE_FROM_SYSTEM |
                      FORMAT_MESSAGE_IGNORE_INSERTS,
                      NULL,
                      ErrorCode,
                      MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                      (LPSTR) &ErrorMsgBuf,
                      0,
                      NULL
                      )) {

        printf("*** %s, error: %s\n", Prefix, (LPSTR)ErrorMsgBuf);
		LocalFree(ErrorMsgBuf);
	}
}

/* Run control flag */

volatile int Running;

ULONG WINAPI TargetThread (PVOID ignored)
{
	do {
		putchar('.');
/***
 * comment the next line in order to quickly get a deadlock due to console's lock.
 ***/
		Sleep(10);
		//SwitchToThread();
	} while (Running);
	
	return 0;
}

void main() {
	HANDLE tt;
	char c;
	int i, sc = 0;
	
	Running = 1;
	
	tt = CreateThread(NULL, 0, TargetThread, NULL, 0, NULL);
	assert(tt != NULL);
	
	for (;;) {
		c = getchar();
		if (c == 's') {
			SuspendThread(tt);
			sc++;
/***
 *  Comment the next line to prevent the deadlock condition!
 ***/
					
			printf("\n+++thread suspended\n");
		} else if (c == 'r') {
			if (ResumeThread(tt) == (DWORD)-1) {
				PrintError("\nResuleThread() failed", GetLastError());
			}
			if (sc)
				sc--;
		} else if (c != '\n') {
			Running = 0;
			break;
		}
	}
	for (i = sc; i > 0; i--)
		ResumeThread(tt);
	
	/* wait until the target threads exits */
	WaitForSingleObject(tt, INFINITE);
}

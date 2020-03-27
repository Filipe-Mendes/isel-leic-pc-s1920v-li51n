/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Control synchronization in Windows
 *
 * To generate the executable, execute: cl graph.c
 *
 * Carlos Martins, March 2016
 *
 ***/

#include <windows.h>
#include <stdio.h>

#define _DEBUG

/*
The action graph is as follows:

- Starts with A;
- After A finishes execution, can be executed B, C, D, and E;
- After D and E finish execution, can be executed F;
- After B, C and F finish execution, can be executed G.

In this design:
- Thread T1 executes A, B and G;
- Thread T2 executes C;
- Thread T3 executes D and F;
- Thread T4 executes E.
*/	

//
// Include the next define in order to user THREAD_JOIN
//

#define USE_THREAD_JOIN

//
// Used synchronizers
//

HANDLE t1_2_t2t3t4;			// manual-reset event

#ifndef USE_THREAD_JOIN
HANDLE t4_2_t3;				// manual-reset event
HANDLE t2t3_2_t1;			// semaphore
#endif

//
// Thread handles
//

HANDLE ths[3];

//
// Sleep constants
//

#define TIMEOUT_MODULUS		2000
#define MIN_TIMEOUT			50

//
// Auxiliary function to simulate action execution.
//

void DoAction(PCHAR ActionName, ULONG Duration)
{
	printf("\n-->%s", ActionName);
	Sleep(Duration);
	printf("\n<--%s", ActionName);
}


//
// Thread T1
//

ULONG WINAPI T1(PVOID ignored)
{
	
	srand((ULONG)((ULONGLONG)&ignored >> 12));
	
	// Do action A
	DoAction("A", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);
	
	// Allow T2, T3 and T4 start
	
	SetEvent(t1_2_t2t3t4);
	
	// Do action B
	DoAction("B", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);

#ifdef USE_THREAD_JOIN

	// Wait until threads T2 and T3 exit. Join with T2 and T3.
	WaitForMultipleObjects(2, ths, TRUE, INFINITE);

#else	

	WaitForSingleObject(t2t3_2_t1, INFINITE);
	WaitForSingleObject(t2t3_2_t1, INFINITE);
#endif
	
	// Do action G
	DoAction("G", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);
	
	// Exit
	return 0;
}

//
// Thread T2
//

ULONG WINAPI T2(PVOID ignored)
{
	
	srand((ULONG)((ULONGLONG)&ignored >> 12));
	
	// wait until t1 completes A
	WaitForSingleObject(t1_2_t2t3t4, INFINITE);	
	
	// Do action C
	DoAction("C", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);

#ifndef USE_THREAD_JOIN
	
	ReleaseSemaphore(t2t3_2_t1, 1, NULL);
#endif
	
	return 0;
}

//
// Thread T3
//

ULONG WINAPI T3(PVOID ignored)
{	

	srand((ULONG)((ULONGLONG)&ignored >> 12));
	
	// wait until t1 completes A
	WaitForSingleObject(t1_2_t2t3t4, INFINITE);	
	
	// Do Action D
	DoAction("D", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);

	// Wait until t4 completes action E
	
#ifdef USE_THREAD_JOIN

	// Wait until T4 exits. Join with T4.
	WaitForSingleObject(ths[2], INFINITE);	
#else
	// Wait until the t4_2_t3 event is signalled
	WaitForSingleObject(t4_2_t3, INFINITE);	
#endif
	
	// Do Action F
	DoAction("F", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);

#ifndef USE_THREAD_JOIN
	
	ReleaseSemaphore(t2t3_2_t1, 1, NULL);
#endif
	
	return 0;
}

//
// Thread T4
//

ULONG WINAPI T4(PVOID ignored)
{
	
	srand((ULONG)((ULONGLONG)&ignored >> 12));
	
	// Wait until t1 completes action A
	WaitForSingleObject(t1_2_t2t3t4, INFINITE);	
	
	// Do action E
	DoAction("E", (rand() % TIMEOUT_MODULUS) + MIN_TIMEOUT);

#ifndef USE_THREAD_JOIN
	
	SetEvent(t4_2_t3);
#endif

	return 0;
}

void main(void)
{
	t1_2_t2t3t4 = CreateEvent(NULL, TRUE, FALSE, NULL);

#ifndef USE_THREAD_JOIN
	
	t4_2_t3 = CreateEvent(NULL, TRUE, FALSE, NULL);
	t2t3_2_t1 = CreateSemaphore(NULL, 0, 2, NULL);
#endif

	ths[0] = CreateThread(NULL, 0, T2, NULL, CREATE_SUSPENDED, NULL);
	ths[1] = CreateThread(NULL, 0, T3, NULL, CREATE_SUSPENDED, NULL);
	ths[2] = CreateThread(NULL, 0, T4, NULL, CREATE_SUSPENDED, NULL);

	ResumeThread(ths[0]);
	ResumeThread(ths[1]);
	ResumeThread(ths[2]);
		
	T1(NULL);

#ifndef USE_THREAD_JOIN

	//
	// Wait until all other threads exit. (Join)
	// 
	
	WaitForMultipleObjects(3, ths, TRUE, INFINITE);

#endif
}

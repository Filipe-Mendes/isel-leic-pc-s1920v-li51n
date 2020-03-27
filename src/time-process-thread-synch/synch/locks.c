/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Mutable shared data access synchronization Windows
 *
 * To generate the executable, execute: cl locks.c
 *
 * Carlos Martins, March 2017
 *
 ***/

#include <windows.h>
#include <stdio.h>
#include <intrin.h>

//
// Comment the next line in order to create only one thread
//

#define TWO_INC_THREADS

//
// Returns true if the program is running on a multiprocessor machine.
//

int IsMultiProcessor(void)
{
	static int _isMP = -1;
	
	if (_isMP < 0) {
		SYSTEM_INFO SysInfo;
		GetSystemInfo(&SysInfo);
		_isMP = SysInfo.dwNumberOfProcessors > 1;
	}
	return _isMP;
}

//
// Yields the processor taking into account the number of processors.
//

void _Yield(void)
{
	if (IsMultiProcessor())
		YieldProcessor();
	else
		SwitchToThread();	// Thread yield
}

//
// Spinlock implementation.
//

#define FREE	1
#define BUSY	0

typedef volatile LONG SPIN_LOCK, *PSPIN_LOCK;

//
// Initializes the spinlock.
//

void InitializeSpinLock(__out PSPIN_LOCK Lock)
{
	*Lock = FREE;
}

//
// Naive version of the acquire spinlock function.
//

void _AcquireSpinLock(__inout PSPIN_LOCK Lock)
{
	while (InterlockedExchange(Lock, BUSY) == BUSY)
		YieldProcessor();	// asm pause;
}

//
// Acquires the spinlock - non-naive version.
//

void AcquireSpinLock(__inout PSPIN_LOCK Lock)
{
	do {
		if (*Lock == FREE && InterlockedExchange(Lock, BUSY) == FREE)
			return;
		
		//
		// Spin until the spinlock seems to be free.
		//
		
		while (*Lock == BUSY)
			_Yield();
	} while (TRUE);
}

//
// Releases the spinlock.
//

VOID ReleaseSpinLock(__inout PSPIN_LOCK Lock )
{

//	InterlockedExchange(Lock, FREE);
// or

	_WriteBarrier();
	*Lock = FREE;
}

/*
 * Implementation of the Windows CRITICAL SECTION.
 */
 
typedef struct __CRITICAL_SECTION  {
	volatile int LockCount;
	int OwnerThreadId;
	int RecursionCount;
	int SpinCount;
	HANDLE LockEvent;
} _CRITICAL_SECTION, *_PCRITICAL_SECTION;

#define CS_FREE		-1

//
// Get the critical section's event. If there is o event, create one.
//
		
static HANDLE _GetCSLockEvent_ (
	__inout _PCRITICAL_SECTION CriticalSection
)	
{
	if (CriticalSection->LockEvent == NULL) {
		
		HANDLE Event = CreateEvent(NULL, FALSE, FALSE, NULL);
		
		if (InterlockedCompareExchangePointer(&CriticalSection->LockEvent, Event, NULL) != NULL) {

			//
			// Someone else already set the wait event reference.
			//
					
			CloseHandle(Event);
		}
	}	
	return CriticalSection->LockEvent;
}

BOOL WINAPI _InitializeCriticalSectionAndSpinCount (
	__out _PCRITICAL_SECTION CriticalSection,
  __in ULONG SpinCount
)
{
	if (!IsMultiProcessor())
		SpinCount = 0;
	CriticalSection->SpinCount = SpinCount;
	CriticalSection->OwnerThreadId = 0;
	CriticalSection->LockEvent = NULL;
	CriticalSection->LockCount = CS_FREE;
	return TRUE;
}

BOOL WINAPI _InitializeCriticalSection (
	__out _PCRITICAL_SECTION CriticalSection
)
{
	return _InitializeCriticalSectionAndSpinCount(CriticalSection, 0);
}

static VOID WINAPI _EnterCriticalSectionWithoutSpin (
	__out _PCRITICAL_SECTION CriticalSection
)
{	
	//
	// Attempt to acquire the critical section
	//
			
	if (InterlockedIncrement(&CriticalSection->LockCount) == 0) {
		
		//
		// Set critical section owner, and initialize the recursion counter.
		//
		
		CriticalSection->OwnerThreadId = GetCurrentThreadId();
		CriticalSection->RecursionCount = 1;
			
	} else if (CriticalSection->OwnerThreadId == GetCurrentThreadId()) {

		//
		// The critical section is already owned, but it is owned by the current thread.
		//
			
		CriticalSection->RecursionCount++;
	} else {
			
		//
		// The critical section is owned by another thread, so the current thread must wait
		// for ownership.
		//
		
		WaitForSingleObject(_GetCSLockEvent_(CriticalSection), INFINITE);
		CriticalSection->OwnerThreadId = GetCurrentThreadId();
		CriticalSection->RecursionCount = 1;
	}
}

VOID WINAPI _EnterCriticalSection (
	__inout _PCRITICAL_SECTION CriticalSection
)
{	
	ULONG SpinCount;
	
	if (CriticalSection->SpinCount == 0)
		_EnterCriticalSectionWithoutSpin(CriticalSection);
	else {
		
		//
		// A non-zero spin count was specified
		//
				
		if (CriticalSection->OwnerThreadId == GetCurrentThreadId()) {
				
			//
			// The critical section is owned by the current thread.
			// Increment the lock count and the recursion count.
			//
				
			InterlockedIncrement(&CriticalSection->LockCount);
			CriticalSection->RecursionCount++;
			return;
		}
			
		//
		// A non-zero spin count was specified, and the current thread is not the owner.
		// So, spin for a while.
		//
			
		for (SpinCount = CriticalSection->SpinCount; SpinCount > 0 ; ) {
			
			if (InterlockedCompareExchange(&CriticalSection->LockCount, 0, CS_FREE) == CS_FREE) {

				//
				// The critical section has been acquired.
				// Set the owning thread and the initialize the recursion count.
				//
				
				CriticalSection->OwnerThreadId = GetCurrentThreadId();
				CriticalSection->RecursionCount = 1;
				return;
			}
			
			//
			// The critical section is currently owned. Spin until it is either unowned
			// or the spin count has reached zero. 
			// If waiters are present, don't spin on the lock since we will never see it go free.
			//
			
			if (CriticalSection->LockCount >= 1)			
				break;
			
			while (SpinCount > 0 && CriticalSection->LockCount != CS_FREE) {					
				YieldProcessor();
				SpinCount--;
			}	
		}
			
		//
		// The spin interval expired.
		// Acquire the critical section unconditionally.
		//
				
		_EnterCriticalSectionWithoutSpin(CriticalSection);
	}
}
BOOL WINAPI _TryEnterCriticalSection (
	__inout _PCRITICAL_SECTION CriticalSection
)
{	
	
	if (InterlockedCompareExchange(&CriticalSection->LockCount, 0, CS_FREE) == CS_FREE) {
		
		//
		// We got the control of the critical section.
		// So, set the owner and initialize recursion count.
		//
						
		CriticalSection->OwnerThreadId = GetCurrentThreadId();
		CriticalSection->RecursionCount = 1;
		return TRUE;
	}
		
	//
	// The critical section is already owned.
	// If it is owned by another thread, return FALSE immediately.
	// If it is owned by this thread, we must increment the lock count here.
	//

	if (CriticalSection->OwnerThreadId == GetCurrentThreadId()) {
		
		InterlockedIncrement(&CriticalSection->LockCount);
		CriticalSection->RecursionCount++;
		return TRUE;
	}
		
	return FALSE;
}

VOID WINAPI _LeaveCriticalSection (
	__inout _PCRITICAL_SECTION CriticalSection
)
{	
	
	if (CriticalSection->OwnerThreadId != GetCurrentThreadId()) {
		
		//
		// The current thread is not the current owner of the critical section.
		// We do not have exceptions, so return!
		//
	
		return;
	}
		
	if (--CriticalSection->RecursionCount > 0) {
		
		//
		// Recursive leave
		//
			
		InterlockedDecrement(&CriticalSection->LockCount);
		return;
	}
		
	//
	// Clear the owner thread
	//
		
	CriticalSection->OwnerThreadId = 0;
		
	if (InterlockedDecrement(&CriticalSection->LockCount) >= 0) {
		
		//
		// At least one thread is waiting, wake one of the waiting threads
		//
		
		SetEvent(_GetCSLockEvent_(CriticalSection));
	}
}

VOID WINAPI _DeleteCriticalSection(
  __inout  _PCRITICAL_SECTION CriticalSection
)
{
	HANDLE Event = CriticalSection->LockEvent;
	if (Event != NULL &&
		InterlockedCompareExchangePointer(&CriticalSection->LockEvent, NULL, Event) == Event)
		CloseHandle(Event);
}

//
// The several flavours of locks.
//

SPIN_LOCK SpinLock;
CRITICAL_SECTION CSLock;
_CRITICAL_SECTION _CSLock;
SRWLOCK SRWLock;
HANDLE MutexLock;
HANDLE SemaphoreLock;
HANDLE AutoResetEventLock;

//
// The shared counter.
//

ULONG SharedCounter;

//
// Increment the shared counter using several ways to synchronize.
// 

FORCEINLINE
void UnprotectedIncrementSharedCounter(void)
{
	SharedCounter++;
}

FORCEINLINE
void NaiveSpinLockProtectedIncrementSharedCounter(void)
{

	AcquireSpinLock(&SpinLock);
	UnprotectedIncrementSharedCounter();
	ReleaseSpinLock(&SpinLock);
}

FORCEINLINE
void SpinLockProtectedIncrementSharedCounter(void)
{

	AcquireSpinLock(&SpinLock);
	UnprotectedIncrementSharedCounter();
	ReleaseSpinLock(&SpinLock);
}

FORCEINLINE
void AtomicIncrementSharedCounter(void)
{
	InterlockedIncrement(&SharedCounter);
}

FORCEINLINE
void CSProtecetedIncrementSharedCounter(void)
{

	EnterCriticalSection(&CSLock);
	UnprotectedIncrementSharedCounter();
	LeaveCriticalSection(&CSLock);
}

FORCEINLINE
void _CSProtecetedIncrementSharedCounter(void)
{

	_EnterCriticalSection(&_CSLock);
	UnprotectedIncrementSharedCounter();
	_LeaveCriticalSection(&_CSLock);
}

FORCEINLINE
void SRWLockProtecetedIncrementSharedCounter(void)
{

	AcquireSRWLockExclusive(&SRWLock);
	UnprotectedIncrementSharedCounter();
	ReleaseSRWLockExclusive(&SRWLock);
}

FORCEINLINE
void MutexProtecetedIncrementSharedCounter(void)
{

	WaitForSingleObject(MutexLock, INFINITE);
	UnprotectedIncrementSharedCounter();
	ReleaseMutex(MutexLock);
}

FORCEINLINE
void SemaphoreProtecetedIncrementSharedCounter(void)
{

	WaitForSingleObject(SemaphoreLock, INFINITE);
	UnprotectedIncrementSharedCounter();
	ReleaseSemaphore(SemaphoreLock, 1, NULL);
}

FORCEINLINE
void AutoResetEventProtecetedIncrementSharedCounter(void)
{

	WaitForSingleObject(AutoResetEventLock, INFINITE);
	UnprotectedIncrementSharedCounter();
	SetEvent(AutoResetEventLock);
}

//
// The private counters are cache line aligned to prevent
// cache line invalidation ping-pong between the two threads.
//


__declspec(align(64))  ULONG Counter0;
__declspec(align(64))  ULONG Counter1;

/*
ULONG Counter0;
ULONG Counter1;
*/
//
// Running test flag.
//

volatile BOOL Running = TRUE;

//
// The increment shared counter thread.
//

ULONG WINAPI IncrementCounterThread(PVOID Argument)
{
	PULONG PrivateCounter = (PULONG)Argument;
	
	/*
	if (PrivateCounter == &Counter0) {
		SetThreadAffinityMask(GetCurrentThread(), 1 << 0);
		printf("&PrivateCounter0: %016x\n", PrivateCounter);
	} else {
		SetThreadAffinityMask(GetCurrentThread(), 1 << 1);
		printf("&PrivateCounter1: %016x\n", PrivateCounter);
	}
	*/
	do {
	
		//UnprotectedIncrementSharedCounter();
		AtomicIncrementSharedCounter();
		//NaiveSpinLockProtectedIncrementSharedCounter();
		//SpinLockProtectedIncrementSharedCounter();
		//CSProtecetedIncrementSharedCounter();
		//_CSProtecetedIncrementSharedCounter();
		//SRWLockProtecetedIncrementSharedCounter();
		//MutexProtecetedIncrementSharedCounter();
		//SemaphoreProtecetedIncrementSharedCounter();
		//AutoResetEventProtecetedIncrementSharedCounter();

		//
		// Increment the private counter.
		//

		*PrivateCounter += 1;		
	} while(Running);
	return 0;
}

//
// The primary thread.
//

void _main(void) {
	HANDLE ThreadHandles[2];
	ULONG Start, Elapsed;
	
	//
	// Sets the primary thread priority to highest to ensure that,
	// when ready, it preempts one of the other threads.
	//
	
	SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);

	//
	// Create and/or initialize the several flavours of locks.
	//
	
	InitializeSpinLock(&SpinLock);	
	InitializeCriticalSection(&CSLock);
	_InitializeCriticalSection(&_CSLock);
	InitializeSRWLock(&SRWLock);
	MutexLock = CreateMutex(NULL, FALSE, NULL);
	SemaphoreLock = CreateSemaphore(NULL, 1, 1, NULL);
	AutoResetEventLock = CreateEvent(NULL, FALSE, TRUE, NULL);

	//
	// Create the increment threads and sleep for a while.
	//
	
	ThreadHandles[0] = CreateThread(NULL, 0, IncrementCounterThread, &Counter0, 0, NULL);

#ifdef TWO_INC_THREADS
	
	ThreadHandles[1] = CreateThread(NULL, 0, IncrementCounterThread, &Counter1, 0, NULL);
#endif
	
	//
	// Run for 5 seconds.
	//

#ifdef TWO_INC_THREADS	
	printf("--run increment threads for 3 seconds\n");
#else
	printf("--run increment thread for 3 seconds\n");
#endif

	Start = GetTickCount();
	Sleep(3000);
	
	//
	// Clear the running thread and synchronize with the termination
	// of the increment threads.
	//
	
	Running = FALSE;
	Elapsed = GetTickCount() - Start;
	
	// Join with increment thread(s)
#ifdef TWO_INC_THREADS
	
	WaitForMultipleObjects(2, ThreadHandles, TRUE, INFINITE);
#else

	WaitForSingleObject(ThreadHandles[0], INFINITE);
#endif

	DeleteCriticalSection(&CSLock);
	_DeleteCriticalSection(&_CSLock);

	//
	// Show results.
	//
	
	printf("--shared counter: %d K, private counters: %d K, diff: %d\n--increment cost: %d ns\n",
		   SharedCounter >> 10,
		   (Counter0 + Counter1) >> 10,
		   (Counter0 + Counter1) - SharedCounter,
		   (ULONG)((Elapsed * 1000000.0) / SharedCounter));
}

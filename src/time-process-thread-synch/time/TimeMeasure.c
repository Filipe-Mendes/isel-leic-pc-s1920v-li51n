/***
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Time measures on Windows
 *
 * To generate the executable execute the command: cl TimeMeasure.c
 *
 * Carlos Martins, October 2016
 *
 ***/

#include <windows.h>
#include <stdio.h>

//
// Compute the CPU frequency.
//

#define SAMPLE_TIME_SEG	5

void ComputeCpuFrequency(void)
{
	LONGLONG TscCounts, StartTscCount;
	ULONG EndTime;

	printf("--compute the cpu frequency\n");
	
	StartTscCount = __rdtsc();

	Sleep(SAMPLE_TIME_SEG * 1000);
	
	TscCounts = __rdtsc() - StartTscCount;
	
	printf("\t-> cpu frequency: %lld Mhz\n", TscCounts / (SAMPLE_TIME_SEG * 1000000));
}

//
// We consider that the system clock period is smaller than 50 ms.
//

#define MAX_PERIOD	50
#define SAMPLES		100

void ComputeSystemClockPeriod(void)
{
	ULONG Diffs[MAX_PERIOD];
	ULONG Samples;
	ULONG Index, Period, Sum;
		
	printf("--compute the system clock period\n");
	ZeroMemory(Diffs, sizeof(Diffs));
	Samples = 0;	
	do {
		ULONG Now = GetTickCount();
		ULONG Increment = GetTickCount() - Now;
		if (Increment > 0) {
			Diffs[(Increment < MAX_PERIOD) ? Increment : MAX_PERIOD - 1]++;
			Samples++;
		}
	} while (Samples < SAMPLES);
	Period = 1;
	Sum = Diffs[1];
	for (Index = 2; Index < MAX_PERIOD - 1; Index++) {
		if (Diffs[Index] > Diffs[Period]) {
			Period = Index;
		}
		Sum += Index * Diffs[Index];
		//printf("Diffs[%d] = %d\n", Index, Diffs[Index]);
	}
	printf("\t-> most frequent system clock period observed: %d ms\n", Period);
	printf("\t-> mean system clock period: %.1f ms, frequency: %.1f Hz\n", (double)Sum / SAMPLES,
		SAMPLES * 1000.0 / Sum);
}

//
// Compute the elapsed time using the several available means.
//

void ComputeElapsedTime(void)
{
	ULONG StartTicks, ElapsedTicks;
	LARGE_INTEGER StartPerfCounter, EndPerfCounter, PerfFrequency;
	
	printf("--compute elapsed time using the system clock timer and the performance counter\n");
	
	StartTicks = GetTickCount();					// milliseconds since system boot.
	QueryPerformanceCounter(&StartPerfCounter);		// periods of the performance counter frequency.
	
	//
	// Sleep 5 seconds.
	//

	Sleep(5000);
	//Sleep(5);
	
	//
	// Compute elapsed time.
	//
	
	QueryPerformanceCounter(&EndPerfCounter);
	ElapsedTicks = GetTickCount() - StartTicks;
	QueryPerformanceFrequency(&PerfFrequency);
	printf("\t-> 5 seconds measured with the performance counter: %lld ms\n",
			((EndPerfCounter.QuadPart - StartPerfCounter.QuadPart) * 1000)/PerfFrequency.QuadPart);
	printf("\t-> 5 seconds measured with the system clock: %d ms\n", ElapsedTicks);
}

//
// Compute the cost of a system call that accesses to an synchronizer's state.
//

#define REPEAT_COUNT	10000000

void ComputeSimpleSyscallCost(void) {
	LARGE_INTEGER Start, End, Frequency;
	LONGLONG TscStart, ClockCycles;
	HANDLE EventHandle;
	ULONG Index;
	LONGLONG ElapsedNanos;
	
	printf("--compute the cost for a simple system service call\n");
	
	//
	// Create a manual-reset event.
	//

	EventHandle = CreateEvent(NULL, TRUE, FALSE, NULL);
	
	// use performance counter as clock
	QueryPerformanceCounter(&Start);
	for (ClockCycles = 0, Index = 0; Index < REPEAT_COUNT; Index++) {
		TscStart = __rdtsc();
		ResetEvent(EventHandle);
		ClockCycles += __rdtsc() - TscStart;
	}
	QueryPerformanceCounter(&End);
	QueryPerformanceFrequency(&Frequency);
	ElapsedNanos = ((End.QuadPart - Start.QuadPart) * 1000000000LL) / Frequency.QuadPart;
	CloseHandle(EventHandle);
	
	printf("\t-> simple system service call cost: %lld ns\n", ElapsedNanos / REPEAT_COUNT);
	printf("\t-> performance frequency: %lld Hz\n", Frequency.QuadPart);
	printf("-> average number of machine instructions executed by simple system call: %lld\n", ClockCycles / REPEAT_COUNT);
}

//
// The primary thread.
//

void main(void) {

	//
	// Sets the primary thread priority to highest to minimize preemption effects.
	//
	
	SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);

	ComputeCpuFrequency();
	ComputeSystemClockPeriod();
	ComputeSimpleSyscallCost();
	ComputeElapsedTime();
}

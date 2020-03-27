#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* PrintError: prints the Windows errror message associated to the specified error code */
void PrintError(__in PCHAR Prefix, __in ULONG ErrorCode)
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

        fprintf(stderr, "*** %s, error: %s\n", Prefix, (LPSTR)ErrorMsgBuf);
		LocalFree(ErrorMsgBuf);
	}
}

/**
Process priority classes:

REALTIME_PRIORITY_CLASS
HIGH_PRIORITY_CLASS
ABOVE_NORMAL_PRIORITY_CLASS
NORMAL_PRIORITY_CLASS
BELOW_NORMAL_PRIORITY_CLASS
IDLE_PRIORITY_CLASS

Thread priorities:

THREAD_PRIORITY_TIME_CRITICAL
THREAD_PRIORITY_HIGHEST
THREAD_PRIORITY_ABOVE_NORMAL
THREAD_PRIORITY_NORMAL
THREAD_PRIORITY_BELOW_NORMAL
THREAD_PRIORITY_LOWEST
THREAD_PRIORITY_IDLE
**/

#define ELEMS(array)		(sizeof(array) / sizeof (*(array)))

typedef struct prioEntry {
	const char *keyword;
	int value;
} PrioEntry;

static PrioEntry threadPriorities[] = {
	{ "AboveNormal", THREAD_PRIORITY_ABOVE_NORMAL },
	{ "BelowNormal", THREAD_PRIORITY_BELOW_NORMAL },
	{ "Highest", THREAD_PRIORITY_HIGHEST },
	{ "Idle", THREAD_PRIORITY_IDLE },
	{ "Lowest", THREAD_PRIORITY_LOWEST },
	{ "Normal", THREAD_PRIORITY_NORMAL },
	{ "TimeCritical", THREAD_PRIORITY_TIME_CRITICAL }, 
};

static PrioEntry priorityClasses[] = {
	{ "AboveNormal", ABOVE_NORMAL_PRIORITY_CLASS },
	{ "BelowNormal", BELOW_NORMAL_PRIORITY_CLASS },
	{ "High", HIGH_PRIORITY_CLASS },
	{ "Idle", IDLE_PRIORITY_CLASS},
	{ "Normal", NORMAL_PRIORITY_CLASS },
	{ "Realtime", REALTIME_PRIORITY_CLASS }, 
};

static int compareKeys(const void *first, const void *second)
{
	return strcmp(((PrioEntry *)first)->keyword, ((PrioEntry *)second)->keyword);
}
	
int main(int argc, char *argv[])
{
	int i, priority = THREAD_PRIORITY_NORMAL, priorityClass = NORMAL_PRIORITY_CLASS;
	DWORD_PTR affinityMask = ~0;
	PrioEntry key, *presult;
	char *eventName = NULL;
	HANDLE hevent = NULL;
	
	if (argc > 1) {
		for (i = 1; i < argc; i++) {
			if (strcmp(argv[i], "--prio") == 0 && argc > i) {
				key.keyword = argv[++i];
				presult = bsearch(&key, threadPriorities, ELEMS(threadPriorities), sizeof(PrioEntry), compareKeys);
				if (presult == NULL) {
					fprintf(stderr, "***\"%s\": invalid thread priority\n", argv[i]);
					return 1;									
				} else
					priority = presult->value;
			} else if (strcmp(argv[i], "--class") == 0 && argc > i) {
				key.keyword = argv[++i];
				presult = bsearch(&key, priorityClasses, ELEMS(priorityClasses), sizeof(PrioEntry), compareKeys);
				if (presult == NULL) {
					fprintf(stderr, "***\"%s\": invalid priority class\n", argv[i]);
					return 1;									
				} else
					priorityClass = presult->value;	
			} else if (strcmp(argv[i], "--affinity") == 0 && argc > i) {
				i++;
				if (sscanf(argv[i], "%zx", &affinityMask) == 0) {
					fprintf(stderr, "***\"%s\": invalid affinity mask\n", argv[i]);
					return 1;
				}
			} else if (strcmp(argv[i], "--event") == 0 && argc > i) {
				eventName = argv[++i];
			} else {
				fprintf(stderr, "***\"%s\": invalid switch\n", argv[i]);
				return 1;
			}
		}
	}
	printf("event: %s, thread prio: %d, prio class: %d, affinity: 0x%08zx\n", eventName, priority, priorityClass, affinityMask);
	if (eventName != NULL) {
		if ((hevent = OpenEvent(SYNCHRONIZE, FALSE, eventName)) == NULL) {
			fprintf(stderr, "***error: can not open event \"%s\"\n", eventName);
			return 1;
		}
	}
	
	/**
	 * Set the definied parameters
	 */
	
	if (!SetPriorityClass(GetCurrentProcess(), priorityClass)) {
		PrintError("SetPriorityClass(), failed", GetLastError());
		return 1;
	}
	
	/* executes a loop */
	if (!SetProcessAffinityMask(GetCurrentProcess(), affinityMask)) {
		PrintError("SetProcessAffinityMask failed", GetLastError());
		return 1;
	}
	
	return 0;
}
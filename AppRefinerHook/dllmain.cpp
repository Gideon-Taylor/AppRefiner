#include "Common.h"
#include "ScintillaUtils.h"
#include "AutoPairing.h"
#include "AutoIndent.h"
#include "HookManager.h"

// DLL entry point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
        // Store the module handle for later use
        g_hModule = hModule;
        // Optimize DLL loading by disabling thread attach/detach notifications
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_THREAD_ATTACH:
        // This should never be called due to DisableThreadLibraryCalls
        break;
    case DLL_THREAD_DETACH:
        // This should never be called due to DisableThreadLibraryCalls
        break;
    case DLL_PROCESS_DETACH:
        // Clean up resources
        break;
    }
    return TRUE;
}

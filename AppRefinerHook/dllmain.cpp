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

        // Keep a reference to the DLL to prevent unloading
        if (g_dllSelfReference == NULL && g_hModule != NULL) {
            char dllPath[MAX_PATH];
            if (GetModuleFileNameA(g_hModule, dllPath, MAX_PATH) > 0) {
                g_dllSelfReference = LoadLibraryA(dllPath);
                if (g_dllSelfReference != NULL) {
                    OutputDebugStringA("Created DLL self-reference to prevent unloading");
                }
            }
        }

        break;
    case DLL_THREAD_ATTACH:
        // This should never be called due to DisableThreadLibraryCalls
        break;
    case DLL_THREAD_DETACH:
        // This should never be called due to DisableThreadLibraryCalls
        break;
    case DLL_PROCESS_DETACH:
        // Clean up resources
        if (g_getMsgHook != NULL) {
            UnhookWindowsHookEx(g_getMsgHook);
            g_getMsgHook = NULL;
        }
        
        // Release the self-reference if one exists
        if (g_dllSelfReference != NULL) {
            // We should be careful not to call FreeLibrary here as we're
            // already in the process of unloading, but we should ensure
            // the reference is cleared
            g_dllSelfReference = NULL;
        }
        break;
    }
    return TRUE;
}

#pragma once

#include "SqlProxyProtocol.h"
#include <windows.h>

namespace ArSqlProxy {

// Called on UI thread from GetMsgHook / MainWindowSubclassProc.
// attach points at AttachInfo in this process (written by AppRefiner).
// Returns TRUE on success.
BOOL HandleAttach(const AttachInfo* attach, uint32_t cb);

// Called on UI thread: drain private work queue and run SAM. Message params ignored.
void HandlePump();

// Main window used to deliver WM_AR_SQLPROXY_PUMP via SendMessage so the pump runs
// even when the UI thread is nested inside SendMessage (PostThreadMessage is not
// dispatched in that state — classic deadlock with AppRefiner catalog SQL).
void SetUiPumpWindow(HWND hwnd);

// Stop pipe thread, fail pending work, close handles. Safe to call multiple times.
void Shutdown();

// True if a proxy session is currently attached.
bool IsAttached();

} // namespace ArSqlProxy

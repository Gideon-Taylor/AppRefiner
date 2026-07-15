#pragma once

#include "SqlProxyProtocol.h"
#include <windows.h>

namespace ArSqlProxy {

// Called on UI thread from GetMsgHook / MainWindowSubclassProc.
// attach points at AttachInfo in this process (written by AppRefiner).
// Starts the pipe IO thread (pipe only — SAM still runs on the UI thread via HandlePump).
// Returns TRUE on success.
BOOL HandleAttach(const AttachInfo* attach, uint32_t cb);

// Called on UI thread from GetMsgHook(WM_AR_SQLPROXY_PUMP): drain private work queue
// and run SAM. Message params ignored (never carry SQL).
void HandlePump();

// Stop pipe thread, fail pending work, close handles. Safe to call multiple times.
void Shutdown();

// True if a proxy session is currently attached.
bool IsAttached();

} // namespace ArSqlProxy

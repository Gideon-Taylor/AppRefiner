using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace AppRefiner
{
    public class AppRefinerSubclasser
    {
        // P/Invoke declarations for Win32 API functions
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out IntPtr lpExitCode);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        // Shared memory functions
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        
        // Message handling
        private const uint WM_USER = 0x0400;
        private const uint WM_SCINTILLA_NOTIFICATION = WM_USER + 1000;
        private const uint WM_SUBCLASS_WINDOW = WM_USER + 1100;
        
        // Process access flags
        private const int PROCESS_CREATE_THREAD = 0x0002;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_OPERATION = 0x0008;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_ALL_ACCESS = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;
        
        // Memory allocation flags
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        
        // File mapping flags
        private const uint FILE_MAP_ALL_ACCESS = 0xF001F;
        private const uint PAGE_READWRITE_SECTION = 0x04;
        
        // Wait flags
        private const uint INFINITE = 0xFFFFFFFF;

        // Shared memory constants
        private const string SHARED_MEM_NAME = "AppRefinerSubclassInfo";
        private const uint SHARED_MEM_SIGNATURE = 0x41525349; // 'ARSI'

        // Shared memory structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SHARED_MODULE_INFO
        {
            public uint Signature;             // Signature to identify our shared memory
            public uint Size;                  // Size of this structure
            public IntPtr ModuleBase;          // Base address of the DLL
            public IntPtr InitSubclassProc;    // Address of InitSubclass function
            public IntPtr RemoveSubclassProc;  // Address of RemoveSubclass function
            public IntPtr SetCallbackWindowProc; // Address of SetCallbackWindow function
            public IntPtr SetHookProc;         // Address of SetHook function
            public IntPtr UnhookProc;          // Address of Unhook function
        }
        
        // Path to the DLL
        private string _dllPath;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dllPath">Optional path to the DLL, otherwise uses default location</param>
        public AppRefinerSubclasser(string dllPath = null)
        {
            // If dllPath is null, assume the DLL is in the same directory as the executable
            _dllPath = dllPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppRefinerSubclass.dll");
            
            if (!File.Exists(_dllPath))
            {
                throw new FileNotFoundException("AppRefinerSubclass.dll not found", _dllPath);
            }
        }
        
        /// <summary>
        /// Gets the subclass address information for a process without performing the subclassing
        /// </summary>
        /// <param name="processId">ID of the process</param>
        /// <returns>SubclassAddressInfo object if successful, null otherwise</returns>
        public SubclassAddressInfo GetSubclassAddressInfo(uint processId)
        {
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Debug.Log($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return null;
            }
            
            try
            {
                // 1. Load the DLL into the target process
                IntPtr remoteDllHandle = LoadRemoteDll(hProcess, _dllPath);
                
                if (remoteDllHandle == IntPtr.Zero)
                {
                    Debug.Log("Failed to load DLL into target process");
                    return null;
                }

                // 2. Access shared memory to get function addresses
                SHARED_MODULE_INFO sharedInfo;
                IntPtr releaseSharedMemFuncAddr = IntPtr.Zero;
                if (!GetSharedModuleInfo(out sharedInfo, hProcess, remoteDllHandle, out releaseSharedMemFuncAddr))
                {
                    Debug.Log("Failed to access shared memory information");
                    return null;
                }
                
                // Release the shared memory when done
                ReleaseRemoteSharedMemory(hProcess, releaseSharedMemFuncAddr);

                // Create and return the address info
                var addressInfo = new SubclassAddressInfo
                {
                    ProcessId = processId,
                    ModuleBase = sharedInfo.ModuleBase,
                    InitSubclassProc = sharedInfo.InitSubclassProc,
                    RemoveSubclassProc = sharedInfo.RemoveSubclassProc,
                    SetCallbackWindowProc = sharedInfo.SetCallbackWindowProc,
                    SetHookProc = sharedInfo.SetHookProc,
                    UnhookProc = sharedInfo.UnhookProc
                };
                
                return addressInfo;
            }
            finally
            {
                // Always close the process handle
                CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Sets a hook in the target thread to perform subclassing on the main thread.
        /// The hook will automatically remove itself after successfully processing the subclassing.
        /// </summary>
        /// <param name="processId">ID of the process</param>
        /// <param name="threadId">ID of the thread to hook</param>
        /// <param name="addressInfo">Pre-obtained subclass address info</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetHookForSubclassing(uint processId, uint threadId, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded || addressInfo.SetHookProc == IntPtr.Zero)
            {
                Debug.Log("Invalid subclass address info or SetHookProc is null");
                return false;
            }
            
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Debug.Log($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Create a remote thread that calls SetHook with the thread ID directly as parameter
                // No need to allocate memory since the parameter is a DWORD value, not a pointer
                IntPtr newThreadId;
                IntPtr hThread = CreateRemoteThread(
                    hProcess, 
                    IntPtr.Zero, 
                    0, 
                    addressInfo.SetHookProc, 
                    new IntPtr(threadId), // Pass the thread ID directly as a parameter
                    0, 
                    out newThreadId);
                
                if (hThread == IntPtr.Zero)
                {
                    Debug.Log($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                    
                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Debug.Log("Remote thread did not complete in time");
                        return false;
                    }
                    
                    // Get the thread exit code (should be hook handle if successful)
                    IntPtr exitCode;
                    if (!GetExitCodeThread(hThread, out exitCode))
                    {
                        Debug.Log($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    bool success = exitCode != IntPtr.Zero;
                    Debug.Log($"Hook set result: {success}, Handle: {exitCode}");
                    
                    if (success)
                    {
                        Debug.Log("The hook will automatically unhook itself after successful subclassing");
                    }
                    
                    return success;
                }
                finally
                {
                    CloseHandle(hThread);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Subclass a window using the pre-obtained subclass address info
        /// </summary>
        /// <param name="processId">ID of the process</param>
        /// <param name="windowHandle">Handle of the window to subclass</param>
        /// <param name="callbackHandle">Handle of the window to receive notifications</param>
        /// <param name="addressInfo">Pre-obtained subclass address info</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SubclassUsingAddressInfo(uint processId, IntPtr windowHandle, IntPtr callbackHandle, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded)
            {
                Debug.Log("Invalid subclass address info");
                return false;
            }

            // First, set the callback window
            if (!SetCallbackWindow(processId, callbackHandle, addressInfo))
            {
                Debug.Log("Failed to set callback window");
                return false;
            }

            // Get the thread ID of the window
            uint threadId = GetWindowThreadProcessId(windowHandle, out _);

            // Set a hook for the thread first - it will auto-unhook after successful subclassing
            bool hookSet = SetHookForSubclassing(processId, threadId, addressInfo);

            if (!hookSet)
            {
                Debug.Log("Failed to set hook, falling back to direct subclassing");
            }

            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);

            if (hProcess == IntPtr.Zero)
            {
                Debug.Log($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }


            // Create a remote thread that calls InitSubclass with the window handle
            IntPtr newThreadId;
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, addressInfo.InitSubclassProc, windowHandle, 0, out newThreadId);

            if (hThread == IntPtr.Zero)
            {
                Debug.Log($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                return false;
            }

            try
            {
                // Wait for the thread to complete
                uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds

                if (waitResult != 0) // WAIT_OBJECT_0 = 0
                {
                    Debug.Log("Remote thread did not complete in time");
                    return false;
                }

                // Get the thread exit code (should be TRUE/1 if successful)
                IntPtr exitCode;
                if (!GetExitCodeThread(hThread, out exitCode))
                {
                    Debug.Log($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                bool success = exitCode.ToInt64() != 0;
                Debug.Log($"Subclass result: {success}");
                return success;
            }
            finally
            {
                CloseHandle(hThread);
            }



            CloseHandle(hProcess);
        }
        
        
        /// <summary>
        /// Inject the DLL into the target process and subclass the specified window
        /// </summary>
        /// <param name="processId">ID of the process</param>
        /// <param name="windowHandle">Handle of the window to subclass</param>
        /// <param name="callbackHandle">Handle of the window to receive notifications</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool InjectAndSubclass(int processId, IntPtr windowHandle, IntPtr callbackHandle)
        {
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Debug.Log($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // 1. Load the DLL into the target process
                IntPtr remoteDllHandle = LoadRemoteDll(hProcess, _dllPath);
                
                if (remoteDllHandle == IntPtr.Zero)
                {
                    Debug.Log("Failed to load DLL into target process");
                    return false;
                }

                // 2. Access shared memory to get function addresses
                SHARED_MODULE_INFO sharedInfo;
                IntPtr releaseSharedMemFuncAddr = IntPtr.Zero;
                if (!GetSharedModuleInfo(out sharedInfo, hProcess, remoteDllHandle, out releaseSharedMemFuncAddr))
                {
                    Debug.Log("Failed to access shared memory information");
                    return false;
                }

                // Verify we have a valid module base and function addresses
                if (sharedInfo.ModuleBase == IntPtr.Zero || 
                    sharedInfo.InitSubclassProc == IntPtr.Zero)
                {
                    Debug.Log("Invalid module or function information in shared memory");
                    return false;
                }
                
                // 3. Allocate memory for the window handle and callback handle parameters
                IntPtr paramMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)(IntPtr.Size * 2), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                
                if (paramMem == IntPtr.Zero)
                {
                    Debug.Log($"Failed to allocate memory in target process: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // 4. Write the window handle and callback handle to the allocated memory
                    byte[] paramBytes = new byte[IntPtr.Size * 2];
                    
                    // Write window handle
                    byte[] hwndBytes = BitConverter.GetBytes(windowHandle.ToInt64());
                    Array.Copy(hwndBytes, 0, paramBytes, 0, IntPtr.Size);
                    
                    // Write callback handle
                    byte[] callbackBytes = BitConverter.GetBytes(callbackHandle.ToInt64());
                    Array.Copy(callbackBytes, 0, paramBytes, IntPtr.Size, IntPtr.Size);
                    
                    UIntPtr bytesWritten;
                    if (!WriteProcessMemory(hProcess, paramMem, paramBytes, (uint)paramBytes.Length, out bytesWritten))
                    {
                        Debug.Log($"Failed to write to process memory: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    // 5. Create a remote thread that calls InitSubclass with the parameters
                    IntPtr threadId;
                    IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, sharedInfo.InitSubclassProc, paramMem, 0, out threadId);
                    
                    if (hThread == IntPtr.Zero)
                    {
                        Debug.Log($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    try
                    {
                        // Wait for the thread to complete
                        uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                        
                        if (waitResult != 0) // WAIT_OBJECT_0 = 0
                        {
                            Debug.Log("Remote thread did not complete in time");
                            return false;
                        }
                        
                        // Get the thread exit code (should be TRUE/1 if successful)
                        IntPtr exitCode;
                        if (!GetExitCodeThread(hThread, out exitCode))
                        {
                            Debug.Log($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                            return false;
                        }
                        
                        bool success = exitCode.ToInt64() != 0;
                        
                        // 6. Release the shared memory now that we have the function addresses we needed
                        if (releaseSharedMemFuncAddr != IntPtr.Zero)
                        {
                            ReleaseRemoteSharedMemory(hProcess, releaseSharedMemFuncAddr);
                        }
                        
                        return success;
                    }
                    finally
                    {
                        CloseHandle(hThread);
                    }
                }
                finally
                {
                    VirtualFreeEx(hProcess, paramMem, 0, MEM_RELEASE);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        
        /// <summary>
        /// Load the DLL into the target process
        /// </summary>
        /// <param name="hProcess">Handle to the target process</param>
        /// <param name="dllPath">Path to the DLL</param>
        /// <returns>Handle to the loaded DLL in the remote process, or IntPtr.Zero on failure</returns>
        private IntPtr LoadRemoteDll(IntPtr hProcess, string dllPath)
        {
            // 1. Allocate memory for the DLL path string
            byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0"); // null-terminated Unicode string
            uint dllPathSize = (uint)dllPathBytes.Length;
            
            IntPtr remoteDllPathAddr = VirtualAllocEx(hProcess, IntPtr.Zero, dllPathSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            
            if (remoteDllPathAddr == IntPtr.Zero)
            {
                Debug.Log($"Failed to allocate memory for DLL path: {Marshal.GetLastWin32Error()}");
                return IntPtr.Zero;
            }
            
            try
            {
                // 2. Write the DLL path to the allocated memory
                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, remoteDllPathAddr, dllPathBytes, dllPathSize, out bytesWritten))
                {
                    Debug.Log($"Failed to write DLL path to process memory: {Marshal.GetLastWin32Error()}");
                    return IntPtr.Zero;
                }
                
                // 3. Get the address of LoadLibraryW
                IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                
                if (loadLibraryAddr == IntPtr.Zero)
                {
                    Debug.Log("Failed to get address of LoadLibraryW");
                    return IntPtr.Zero;
                }
                
                // 4. Create a remote thread that calls LoadLibraryW with the DLL path
                IntPtr threadId;
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, remoteDllPathAddr, 0, out threadId);
                
                if (hThread == IntPtr.Zero)
                {
                    Debug.Log($"Failed to create remote thread for LoadLibraryW: {Marshal.GetLastWin32Error()}");
                    return IntPtr.Zero;
                }
                
                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                    
                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Debug.Log("LoadLibraryW remote thread did not complete in time");
                        return IntPtr.Zero;
                    }
                    
                    // Get the thread exit code (handle to the loaded module)
                    IntPtr hLibModule;
                    if (!GetExitCodeThread(hThread, out hLibModule))
                    {
                        Debug.Log($"Failed to get LoadLibraryW thread exit code: {Marshal.GetLastWin32Error()}");
                        return IntPtr.Zero;
                    }
                    
                    return hLibModule;
                }
                finally
                {
                    CloseHandle(hThread);
                }
            }
            finally
            {
                VirtualFreeEx(hProcess, remoteDllPathAddr, 0, MEM_RELEASE);
            }
        }
        
        /// <summary>
        /// Gets the module and function information from shared memory
        /// </summary>
        /// <param name="info">Structure to receive the information</param>
        /// <param name="hProcess">Handle to the target process</param>
        /// <param name="hModule">Handle to the loaded module</param>
        /// <param name="releaseSharedMemFuncAddr">Output parameter to get ReleaseSharedMemory function address</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool GetSharedModuleInfo(out SHARED_MODULE_INFO info, IntPtr hProcess, IntPtr hModule, out IntPtr releaseSharedMemFuncAddr)
        {
            info = new SHARED_MODULE_INFO();
            releaseSharedMemFuncAddr = IntPtr.Zero;
            
            // Open existing shared memory
            IntPtr hSharedMem = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE_SECTION, 0, (uint)Marshal.SizeOf(typeof(SHARED_MODULE_INFO)), SHARED_MEM_NAME);
            
            if (hSharedMem == IntPtr.Zero)
            {
                Debug.Log($"Failed to open shared memory: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Map the shared memory
                IntPtr pSharedInfo = MapViewOfFile(hSharedMem, FILE_MAP_ALL_ACCESS, 0, 0, UIntPtr.Zero);
                
                if (pSharedInfo == IntPtr.Zero)
                {
                    Debug.Log($"Failed to map view of shared memory: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // Copy the shared memory data to our structure
                    info = (SHARED_MODULE_INFO)Marshal.PtrToStructure(pSharedInfo, typeof(SHARED_MODULE_INFO));
                    
                    // Verify the signature
                    if (info.Signature != SHARED_MEM_SIGNATURE)
                    {
                        Debug.Log($"Invalid shared memory signature: 0x{info.Signature:X8}");
                        return false;
                    }
                    
                    // Get the address of the ReleaseSharedMemory function
                    // We're going to use GetProcAddress locally since we now have the module address
                    IntPtr localModuleHandle = LoadLibrary(_dllPath);
                    if (localModuleHandle != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr localReleaseAddr = GetProcAddress(localModuleHandle, "ReleaseSharedMemory");
                            if (localReleaseAddr != IntPtr.Zero)
                            {
                                // Calculate the offset of the function from the module base
                                long offset = (long)localReleaseAddr - (long)localModuleHandle;
                                
                                // Apply the offset to the remote module base to get the remote function address
                                releaseSharedMemFuncAddr = new IntPtr((long)info.ModuleBase + offset);
                            }
                        }
                        finally
                        {
                            FreeLibrary(localModuleHandle);
                        }
                    }
                    
                    return true;
                }
                finally
                {
                    UnmapViewOfFile(pSharedInfo);
                }
            }
            finally
            {
                CloseHandle(hSharedMem);
            }
        }
        
        /// <summary>
        /// Releases the shared memory in the remote process
        /// </summary>
        /// <param name="hProcess">Handle to the target process</param>
        /// <param name="releaseSharedMemFuncAddr">Address of the ReleaseSharedMemory function</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool ReleaseRemoteSharedMemory(IntPtr hProcess, IntPtr releaseSharedMemFuncAddr)
        {
            if (releaseSharedMemFuncAddr == IntPtr.Zero)
            {
                return false;
            }
            
            // Create a remote thread that calls ReleaseSharedMemory
            IntPtr threadId;
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, releaseSharedMemFuncAddr, IntPtr.Zero, 0, out threadId);
            
            if (hThread == IntPtr.Zero)
            {
                Debug.Log($"Failed to create remote thread for ReleaseSharedMemory: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Wait for the thread to complete
                uint waitResult = WaitForSingleObject(hThread, 1000); // Wait up to 1 second
                
                if (waitResult != 0) // WAIT_OBJECT_0 = 0
                {
                    Debug.Log("ReleaseSharedMemory remote thread did not complete in time");
                    return false;
                }
                
                // Get the thread exit code
                IntPtr exitCode;
                if (!GetExitCodeThread(hThread, out exitCode))
                {
                    Debug.Log($"Failed to get ReleaseSharedMemory thread exit code: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                return exitCode.ToInt64() != 0;
            }
            finally
            {
                CloseHandle(hThread);
            }
        }
        
        // Additional P/Invoke declarations for ReleaseSharedMemory
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);

        /// <summary>
        /// Sets the callback window for receiving notifications
        /// </summary>
        /// <param name="processId">ID of the process</param>
        /// <param name="callbackHandle">Handle of the window to receive notifications</param>
        /// <param name="addressInfo">Pre-obtained subclass address info</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetCallbackWindow(uint processId, IntPtr callbackHandle, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded || addressInfo.SetCallbackWindowProc == IntPtr.Zero)
            {
                Debug.Log("Invalid subclass address info");
                return false;
            }
            
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Debug.Log($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }

            try
            {

                // Create a remote thread that calls SetCallbackWindow with the parameter
                IntPtr threadId;
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, addressInfo.SetCallbackWindowProc, callbackHandle, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    Debug.Log($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds

                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Debug.Log("Remote thread did not complete in time");
                        return false;
                    }

                    // Get the thread exit code (should be TRUE/1 if successful)
                    IntPtr exitCode;
                    if (!GetExitCodeThread(hThread, out exitCode))
                    {
                        Debug.Log($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                        return false;
                    }

                    bool success = exitCode.ToInt64() != 0;
                    return success;
                }
                finally
                {
                    CloseHandle(hThread);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }

    /// <summary>
    /// Stores the subclass DLL addresses and information for a specific process
    /// </summary>
    public class SubclassAddressInfo
    {
        /// <summary>
        /// Process ID this address info belongs to
        /// </summary>
        public uint ProcessId { get; set; }
        
        /// <summary>
        /// Base address of the module in the target process
        /// </summary>
        public IntPtr ModuleBase { get; set; }
        
        /// <summary>
        /// Address of the InitSubclass function in the target process
        /// </summary>
        public IntPtr InitSubclassProc { get; set; }
        
        /// <summary>
        /// Address of the RemoveSubclass function in the target process
        /// </summary>
        public IntPtr RemoveSubclassProc { get; set; }
        
        /// <summary>
        /// Address of the SetCallbackWindow function in the target process
        /// </summary>
        public IntPtr SetCallbackWindowProc { get; set; }
        
        /// <summary>
        /// Address of the SetHook function in the target process
        /// </summary>
        public IntPtr SetHookProc { get; set; }
        
        /// <summary>
        /// Address of the Unhook function in the target process
        /// </summary>
        public IntPtr UnhookProc { get; set; }
        
        /// <summary>
        /// Returns true if the DLL is loaded and addresses are valid
        /// </summary>
        public bool IsLoaded => ModuleBase != IntPtr.Zero && InitSubclassProc != IntPtr.Zero;
    }
} 
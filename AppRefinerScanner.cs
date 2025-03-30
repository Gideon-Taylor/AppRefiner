using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScintillaScanner
{
    public class MainForm : Form
    {
        // Win32 API imports
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
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

        // Message constants
        private const int WM_USER = 0x0400;
        private const int WM_NEW_SCINTILLA_WINDOW = WM_USER + 1004;

        // Process access flags
        private const int PROCESS_CREATE_THREAD = 0x0002;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_OPERATION = 0x0008;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_ALL_ACCESS = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;

        // UI Controls
        private Button btnStartScanner;
        private Button btnStopScanner;
        private ListBox lstDetectedEditors;
        private Label lblStatus;

        // Track detected editors
        private HashSet<IntPtr> detectedEditors = new HashSet<IntPtr>();

        // DLL function pointers
        private Dictionary<uint, SubclassAddressInfo> addressInfoCache = new Dictionary<uint, SubclassAddressInfo>();

        public MainForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Set up the form
            this.Text = "Scintilla Scanner Demo";
            this.Size = new System.Drawing.Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create controls
            btnStartScanner = new Button
            {
                Text = "Start Scanner",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(120, 30)
            };
            btnStartScanner.Click += BtnStartScanner_Click;

            btnStopScanner = new Button
            {
                Text = "Stop Scanner",
                Location = new System.Drawing.Point(150, 20),
                Size = new System.Drawing.Size(120, 30),
                Enabled = false
            };
            btnStopScanner.Click += BtnStopScanner_Click;

            lblStatus = new Label
            {
                Text = "Scanner not running",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(560, 20)
            };

            lstDetectedEditors = new ListBox
            {
                Location = new System.Drawing.Point(20, 90),
                Size = new System.Drawing.Size(560, 250),
                IntegralHeight = false
            };

            // Add controls to form
            this.Controls.Add(btnStartScanner);
            this.Controls.Add(btnStopScanner);
            this.Controls.Add(lblStatus);
            this.Controls.Add(lstDetectedEditors);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Handle notification about new Scintilla window from the DLL
            if (m.Msg == WM_NEW_SCINTILLA_WINDOW)
            {
                IntPtr hScintillaWnd = m.WParam;
                IntPtr hParentWnd = m.LParam;

                Console.WriteLine($"Received notification about new Scintilla window: {hScintillaWnd}, Parent: {hParentWnd}");

                if (hScintillaWnd != IntPtr.Zero && IsWindow(hScintillaWnd))
                {
                    // Add to our list if it's not already there
                    if (!detectedEditors.Contains(hScintillaWnd))
                    {
                        detectedEditors.Add(hScintillaWnd);
                        
                        // Update the UI on the UI thread
                        this.BeginInvoke(new Action(() =>
                        {
                            lstDetectedEditors.Items.Add($"Scintilla Window: 0x{hScintillaWnd.ToInt64():X}, Parent: 0x{hParentWnd.ToInt64():X}");
                            lblStatus.Text = $"Detected {detectedEditors.Count} Scintilla editors";
                        }));
                    }
                }
            }
        }

        private void BtnStartScanner_Click(object sender, EventArgs e)
        {
            try
            {
                // Find all running PSIDE processes
                var psideProcesses = Process.GetProcessesByName("pside");
                if (psideProcesses.Length == 0)
                {
                    MessageBox.Show("No PSIDE processes found. Please start PeopleSoft Application Designer first.", "No PSIDE Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool anySuccess = false;
                foreach (var process in psideProcesses)
                {
                    try
                    {
                        uint processId = (uint)process.Id;
                        Console.WriteLine($"Initializing scanner for PSIDE process: {processId}");

                        // Start the scanner for this process
                        bool success = StartScintillaScanner(processId, this.Handle);
                        Console.WriteLine($"Started Scintilla scanner for process {processId}: {success}");
                        
                        if (success)
                            anySuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing scanner for process {process.Id}: {ex.Message}");
                    }
                }

                if (anySuccess)
                {
                    btnStartScanner.Enabled = false;
                    btnStopScanner.Enabled = true;
                    lblStatus.Text = "Scanner running...";
                }
                else
                {
                    MessageBox.Show("Failed to start scanners for any PSIDE processes.", "Scanner Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing scanners: {ex.Message}", "Scanner Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStopScanner_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (var entry in addressInfoCache)
                {
                    StopScintillaScanner(entry.Key, entry.Value);
                }

                addressInfoCache.Clear();
                btnStartScanner.Enabled = true;
                btnStopScanner.Enabled = false;
                lblStatus.Text = $"Scanner stopped. Detected {detectedEditors.Count} editors.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping scanners: {ex.Message}", "Scanner Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool StartScintillaScanner(uint processId, IntPtr callbackWindow)
        {
            // Get or cache the subclass address info
            SubclassAddressInfo addressInfo;
            if (addressInfoCache.ContainsKey(processId))
            {
                addressInfo = addressInfoCache[processId];
            }
            else
            {
                addressInfo = GetSubclassAddressInfo(processId);
                if (addressInfo == null || !addressInfo.IsLoaded)
                {
                    Console.WriteLine($"Failed to get subclass address info for process {processId}");
                    return false;
                }
                addressInfoCache[processId] = addressInfo;
            }

            // Set the callback window
            if (!SetCallbackWindow(processId, callbackWindow, addressInfo))
            {
                Console.WriteLine($"Failed to set callback window for process {processId}");
                return false;
            }

            // Start the scanner
            return StartScanner(processId, callbackWindow, addressInfo);
        }

        private bool StopScintillaScanner(uint processId, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded || addressInfo.StopScannerProc == IntPtr.Zero)
            {
                Console.WriteLine("Invalid subclass address info or StopScannerProc is null");
                return false;
            }
            
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Create a remote thread that calls StopScintillaScanner
                IntPtr newThreadId;
                IntPtr hThread = CreateRemoteThread(
                    hProcess, 
                    IntPtr.Zero, 
                    0, 
                    addressInfo.StopScannerProc, 
                    IntPtr.Zero,
                    0, 
                    out newThreadId);
                
                if (hThread == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                    
                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Console.WriteLine("Remote thread did not complete in time");
                        return false;
                    }
                    
                    // Get the thread exit code (should be non-zero if successful)
                    IntPtr exitCode;
                    if (!GetExitCodeThread(hThread, out exitCode))
                    {
                        Console.WriteLine($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    bool success = exitCode.ToInt32() != 0;
                    Console.WriteLine($"Stop scanner result: {success}");
                    return success;
                }
                finally
                {
                    // Always close the thread handle
                    CloseHandle(hThread);
                }
            }
            finally
            {
                // Always close the process handle
                CloseHandle(hProcess);
            }
        }

        private bool StartScanner(uint processId, IntPtr callbackWindow, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded || addressInfo.StartScannerProc == IntPtr.Zero)
            {
                Console.WriteLine("Invalid subclass address info or StartScannerProc is null");
                return false;
            }
            
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Create a remote thread that calls StartScintillaScanner with the callback window handle
                IntPtr newThreadId;
                IntPtr hThread = CreateRemoteThread(
                    hProcess, 
                    IntPtr.Zero, 
                    0, 
                    addressInfo.StartScannerProc, 
                    callbackWindow,
                    0, 
                    out newThreadId);
                
                if (hThread == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                    
                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Console.WriteLine("Remote thread did not complete in time");
                        return false;
                    }
                    
                    // Get the thread exit code (should be non-zero if successful)
                    IntPtr exitCode;
                    if (!GetExitCodeThread(hThread, out exitCode))
                    {
                        Console.WriteLine($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    bool success = exitCode.ToInt32() != 0;
                    Console.WriteLine($"Start scanner result: {success}");
                    return success;
                }
                finally
                {
                    // Always close the thread handle
                    CloseHandle(hThread);
                }
            }
            finally
            {
                // Always close the process handle
                CloseHandle(hProcess);
            }
        }

        private bool SetCallbackWindow(uint processId, IntPtr callbackWindow, SubclassAddressInfo addressInfo)
        {
            if (!addressInfo.IsLoaded || addressInfo.SetCallbackWindowProc == IntPtr.Zero)
            {
                Console.WriteLine("Invalid subclass address info or SetCallbackWindowProc is null");
                return false;
            }
            
            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            try
            {
                // Create a remote thread that calls SetCallbackWindow with the callback window handle
                IntPtr newThreadId;
                IntPtr hThread = CreateRemoteThread(
                    hProcess, 
                    IntPtr.Zero, 
                    0, 
                    addressInfo.SetCallbackWindowProc, 
                    callbackWindow, 
                    0, 
                    out newThreadId);
                
                if (hThread == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to create remote thread: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                
                try
                {
                    // Wait for the thread to complete
                    uint waitResult = WaitForSingleObject(hThread, 5000); // Wait up to 5 seconds
                    
                    if (waitResult != 0) // WAIT_OBJECT_0 = 0
                    {
                        Console.WriteLine("Remote thread did not complete in time");
                        return false;
                    }
                    
                    // Get the thread exit code (should be 1 if successful)
                    IntPtr exitCode;
                    if (!GetExitCodeThread(hThread, out exitCode))
                    {
                        Console.WriteLine($"Failed to get thread exit code: {Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    
                    bool success = exitCode.ToInt32() != 0;
                    Console.WriteLine($"Set callback window result: {success}");
                    return success;
                }
                finally
                {
                    // Always close the thread handle
                    CloseHandle(hThread);
                }
            }
            finally
            {
                // Always close the process handle
                CloseHandle(hProcess);
            }
        }

        private SubclassAddressInfo GetSubclassAddressInfo(uint processId)
        {
            // This is a simplified version - you would use your AppRefinerSubclasser class here
            // For demonstration purposes, we're showing a direct implementation

            // Open the target process
            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, (int)processId);
            
            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to open process: {Marshal.GetLastWin32Error()}");
                return null;
            }
            
            try
            {
                // For demo purposes, just use hardcoded values or try to get function addresses
                // In a real implementation, you would use your shared memory approach to get function addresses
                var dllModule = GetModuleHandle("AppRefinerSubclass.dll");
                if (dllModule == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to get module handle for AppRefinerSubclass.dll");
                    return null;
                }

                // Get function addresses
                IntPtr initSubclassProc = GetProcAddress(dllModule, "InitSubclass");
                IntPtr removeSubclassProc = GetProcAddress(dllModule, "RemoveSubclass");
                IntPtr setCallbackWindowProc = GetProcAddress(dllModule, "SetCallbackWindow");
                IntPtr setHookProc = GetProcAddress(dllModule, "SetHook");
                IntPtr unhookProc = GetProcAddress(dllModule, "Unhook");
                IntPtr startScannerProc = GetProcAddress(dllModule, "StartScintillaScanner");
                IntPtr stopScannerProc = GetProcAddress(dllModule, "StopScintillaScanner");

                // Create and return the address info
                var addressInfo = new SubclassAddressInfo
                {
                    ProcessId = processId,
                    ModuleBase = dllModule,
                    InitSubclassProc = initSubclassProc,
                    RemoveSubclassProc = removeSubclassProc,
                    SetCallbackWindowProc = setCallbackWindowProc,
                    SetHookProc = setHookProc,
                    UnhookProc = unhookProc,
                    StartScannerProc = startScannerProc,
                    StopScannerProc = stopScannerProc
                };
                
                return addressInfo;
            }
            finally
            {
                // Always close the process handle
                CloseHandle(hProcess);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class SubclassAddressInfo
    {
        /// <summary>
        /// Gets or sets the process ID for which this address info applies
        /// </summary>
        public uint ProcessId { get; set; }
        
        /// <summary>
        /// Gets or sets the base address of the DLL module in the remote process
        /// </summary>
        public IntPtr ModuleBase { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the InitSubclass function in the remote process
        /// </summary>
        public IntPtr InitSubclassProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the RemoveSubclass function in the remote process
        /// </summary>
        public IntPtr RemoveSubclassProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the SetCallbackWindow function in the remote process
        /// </summary>
        public IntPtr SetCallbackWindowProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the SetHook function in the remote process
        /// </summary>
        public IntPtr SetHookProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the Unhook function in the remote process
        /// </summary>
        public IntPtr UnhookProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the StartScintillaScanner function in the remote process
        /// </summary>
        public IntPtr StartScannerProc { get; set; }
        
        /// <summary>
        /// Gets or sets the address of the StopScintillaScanner function in the remote process
        /// </summary>
        public IntPtr StopScannerProc { get; set; }
        
        /// <summary>
        /// Gets whether the DLL is loaded and the required functions are available
        /// </summary>
        public bool IsLoaded => ModuleBase != IntPtr.Zero && InitSubclassProc != IntPtr.Zero;
    }
} 
using DiffPlex.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner
{
    public class AppDesignerProcess
    {
        public Dictionary<IntPtr, ScintillaEditor> Editors = [];
        private (IntPtr address, uint size) processBuffer = (0, 0);
        public IntPtr ProcessBuffer { get { return processBuffer.address; } }

        private uint processId = 0;
        private IntPtr processHandle = IntPtr.Zero;
        public bool HasLexilla { get; set; }

        public AppDesignerProcess(uint pid)
        {
            processId = pid;
            processHandle = WinApi.OpenProcess(WinApi.PROCESS_VM_READ | WinApi.PROCESS_VM_WRITE | WinApi.PROCESS_VM_OPERATION, false, processId);

            // Check if any module in the process has the name "Lexilla.dll"
            Process process = Process.GetProcessById((int)processId);
            var lexillaLoaded = process.Modules
                    .Cast<ProcessModule>()
                    .Any(module => string.Equals(module.ModuleName, "Lexilla.dll", StringComparison.OrdinalIgnoreCase));
            HasLexilla = lexillaLoaded;

        }
        public ScintillaEditor GetOrInitEditor(IntPtr hwnd)
        {
            if (Editors.TryGetValue(hwnd, out ScintillaEditor editor))
            {
                return editor;
            }
            else
            {
                return InitEditor(hwnd);
            }
        }
        public ScintillaEditor InitEditor(IntPtr hWnd)
        {
            var caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            if (caption == "Suppress")
            {
                Thread.Sleep(1000);
                caption = WindowHelper.GetGrandparentWindowCaption(hWnd);
            }
            // Get the process ID associated with the Scintilla window.
            var threadId = WinApi.GetWindowThreadProcessId(hWnd, out uint processId);

            var editor = new ScintillaEditor(this, hWnd, processId, threadId, caption);
            Editors.Add(hWnd, editor);


            // Initialize annotations right after editor creation
            ScintillaManager.InitAnnotationStyles(editor);

            return editor;
        }

        /// <summary>
        /// Optionally call this method to clean up persistent resources when your application exits.
        /// </summary>
        public void Cleanup()
        {
            foreach (var editor in Editors.Values)
            {
                editor.Cleanup();
            }
            WinApi.CloseHandle(processHandle);
        }


        public IntPtr GetStandaloneProcessBuffer(uint neededSize)
        {
            var buffer = WinApi.VirtualAllocEx(processHandle, IntPtr.Zero, neededSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
            return buffer;
        }

        public void FreeStandaloneProcessBuffer(IntPtr address)
        {
            WinApi.VirtualFreeEx(processHandle, address, 0, WinApi.MEM_RELEASE);
        }

        public IntPtr GetProcessBuffer(uint neededSize)
        {
            var currentBuffer = processBuffer.address;
            var currentSize = processBuffer.size;
            if (neededSize > currentSize) {
                if (currentBuffer != IntPtr.Zero)
                {
                    WinApi.VirtualFreeEx(processHandle, currentBuffer, 0, WinApi.MEM_RELEASE);
                }
                var buffer = WinApi.VirtualAllocEx(processHandle, IntPtr.Zero, neededSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
                processBuffer = (buffer, neededSize);
            }

            return processBuffer.address;
        }


    }
}

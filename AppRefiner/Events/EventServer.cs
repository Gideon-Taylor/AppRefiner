using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Events
{
    // Message types for pipe communication
    public enum MessageType
    {
        Dwell = 1
    }

    // Message header structure that matches the C++ definition
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MessageHeader
    {
        public IntPtr hwndEditor;   // Handle to the editor window
        public uint messageType;    // Type of message (MessageType enum)
    }

    // Dwell event data structure that matches the C++ definition
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DwellEventData
    {
        public int position;        // Position in the document
        public int x;               // X coordinate
        public int y;               // Y coordinate
        [MarshalAs(UnmanagedType.I1)]  // Explicitly specify as 1-byte boolean
        public bool isStop;         // True if this is a stop event
    }

    // Event args for dwell events
    public class DwellEventArgs : EventArgs
    {
        public IntPtr EditorHandle { get; }
        public bool IsStop { get; }
        public int Position { get; }
        public int X { get; }
        public int Y { get; }

        public DwellEventArgs(IntPtr editorHandle, int position, int x, int y,  bool isStop)
        {
            EditorHandle = editorHandle;
            Position = position;
            X = x;
            Y = y;
            IsStop = isStop;
        }
    }
    internal static class EventServer
    {
        // Pipe related fields
        private static NamedPipeServerStream? pipeServer;
        private static CancellationTokenSource? pipeServerCts;

        private static bool isPipeServerRunning = false;

        // Delegate for dwell event callbacks
        public delegate void DwellEventHandler(object sender, DwellEventArgs e);
        public static event DwellEventHandler? OnDwellEvent;

        public static string PipeName;
        public static uint PipeID; 
        public static bool Running { get { return isPipeServerRunning; } }

        static EventServer()
        {
            Random random = new Random();
            PipeID = (uint)random.Next(1, int.MaxValue);
            PipeName = $"AppRefinerNotifyPipe-{PipeID:X}";
        }

        // Win32 API imports
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, UIntPtr wParam, IntPtr lParam);


        // DLL imports
        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SetHook(uint threadId);

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Unhook();

        [DllImport("AppRefinerHook.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint GetPipeNameMessageID();

        // Method to send pipe name to the hooked thread
        public static bool SendPipeNameToHookedThread(uint threadId, uint pipeId)
        {
            // Get the message ID for setting pipe name
            uint msgId = GetPipeNameMessageID();

            // Send the thread message with the pipe ID
            bool result = PostThreadMessage(threadId, msgId, new UIntPtr(pipeId), IntPtr.Zero);

            if (result)
            {
                // Format the pipe name locally for our own use
                System.Diagnostics.Debug.WriteLine($"Sent pipe name ID {pipeId:X} to thread {threadId:X}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send pipe name ID to thread {threadId:X}");
            }

            return result;
        }

        // Named pipe methods
        public static bool StartPipeServer()
        {
            if (isPipeServerRunning)
                return true;

            try
            {
                pipeServerCts = new CancellationTokenSource();
                Task.Run(() => PipeServerLoop(pipeServerCts.Token), pipeServerCts.Token);
                isPipeServerRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting pipe server: {ex.Message}");
                return false;
            }
        }

        public static void StopPipeServer()
        {
            if (!isPipeServerRunning)
                return;

            try
            {
                pipeServerCts?.Cancel();
                pipeServerCts?.Dispose();
                pipeServerCts = null;
                isPipeServerRunning = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping pipe server: {ex.Message}");
            }
        }

        private static async void PipeServerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message))
                    {
                        System.Diagnostics.Debug.WriteLine("Pipe server waiting for connection...");
                        await pipeServer.WaitForConnectionAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine("Pipe client connected.");

                        // Process messages until the client disconnects or we're cancelled
                        while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                        {
                            // Read the message header
                            MessageHeader header = new MessageHeader();
                            byte[] headerBuffer = new byte[Marshal.SizeOf<MessageHeader>()];

                            int bytesRead = await pipeServer.ReadAsync(headerBuffer, 0, headerBuffer.Length, cancellationToken);
                            if (bytesRead != headerBuffer.Length)
                            {
                                System.Diagnostics.Debug.WriteLine("Failed to read complete header");
                                break;
                            }

                            // Deserialize the header
                            GCHandle pinnedHeader = GCHandle.Alloc(headerBuffer, GCHandleType.Pinned);
                            try
                            {
                                header = Marshal.PtrToStructure<MessageHeader>(pinnedHeader.AddrOfPinnedObject());
                            }
                            finally
                            {
                                pinnedHeader.Free();
                            }

                            // Process the message based on its type
                            MessageType messageType = (MessageType)header.messageType;

                            if (messageType == MessageType.Dwell)
                            {
                                // Read the dwell event data
                                DwellEventData dwellData = new DwellEventData();
                                byte[] dwellBuffer = new byte[Marshal.SizeOf<DwellEventData>()];

                                bytesRead = await pipeServer.ReadAsync(dwellBuffer, 0, dwellBuffer.Length, cancellationToken);
                                if (bytesRead != dwellBuffer.Length)
                                {
                                    System.Diagnostics.Debug.WriteLine("Failed to read complete dwell data");
                                    break;
                                }

                                // Deserialize the dwell data
                                GCHandle pinnedDwell = GCHandle.Alloc(dwellBuffer, GCHandleType.Pinned);
                                try
                                {
                                    dwellData = Marshal.PtrToStructure<DwellEventData>(pinnedDwell.AddrOfPinnedObject());
                                }
                                finally
                                {
                                    pinnedDwell.Free();
                                }

                                // Create event args
                                DwellEventArgs args = new DwellEventArgs(
                                    header.hwndEditor,
                                    dwellData.position,
                                    dwellData.x,
                                    dwellData.y,
                                    dwellData.isStop
                                );

                                // Raise the appropriate event
                                if (messageType == MessageType.Dwell)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Received DwellStart - Position: {dwellData.position}, X: {dwellData.x}, Y: {dwellData.y} IsStop: {dwellData.isStop}");
                                    OnDwellEvent?.Invoke(sender: new object(), args);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Received unknown message type: {messageType}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, just exit
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in pipe server: {ex.Message}");
                    // Wait a bit before trying again
                    await Task.Delay(1000, cancellationToken);
                }
            }

            System.Diagnostics.Debug.WriteLine("Pipe server stopped.");
        }
    }
}

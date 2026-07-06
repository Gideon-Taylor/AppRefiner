using System;
using System.Collections.Generic;
using System.Linq;

namespace AppRefiner
{
    /// <summary>
    /// Manages memory buffers allocated in a remote process.
    /// Tracks all RemoteBuffer instances and provides lifecycle management.
    ///
    /// Thread safety: buffer lookup/creation is internally synchronized on <see cref="SyncRoot"/>.
    /// Named buffers are shared process-wide, so any multi-step sequence against one
    /// (write → SendMessage → read-back) must hold <c>lock (memoryManager.SyncRoot)</c> for the
    /// whole sequence — otherwise another thread can overwrite or resize (free!) the buffer
    /// between steps. Holding the lock across a cross-process SendMessage is safe: a thread
    /// blocked in SendMessage still services incoming sent messages, so the remote process
    /// cannot deadlock against us. Buffers from <see cref="CreateTempBuffer"/> are caller-owned
    /// and need no locking.
    /// </summary>
    public class MemoryManager
    {
        private readonly Dictionary<string, RemoteBuffer> _buffers = new Dictionary<string, RemoteBuffer>();
        private readonly AppDesignerProcess _process;

        /// <summary>
        /// Synchronizes all use of shared named buffers. Hold this lock for the full
        /// write → SendMessage → read sequence, not just the individual buffer calls.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Creates a new MemoryManager for the specified AppDesigner process
        /// </summary>
        /// <param name="process">The AppDesigner process to manage memory for</param>
        public MemoryManager(AppDesignerProcess process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            Debug.Log($"MemoryManager: Created for process {process.ProcessId}");
        }

        /// <summary>
        /// Gets an existing buffer by name, or creates a new one if it doesn't exist
        /// </summary>
        /// <param name="name">Name of the buffer</param>
        /// <param name="initialSize">Initial size if creating a new buffer (default 4096 bytes)</param>
        /// <returns>The RemoteBuffer instance</returns>
        public RemoteBuffer GetOrCreateBuffer(string name, uint initialSize = 4096)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Buffer name cannot be null or whitespace", nameof(name));
            }

            lock (SyncRoot)
            {
                if (_buffers.TryGetValue(name, out RemoteBuffer? buffer))
                {
                    Debug.Log($"MemoryManager: Reusing existing buffer '{name}'");
                    if (initialSize > buffer.Size)
                    {
                        Debug.Log("Resizing buffer...");
                        buffer.Resize(initialSize);
                    }
                    return buffer;
                }

                // Create new buffer
                buffer = new RemoteBuffer(_process.ProcessHandle, _process.ProcessId, name, initialSize);
                _buffers[name] = buffer;
                Debug.Log($"MemoryManager: Created new buffer '{name}' with size {initialSize}");
                return buffer;
            }
        }

        /// <summary>
        /// Gets an existing buffer by name
        /// </summary>
        /// <param name="name">Name of the buffer</param>
        /// <returns>The RemoteBuffer instance, or null if not found</returns>
        public RemoteBuffer? GetBuffer(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Buffer name cannot be null or whitespace", nameof(name));
            }

            lock (SyncRoot)
            {
                return _buffers.TryGetValue(name, out RemoteBuffer? buffer) ? buffer : null;
            }
        }

        /// <summary>
        /// Creates a new buffer with the specified name and size
        /// </summary>
        /// <param name="name">Name of the buffer (must be unique)</param>
        /// <param name="initialSize">Initial size in bytes</param>
        /// <returns>The newly created RemoteBuffer instance</returns>
        /// <exception cref="InvalidOperationException">Thrown if a buffer with the same name already exists</exception>
        public RemoteBuffer CreateTempBuffer(uint initialSize = 4096)
        {
            var buffer = new RemoteBuffer(_process.ProcessHandle, _process.ProcessId, "temp", initialSize);
            return buffer;
        }

        /// <summary>
        /// Removes a buffer by name, freeing its memory in the remote process
        /// </summary>
        /// <param name="name">Name of the buffer to remove</param>
        /// <returns>True if the buffer was found and removed, false otherwise</returns>
        public bool RemoveBuffer(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_buffers.TryGetValue(name, out RemoteBuffer? buffer))
                {
                    buffer.Free();
                    _buffers.Remove(name);
                    Debug.Log($"MemoryManager: Removed buffer '{name}'");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the number of buffers currently managed
        /// </summary>
        public int BufferCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _buffers.Count;
                }
            }
        }

        /// <summary>
        /// Gets the names of all managed buffers
        /// </summary>
        /// <returns>Collection of buffer names</returns>
        public IEnumerable<string> GetBufferNames()
        {
            lock (SyncRoot)
            {
                return _buffers.Keys.ToList();
            }
        }

        /// <summary>
        /// Frees all managed buffers in the remote process
        /// </summary>
        public void Cleanup()
        {
            lock (SyncRoot)
            {
                Debug.Log($"MemoryManager: Cleaning up {_buffers.Count} buffers for process {_process.ProcessId}");

                foreach (var kvp in _buffers)
                {
                    try
                    {
                        kvp.Value.Free();
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"MemoryManager: Error freeing buffer '{kvp.Key}': {ex.Message}");
                    }
                }

                _buffers.Clear();
                Debug.Log($"MemoryManager: Cleanup complete");
            }
        }

        // Note: no finalizer, deliberately — Cleanup() is called explicitly from
        // AppDesignerProcess.Cleanup(). A GC-time VirtualFreeEx could run after the
        // process handle was closed and its OS handle value recycled, freeing memory
        // in an unrelated process (see RemoteBuffer for the same reasoning).
    }
}

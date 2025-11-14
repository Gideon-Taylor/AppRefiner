using System;
using System.Collections.Generic;

namespace AppRefiner
{
    /// <summary>
    /// Manages memory buffers allocated in a remote process.
    /// Tracks all RemoteBuffer instances and provides lifecycle management.
    /// </summary>
    public class MemoryManager
    {
        private readonly Dictionary<string, RemoteBuffer> _buffers = new Dictionary<string, RemoteBuffer>();
        private readonly AppDesignerProcess _process;

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

            if (_buffers.TryGetValue(name, out RemoteBuffer? buffer))
            {
                Debug.Log($"MemoryManager: Reusing existing buffer '{name}'");
                return buffer;
            }

            // Create new buffer
            buffer = new RemoteBuffer(_process.ProcessHandle, _process.ProcessId, name, initialSize);
            _buffers[name] = buffer;
            Debug.Log($"MemoryManager: Created new buffer '{name}' with size {initialSize}");
            return buffer;
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

            return _buffers.TryGetValue(name, out RemoteBuffer? buffer) ? buffer : null;
        }

        /// <summary>
        /// Creates a new buffer with the specified name and size
        /// </summary>
        /// <param name="name">Name of the buffer (must be unique)</param>
        /// <param name="initialSize">Initial size in bytes</param>
        /// <returns>The newly created RemoteBuffer instance</returns>
        /// <exception cref="InvalidOperationException">Thrown if a buffer with the same name already exists</exception>
        public RemoteBuffer CreateBuffer(string name, uint initialSize = 4096)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Buffer name cannot be null or whitespace", nameof(name));
            }

            if (_buffers.ContainsKey(name))
            {
                throw new InvalidOperationException($"A buffer with the name '{name}' already exists. Use GetOrCreateBuffer or remove the existing buffer first.");
            }

            var buffer = new RemoteBuffer(_process.ProcessHandle, _process.ProcessId, name, initialSize);
            _buffers[name] = buffer;
            Debug.Log($"MemoryManager: Created buffer '{name}' with size {initialSize}");
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

            if (_buffers.TryGetValue(name, out RemoteBuffer? buffer))
            {
                buffer.Free();
                _buffers.Remove(name);
                Debug.Log($"MemoryManager: Removed buffer '{name}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the number of buffers currently managed
        /// </summary>
        public int BufferCount => _buffers.Count;

        /// <summary>
        /// Gets the names of all managed buffers
        /// </summary>
        /// <returns>Collection of buffer names</returns>
        public IEnumerable<string> GetBufferNames()
        {
            return _buffers.Keys;
        }

        /// <summary>
        /// Frees all managed buffers in the remote process
        /// </summary>
        public void Cleanup()
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

        /// <summary>
        /// Ensures cleanup happens even if not called explicitly
        /// </summary>
        ~MemoryManager()
        {
            Cleanup();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppRefiner
{
    /// <summary>
    /// Represents a memory buffer allocated in a remote process.
    /// Provides automatic resizing and helper methods for writing strings and raw data.
    /// Supports sequential writing mode where strings are written one after another.
    /// </summary>
    public class RemoteBuffer
    {
        private uint _writeOffset = 0;

        /// <summary>
        /// Name of this buffer for debugging purposes
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Address of the allocated memory in the remote process
        /// </summary>
        public IntPtr Address { get; private set; }

        /// <summary>
        /// Current size of the allocated buffer in bytes
        /// </summary>
        public uint Size { get; private set; }

        /// <summary>
        /// Current write offset for sequential writing mode
        /// </summary>
        public uint WriteOffset => _writeOffset;

        /// <summary>
        /// Handle to the remote process
        /// </summary>
        public IntPtr ProcessHandle { get; }

        /// <summary>
        /// Process ID for debugging purposes
        /// </summary>
        public uint ProcessId { get; }

        /// <summary>
        /// Creates a new RemoteBuffer by allocating memory in the target process
        /// </summary>
        /// <param name="processHandle">Handle to the target process</param>
        /// <param name="processId">Process ID for debugging</param>
        /// <param name="name">Name of this buffer for debugging</param>
        /// <param name="initialSize">Initial size in bytes to allocate</param>
        public RemoteBuffer(IntPtr processHandle, uint processId, string name, uint initialSize = 4096)
        {
            ProcessHandle = processHandle;
            ProcessId = processId;
            Name = name;

            // Allocate initial buffer
            Address = WinApi.VirtualAllocEx(processHandle, IntPtr.Zero, initialSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
            if (Address == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to allocate {initialSize} bytes in process {processId} for buffer '{name}'");
            }

            Size = initialSize;
            Debug.Log($"RemoteBuffer '{name}': Allocated {initialSize} bytes at 0x{Address:X} in process {processId}");
        }

        /// <summary>
        /// Resizes the buffer by allocating a new buffer, copying existing data, and freeing the old buffer.
        /// Note: Resizing invalidates all previously returned addresses! Caller must re-write all data.
        /// The write offset is reset to 0 after resizing.
        /// </summary>
        /// <param name="newSize">New size in bytes</param>
        public void Resize(uint newSize)
        {
            if (newSize == Size)
            {
                return; // Already the correct size
            }

            Debug.Log($"RemoteBuffer '{Name}': Resizing from {Size} to {newSize} bytes");

            // Allocate new buffer
            IntPtr newAddress = WinApi.VirtualAllocEx(ProcessHandle, IntPtr.Zero, newSize, WinApi.MEM_COMMIT, WinApi.PAGE_READWRITE);
            if (newAddress == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to allocate {newSize} bytes in process {ProcessId} for buffer '{Name}' during resize");
            }

            // Copy existing data if we're growing the buffer
            if (newSize > Size && Size > 0 && Address != IntPtr.Zero)
            {
                byte[] tempBuffer = new byte[Size];
                if (WinApi.ReadProcessMemory(ProcessHandle, Address, tempBuffer, (int)Size, out int bytesRead) && bytesRead == Size)
                {
                    WinApi.WriteProcessMemory(ProcessHandle, newAddress, tempBuffer, (int)Size, out int bytesWritten);
                    Debug.Log($"RemoteBuffer '{Name}': Copied {bytesWritten} bytes to new buffer");
                }
                else
                {
                    Debug.Log($"RemoteBuffer '{Name}': Warning - Failed to read old data during resize");
                }
            }

            // Free old buffer
            if (Address != IntPtr.Zero)
            {
                WinApi.VirtualFreeEx(ProcessHandle, Address, 0, WinApi.MEM_RELEASE);
                Debug.Log($"RemoteBuffer '{Name}': Freed old buffer at 0x{Address:X}");
            }

            // Update to new buffer and reset write offset since all addresses are now invalid
            Address = newAddress;
            Size = newSize;
            _writeOffset = 0;
            Debug.Log($"RemoteBuffer '{Name}': Resize complete, new address 0x{Address:X}, write offset reset to 0");
        }

        /// <summary>
        /// Resets the sequential write offset back to the start of the buffer.
        /// Call this before starting a new batch of sequential writes.
        /// </summary>
        public void Reset()
        {
            _writeOffset = 0;
            Debug.Log($"RemoteBuffer '{Name}': Write offset reset to 0");
        }

        /// <summary>
        /// Writes a string to the buffer.
        /// If offset is null (sequential mode), writes at the current write offset and advances it.
        /// If offset is provided (absolute mode), writes at that specific location.
        /// </summary>
        /// <param name="text">Text to write</param>
        /// <param name="encoding">Encoding to use (defaults to Unicode/UTF-16)</param>
        /// <param name="offset">Offset in bytes from start of buffer. If null, uses sequential write mode.</param>
        /// <returns>Address where the string was written, or null if write would exceed buffer size</returns>
        public IntPtr? WriteString(string text, Encoding? encoding = null, uint? offset = null)
        {
            encoding ??= Encoding.Unicode;

            // Determine actual write offset
            bool sequentialMode = !offset.HasValue;
            uint actualOffset = offset ?? _writeOffset;

            // Calculate required size
            byte[] stringBytes = encoding.GetBytes(text);
            int nullTerminatorSize = encoding == Encoding.Unicode ? 2 : 1;
            uint requiredSize = actualOffset + (uint)stringBytes.Length + (uint)nullTerminatorSize;

            // Check if write would exceed buffer - return null instead of auto-resizing
            if (requiredSize > Size)
            {
                Debug.Log($"RemoteBuffer '{Name}': Write would exceed buffer size ({requiredSize} > {Size}), returning null");
                return null;
            }

            // Create buffer with null terminator
            byte[] buffer = new byte[stringBytes.Length + nullTerminatorSize];
            Buffer.BlockCopy(stringBytes, 0, buffer, 0, stringBytes.Length);
            // Null terminator is already zero-initialized

            // Write to remote process
            IntPtr writeAddress = IntPtr.Add(Address, (int)actualOffset);
            if (!WinApi.WriteProcessMemory(ProcessHandle, writeAddress, buffer, buffer.Length, out int bytesWritten) || bytesWritten != buffer.Length)
            {
                throw new InvalidOperationException($"Failed to write string to buffer '{Name}' at offset {actualOffset}");
            }

            // Advance write offset if in sequential mode
            if (sequentialMode)
            {
                _writeOffset = requiredSize;
                Debug.Log($"RemoteBuffer '{Name}': Wrote {bytesWritten} bytes at offset {actualOffset} (sequential, new offset: {_writeOffset})");
            }
            else
            {
                Debug.Log($"RemoteBuffer '{Name}': Wrote {bytesWritten} bytes at offset {actualOffset} (absolute mode)");
            }

            return writeAddress;
        }

        /// <summary>
        /// Writes multiple null-terminated strings sequentially to the buffer.
        /// If offset is null (sequential mode), writes starting at the current write offset.
        /// If offset is provided (absolute mode), writes starting at that location.
        /// </summary>
        /// <param name="strings">Strings to write</param>
        /// <param name="encoding">Encoding to use (defaults to Unicode/UTF-16)</param>
        /// <param name="offset">Starting offset in bytes. If null, uses sequential write mode.</param>
        /// <returns>List of addresses where each string starts, or null if any write would exceed buffer</returns>
        public List<IntPtr>? WriteStrings(IEnumerable<string> strings, Encoding? encoding = null, uint? offset = null)
        {
            encoding ??= Encoding.Unicode;
            int nullTerminatorSize = encoding == Encoding.Unicode ? 2 : 1;

            // Calculate total size needed
            var stringList = strings.ToList();
            uint startOffset = offset ?? _writeOffset;
            uint totalSize = startOffset;
            foreach (var str in stringList)
            {
                totalSize += (uint)encoding.GetByteCount(str) + (uint)nullTerminatorSize;
            }

            // Check if writes would exceed buffer - return null instead of auto-resizing
            if (totalSize > Size)
            {
                Debug.Log($"RemoteBuffer '{Name}': WriteStrings would exceed buffer size ({totalSize} > {Size}), returning null");
                return null;
            }

            // Write each string and collect addresses
            List<IntPtr> addresses = new List<IntPtr>();

            foreach (var str in stringList)
            {
                // Use sequential mode (offset = null) so WriteString handles offset tracking
                IntPtr? stringAddress = WriteString(str, encoding, offset: null);
                if (!stringAddress.HasValue)
                {
                    // This shouldn't happen since we pre-checked size, but handle it
                    Debug.Log($"RemoteBuffer '{Name}': WriteStrings failed mid-write");
                    return null;
                }
                addresses.Add(stringAddress.Value);
            }

            Debug.Log($"RemoteBuffer '{Name}': Wrote {stringList.Count} strings, total {totalSize - startOffset} bytes");
            return addresses;
        }

        /// <summary>
        /// Writes raw bytes to the buffer.
        /// If offset is null (sequential mode), writes at the current write offset and advances it.
        /// If offset is provided (absolute mode), writes at that specific location.
        /// </summary>
        /// <param name="data">Data to write</param>
        /// <param name="offset">Offset in bytes from start of buffer. If null, uses sequential write mode.</param>
        /// <returns>Address where data was written, or null if write would exceed buffer size</returns>
        public IntPtr? Write(byte[] data, uint? offset = null)
        {
            if (data == null || data.Length == 0)
            {
                return offset.HasValue ? IntPtr.Add(Address, (int)offset.Value) : IntPtr.Add(Address, (int)_writeOffset);
            }

            // Determine actual write offset
            bool sequentialMode = !offset.HasValue;
            uint actualOffset = offset ?? _writeOffset;

            uint requiredSize = actualOffset + (uint)data.Length;

            // Check if write would exceed buffer - return null instead of auto-resizing
            if (requiredSize > Size)
            {
                Debug.Log($"RemoteBuffer '{Name}': Write would exceed buffer size ({requiredSize} > {Size}), returning null");
                return null;
            }

            // Write to remote process
            IntPtr writeAddress = IntPtr.Add(Address, (int)actualOffset);
            if (!WinApi.WriteProcessMemory(ProcessHandle, writeAddress, data, data.Length, out int bytesWritten) || bytesWritten != data.Length)
            {
                throw new InvalidOperationException($"Failed to write {data.Length} bytes to buffer '{Name}' at offset {actualOffset}");
            }

            // Advance write offset if in sequential mode
            if (sequentialMode)
            {
                _writeOffset = requiredSize;
                Debug.Log($"RemoteBuffer '{Name}': Wrote {bytesWritten} raw bytes at offset {actualOffset} (sequential, new offset: {_writeOffset})");
            }
            else
            {
                Debug.Log($"RemoteBuffer '{Name}': Wrote {bytesWritten} raw bytes at offset {actualOffset} (absolute mode)");
            }

            return writeAddress;
        }

        /// <summary>
        /// Writes multiple binary blobs sequentially to the buffer.
        /// If offset is null (sequential mode), writes starting at the current write offset.
        /// If offset is provided (absolute mode), writes starting at that location.
        /// Useful for writing multiple icons, images, or structures in one batch.
        /// </summary>
        /// <param name="blobs">Binary data blobs to write</param>
        /// <param name="offset">Starting offset in bytes. If null, uses sequential write mode.</param>
        /// <returns>List of addresses where each blob starts, or null if any write would exceed buffer</returns>
        public List<IntPtr>? WriteBlobs(IEnumerable<byte[]> blobs, uint? offset = null)
        {
            var blobList = blobs.ToList();
            if (blobList.Count == 0)
            {
                return new List<IntPtr>();
            }

            // Calculate total size needed
            uint startOffset = offset ?? _writeOffset;
            uint totalSize = startOffset;
            foreach (var blob in blobList)
            {
                totalSize += (uint)blob.Length;
            }

            // Check if writes would exceed buffer - return null instead of auto-resizing
            if (totalSize > Size)
            {
                Debug.Log($"RemoteBuffer '{Name}': WriteBlobs would exceed buffer size ({totalSize} > {Size}), returning null");
                return null;
            }

            // Write each blob and collect addresses
            List<IntPtr> addresses = new List<IntPtr>();

            foreach (var blob in blobList)
            {
                // Use sequential mode (offset = null) so Write handles offset tracking
                IntPtr? blobAddress = Write(blob, offset: null);
                if (!blobAddress.HasValue)
                {
                    // This shouldn't happen since we pre-checked size, but handle it
                    Debug.Log($"RemoteBuffer '{Name}': WriteBlobs failed mid-write");
                    return null;
                }
                addresses.Add(blobAddress.Value);
            }

            Debug.Log($"RemoteBuffer '{Name}': Wrote {blobList.Count} blobs, total {totalSize - startOffset} bytes");
            return addresses;
        }

        /// <summary>
        /// Reads raw bytes from the buffer
        /// </summary>
        /// <param name="size">Number of bytes to read</param>
        /// <param name="offset">Offset in bytes from the start of the buffer</param>
        /// <returns>Bytes read from the buffer</returns>
        public byte[] Read(int size, uint offset = 0)
        {
            if (offset + size > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(size), $"Cannot read {size} bytes at offset {offset} from buffer of size {Size}");
            }

            byte[] buffer = new byte[size];
            IntPtr readAddress = IntPtr.Add(Address, (int)offset);

            if (!WinApi.ReadProcessMemory(ProcessHandle, readAddress, buffer, size, out int bytesRead) || bytesRead != size)
            {
                throw new InvalidOperationException($"Failed to read {size} bytes from buffer '{Name}' at offset {offset}");
            }

            return buffer;
        }

        /// <summary>
        /// Frees the allocated memory in the remote process
        /// </summary>
        public void Free()
        {
            if (Address != IntPtr.Zero)
            {
                WinApi.VirtualFreeEx(ProcessHandle, Address, 0, WinApi.MEM_RELEASE);
                Debug.Log($"RemoteBuffer '{Name}': Freed buffer at 0x{Address:X}");
                Address = IntPtr.Zero;
                Size = 0;
            }
        }

        /// <summary>
        /// Ensures the buffer is freed when garbage collected
        /// </summary>
        ~RemoteBuffer()
        {
            Free();
        }
    }
}

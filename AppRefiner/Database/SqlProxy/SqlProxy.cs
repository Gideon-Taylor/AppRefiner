using System.Runtime.InteropServices;
using System.Text;

namespace AppRefiner.Database.SqlProxy
{
    /// <summary>
    /// Client for the in-process SAM SQL proxy inside App Designer.
    /// Creates anonymous pipes, bootstraps the hook, and runs SELECT-only queries
    /// against the signed-on session. Call <see cref="TryAttach"/> after hooks are installed.
    /// </summary>
    public sealed class SqlProxy : IDisposable
    {
        private readonly AppDesignerProcess process;
        private readonly object ioLock = new();
        private SafePipeHandle? requestWrite;
        private SafePipeHandle? responseRead;
        private RemoteBuffer? attachBuffer;
        private uint nextRequestId = 1;
        private bool disposed;

        private SqlProxy(AppDesignerProcess process)
        {
            this.process = process;
        }

        public bool IsAttached => !disposed && requestWrite != null && !requestWrite.IsInvalid;

        /// <summary>
        /// Creates anonymous pipes, duplicates the hook ends into the App Designer process,
        /// and sends WM_AR_SQLPROXY_ATTACH. Returns null on failure.
        /// </summary>
        public static SqlProxy? TryAttach(AppDesignerProcess process)
        {
            if (process == null)
            {
                return null;
            }

            if (process.ProcessHandle == IntPtr.Zero || process.MainThreadId == 0)
            {
                Debug.Log("SqlProxy.TryAttach: process handle or main thread missing");
                return null;
            }

            if (!WinApi.CreatePipe(out var reqReadLocal, out var reqWriteLocal, IntPtr.Zero, 0))
            {
                Debug.Log($"SqlProxy.TryAttach: CreatePipe request failed err={Marshal.GetLastWin32Error()}");
                return null;
            }

            if (!WinApi.CreatePipe(out var respReadLocal, out var respWriteLocal, IntPtr.Zero, 0))
            {
                Debug.Log($"SqlProxy.TryAttach: CreatePipe response failed err={Marshal.GetLastWin32Error()}");
                WinApi.CloseHandle(reqReadLocal);
                WinApi.CloseHandle(reqWriteLocal);
                return null;
            }

            IntPtr remoteReqRead = IntPtr.Zero;
            IntPtr remoteRespWrite = IntPtr.Zero;
            IntPtr currentProcess = WinApi.GetCurrentProcess();

            try
            {
                if (!WinApi.DuplicateHandle(
                        currentProcess, reqReadLocal,
                        process.ProcessHandle, out remoteReqRead,
                        0, false, WinApi.DUPLICATE_SAME_ACCESS))
                {
                    Debug.Log($"SqlProxy.TryAttach: DuplicateHandle request read failed err={Marshal.GetLastWin32Error()}");
                    return null;
                }

                if (!WinApi.DuplicateHandle(
                        currentProcess, respWriteLocal,
                        process.ProcessHandle, out remoteRespWrite,
                        0, false, WinApi.DUPLICATE_SAME_ACCESS))
                {
                    Debug.Log($"SqlProxy.TryAttach: DuplicateHandle response write failed err={Marshal.GetLastWin32Error()}");
                    return null;
                }

                // Local ends only needed by the remote side — close them here.
                WinApi.CloseHandle(reqReadLocal);
                reqReadLocal = IntPtr.Zero;
                WinApi.CloseHandle(respWriteLocal);
                respWriteLocal = IntPtr.Zero;

                var proxy = new SqlProxy(process)
                {
                    requestWrite = new SafePipeHandle(reqWriteLocal, ownsHandle: true),
                    responseRead = new SafePipeHandle(respReadLocal, ownsHandle: true),
                };
                reqWriteLocal = IntPtr.Zero;
                respReadLocal = IntPtr.Zero;

                var attach = new SqlProxyAttachInfo
                {
                    Magic = SqlProxyProtocol.Magic,
                    Version = SqlProxyProtocol.Version,
                    StructSize = (uint)SqlProxyProtocol.AttachInfoSize,
                    Flags = 0,
                    RequestReadHandle = unchecked((ulong)remoteReqRead.ToInt64()),
                    ResponseWriteHandle = unchecked((ulong)remoteRespWrite.ToInt64()),
                };

                // Handles are now owned by the hook after a successful attach message;
                // do not CloseHandle the remote values from this process.
                remoteReqRead = IntPtr.Zero;
                remoteRespWrite = IntPtr.Zero;

                byte[] attachBytes = StructureToBytes(attach);
                proxy.attachBuffer = new RemoteBuffer(
                    process.ProcessHandle, process.ProcessId, "SqlProxyAttach", (uint)attachBytes.Length);
                if (proxy.attachBuffer.Write(attachBytes) == null)
                {
                    Debug.Log("SqlProxy.TryAttach: failed to write attach struct to remote process");
                    proxy.Dispose();
                    return null;
                }

                // Prefer synchronous SendMessage to the subclassed main window for a return value.
                bool attached = false;
                if (process.MainWindowHandle != IntPtr.Zero && WinApi.IsWindow(process.MainWindowHandle))
                {
                    IntPtr result = WinApi.SendMessage(
                        process.MainWindowHandle,
                        (int)SqlProxyProtocol.WM_AR_SQLPROXY_ATTACH,
                        proxy.attachBuffer.Address,
                        (IntPtr)SqlProxyProtocol.AttachInfoSize);
                    attached = result != IntPtr.Zero;
                    Debug.Log($"SqlProxy.TryAttach: SendMessage ATTACH returned 0x{result.ToInt64():X}");
                }

                if (!attached)
                {
                    // Fallback: post to the UI thread (no return value); verify with Ping.
                    bool posted = EventHookInstallerPostThreadMessage(
                        process.MainThreadId,
                        SqlProxyProtocol.WM_AR_SQLPROXY_ATTACH,
                        proxy.attachBuffer.Address,
                        (IntPtr)SqlProxyProtocol.AttachInfoSize);
                    if (!posted)
                    {
                        Debug.Log($"SqlProxy.TryAttach: PostThreadMessage failed err={Marshal.GetLastWin32Error()}");
                        proxy.Dispose();
                        return null;
                    }

                    // Brief settle for the hook to start the IO thread.
                    Thread.Sleep(50);
                    try
                    {
                        var pong = proxy.Ping(CancellationToken.None);
                        attached = pong.Status == SqlProxyStatus.Ok;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"SqlProxy.TryAttach: Ping after post failed: {ex.Message}");
                        attached = false;
                    }
                }

                if (!attached)
                {
                    Debug.Log("SqlProxy.TryAttach: hook did not accept attach");
                    proxy.Dispose();
                    return null;
                }

                Debug.Log($"SqlProxy.TryAttach: success for process {process.ProcessId}");
                return proxy;
            }
            finally
            {
                if (reqReadLocal != IntPtr.Zero) WinApi.CloseHandle(reqReadLocal);
                if (reqWriteLocal != IntPtr.Zero) WinApi.CloseHandle(reqWriteLocal);
                if (respReadLocal != IntPtr.Zero) WinApi.CloseHandle(respReadLocal);
                if (respWriteLocal != IntPtr.Zero) WinApi.CloseHandle(respWriteLocal);
                // If we failed after duplicating but before ownership transfer, we cannot
                // easily close remote handles without a remote CloseHandle; attach failure
                // leaves them for process exit. Successful attach transfers ownership to the hook.
            }
        }

        public SqlProxyPong Ping(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            lock (ioLock)
            {
                uint id = nextRequestId++;
                WriteFrame(SqlProxyMsgType.Ping, id, Array.Empty<byte>());
                var (msgType, requestId, payload) = ReadFrame(cancellationToken);
                if (requestId != id)
                {
                    throw new InvalidOperationException($"SqlProxy.Ping: response id {requestId} != {id}");
                }

                if (msgType == SqlProxyMsgType.Error)
                {
                    var err = ReadErrorPayload(payload);
                    return new SqlProxyPong { Status = err.status, DbType = null };
                }

                if (msgType != SqlProxyMsgType.Pong && msgType != SqlProxyMsgType.Hello)
                {
                    throw new InvalidOperationException($"SqlProxy.Ping: unexpected msgType {msgType}");
                }

                var reader = new PayloadReader(payload);
                uint status = reader.ReadUInt32();
                string? dbType = reader.Remaining >= 4 ? reader.ReadWString() : null;
                return new SqlProxyPong
                {
                    Status = (SqlProxyStatus)status,
                    DbType = string.IsNullOrEmpty(dbType) ? null : dbType,
                };
            }
        }

        /// <summary>
        /// Convenience: string binds; result layout from SAM describe (SamNumResultCols / SamDescribeCol).
        /// </summary>
        public SqlProxyResult ExecSql(
            string sql,
            IReadOnlyList<string>? binds = null,
            int maxRows = 100,
            uint defaultStringChars = 512,
            CancellationToken cancellationToken = default)
        {
            var bindList = binds?.Select(SqlProxyBind.String).ToList() ?? new List<SqlProxyBind>();
            return ExecSql(sql, bindList, columnOverrides: null, maxRows, defaultStringChars, cancellationToken);
        }

        /// <summary>
        /// Preferred ExecSql: binds typed; columns discovered via SAM describe unless
        /// <paramref name="columnOverrides"/> is non-empty (test/legacy override only).
        /// </summary>
        public SqlProxyResult ExecSql(
            string sql,
            IReadOnlyList<SqlProxyBind>? binds,
            IReadOnlyList<SqlProxyColumnSpec>? columnOverrides = null,
            int maxRows = 100,
            uint defaultStringChars = 512,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(sql))
            {
                return new SqlProxyResult
                {
                    Status = SqlProxyStatus.BadRequest,
                    ErrorMessage = "Empty SQL",
                };
            }

            if (!IsSelectOnly(sql))
            {
                return new SqlProxyResult
                {
                    Status = SqlProxyStatus.Rejected,
                    ErrorMessage = "Only SELECT statements are allowed",
                };
            }

            lock (ioLock)
            {
                uint id = nextRequestId++;
                byte[] payload = BuildExecSqlPayload(sql, binds, columnOverrides, maxRows, defaultStringChars);
                WriteFrame(SqlProxyMsgType.ExecSql, id, payload);
                var (msgType, requestId, responsePayload) = ReadFrame(cancellationToken);
                if (requestId != id)
                {
                    throw new InvalidOperationException($"SqlProxy.ExecSql: response id {requestId} != {id}");
                }

                if (msgType == SqlProxyMsgType.Error)
                {
                    var err = ReadErrorPayload(responsePayload);
                    return new SqlProxyResult
                    {
                        Status = err.status,
                        ErrorMessage = err.message,
                    };
                }

                if (msgType != SqlProxyMsgType.ExecSqlResult)
                {
                    throw new InvalidOperationException($"SqlProxy.ExecSql: unexpected msgType {msgType}");
                }

                return ParseExecSqlResult(responsePayload);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            try
            {
                requestWrite?.Dispose();
            }
            catch { /* ignore */ }
            requestWrite = null;

            try
            {
                responseRead?.Dispose();
            }
            catch { /* ignore */ }
            responseRead = null;

            try
            {
                attachBuffer?.Free();
            }
            catch { /* ignore */ }
            attachBuffer = null;
        }

        #region Framing

        private void WriteFrame(SqlProxyMsgType msgType, uint requestId, byte[] payload)
        {
            if (requestWrite == null || requestWrite.IsInvalid)
            {
                throw new ObjectDisposedException(nameof(SqlProxy));
            }

            if (payload.Length > SqlProxyProtocol.MaxFrameBytes - SqlProxyProtocol.FrameHeaderSize)
            {
                throw new InvalidOperationException("SqlProxy payload too large");
            }

            var hdr = new SqlProxyFrameHeader
            {
                Magic = SqlProxyProtocol.Magic,
                Version = SqlProxyProtocol.Version,
                MsgType = (uint)msgType,
                RequestId = requestId,
                Flags = 0,
                PayloadLen = (uint)payload.Length,
            };

            byte[] headerBytes = StructureToBytes(hdr);
            WriteAll(requestWrite.DangerousGetHandle(), headerBytes);
            if (payload.Length > 0)
            {
                WriteAll(requestWrite.DangerousGetHandle(), payload);
            }
        }

        private (SqlProxyMsgType msgType, uint requestId, byte[] payload) ReadFrame(CancellationToken cancellationToken)
        {
            if (responseRead == null || responseRead.IsInvalid)
            {
                throw new ObjectDisposedException(nameof(SqlProxy));
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] headerBytes = ReadAll(responseRead.DangerousGetHandle(), SqlProxyProtocol.FrameHeaderSize);
            var hdr = BytesToStructure<SqlProxyFrameHeader>(headerBytes);

            if (hdr.Magic != SqlProxyProtocol.Magic)
            {
                throw new InvalidOperationException("SqlProxy: bad response magic");
            }

            if (hdr.Version != SqlProxyProtocol.Version)
            {
                throw new InvalidOperationException($"SqlProxy: version mismatch ({hdr.Version})");
            }

            if (hdr.PayloadLen > SqlProxyProtocol.MaxFrameBytes - SqlProxyProtocol.FrameHeaderSize)
            {
                throw new InvalidOperationException("SqlProxy: response payload too large");
            }

            byte[] payload = hdr.PayloadLen == 0
                ? Array.Empty<byte>()
                : ReadAll(responseRead.DangerousGetHandle(), (int)hdr.PayloadLen);

            return ((SqlProxyMsgType)hdr.MsgType, hdr.RequestId, payload);
        }

        private static byte[] BuildExecSqlPayload(
            string sql,
            IReadOnlyList<SqlProxyBind>? binds,
            IReadOnlyList<SqlProxyColumnSpec>? columnOverrides,
            int maxRows,
            uint defaultStringChars)
        {
            var w = new PayloadWriter();
            w.WriteUInt32(0); // flags
            w.WriteWString(sql);

            int bindCount = binds?.Count ?? 0;
            w.WriteUInt32((uint)bindCount);
            if (binds != null)
            {
                foreach (var b in binds)
                {
                    w.WriteUInt32((uint)b.Type);
                    if (b.Type == SqlProxyValueType.Int32)
                    {
                        w.WriteInt32(b.IntValue);
                    }
                    else
                    {
                        w.WriteWString(b.StringValue ?? string.Empty);
                    }
                }
            }

            // colCount 0 = SAM describe path (preferred)
            int colCount = columnOverrides?.Count ?? 0;
            w.WriteUInt32((uint)colCount);
            if (columnOverrides != null)
            {
                foreach (var col in columnOverrides)
                {
                    w.WriteUInt32((uint)col.Type);
                    w.WriteUInt32(col.MaxSize);
                }
            }

            w.WriteUInt32((uint)Math.Max(1, maxRows));
            w.WriteUInt32(defaultStringChars == 0 ? 512u : defaultStringChars);
            return w.ToArray();
        }

        private static SqlProxyResult ParseExecSqlResult(byte[] payload)
        {
            var r = new PayloadReader(payload);
            var status = (SqlProxyStatus)r.ReadUInt32();
            int samRc = unchecked((int)r.ReadUInt32());
            string error = r.ReadWString();
            int colCount = (int)r.ReadUInt32();

            var columns = new List<SqlProxyColumnInfo>(colCount);
            for (int i = 0; i < colCount; i++)
            {
                columns.Add(new SqlProxyColumnInfo
                {
                    Name = r.ReadWString(),
                    Type = (SqlProxyValueType)r.ReadUInt32(),
                    SamTypeId = r.ReadInt32(),
                });
            }

            int rowCount = (int)r.ReadUInt32();

            var cells = new List<SqlProxyCell>();
            int expected = colCount * rowCount;
            for (int i = 0; i < expected; i++)
            {
                var cellType = (SqlProxyValueType)r.ReadUInt32();
                switch (cellType)
                {
                    case SqlProxyValueType.String:
                        cells.Add(new SqlProxyCell
                        {
                            Type = SqlProxyValueType.String,
                            StringValue = r.ReadWString(),
                        });
                        break;
                    case SqlProxyValueType.Int32:
                        cells.Add(new SqlProxyCell
                        {
                            Type = SqlProxyValueType.Int32,
                            IntValue = r.ReadInt32(),
                        });
                        break;
                    case SqlProxyValueType.LongRaw:
                        cells.Add(new SqlProxyCell
                        {
                            Type = SqlProxyValueType.LongRaw,
                            Bytes = r.ReadBytes(),
                        });
                        break;
                    case SqlProxyValueType.LongText:
                        cells.Add(new SqlProxyCell
                        {
                            Type = SqlProxyValueType.LongText,
                            StringValue = r.ReadWString(),
                            Bytes = r.ReadBytes(),
                        });
                        break;
                    default:
                        cells.Add(new SqlProxyCell { Type = SqlProxyValueType.Null });
                        break;
                }
            }

            return new SqlProxyResult
            {
                Status = status,
                SamReturnCode = samRc,
                ErrorMessage = string.IsNullOrEmpty(error) ? null : error,
                Columns = columns,
                ColumnCount = colCount,
                RowCount = rowCount,
                Cells = cells,
            };
        }

        private static (SqlProxyStatus status, string message) ReadErrorPayload(byte[] payload)
        {
            var r = new PayloadReader(payload);
            var status = (SqlProxyStatus)r.ReadUInt32();
            string message = r.Remaining >= 4 ? r.ReadWString() : string.Empty;
            return (status, message);
        }

        #endregion

        #region Helpers

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (requestWrite == null || responseRead == null)
            {
                throw new ObjectDisposedException(nameof(SqlProxy));
            }
        }

        public static bool IsSelectOnly(string sql)
        {
            ReadOnlySpan<char> s = sql.AsSpan().TrimStart();
            if (s.Length < 6) return false;
            if (!s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.Length == 6) return true;
            char next = s[6];
            return char.IsWhiteSpace(next) || next == '(';
        }

        private static bool EventHookInstallerPostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return Events.EventHookInstaller.PostThreadMessage(threadId, msg, wParam, lParam);
        }

        private static void WriteAll(IntPtr handle, byte[] data)
        {
            // Anonymous pipes are not overlapped. On the UI STA thread we must not block
            // inside WriteFile: the hook's PostThreadMessage(PUMP) path needs this thread
            // able to complete cross-thread SendMessage from App Designer (otherwise the
            // AD UI thread can stall inside Sam* and never run the pump → deadlock).
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                RunPipeIoWithSentMessageDispatch(() => WriteAllBlocking(handle, data));
                return;
            }

            WriteAllBlocking(handle, data);
        }

        private static void WriteAllBlocking(IntPtr handle, byte[] data)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                if (!WinApi.WriteFile(handle, data, offset, data.Length - offset, out int written) || written <= 0)
                {
                    throw new IOException($"SqlProxy WriteFile failed err={Marshal.GetLastWin32Error()}");
                }
                offset += written;
            }
        }

        private static byte[] ReadAll(IntPtr handle, int size)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return RunPipeIoWithSentMessageDispatch(() => ReadAllBlocking(handle, size));
            }

            return ReadAllBlocking(handle, size);
        }

        private static byte[] ReadAllBlocking(IntPtr handle, int size)
        {
            byte[] buffer = new byte[size];
            int offset = 0;
            while (offset < size)
            {
                if (!WinApi.ReadFile(handle, buffer, offset, size - offset, out int read) || read <= 0)
                {
                    throw new IOException($"SqlProxy ReadFile failed err={Marshal.GetLastWin32Error()}");
                }
                offset += read;
            }
            return buffer;
        }

        /// <summary>
        /// Runs blocking pipe IO on a worker thread while the calling STA thread dispatches
        /// only cross-thread <c>SendMessage</c> (not posted UI work). Required so App Designer
        /// can finish nested sends while the SQL proxy waits for <c>WM_AR_SQLPROXY_PUMP</c>.
        /// </summary>
        private static void RunPipeIoWithSentMessageDispatch(Action action)
        {
            Exception? error = null;
            using var done = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    done.Set();
                }
            });

            WinApi.WaitForHandleDispatchingSentMessages(done);
            if (error != null)
            {
                throw error;
            }
        }

        private static T RunPipeIoWithSentMessageDispatch<T>(Func<T> func)
        {
            T? result = default;
            Exception? error = null;
            using var done = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    result = func();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    done.Set();
                }
            });

            WinApi.WaitForHandleDispatchingSentMessages(done);
            if (error != null)
            {
                throw error;
            }

            return result!;
        }

        private static byte[] StructureToBytes<T>(T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }

        private static T BytesToStructure<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length < size)
            {
                throw new ArgumentException("Buffer too small for structure");
            }
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr)!;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        #endregion

        #region Payload codec

        private sealed class PayloadWriter
        {
            private readonly MemoryStream stream = new();

            public void WriteUInt32(uint value)
            {
                stream.Write(BitConverter.GetBytes(value), 0, 4);
            }

            public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

            public void WriteWString(string value)
            {
                value ??= string.Empty;
                WriteUInt32((uint)value.Length);
                byte[] utf16 = Encoding.Unicode.GetBytes(value);
                stream.Write(utf16, 0, utf16.Length);
            }

            public byte[] ToArray() => stream.ToArray();
        }

        private sealed class PayloadReader
        {
            private readonly byte[] data;
            private int pos;

            public PayloadReader(byte[] data)
            {
                this.data = data;
            }

            public int Remaining => data.Length - pos;

            public uint ReadUInt32()
            {
                if (pos + 4 > data.Length) throw new InvalidOperationException("Payload underrun (u32)");
                uint v = BitConverter.ToUInt32(data, pos);
                pos += 4;
                return v;
            }

            public int ReadInt32() => unchecked((int)ReadUInt32());

            public string ReadWString()
            {
                uint cch = ReadUInt32();
                int bytes = checked((int)cch * 2);
                if (pos + bytes > data.Length) throw new InvalidOperationException("Payload underrun (wstring)");
                string s = Encoding.Unicode.GetString(data, pos, bytes);
                pos += bytes;
                return s;
            }

            public byte[] ReadBytes()
            {
                uint len = ReadUInt32();
                int n = checked((int)len);
                if (pos + n > data.Length) throw new InvalidOperationException("Payload underrun (bytes)");
                byte[] b = new byte[n];
                Buffer.BlockCopy(data, pos, b, 0, n);
                pos += n;
                return b;
            }
        }

        /// <summary>
        /// Thin wrapper so we always CloseHandle pipe ends we own.
        /// </summary>
        private sealed class SafePipeHandle : IDisposable
        {
            private IntPtr handle;
            private readonly bool ownsHandle;

            public SafePipeHandle(IntPtr handle, bool ownsHandle)
            {
                this.handle = handle;
                this.ownsHandle = ownsHandle;
            }

            public bool IsInvalid => handle == IntPtr.Zero;

            public IntPtr DangerousGetHandle() => handle;

            public void Dispose()
            {
                if (ownsHandle && handle != IntPtr.Zero)
                {
                    WinApi.CloseHandle(handle);
                    handle = IntPtr.Zero;
                }
            }
        }

        #endregion
    }
}

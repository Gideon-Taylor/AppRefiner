using System.Runtime.InteropServices;

namespace AppRefiner.Database.SqlProxy
{
    /// <summary>
    /// Shared wire protocol with AppRefinerHook (SqlProxyProtocol.h). Keep values identical.
    /// </summary>
    public static class SqlProxyProtocol
    {
        public const uint Magic = 0x51535241; // 'ARSQ'
        public const uint Version = 1;
        public const int MaxFrameBytes = 16 * 1024 * 1024;
        public const int FrameHeaderSize = 24;
        public const int AttachInfoSize = 32;

        public const uint WM_USER = 0x0400;
        public const uint WM_AR_SQLPROXY_ATTACH = WM_USER + 1013;
        public const uint WM_AR_SQLPROXY_PUMP = WM_USER + 1014;
    }

    public enum SqlProxyMsgType : uint
    {
        Hello = 1,
        Ping = 2,
        Pong = 3,
        ExecSql = 4,
        ExecSqlResult = 5,
        Error = 6,
    }

    public enum SqlProxyStatus : uint
    {
        Ok = 0,
        NotAttached = 1,
        NotSignedOn = 2,
        SamError = 3,
        Busy = 4,
        BadRequest = 5,
        Rejected = 6,
        Truncated = 7,
        VersionMismatch = 8,
        Disconnected = 9,
    }

    /// <summary>Bind / column / cell type codes (see SQL-Proxy-Type-Catalog.md).</summary>
    public enum SqlProxyValueType : uint
    {
        Null = 0,
        String = 1,
        Int32 = 2,
        LongRaw = 3,
        LongText = 4,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SqlProxyFrameHeader
    {
        public uint Magic;
        public uint Version;
        public uint MsgType;
        public uint RequestId;
        public uint Flags;
        public uint PayloadLen;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SqlProxyAttachInfo
    {
        public uint Magic;
        public uint Version;
        public uint StructSize;
        public uint Flags;
        public ulong RequestReadHandle;
        public ulong ResponseWriteHandle;
    }
}

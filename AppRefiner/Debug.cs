using System;
using AppRefiner.Dialogs;

namespace AppRefiner
{
    /// <summary>
    /// Static class for debugging functionality throughout the application
    /// </summary>
    public static class Debug
    {
        /// <summary>
        /// Opens the debug dialog
        /// </summary>
        /// <param name="parentHandle">Handle to the parent window</param>
        /// <returns>The debug dialog instance</returns>
        public static DebugDialog ShowDebugDialog(IntPtr parentHandle)
        {
            return DebugDialog.ShowDialog(parentHandle);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message)
        {
            DebugDialog.Log(message, DebugMessageType.Info);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message to log</param>
        public static void LogWarning(string message)
        {
            DebugDialog.Log(message, DebugMessageType.Warning);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">The error message to log</param>
        public static void LogError(string message)
        {
            DebugDialog.Log(message, DebugMessageType.Error);
        }

        /// <summary>
        /// Logs an exception with stack trace
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <param name="context">Optional context information</param>
        public static void LogException(Exception ex, string context = "")
        {
            string message = string.IsNullOrEmpty(context) 
                ? $"Exception: {ex.Message}\n{ex.StackTrace}" 
                : $"Exception in {context}: {ex.Message}\n{ex.StackTrace}";
                
            DebugDialog.Log(message, DebugMessageType.Error);
            
            // Log inner exceptions if they exist
            if (ex.InnerException != null)
            {
                LogException(ex.InnerException, "Inner Exception");
            }
        }
    }
} 
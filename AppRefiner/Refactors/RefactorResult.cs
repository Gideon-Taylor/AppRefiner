namespace AppRefiner.Refactors
{
    /// <summary>
    /// Represents the result of a refactoring operation
    /// </summary>
    public class RefactorResult
    {
        /// <summary>
        /// Whether the refactoring was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Optional message providing details about the result
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Creates a new refactoring result
        /// </summary>
        /// <param name="success">Whether the refactoring was successful</param>
        /// <param name="message">Optional message providing details</param>
        public RefactorResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static RefactorResult Successful => new(true);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        public static RefactorResult Failed(string message) => new(false, message);
    }
}

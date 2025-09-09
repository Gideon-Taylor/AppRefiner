namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents a snapshot of file content in the database
    /// </summary>
    public class Snapshot
    {
        /// <summary>
        /// Gets or sets the unique identifier of the snapshot
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the database name associated with this snapshot
        /// </summary>
        public string? DBName { get; set; }

        /// <summary>
        /// Gets or sets the relative file path of the snapshot
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title or caption of the file when it was saved
        /// </summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time when the snapshot was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the content of the file at the time of the snapshot
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets a formatted description of the snapshot suitable for display in a list
        /// </summary>
        public string Description => $"{CreatedAt:yyyy-MM-dd HH:mm:ss}: {Caption}";
    }
}
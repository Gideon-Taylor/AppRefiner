namespace AppRefiner.Database.Models
{
    /// <summary>
    /// Represents an HTML definition with its content and bind information
    /// </summary>
    public class HtmlDefinition
    {
        /// <summary>
        /// Gets the HTML content
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Gets the maximum bind number found in the HTML
        /// </summary>
        public int BindCount { get; }

        /// <summary>
        /// Creates a new HTML definition
        /// </summary>
        /// <param name="content">The HTML content</param>
        /// <param name="bindCount">The maximum bind number</param>
        public HtmlDefinition(string content, int bindCount)
        {
            Content = content;
            BindCount = bindCount;
        }
    }
}

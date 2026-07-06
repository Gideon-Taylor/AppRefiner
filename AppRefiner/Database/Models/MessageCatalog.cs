namespace AppRefiner.Database.Models
{
    /// <summary>
    /// A Message Catalog set (one PSMSGSETDEFN row).
    /// </summary>
    public class MessageSetInfo
    {
        public int SetNumber { get; }
        public string Description { get; }

        public MessageSetInfo(int setNumber, string description)
        {
            SetNumber = setNumber;
            Description = description;
        }
    }

    /// <summary>
    /// A Message Catalog entry (one PSMSGCATDEFN row).
    /// </summary>
    public class MessageCatalogEntry
    {
        public int SetNumber { get; }
        public int MessageNumber { get; }

        /// <summary>Raw MSG_SEVERITY code: M, W, E, or C.</summary>
        public string SeverityCode { get; }

        public string MessageText { get; }

        /// <summary>Long explain text (DESCRLONG); empty when none.</summary>
        public string ExplainText { get; }

        public string Severity => SeverityCode switch
        {
            "M" => "Message",
            "W" => "Warning",
            "E" => "Error",
            "C" => "Cancel",
            _ => SeverityCode
        };

        public MessageCatalogEntry(int setNumber, int messageNumber, string severityCode,
            string messageText, string explainText)
        {
            SetNumber = setNumber;
            MessageNumber = messageNumber;
            SeverityCode = severityCode;
            MessageText = messageText;
            ExplainText = explainText;
        }
    }
}

namespace AppRefiner
{
    /// <summary>
    /// Argument positions (0-based) of the message_set / message_num / default_msg_txt
    /// parameters for a built-in function that reads the Message Catalog.
    /// ColdLeadingArgs holds placeholder text for the arguments that precede message_set,
    /// used when inserting a complete call from scratch (empty for most functions).
    /// </summary>
    public record MessageCatalogArgInfo(int SetArg, int NumArg, int DefaultTxtArg, string ColdLeadingArgs);

    /// <summary>
    /// The exhaustive set of built-in functions that take message_set/message_num
    /// parameters, and where those parameters sit in each signature:
    ///
    ///   CreateException(message_set, message_num, default_txt, any*)
    ///   MessageBox(style, title, message_set, message_num, default_msg_txt, paramlist*)
    ///   MsgBoxButtonOverride(style, title, array_of_button_labels, message_set, message_num, default_msg_txt, paramlist*)
    ///   MsgGet(message_set, message_num, default_msg_txt, any*)
    ///   MsgGetExplainText(message_set, message_num, default_msg_txt, any*)
    ///   MsgGetText(message_set, message_num, default_msg_txt, any*)
    ///
    /// Single source of truth for the tooltip provider, the Ctrl+Space detection,
    /// and the catalog dialog's insert logic.
    /// </summary>
    public static class MessageCatalogFunctions
    {
        private static readonly Dictionary<string, MessageCatalogArgInfo> Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["MsgGet"] = new(0, 1, 2, ""),
                ["MsgGetText"] = new(0, 1, 2, ""),
                ["MsgGetExplainText"] = new(0, 1, 2, ""),
                ["CreateException"] = new(0, 1, 2, ""),
                ["MessageBox"] = new(2, 3, 4, "0, \"\", "),
                ["MsgBoxButtonOverride"] = new(3, 4, 5, "0, \"\", CreateArray(\"OK\"), "),
            };

        /// <summary>All mapped function names, for UI pickers (alphabetical).</summary>
        public static IReadOnlyList<string> FunctionNames { get; } =
            Map.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        public static bool TryGetArgPositions(string functionName, out MessageCatalogArgInfo info)
        {
            return Map.TryGetValue(functionName, out info!);
        }
    }
}

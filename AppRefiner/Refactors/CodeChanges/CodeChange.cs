namespace AppRefiner.Refactors.CodeChanges
{
    /// <summary>
    /// Base class for all code changes that can be applied during refactoring
    /// </summary>
    public abstract class CodeChange
    {
        /// <summary>
        /// Starting position in the source text where the change begins
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Description of the change being made
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Applies the change to a StringBuilder containing the source text
        /// </summary>
        public abstract void Apply(System.Text.StringBuilder source);
    }

    public class DeleteChange : CodeChange
    {
        public int EndIndex { get; set; }

        public override void Apply(System.Text.StringBuilder source)
        {
            source.Remove(StartIndex, EndIndex - StartIndex);
        }
    }

    public class InsertChange : CodeChange
    {
        public string TextToInsert { get; set; }
        public InsertChange(string textToInsert)
        {
            TextToInsert = textToInsert;
        }

        public override void Apply(System.Text.StringBuilder source)
        {
            source.Insert(StartIndex, TextToInsert);
        }
    }

    public class ReplaceChange : CodeChange
    {
        public int EndIndex { get; set; }
        public string NewText { get; set; }

        public ReplaceChange(int endIndex, string newText)
        {
            EndIndex = endIndex;
            NewText = newText;
        }

        public override void Apply(System.Text.StringBuilder source)
        {
            source.Remove(StartIndex, EndIndex - StartIndex);
            source.Insert(StartIndex, NewText);
        }
    }
}

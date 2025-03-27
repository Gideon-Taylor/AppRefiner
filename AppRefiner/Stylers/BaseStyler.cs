using AppRefiner.PeopleCode;

namespace AppRefiner.Stylers
{
    public struct CodeAnnotation
    {
        public string Message;
        public int LineNumber;
    }

    public struct CodeHighlight
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public uint Color { get; set; }
        public string? Tooltip { get; set; }
    }

    public struct CodeColor
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public FontColor Color { get; set; }
    }

    public abstract class BaseStyler : PeopleCodeParserBaseListener
    {
        public List<CodeAnnotation>? Annotations;
        public List<CodeHighlight>? Highlights;
        public List<CodeColor>? Colors;
        public List<Antlr4.Runtime.IToken>? Comments;
        public abstract void Reset();

        public bool Active = false;
        public string Description = "Description not set";

    }
}

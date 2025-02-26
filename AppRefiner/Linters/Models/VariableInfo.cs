namespace AppRefiner.Linters.Models
{
    public class VariableInfo
    {
        public string Name { get; }
        public string Type { get; }
        public int Line { get; }
        public (int Start, int Stop) Span { get; }
        public bool Used { get; set; }

        public VariableInfo(string name, string type, int line, (int Start, int Stop) span)
        {
            Name = name;
            Type = type;
            Line = line;
            Span = span;
            Used = false;
        }
    }
}

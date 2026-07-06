namespace AppRefiner
{
    /// <summary>A free run of message numbers. End == null means open-ended.</summary>
    public record MessageNumberRange(int Start, int? End)
    {
        public string Label => End == null
            ? $"{Start}+ (open)"
            : $"{Start}–{End} ({End.Value - Start + 1} free)";
    }

    /// <summary>
    /// Computes the free number ranges of a message set from its used numbers.
    /// Message numbers start at 1. An empty set yields a single open range at 1.
    /// </summary>
    public static class MessageCatalogFreeRanges
    {
        public static List<MessageNumberRange> Compute(IReadOnlyCollection<int> usedNumbers)
        {
            var ranges = new List<MessageNumberRange>();
            var sorted = usedNumbers.Distinct().OrderBy(n => n).ToList();

            if (sorted.Count == 0)
            {
                ranges.Add(new MessageNumberRange(1, null));
                return ranges;
            }

            if (sorted[0] > 1)
            {
                ranges.Add(new MessageNumberRange(1, sorted[0] - 1));
            }

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i + 1] > sorted[i] + 1)
                {
                    ranges.Add(new MessageNumberRange(sorted[i] + 1, sorted[i + 1] - 1));
                }
            }

            ranges.Add(new MessageNumberRange(sorted[^1] + 1, null));
            return ranges;
        }
    }
}

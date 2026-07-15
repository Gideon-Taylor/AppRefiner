namespace AppRefiner.Database
{
    /// <summary>
    /// Per-connection cache of Application Class existence and decoded source text.
    /// Keys are qualified paths (e.g. <c>PKG:SUB:Class</c>), case-insensitive.
    /// Evict on save of that program via <see cref="Invalidate"/>.
    /// </summary>
    internal sealed class AppClassCatalogCache
    {
        private sealed class Entry
        {
            public bool? Exists;
            public bool SourceResolved;
            public string? Source;
        }

        private readonly object gate = new();
        private readonly Dictionary<string, Entry> entries =
            new(StringComparer.OrdinalIgnoreCase);

        public static string NormalizeKey(string appClassPath) =>
            (appClassPath ?? string.Empty).Trim();

        public bool TryGetExists(string appClassPath, out bool exists)
        {
            exists = false;
            string key = NormalizeKey(appClassPath);
            if (key.Length == 0) return false;

            lock (gate)
            {
                if (!entries.TryGetValue(key, out var entry) || entry.Exists is null)
                {
                    return false;
                }

                exists = entry.Exists.Value;
                return true;
            }
        }

        public void SetExists(string appClassPath, bool exists)
        {
            string key = NormalizeKey(appClassPath);
            if (key.Length == 0) return;

            lock (gate)
            {
                var entry = GetOrCreate_NoLock(key);
                entry.Exists = exists;
                if (!exists)
                {
                    entry.SourceResolved = true;
                    entry.Source = null;
                }
            }
        }

        /// <summary>
        /// True when source has been fetched (including a confirmed miss with <c>source == null</c>).
        /// </summary>
        public bool TryGetSource(string appClassPath, out string? source)
        {
            source = null;
            string key = NormalizeKey(appClassPath);
            if (key.Length == 0) return false;

            lock (gate)
            {
                if (!entries.TryGetValue(key, out var entry) || !entry.SourceResolved)
                {
                    return false;
                }

                source = entry.Source;
                return true;
            }
        }

        public void SetSource(string appClassPath, string? source)
        {
            string key = NormalizeKey(appClassPath);
            if (key.Length == 0) return;

            lock (gate)
            {
                var entry = GetOrCreate_NoLock(key);
                entry.SourceResolved = true;
                entry.Source = string.IsNullOrEmpty(source) ? null : source;
                entry.Exists = entry.Source != null;
            }
        }

        /// <summary>
        /// Removes one path, or clears the entire cache when <paramref name="appClassPath"/> is null/empty.
        /// </summary>
        public void Invalidate(string? appClassPath = null)
        {
            lock (gate)
            {
                if (string.IsNullOrWhiteSpace(appClassPath))
                {
                    entries.Clear();
                    return;
                }

                entries.Remove(NormalizeKey(appClassPath));
            }
        }

        public void Clear() => Invalidate(null);

        private Entry GetOrCreate_NoLock(string key)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                entries[key] = entry;
            }
            return entry;
        }
    }
}

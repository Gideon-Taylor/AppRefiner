using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests.Infrastructure;

internal sealed class TestProgramSourceProvider : IProgramSourceProvider
{
    private readonly ConcurrentDictionary<string, string> _sources;

    public TestProgramSourceProvider(IEnumerable<KeyValuePair<string, string>> sources)
    {
        _sources = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sources)
        {
            _sources[pair.Key] = pair.Value;
        }
    }

    public Task<(bool found, string? source)> TryGetProgramSourceAsync(string qualifiedName)
    {
        if (_sources.TryGetValue(qualifiedName, out var source))
        {
            return Task.FromResult<(bool, string?)>((true, source));
        }

        return Task.FromResult<(bool, string?)>((false, null));
    }
}

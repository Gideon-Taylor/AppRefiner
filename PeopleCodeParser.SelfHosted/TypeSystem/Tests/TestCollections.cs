using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

[CollectionDefinition("TypeSystemCache", DisableParallelization = true)]
public sealed class TypeSystemCacheCollection : ICollectionFixture<TypeSystemCacheFixture>
{
}

public sealed class TypeSystemCacheFixture
{
}

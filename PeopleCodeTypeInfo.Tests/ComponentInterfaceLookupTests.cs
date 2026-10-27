using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// The resolver's cached three-state component interface lookup, and the definition contract's
/// path, property and standard-method helpers. Not parallel: some tests change the process-wide
/// <see cref="ITypeMetadataResolver.ComponentInterfaceRecheckInterval"/>.
/// </summary>
[Collection(ComponentInterfaceRecheckCollection.Name)]
public class ComponentInterfaceLookupTests
{
    /// <summary>
    /// Counts Core calls and answers with a scripted lookup (or throws).
    /// </summary>
    private sealed class ScriptedResolver : ITypeMetadataResolver
    {
        public int CoreCalls;
        public int FailuresReported;
        public Func<string, CompIntfcLookup> Script = _ => CompIntfcLookup.Unknown;

        protected override void OnComponentInterfaceLookupFailed(string name, Exception exception) => FailuresReported++;

        protected override CompIntfcLookup GetComponentInterfaceCore(string name)
        {
            CoreCalls++;
            return Script(name);
        }

        protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName) => null;
        protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName) => Task.FromResult<TypeMetadata?>(null);
        protected override TypeInfo GetFieldTypeCore(string fieldName) => AnyTypeInfo.Instance;
        protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName) => Task.FromResult<TypeInfo>(AnyTypeInfo.Instance);
        protected override List<string> GetClassesInPackageCore(string packagePath) => new();
    }

    private static CompIntfcProperty Prop(string name, string record = "PSOPRDEFN", bool readOnly = false)
        => new(name, record, name, PeopleCodeType.String, readOnly, null);

    private static Dictionary<string, T> Dict<T>(params (string Key, T Value)[] items)
    {
        var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in items) dict[key] = value;
        return dict;
    }

    /// <summary>
    /// A USER_PROFILE-shaped definition: UserID is a key only, Roles has a nested RouteControls
    /// collection, and Cancel is the only standard method not enabled.
    /// </summary>
    private static CompIntfcDefinition UserProfile(int version = 1)
    {
        var routeControls = new CompIntfcCollection
        {
            Name = "RouteControls",
            RecordName = "PSROLEUSER_RTE",
            Properties = Dict(("RouteControlProfile", Prop("RouteControlProfile", "PSROLEUSER_RTE"))),
        };
        var roles = new CompIntfcCollection
        {
            Name = "Roles",
            RecordName = "PSROLEUSER_VW",
            Properties = Dict(("RoleName", Prop("RoleName", "PSROLEUSER_VW"))),
            Collections = Dict(("RouteControls", routeControls)),
        };
        return new CompIntfcDefinition
        {
            Name = "USER_PROFILE",
            Version = version,
            StandardMethods = CompIntfcStandardMethods.Create | CompIntfcStandardMethods.Find
                | CompIntfcStandardMethods.Get | CompIntfcStandardMethods.Save,
            Properties = Dict(("UserDescription", Prop("UserDescription")), ("LastSignonDateTime", Prop("LastSignonDateTime", readOnly: true))),
            Collections = Dict(("Roles", roles)),
            GetKeys = new[] { Prop("UserID") },
            CreateKeys = new[] { Prop("UserID") },
            FindKeys = new[] { Prop("UserID"), Prop("FindOnlyKey") },
            CustomMethods = Dict(("ResetPassword", new CompIntfcMethod("ResetPassword", new FunctionInfo { Name = "ResetPassword" }))),
        };
    }

    [Fact]
    public void Found_is_cached()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };

        var first = resolver.GetComponentInterface("USER_PROFILE");
        var second = resolver.GetComponentInterface("USER_PROFILE");

        Assert.Equal(CompIntfcLookupStatus.Found, first.Status);
        Assert.Same(first.Definition, second.Definition);
        Assert.Equal(1, resolver.CoreCalls);
        Assert.Contains("USER_PROFILE", resolver.CachedComponentInterfaces);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };

        resolver.GetComponentInterface("user_profile");
        var lookup = resolver.GetComponentInterface("USER_PROFILE");

        Assert.Equal(CompIntfcLookupStatus.Found, lookup.Status);
        Assert.Equal(1, resolver.CoreCalls);
    }

    [Fact]
    public void Missing_is_cached_and_reported()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Missing };

        var first = resolver.GetComponentInterface("NO_SUCH_CI");
        var second = resolver.GetComponentInterface("NO_SUCH_CI");

        Assert.Equal(CompIntfcLookupStatus.Missing, first.Status);
        Assert.Equal(CompIntfcLookupStatus.Missing, second.Status);
        Assert.Null(second.Definition);
        Assert.Equal(1, resolver.CoreCalls);
    }

    [Fact]
    public void Unknown_is_not_cached()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Unknown };

        resolver.GetComponentInterface("USER_PROFILE");
        var lookup = resolver.GetComponentInterface("USER_PROFILE");

        Assert.Equal(CompIntfcLookupStatus.Unknown, lookup.Status);
        Assert.Equal(2, resolver.CoreCalls);
        Assert.Empty(resolver.CachedComponentInterfaces);
    }

    [Fact]
    public void Core_exception_is_unknown()
    {
        var resolver = new ScriptedResolver { Script = _ => throw new InvalidOperationException("db down") };

        var lookup = resolver.GetComponentInterface("USER_PROFILE");

        Assert.Equal(CompIntfcLookupStatus.Unknown, lookup.Status);
        Assert.Null(lookup.Definition);
        Assert.Empty(resolver.CachedComponentInterfaces);
    }

    [Fact]
    public void Exception_unknown_is_negative_cached_for_ttl()
    {
        var resolver = new ScriptedResolver { Script = _ => throw new InvalidOperationException("db down") };

        var first = resolver.GetComponentInterface("USER_PROFILE");
        var second = resolver.GetComponentInterface("user_profile");

        Assert.Equal(CompIntfcLookupStatus.Unknown, first.Status);
        Assert.Equal(CompIntfcLookupStatus.Unknown, second.Status);
        Assert.Equal(1, resolver.CoreCalls);
        Assert.Equal(1, resolver.FailuresReported);
        Assert.Empty(resolver.CachedComponentInterfaces);
    }

    [Fact]
    public void Exception_unknown_retries_after_ttl()
    {
        var original = ITypeMetadataResolver.ComponentInterfaceFailureRetryInterval;
        try
        {
            ITypeMetadataResolver.ComponentInterfaceFailureRetryInterval = TimeSpan.Zero;
            var resolver = new ScriptedResolver { Script = _ => throw new InvalidOperationException("db down") };

            resolver.GetComponentInterface("USER_PROFILE");
            var second = resolver.GetComponentInterface("USER_PROFILE");

            Assert.Equal(CompIntfcLookupStatus.Unknown, second.Status);
            Assert.Equal(2, resolver.CoreCalls);
        }
        finally
        {
            ITypeMetadataResolver.ComponentInterfaceFailureRetryInterval = original;
        }
    }

    [Fact]
    public void Invalidate_clears_negative_cache()
    {
        var resolver = new ScriptedResolver { Script = _ => throw new InvalidOperationException("db down") };

        resolver.GetComponentInterface("USER_PROFILE");
        resolver.InvalidateComponentInterface("user_profile");
        resolver.GetComponentInterface("USER_PROFILE");
        Assert.Equal(2, resolver.CoreCalls);

        resolver.InvalidateComponentInterface(null);
        resolver.GetComponentInterface("USER_PROFILE");
        Assert.Equal(3, resolver.CoreCalls);

        resolver.Clear();
        resolver.Script = _ => CompIntfcLookup.Found(UserProfile());
        var lookup = resolver.GetComponentInterface("USER_PROFILE");
        Assert.Equal(4, resolver.CoreCalls);
        Assert.Equal(CompIntfcLookupStatus.Found, lookup.Status);
    }

    [Fact]
    public void Recheck_after_interval()
    {
        var original = ITypeMetadataResolver.ComponentInterfaceRecheckInterval;
        try
        {
            ITypeMetadataResolver.ComponentInterfaceRecheckInterval = TimeSpan.Zero;
            var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };

            resolver.GetComponentInterface("USER_PROFILE");
            resolver.GetComponentInterface("USER_PROFILE");

            Assert.Equal(2, resolver.CoreCalls);
        }
        finally
        {
            ITypeMetadataResolver.ComponentInterfaceRecheckInterval = original;
        }
    }

    /// <summary>
    /// Looks USER_PROFILE up inside a refinement frame, caches PKG:A as depending on it, then makes
    /// every later lookup a recheck. Returns whether PKG:A survives the second lookup.
    /// </summary>
    private static bool DependentSurvivesRecheck(Func<int, CompIntfcLookup> scriptByCall)
    {
        var original = ITypeMetadataResolver.ComponentInterfaceRecheckInterval;
        try
        {
            var resolver = new ScriptedResolver();
            resolver.Script = _ => scriptByCall(resolver.CoreCalls);

            var parent = RefinementDependencyCollector.Current;
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RefinementDependencyCollector.Current = dependencies;
            try
            {
                resolver.GetComponentInterface("USER_PROFILE");
            }
            finally
            {
                RefinementDependencyCollector.Current = parent;
            }
            resolver.Cache.Set("PKG:A", new TypeMetadata { QualifiedName = "PKG:A", Name = "A", RefinementDependencies = dependencies });

            ITypeMetadataResolver.ComponentInterfaceRecheckInterval = TimeSpan.Zero;
            resolver.GetComponentInterface("USER_PROFILE");

            Assert.Equal(2, resolver.CoreCalls);
            return resolver.Cache.Contains("PKG:A");
        }
        finally
        {
            ITypeMetadataResolver.ComponentInterfaceRecheckInterval = original;
        }
    }

    [Fact]
    public void Recheck_with_new_version_evicts_dependents()
    {
        Assert.False(DependentSurvivesRecheck(call => CompIntfcLookup.Found(UserProfile(version: call))));
    }

    [Fact]
    public void Recheck_with_same_version_keeps_dependents()
    {
        Assert.True(DependentSurvivesRecheck(_ => CompIntfcLookup.Found(UserProfile(version: 1))));
    }

    [Fact]
    public void Recheck_with_changed_status_evicts_dependents()
    {
        Assert.False(DependentSurvivesRecheck(call => call == 1 ? CompIntfcLookup.Found(UserProfile()) : CompIntfcLookup.Missing));
        Assert.False(DependentSurvivesRecheck(call => call == 1 ? CompIntfcLookup.Missing : CompIntfcLookup.Found(UserProfile())));
        Assert.True(DependentSurvivesRecheck(_ => CompIntfcLookup.Missing));
    }

    [Fact]
    public void Clear_drops_component_interface_lookups()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };
        resolver.GetComponentInterface("USER_PROFILE");

        resolver.Clear();

        Assert.Empty(resolver.CachedComponentInterfaces);
        resolver.GetComponentInterface("USER_PROFILE");
        Assert.Equal(2, resolver.CoreCalls);
    }

    [Fact]
    public void Invalidate_one_and_all()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };
        resolver.GetComponentInterface("USER_PROFILE");
        resolver.GetComponentInterface("OTHER_CI");
        Assert.Equal(2, resolver.CoreCalls);

        resolver.InvalidateComponentInterface("user_profile");
        Assert.DoesNotContain("USER_PROFILE", resolver.CachedComponentInterfaces);
        Assert.Contains("OTHER_CI", resolver.CachedComponentInterfaces);
        resolver.GetComponentInterface("USER_PROFILE");
        Assert.Equal(3, resolver.CoreCalls);

        resolver.InvalidateComponentInterface(null);
        Assert.Empty(resolver.CachedComponentInterfaces);
        resolver.GetComponentInterface("OTHER_CI");
        Assert.Equal(4, resolver.CoreCalls);
    }

    [Fact]
    public void Invalidate_evicts_dependent_type_metadata()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };

        var parent = RefinementDependencyCollector.Current;
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RefinementDependencyCollector.Current = dependencies;
        try
        {
            resolver.GetComponentInterface("user_profile");
        }
        finally
        {
            RefinementDependencyCollector.Current = parent;
        }
        Assert.Contains("CompIntfc:USER_PROFILE", dependencies);

        resolver.Cache.Set("PKG:A", new TypeMetadata { QualifiedName = "PKG:A", Name = "A", RefinementDependencies = dependencies });
        Assert.True(resolver.Cache.Contains("PKG:A"));

        resolver.InvalidateComponentInterface("USER_PROFILE");

        Assert.False(resolver.Cache.Contains("PKG:A"));
    }

    [Fact]
    public void Invalidate_all_evicts_dependent_type_metadata()
    {
        var resolver = new ScriptedResolver { Script = _ => CompIntfcLookup.Found(UserProfile()) };
        resolver.GetComponentInterface("USER_PROFILE");
        resolver.Cache.Set("PKG:A", new TypeMetadata
        {
            QualifiedName = "PKG:A",
            Name = "A",
            RefinementDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CompIntfc:USER_PROFILE" },
        });

        resolver.InvalidateComponentInterface(null);

        Assert.False(resolver.Cache.Contains("PKG:A"));
    }

    [Fact]
    public void FindNode_walks_paths()
    {
        var ci = UserProfile();

        Assert.Same(ci, ci.FindNode(null));
        Assert.Same(ci, ci.FindNode(""));
        Assert.Equal("Roles", ci.FindNode("roles")?.Name);
        Assert.Equal("RouteControls", ci.FindNode("Roles.RouteControls")?.Name);
        Assert.Equal("PSROLEUSER_RTE", ci.FindNode("ROLES.routecontrols")?.RecordName);
        Assert.Null(ci.FindNode("Nope"));
        Assert.Null(ci.FindNode("Roles.Nope"));
        Assert.IsType<CompIntfcCollection>(ci.FindNode("Roles"));
    }

    [Fact]
    public void FindRootProperty_includes_key_only_names()
    {
        var ci = UserProfile();

        Assert.Equal("UserDescription", ci.FindRootProperty("userdescription")?.Name);
        Assert.Equal("UserID", ci.FindRootProperty("USERID")?.Name);
        Assert.Equal("FindOnlyKey", ci.FindRootProperty("FindOnlyKey")?.Name);
        Assert.True(ci.FindRootProperty("LastSignonDateTime")?.IsReadOnly);
        Assert.Null(ci.FindRootProperty("RoleName"));
        Assert.Null(ci.FindRootProperty("Nope"));
    }

    [Fact]
    public void IsStandardMethodEnabled_gates_only_the_five()
    {
        var ci = UserProfile();

        Assert.False(ci.IsStandardMethodEnabled("Cancel"));
        Assert.False(ci.IsStandardMethodEnabled("cancel"));
        Assert.True(ci.IsStandardMethodEnabled("Create"));
        Assert.True(ci.IsStandardMethodEnabled("Find"));
        Assert.True(ci.IsStandardMethodEnabled("GET"));
        Assert.True(ci.IsStandardMethodEnabled("Save"));
        Assert.True(ci.IsStandardMethodEnabled("GetPropertyByName"));
        Assert.True(ci.IsStandardMethodEnabled("ResetPassword"));

        var none = new CompIntfcDefinition { Name = "BARE" };
        foreach (var name in new[] { "Cancel", "Create", "Find", "Get", "Save" })
            Assert.False(none.IsStandardMethodEnabled(name));
        Assert.True(none.IsStandardMethodEnabled("CopyRowset"));
        Assert.Empty(none.Properties);
        Assert.Empty(none.Collections);
        Assert.Empty(none.GetKeys);
        Assert.Empty(none.CustomMethods);
    }
}

/// <summary>
/// Runs its tests apart from every other class, since they change process-wide CI lookup settings.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ComponentInterfaceRecheckCollection
{
    public const string Name = "Component interface recheck interval";
}

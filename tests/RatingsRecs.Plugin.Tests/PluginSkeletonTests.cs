using System;
using Jellyfin.Plugin.RatingsRecs;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class PluginSkeletonTests
{
    [Fact]
    public void PluginGuid_IsTheFixedValue()
    {
        Assert.Equal(Guid.Parse("4d56ae7e-38cb-431c-bff8-e135c2087aa5"), Guid.Parse(Jellyfin.Plugin.RatingsRecs.Plugin.PluginGuid));
    }

    [Theory]
    [InlineData("Jellyfin.Plugin.RatingsRecs.Configuration.configPage.html")]
    [InlineData("Jellyfin.Plugin.RatingsRecs.Web.ratings.js")]
    [InlineData("Jellyfin.Plugin.RatingsRecs.Web.ratings.css")]
    public void EmbeddedResource_IsPresent(string resourceName)
    {
        var names = typeof(ServiceRegistrator).Assembly.GetManifestResourceNames();

        Assert.Contains(resourceName, names);
    }

    [Fact]
    public void TargetAbi_MatchesBuildLine()
    {
#if JF12
        Assert.Equal("12.0.0.0", BuildInfo.TargetAbi);
        Assert.Equal("12", BuildInfo.JellyfinLine);
#else
        Assert.Equal("10.11.9.0", BuildInfo.TargetAbi);
        Assert.Equal("10.11", BuildInfo.JellyfinLine);
#endif
    }

    [Fact]
    public void EngineTypes_AreCompiledIntoPluginAssembly()
    {
        var pluginAssembly = typeof(ServiceRegistrator).Assembly;

        Assert.NotNull(pluginAssembly.GetType("RatingsRecs.Engine.EngineInfo"));
        Assert.DoesNotContain(pluginAssembly.GetReferencedAssemblies(), a => a.Name == "RatingsRecs.Engine");
    }

    [Fact]
    public void PluginAssembly_HasVersionFromBuildYaml()
    {
        var version = typeof(ServiceRegistrator).Assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.True(version!.Major >= 1);
    }
}

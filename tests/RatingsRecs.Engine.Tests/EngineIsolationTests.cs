using System;
using System.Linq;
using RatingsRecs.Engine;
using Xunit;

namespace RatingsRecs.Engine.Tests;

public class EngineIsolationTests
{
    [Fact]
    public void EngineAssembly_DoesNotReferenceJellyfin()
    {
        var references = typeof(EngineInfo).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Jellyfin", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("MediaBrowser", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Emby", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ModelVersion_IsPositive()
    {
        Assert.True(EngineInfo.ModelVersion > 0);
    }
}

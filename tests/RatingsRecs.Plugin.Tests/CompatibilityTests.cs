using System;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class CompatibilityTests
{
    [Theory]
    [InlineData("10.11", "10.11.9", HookState.Active)]
    [InlineData("10.11", "10.11.11", HookState.Active)]
    [InlineData("12", "12.0.0", HookState.Active)]
    [InlineData("12", "12.4.0", HookState.Active)]
    [InlineData("12", "13.0.0", HookState.Active)]
    [InlineData("10.11", "12.4.0", HookState.Failed)]
    [InlineData("10.11", "10.11.8", HookState.Failed)]
    [InlineData("12", "10.11.11", HookState.Failed)]
    public void TheBuildMustMatchTheServerLine(string buildLine, string server, HookState expected)
    {
        var (state, message) = Compatibility.Check(buildLine, Version.Parse(server));

        Assert.Equal(expected, state);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void TheMessageTellsWhichZipToInstall()
    {
        var (_, message) = Compatibility.Check("10.11", new Version(12, 4, 0));

        Assert.Contains("jf12", message, StringComparison.Ordinal);
    }
}

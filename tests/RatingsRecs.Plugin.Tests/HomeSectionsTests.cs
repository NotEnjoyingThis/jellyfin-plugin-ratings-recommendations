using System.Text.Json;
using Jellyfin.Plugin.RatingsRecs.Home;
using Jellyfin.Plugin.RatingsRecs.Web;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class HomeSectionsTests
{
    [Theory]
    [InlineData("Auto", true, "hss")]
    [InlineData("Auto", false, "script")]
    [InlineData("HomeScreenSections", false, "script")]
    [InlineData("HomeScreenSections", true, "hss")]
    [InlineData("Script", true, "script")]
    [InlineData("Off", true, "off")]
    [InlineData(null, false, "script")]
    public void HomeRows_FallBackToTheScriptWhenHomeScreenSectionsIsNotWorking(string? mode, bool hssActive, string expected)
    {
        Assert.Equal(expected, WebAssets.HomeRows(mode, hssActive));
    }

    [Theory]
    [InlineData(nameof(HomeSectionResults.GetRecommended))]
    [InlineData(nameof(HomeSectionResults.GetRateThese))]
    public void ResultsMethods_HaveTheSignatureHomeScreenSectionsCalls(string method)
    {
        var info = typeof(HomeSectionResults).GetMethod(method);

        Assert.NotNull(info);
        Assert.False(info!.IsStatic);
        Assert.Equal(typeof(QueryResult<BaseItemDto>), info.ReturnType);
        var parameter = Assert.Single(info.GetParameters()).ParameterType;
        Assert.NotNull(parameter.GetProperty("UserId"));
        Assert.NotNull(parameter.GetProperty("AdditionalData"));
    }

    [Fact]
    public void SectionPayload_HasTheDocumentedFields()
    {
        using var doc = JsonDocument.Parse(HomeSectionsBridge.SectionPayload(HomeSectionsBridge.RecommendedSectionId, "Consigliati per te", nameof(HomeSectionResults.GetRecommended)));
        var root = doc.RootElement;

        Assert.Equal("RatingsRecsRecommended", root.GetProperty("id").GetString());
        Assert.Equal("Consigliati per te", root.GetProperty("displayText").GetString());
        Assert.Equal(typeof(HomeSectionResults).Assembly.FullName, root.GetProperty("resultsAssembly").GetString());
        Assert.Equal(typeof(HomeSectionResults).FullName, root.GetProperty("resultsClass").GetString());
        Assert.Equal("GetRecommended", root.GetProperty("resultsMethod").GetString());
        Assert.False(root.TryGetProperty("route", out _));
    }

    [Fact]
    public void PagePayload_PointsToTheForYouPage()
    {
        using var doc = JsonDocument.Parse(HomeSectionsBridge.PagePayload("Per te"));

        Assert.Equal("/RatingsRecs/Web/foryou.html", doc.RootElement.GetProperty("url").GetString());
        Assert.Equal("Per te", doc.RootElement.GetProperty("displayText").GetString());
        Assert.Equal("thumb_up", doc.RootElement.GetProperty("icon").GetString());
    }
}

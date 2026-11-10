using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Web;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class WebInjectionTests
{
    private const string Page = "<!DOCTYPE html><html><head><title>Jellyfin</title></head><body><div id=\"reactRoot\"></div></body></html>";

    [Fact]
    public void Inject_AddsStylesheetInHeadAndScriptBeforeBodyEnd()
    {
        var result = IndexTransformation.Inject(Page, "1.0.0.0");

        Assert.Contains("<link rel=\"stylesheet\" href=\"../RatingsRecs/Web/ratings.css?v=1.0.0.0\"></head>", result, StringComparison.Ordinal);
        Assert.Contains("<script defer src=\"../RatingsRecs/Web/ratings.js?v=1.0.0.0\"></script></body>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Inject_IsIdempotent()
    {
        var once = IndexTransformation.Inject(Page, "1.0.0.0");

        Assert.Equal(once, IndexTransformation.Inject(once, "1.0.0.0"));
        Assert.Single(Regex.Matches(once, "ratings\\.js"));
    }

    [Fact]
    public void Inject_LeavesNonHtmlUnchanged()
    {
        Assert.Equal("not html", IndexTransformation.Inject("not html", "1"));
    }

    [Fact]
    public void Transform_NeverThrows_AndReturnsContent()
    {
        Assert.Equal(string.Empty, IndexTransformation.Transform(null!));
        Assert.Contains("ratings.js", IndexTransformation.Transform(new TransformPayload { Contents = Page }), StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationPayload_HasTheFieldsFileTransformationExpects()
    {
        using var doc = JsonDocument.Parse(WebInjector.BuildPayload());
        var root = doc.RootElement;

        Assert.Equal(WebInjector.TransformationId, root.GetProperty("id").GetGuid());
        Assert.Matches(root.GetProperty("fileNamePattern").GetString()!, "index.html");
        Assert.Equal(typeof(IndexTransformation).Assembly.FullName, root.GetProperty("callbackAssembly").GetString());
        Assert.Equal(typeof(IndexTransformation).FullName, root.GetProperty("callbackClass").GetString());
        Assert.Equal("Transform", root.GetProperty("callbackMethod").GetString());

        var method = typeof(IndexTransformation).GetMethod(root.GetProperty("callbackMethod").GetString()!);
        Assert.NotNull(method);
        Assert.True(method!.IsStatic && method.IsPublic);
        Assert.Equal(typeof(string), method.ReturnType);
        Assert.NotNull(method.GetParameters()[0].ParameterType.GetProperty("Contents"));
    }

    [Fact]
    public void Script_StartsWithBootstrapWithBothLanguagesAndCustomNames()
    {
        var config = new PluginConfiguration { NameAmazing = "Capolavoro" };

        var script = WebAssets.BuildScript(config);

        Assert.StartsWith("window.RatingsRecsBootstrap = ", script, StringComparison.Ordinal);
        var json = script.Substring("window.RatingsRecsBootstrap = ".Length, script.IndexOf(";\n", StringComparison.Ordinal) - "window.RatingsRecsBootstrap = ".Length);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Capolavoro", root.GetProperty("levelNames").GetProperty("3").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("levelNames").GetProperty("-3").ValueKind);
        Assert.Equal("Detesto", root.GetProperty("strings").GetProperty("it").GetProperty("level.-3").GetString());
        Assert.Equal("Hate it", root.GetProperty("strings").GetProperty("en").GetProperty("level.-3").GetString());
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslationFiles_HaveTheSameKeys()
    {
        using var en = JsonDocument.Parse(WebAssets.ReadText("i18n.en.json")!);
        using var it = JsonDocument.Parse(WebAssets.ReadText("i18n.it.json")!);

        foreach (var property in en.RootElement.EnumerateObject())
        {
            Assert.True(it.RootElement.TryGetProperty(property.Name, out _), "it.json misses " + property.Name);
        }

        foreach (var property in it.RootElement.EnumerateObject())
        {
            Assert.True(en.RootElement.TryGetProperty(property.Name, out _), "en.json misses " + property.Name);
        }
    }

    [Fact]
    public void ETag_ChangesWithContent()
    {
        Assert.Equal(WebAssets.ETag("a"), WebAssets.ETag("a"));
        Assert.NotEqual(WebAssets.ETag("a"), WebAssets.ETag("b"));
        Assert.StartsWith("\"", WebAssets.ETag("a"), StringComparison.Ordinal);
    }
}

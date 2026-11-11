using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.RatingsRecs;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Jellyfin.Plugin.RatingsRecs.Override;
using Jellyfin.Plugin.RatingsRecs.Similar;
using Jellyfin.Plugin.RatingsRecs.Web;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class SettingsTranslationTests
{
    private static HashSet<string> Keys(string language)
    {
        var json = WebAssets.ReadText("i18n.config-" + language + ".json");
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static string SettingsPage()
    {
        using var stream = typeof(Jellyfin.Plugin.RatingsRecs.Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RatingsRecs.Configuration.configPage.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    [Fact]
    public void EnglishAndItalian_HaveTheSameKeys()
    {
        var en = Keys("en");
        var it = Keys("it");

        Assert.Empty(en.Except(it));
        Assert.Empty(it.Except(en));
    }

    [Fact]
    public void EveryKeyUsedByTheSettingsPage_IsTranslated()
    {
        var page = SettingsPage();
        var used = new HashSet<string>(StringComparer.Ordinal);
        used.UnionWith(Regex.Matches(page, "data-rr-i18n(?:-html)?=\"([^\"]+)\"").Select(m => m.Groups[1].Value));
        // Literal keys only: prefixes such as t('engine.' + f.key) are covered by the lists below.
        used.UnionWith(Regex.Matches(page, @"\bt\('([A-Za-z.]+)'").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith('.')));
        used.UnionWith(Regex.Matches(page, @"\{ key: '([A-Za-z]+)'").Select(m => "engine." + m.Groups[1].Value));
        used.UnionWith(new[] { "Hate", "Watchable", "Good", "Amazing" }.Select(l => "level." + l));
        used.UnionWith(Enum.GetNames<HookState>().Select(s => "state." + s));
        used.UnionWith(new[] { HookStatusRegistry.WebInjection, Compatibility.HookName, "HomeScreenSections", "PluginPages", SimilarProviderOrder.HookName, SuggestionsOverride.HookName }.Select(h => "hook." + h));
        used.UnionWith(new[] { "dep.notInstalledRequired", "dep.notInstalled" });

        Assert.Empty(used.Except(Keys("en")));
        Assert.True(used.Count > 80, "the page should use its translation keys, found " + used.Count);
    }

    [Fact]
    public void TheSettingsPageHasNoInlineBilingualText()
    {
        // Texts live in Web/i18n/config.*.json; "English / Italiano" labels must not come back.
        Assert.DoesNotMatch(new Regex(@"/ (Voti|Consegna|Motore|Strumenti|Dipendenze|Attivo)\b"), SettingsPage());
    }
}

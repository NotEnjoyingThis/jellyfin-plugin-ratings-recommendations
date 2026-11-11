using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RatingsRecs.Configuration;

/// <summary>
/// Plugin configuration. Settings are added milestone by milestone (see docs/DESIGN.md, section 7.4).
/// Defaults are the values of the design document.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the display name of the "Hate it" level (-3). Empty = translated default.
    /// </summary>
    public string NameHate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Watchable" level (-1). Empty = translated default.
    /// </summary>
    public string NameWatchable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Good" level (+1). Empty = translated default.
    /// </summary>
    public string NameGood { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Amazing" level (+3). Empty = translated default.
    /// </summary>
    public string NameAmazing { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile value of the "Hate it" level.
    /// </summary>
    public double ValueHate { get; set; } = -3;

    /// <summary>
    /// Gets or sets the profile value of the "Watchable" level.
    /// </summary>
    public double ValueWatchable { get; set; } = -1;

    /// <summary>
    /// Gets or sets the profile value of the "Good" level.
    /// </summary>
    public double ValueGood { get; set; } = 1;

    /// <summary>
    /// Gets or sets the profile value of the "Amazing" level.
    /// </summary>
    public double ValueAmazing { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether an "Amazing" rating adds the item to Favourites.
    /// </summary>
    public bool AmazingSetsFavorite { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a Favourite set by another client (without thumbs) counts as an implicit rating.
    /// </summary>
    public bool FavoriteFromOtherClientsCounts { get; set; } = true;

    /// <summary>
    /// Gets or sets the implicit value of a Favourite set by another client without an explicit rating.
    /// </summary>
    public double ImplicitFavoriteValue { get; set; } = 3;

    /// <summary>
    /// Gets or sets the implicit value of an item watched to the end without a rating.
    /// </summary>
    public double ImplicitWatchedValue { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the implicit value of an abandoned item (progress below 20%, idle for 30+ days).
    /// </summary>
    public double ImplicitAbandonedValue { get; set; } = -0.5;

    /// <summary>Gets or sets the server default series rating mode: "Series" (default), "Season" or "Both".</summary>
    public string SeriesRatingMode { get; set; } = "Series";

    /// <summary>Gets or sets a value indicating whether users can choose their own series rating mode.</summary>
    public bool UsersCanChooseSeriesMode { get; set; } = true;

    /// <summary>Gets or sets the weight of the best season in a series score.</summary>
    public double SeriesBestSeasonWeight { get; set; } = 0.6;

    /// <summary>Gets or sets the weight of the average season in a series score.</summary>
    public double SeriesAverageSeasonWeight { get; set; } = 0.4;

    /// <summary>Gets or sets how the home rows are shown: "Auto" (default), "HomeScreenSections", "Script" or "Off".</summary>
    public string HomeRowsMode { get; set; } = "Auto";

    /// <summary>Gets or sets the number of items in the home rows.</summary>
    public int HomeRowSize { get; set; } = 20;

    /// <summary>Gets or sets a value indicating whether the "Similar, picked for you" row is added to detail pages.</summary>
    public bool ShowSimilarRow { get; set; } = true;

    /// <summary>Gets or sets the number of items in the "Similar, picked for you" row.</summary>
    public int SimilarRowSize { get; set; } = 12;

    /// <summary>Gets or sets a value indicating whether to add the "For you" entry to the side menu.</summary>
    public bool ShowMenuEntry { get; set; } = true;

    /// <summary>
    /// Gets or sets the libraries (ids, "N" format) where the Jellyfin 12 "similar items" provider is active.
    /// On save the plugin puts its provider first in their provider order and removes it from the others.
    /// Empty by default: the plugin changes no library setting until an administrator selects libraries.
    /// </summary>
#pragma warning disable CA1819 // XML-serialized configuration: arrays are the format Jellyfin uses for plugin settings.
    public string[] SimilarProviderLibraryIds { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>Gets or sets the genre weight α.</summary>
    public double WeightGenre { get; set; } = 1.0;

    /// <summary>Gets or sets the director weight α.</summary>
    public double WeightDirector { get; set; } = 1.2;

    /// <summary>Gets or sets the actor weight α.</summary>
    public double WeightActor { get; set; } = 0.8;

    /// <summary>Gets or sets the writer weight α.</summary>
    public double WeightWriter { get; set; } = 0.6;

    /// <summary>Gets or sets the tag weight α.</summary>
    public double WeightTag { get; set; } = 0.5;

    /// <summary>Gets or sets the studio weight α.</summary>
    public double WeightStudio { get; set; } = 0.3;

    /// <summary>Gets or sets the decade weight α.</summary>
    public double WeightDecade { get; set; } = 0.4;

    /// <summary>Gets or sets the collection (saga) weight α.</summary>
    public double WeightCollection { get; set; } = 1.0;

    /// <summary>Gets or sets the smoothing constant k.</summary>
    public double SmoothingK { get; set; } = 2;

    /// <summary>Gets or sets the rating half-life in days (0 = no decay).</summary>
    public double HalfLifeDays { get; set; } = 365;

    /// <summary>Gets or sets the taste coefficient.</summary>
    public double TasteWeight { get; set; } = 0.75;

    /// <summary>Gets or sets the quality coefficient.</summary>
    public double QualityWeight { get; set; } = 0.15;

    /// <summary>Gets or sets the exploration coefficient.</summary>
    public double ExplorationWeight { get; set; } = 0.10;

    /// <summary>Gets or sets the explicit ratings needed to leave the cold start.</summary>
    public int ColdStartThreshold { get; set; } = 5;

    /// <summary>Gets or sets the diversity λ (MMR).</summary>
    public double DiversityLambda { get; set; } = 0.8;

    /// <summary>Gets or sets the proximity penalty factor.</summary>
    public double ProximityPenalty { get; set; } = 0.5;

    /// <summary>Gets or sets a value indicating whether other chapters of a saga rated -3 are excluded.</summary>
    public bool ExcludeHatedCollections { get; set; } = true;

    /// <summary>Gets or sets the number of explicit ratings after which "Rate these" disappears.</summary>
    public int OnboardingHideAfter { get; set; } = 15;
}

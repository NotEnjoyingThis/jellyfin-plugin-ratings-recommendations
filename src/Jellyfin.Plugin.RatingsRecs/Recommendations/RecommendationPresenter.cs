using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Api;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Serves recommendations from the cache (never computes): filters by type, drops items rated or watched
/// since the last computation and items the user can no longer access, and converts to <c>BaseItemDto</c>.
/// </summary>
public sealed class RecommendationPresenter
{
    /// <summary>The largest page size.</summary>
    public const int MaxLimit = 200;

    private readonly RecommendationService _service;
    private readonly RecomputeScheduler _scheduler;
    private readonly ILibraryGateway _library;
    private readonly IUserDataGateway _userData;
    private readonly IRatingStore _store;
    private readonly IDtoService _dtoService;
    private readonly IPluginConfigProvider _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationPresenter"/> class.
    /// </summary>
    /// <param name="service">The recommendation service.</param>
    /// <param name="scheduler">The recompute scheduler.</param>
    /// <param name="library">The library gateway.</param>
    /// <param name="userData">The user data gateway.</param>
    /// <param name="store">The rating store.</param>
    /// <param name="dtoService">The DTO service.</param>
    /// <param name="config">The configuration.</param>
    public RecommendationPresenter(RecommendationService service, RecomputeScheduler scheduler, ILibraryGateway library, IUserDataGateway userData, IRatingStore store, IDtoService dtoService, IPluginConfigProvider config)
    {
        _service = service;
        _scheduler = scheduler;
        _library = library;
        _userData = userData;
        _store = store;
        _dtoService = dtoService;
        _config = config;
    }

    /// <summary>
    /// Gets a page of recommendations.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="startIndex">The first index.</param>
    /// <param name="type">"Movie", "Series" or <c>null</c> for both.</param>
    /// <returns>The response.</returns>
    public RecommendationsResponse Recommendations(User user, int limit, int startIndex, string? type)
    {
        ArgumentNullException.ThrowIfNull(user);
        var cached = _service.Get(user.Id);
        if (cached is null)
        {
            _scheduler.EnsureComputed(user.Id);
            return new RecommendationsResponse { Status = "Computing" };
        }

        var rated = _store.GetAll(user.Id).Select(r => r.ItemId).ToHashSet();
        var eligible = cached.Ranked
            .Where(s => !rated.Contains(s.ItemId))
            .Where(s => type is null || string.Equals(cached.Kinds.GetValueOrDefault(s.ItemId), type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var page = new List<(BaseItem Item, ExplanationDto Explanation)>();
        var skipped = 0;
        foreach (var scored in eligible)
        {
            if (page.Count >= Math.Clamp(limit, 1, MaxLimit))
            {
                break;
            }

            var item = Visible(user, scored.ItemId);
            if (item is null)
            {
                continue;
            }

            if (skipped < startIndex)
            {
                skipped++;
                continue;
            }

            page.Add((item, ExplanationDto.From(item.Id, scored.Explanation)));
        }

        return new RecommendationsResponse
        {
            Items = ToDtos(user, page.Select(p => p.Item).ToList()),
            Explanations = page.Select(p => p.Explanation).ToList(),
            TotalRecordCount = eligible.Count,
            StartIndex = startIndex,
            ComputedUtc = cached.ComputedUtc,
            IsColdStart = cached.IsColdStart,
            ExplicitRatingCount = cached.ExplicitRatingCount,
        };
    }

    /// <summary>
    /// Gets the "Rate these" items; hidden once the user has enough explicit ratings.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="limit">The number of items.</param>
    /// <returns>The response.</returns>
    public RecommendationsResponse Onboarding(User user, int limit)
    {
        ArgumentNullException.ThrowIfNull(user);
        var ratings = _store.GetAll(user.Id);
        var explicitCount = ratings.Count(r => r.IsExplicit);
        if (explicitCount >= _config.Current.OnboardingHideAfter)
        {
            return new RecommendationsResponse { Hidden = true, ExplicitRatingCount = explicitCount };
        }

        var cached = _service.Get(user.Id);
        if (cached is null)
        {
            _scheduler.EnsureComputed(user.Id);
            return new RecommendationsResponse { Status = "Computing", ExplicitRatingCount = explicitCount };
        }

        var rated = ratings.Where(r => r.IsExplicit).Select(r => r.ItemId).ToHashSet();
        var items = new List<BaseItem>();
        foreach (var id in cached.Onboarding.Where(id => !rated.Contains(id)))
        {
            if (items.Count >= Math.Clamp(limit, 1, MaxLimit))
            {
                break;
            }

            var item = _library.GetItem(id);
            if (item is not null && item.IsVisibleStandalone(user))
            {
                items.Add(item);
            }
        }

        return new RecommendationsResponse
        {
            Items = ToDtos(user, items),
            TotalRecordCount = items.Count,
            ComputedUtc = cached.ComputedUtc,
            IsColdStart = explicitCount < _config.Current.ColdStartThreshold,
            ExplicitRatingCount = explicitCount,
        };
    }

    /// <summary>
    /// Gets the items similar to an open item, ranked with the user's taste (from the cached profile).
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="itemId">The open item.</param>
    /// <param name="limit">The number of items.</param>
    /// <returns>The response, or <c>null</c> if the item does not exist or is not accessible.</returns>
    public RecommendationsResponse? Similar(User user, Guid itemId, int limit)
    {
        ArgumentNullException.ThrowIfNull(user);
        var source = _library.GetItem(itemId);
        if (source is null || !source.IsVisibleStandalone(user))
        {
            return null;
        }

        var cached = _service.Get(user.Id);
        if (cached?.Context is null)
        {
            _scheduler.EnsureComputed(user.Id);
            return new RecommendationsResponse { Status = "Computing" };
        }

        // Ratings given since the last computation count too: a negatively rated item never appears.
        var negative = _store.GetAll(user.Id).Where(r => r.Value < 0).Select(r => r.ItemId).ToHashSet();
        var take = Math.Clamp(limit, 1, MaxLimit);
        var page = new List<(BaseItem Item, ExplanationDto Explanation)>();
        foreach (var scored in global::RatingsRecs.Engine.SimilarRanker.Rank(cached.Context, itemId, take + negative.Count + 10))
        {
            if (page.Count >= take)
            {
                break;
            }

            if (negative.Contains(scored.ItemId))
            {
                continue;
            }

            var item = _library.GetItem(scored.ItemId);
            if (item is not null && item.IsVisibleStandalone(user))
            {
                page.Add((item, ExplanationDto.From(item.Id, scored.Explanation)));
            }
        }

        return new RecommendationsResponse
        {
            Items = ToDtos(user, page.Select(p => p.Item).ToList()),
            Explanations = page.Select(p => p.Explanation).ToList(),
            TotalRecordCount = page.Count,
            ComputedUtc = cached.ComputedUtc,
            IsColdStart = cached.IsColdStart,
            ExplicitRatingCount = cached.ExplicitRatingCount,
        };
    }

    /// <summary>
    /// Items for the Jellyfin 12 "similar items" provider: similar items ranked with the user's taste, filled up to the
    /// limit with the other items of the same kind and, only if still short, the negatively rated ones (last).
    /// Served from the cached context (never computes).
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="source">The open item.</param>
    /// <param name="limit">How many items Jellyfin asked for.</param>
    /// <param name="excludeIds">Ids Jellyfin asked to leave out.</param>
    /// <returns>The items, or <c>null</c> while the recommendations are being computed.</returns>
    public IReadOnlyList<BaseItem>? ProviderItems(User user, BaseItem source, int limit, IReadOnlyCollection<Guid>? excludeIds)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(source);
        var cached = _service.Get(user.Id);
        if (cached?.Context is null)
        {
            _scheduler.EnsureComputed(user.Id);
            return null;
        }

        var exclude = excludeIds is null ? [] : excludeIds.ToHashSet();
        var liveNegatives = _store.GetAll(user.Id).Where(r => r.Value < 0).ToDictionary(r => r.ItemId, r => (double)r.Value);
        var take = Math.Clamp(limit, 1, MaxLimit);
        var items = new List<BaseItem>();
        foreach (var id in global::RatingsRecs.Engine.SimilarRanker.OrderForProvider(cached.Context, source.Id, liveNegatives))
        {
            if (items.Count >= take)
            {
                break;
            }

            if (exclude.Contains(id))
            {
                continue;
            }

            var item = _library.GetItem(id);
            if (item is not null && item.IsVisibleStandalone(user))
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Reorders a list chosen by Jellyfin with the user's taste and drops negatively rated items (cached context only).
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="itemIds">The items, in Jellyfin's order.</param>
    /// <returns>The new order, or <c>null</c> while the recommendations are being computed.</returns>
    public IReadOnlyList<Guid>? ReorderByTaste(User user, IReadOnlyList<Guid> itemIds)
    {
        ArgumentNullException.ThrowIfNull(user);
        var cached = _service.Get(user.Id);
        if (cached?.Context is null)
        {
            _scheduler.EnsureComputed(user.Id);
            return null;
        }

        var liveNegatives = _store.GetAll(user.Id).Where(r => r.Value < 0).ToDictionary(r => r.ItemId, r => (double)r.Value);
        return global::RatingsRecs.Engine.TasteOrder.Order(cached.Context, itemIds, liveNegatives);
    }

    private BaseItem? Visible(User user, Guid id)
    {
        var item = _library.GetItem(id);
        if (item is null || !item.IsVisibleStandalone(user))
        {
            return null;
        }

        // Watched since the last computation: not a candidate any more.
        return _userData.Get(user, item)?.Played == true ? null : item;
    }

    private IReadOnlyList<MediaBrowser.Model.Dto.BaseItemDto> ToDtos(User user, IReadOnlyList<BaseItem> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var options = new DtoOptions
        {
            Fields = [ItemFields.PrimaryImageAspectRatio, ItemFields.Genres, ItemFields.Overview],
            EnableImages = true,
            EnableUserData = true,
            ImageTypeLimit = 1,
            ImageTypes = [ImageType.Primary, ImageType.Thumb, ImageType.Backdrop],
        };
        return _dtoService.GetBaseItemDtos(items, options, user);
    }
}

﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Manager
{
    public static class ProviderUtils
    {
        public static void MergeBaseItemData<T>(MetadataResult<T> sourceResult,
            MetadataResult<T> targetResult,
            List<MetadataFields> lockedFields,
            bool replaceData,
            bool mergeMetadataSettings)
            where T : BaseItem
        {
            var source = sourceResult.Item;
            var target = targetResult.Item;

            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (!lockedFields.Contains(MetadataFields.Name))
            {
                if (replaceData || string.IsNullOrEmpty(target.Name))
                {
                    // Safeguard against incoming data having an emtpy name
                    if (!string.IsNullOrWhiteSpace(source.Name))
                    {
                        target.Name = source.Name;
                    }
                }
            }

            if (replaceData || string.IsNullOrEmpty(target.OriginalTitle))
            {
                // Safeguard against incoming data having an emtpy name
                if (!string.IsNullOrWhiteSpace(source.OriginalTitle))
                {
                    target.OriginalTitle = source.OriginalTitle;
                }
            }

            if (replaceData || !target.CommunityRating.HasValue)
            {
                target.CommunityRating = source.CommunityRating;
            }

            if (replaceData || !target.EndDate.HasValue)
            {
                target.EndDate = source.EndDate;
            }

            if (!lockedFields.Contains(MetadataFields.Genres))
            {
                if (replaceData || target.Genres.Count == 0)
                {
                    target.Genres = source.Genres;
                }
            }

            if (replaceData || string.IsNullOrEmpty(target.HomePageUrl))
            {
                target.HomePageUrl = source.HomePageUrl;
                if (!string.IsNullOrWhiteSpace(target.HomePageUrl) && target.HomePageUrl.IndexOf("http", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    target.HomePageUrl = "http://" + target.HomePageUrl;
                }
            }

            if (replaceData || !target.IndexNumber.HasValue)
            {
                target.IndexNumber = source.IndexNumber;
            }

            if (!lockedFields.Contains(MetadataFields.OfficialRating))
            {
                if (replaceData || string.IsNullOrEmpty(target.OfficialRating))
                {
                    target.OfficialRating = source.OfficialRating;
                }
            }

            if (replaceData || string.IsNullOrEmpty(target.OfficialRatingDescription))
            {
                target.OfficialRatingDescription = source.OfficialRatingDescription;
            }

            if (replaceData || string.IsNullOrEmpty(target.CustomRating))
            {
                target.CustomRating = source.CustomRating;
            }

            if (replaceData || string.IsNullOrEmpty(target.Tagline))
            {
                target.Tagline = source.Tagline;
            }

            if (!lockedFields.Contains(MetadataFields.Overview))
            {
                if (replaceData || string.IsNullOrEmpty(target.Overview))
                {
                    target.Overview = source.Overview;
                }
            }

            if (replaceData || !target.ParentIndexNumber.HasValue)
            {
                target.ParentIndexNumber = source.ParentIndexNumber;
            }

            if (!lockedFields.Contains(MetadataFields.Cast))
            {
                if (replaceData || targetResult.People == null || targetResult.People.Count == 0)
                {
                    targetResult.People = sourceResult.People;
                }
            }

            if (replaceData || !target.PremiereDate.HasValue)
            {
                target.PremiereDate = source.PremiereDate;
            }

            if (replaceData || !target.ProductionYear.HasValue)
            {
                target.ProductionYear = source.ProductionYear;
            }

            if (!lockedFields.Contains(MetadataFields.Runtime))
            {
                if (replaceData || !target.RunTimeTicks.HasValue)
                {
                    if (!(target is Audio) && !(target is Video))
                    {
                        target.RunTimeTicks = source.RunTimeTicks;
                    }
                }
            }

            if (!lockedFields.Contains(MetadataFields.Studios))
            {
                if (replaceData || target.Studios.Count == 0)
                {
                    target.Studios = source.Studios;
                }
            }

            if (!lockedFields.Contains(MetadataFields.Tags))
            {
                if (replaceData || target.Tags.Count == 0)
                {
                    target.Tags = source.Tags;
                }
            }

            if (!lockedFields.Contains(MetadataFields.Keywords))
            {
                if (replaceData || target.Keywords.Count == 0)
                {
                    target.Keywords = source.Keywords;
                }
            }

            if (!lockedFields.Contains(MetadataFields.ProductionLocations))
            {
                if (replaceData || target.ProductionLocations.Count == 0)
                {
                    target.ProductionLocations = source.ProductionLocations;
                }
            }

            if (replaceData || !target.VoteCount.HasValue)
            {
                target.VoteCount = source.VoteCount;
            }

            foreach (var id in source.ProviderIds)
            {
                var key = id.Key;

                // Don't replace existing Id's.
                if (replaceData || !target.ProviderIds.ContainsKey(key))
                {
                    target.ProviderIds[key] = id.Value;
                }
            }

            MergeAlbumArtist(source, target, lockedFields, replaceData);
            MergeCriticRating(source, target, lockedFields, replaceData);
            MergeAwards(source, target, lockedFields, replaceData);
            MergeTrailers(source, target, lockedFields, replaceData);

            if (mergeMetadataSettings)
            {
                MergeMetadataSettings(source, target);
            }
        }

        public static void MergeMetadataSettings(BaseItem source,
           BaseItem target)
        {
            target.ForcedSortName = source.ForcedSortName;
            target.LockedFields = source.LockedFields;
            target.IsLocked = source.IsLocked;
            target.DisplayMediaType = source.DisplayMediaType;

            // Grab the value if it's there, but if not then don't overwrite the default
            if (source.DateCreated != default(DateTime))
            {
                target.DateCreated = source.DateCreated;
            }

            target.PreferredMetadataCountryCode = source.PreferredMetadataCountryCode;
            target.PreferredMetadataLanguage = source.PreferredMetadataLanguage;

            var sourceHasDisplayOrder = source as IHasDisplayOrder;
            var targetHasDisplayOrder = target as IHasDisplayOrder;

            if (sourceHasDisplayOrder != null && targetHasDisplayOrder != null)
            {
                targetHasDisplayOrder.DisplayOrder = sourceHasDisplayOrder.DisplayOrder;
            }
        }

        private static void MergeAlbumArtist(BaseItem source, BaseItem target, List<MetadataFields> lockedFields, bool replaceData)
        {
            var sourceHasAlbumArtist = source as IHasAlbumArtist;
            var targetHasAlbumArtist = target as IHasAlbumArtist;

            if (sourceHasAlbumArtist != null && targetHasAlbumArtist != null)
            {
                if (replaceData || targetHasAlbumArtist.AlbumArtists.Count == 0)
                {
                    targetHasAlbumArtist.AlbumArtists = sourceHasAlbumArtist.AlbumArtists;
                }
            }
        }

        private static void MergeAwards(BaseItem source, BaseItem target, List<MetadataFields> lockedFields, bool replaceData)
        {
            var sourceCast = source as IHasAwards;
            var targetCast = target as IHasAwards;

            if (sourceCast != null && targetCast != null)
            {
                if (replaceData || string.IsNullOrEmpty(targetCast.AwardSummary))
                {
                    targetCast.AwardSummary = sourceCast.AwardSummary;
                }
            }
        }

        private static void MergeCriticRating(BaseItem source, BaseItem target, List<MetadataFields> lockedFields, bool replaceData)
        {
            if (replaceData || !target.CriticRating.HasValue)
            {
                target.CriticRating = source.CriticRating;
            }

            if (replaceData || string.IsNullOrEmpty(target.CriticRatingSummary))
            {
                target.CriticRatingSummary = source.CriticRatingSummary;
            }
        }

        private static void MergeTrailers(BaseItem source, BaseItem target, List<MetadataFields> lockedFields, bool replaceData)
        {
            var sourceCast = source as IHasTrailers;
            var targetCast = target as IHasTrailers;

            if (sourceCast != null && targetCast != null)
            {
                if (replaceData || targetCast.RemoteTrailers.Count == 0)
                {
                    targetCast.RemoteTrailers = sourceCast.RemoteTrailers;
                }
            }
        }
    }
}

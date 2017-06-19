﻿using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Controller.Entities.Movies
{
    /// <summary>
    /// Class Movie
    /// </summary>
    public class Movie : Video, IHasSpecialFeatures, IHasTrailers, IHasAwards, IHasLookupInfo<MovieInfo>, ISupportsBoxSetGrouping
    {
        public List<Guid> SpecialFeatureIds { get; set; }

        public Movie()
        {
            SpecialFeatureIds = new List<Guid>();
            RemoteTrailers = new List<MediaUrl>();
            LocalTrailerIds = new List<Guid>();
            RemoteTrailerIds = new List<Guid>();
        }

        public string AwardSummary { get; set; }

        public List<Guid> LocalTrailerIds { get; set; }
        public List<Guid> RemoteTrailerIds { get; set; }

        public List<MediaUrl> RemoteTrailers { get; set; }

        /// <summary>
        /// Gets or sets the name of the TMDB collection.
        /// </summary>
        /// <value>The name of the TMDB collection.</value>
        public string TmdbCollectionName { get; set; }

        [IgnoreDataMember]
        public string CollectionName
        {
            get { return TmdbCollectionName; }
            set { TmdbCollectionName = value; }
        }

        public override double? GetDefaultPrimaryImageAspectRatio()
        {
            double value = 2;
            value /= 3;

            return value;
        }

        [IgnoreDataMember]
        protected override bool SupportsIsInMixedFolderDetection
        {
            get
            {
                return false;
            }
        }

        protected override async Task<bool> RefreshedOwnedItems(MetadataRefreshOptions options, List<FileSystemMetadata> fileSystemChildren, CancellationToken cancellationToken)
        {
            var hasChanges = await base.RefreshedOwnedItems(options, fileSystemChildren, cancellationToken).ConfigureAwait(false);

            // Must have a parent to have special features
            // In other words, it must be part of the Parent/Child tree
            if (LocationType == LocationType.FileSystem && GetParent() != null && !DetectIsInMixedFolder())
            {
                var specialFeaturesChanged = await RefreshSpecialFeatures(options, fileSystemChildren, cancellationToken).ConfigureAwait(false);

                if (specialFeaturesChanged)
                {
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        private async Task<bool> RefreshSpecialFeatures(MetadataRefreshOptions options, List<FileSystemMetadata> fileSystemChildren, CancellationToken cancellationToken)
        {
            var newItems = LibraryManager.FindExtras(this, fileSystemChildren, options.DirectoryService).ToList();
            var newItemIds = newItems.Select(i => i.Id).ToList();

            var itemsChanged = !SpecialFeatureIds.SequenceEqual(newItemIds);

            var tasks = newItems.Select(i => RefreshMetadataForOwnedItem(i, false, options, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            SpecialFeatureIds = newItemIds;

            return itemsChanged;
        }

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Movie;
        }

        public MovieInfo GetLookupInfo()
        {
            var info = GetItemLookupInfo<MovieInfo>();

            if (!DetectIsInMixedFolder())
            {
                var name = System.IO.Path.GetFileName(ContainingFolderPath);

                if (VideoType == VideoType.VideoFile || VideoType == VideoType.Iso)
                {
                    if (string.Equals(name, System.IO.Path.GetFileName(Path), StringComparison.OrdinalIgnoreCase))
                    {
                        // if the folder has the file extension, strip it
                        name = System.IO.Path.GetFileNameWithoutExtension(name);
                    }
                }

                info.Name = name;
            }

            return info;
        }

        public override bool BeforeMetadataRefresh()
        {
            var hasChanges = base.BeforeMetadataRefresh();

            if (!ProductionYear.HasValue)
            {
                var info = LibraryManager.ParseName(Name);

                var yearInName = info.Year;

                if (yearInName.HasValue)
                {
                    ProductionYear = yearInName;
                    hasChanges = true;
                }
                else
                {
                    // Try to get the year from the folder name
                    if (!DetectIsInMixedFolder())
                    {
                        info = LibraryManager.ParseName(System.IO.Path.GetFileName(ContainingFolderPath));

                        yearInName = info.Year;

                        if (yearInName.HasValue)
                        {
                            ProductionYear = yearInName;
                            hasChanges = true;
                        }
                    }
                }
            }

            return hasChanges;
        }

        public override List<ExternalUrl> GetRelatedUrls()
        {
            var list = base.GetRelatedUrls();

            var imdbId = this.GetProviderId(MetadataProviders.Imdb);
            if (!string.IsNullOrWhiteSpace(imdbId))
            {
                list.Add(new ExternalUrl
                {
                    Name = "Trakt",
                    Url = string.Format("https://trakt.tv/movies/{0}", imdbId)
                });
            }

            return list;
        }

        [IgnoreDataMember]
        public override bool StopRefreshIfLocalMetadataFound
        {
            get
            {
                // Need people id's from internet metadata
                return false;
            }
        }
    }
}

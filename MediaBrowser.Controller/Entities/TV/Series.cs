﻿using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Controller.Entities.TV
{
    /// <summary>
    /// Class Series
    /// </summary>
    public class Series : Folder, IHasTrailers, IHasDisplayOrder, IHasLookupInfo<SeriesInfo>, IMetadataContainer
    {
        public Series()
        {
            AirDays = new List<DayOfWeek>();

            RemoteTrailers = new List<MediaUrl>();
            LocalTrailerIds = new List<Guid>();
            RemoteTrailerIds = new List<Guid>();
        }

        [IgnoreDataMember]
        public override bool SupportsAddingToPlaylist
        {
            get { return true; }
        }

        [IgnoreDataMember]
        public override bool IsPreSorted
        {
            get
            {
                return true;
            }
        }

        [IgnoreDataMember]
        public override bool SupportsDateLastMediaAdded
        {
            get
            {
                return true;
            }
        }

        public List<Guid> LocalTrailerIds { get; set; }
        public List<Guid> RemoteTrailerIds { get; set; }

        public List<MediaUrl> RemoteTrailers { get; set; }

        /// <summary>
        /// airdate, dvd or absolute
        /// </summary>
        public string DisplayOrder { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public SeriesStatus? Status { get; set; }
        /// <summary>
        /// Gets or sets the air days.
        /// </summary>
        /// <value>The air days.</value>
        public List<DayOfWeek> AirDays { get; set; }
        /// <summary>
        /// Gets or sets the air time.
        /// </summary>
        /// <value>The air time.</value>
        public string AirTime { get; set; }

        /// <summary>
        /// Gets or sets the date last episode added.
        /// </summary>
        /// <value>The date last episode added.</value>
        [IgnoreDataMember]
        public DateTime DateLastEpisodeAdded
        {
            get
            {
                return DateLastMediaAdded ?? DateTime.MinValue;
            }
        }

        public override double? GetDefaultPrimaryImageAspectRatio()
        {
            double value = 2;
            value /= 3;

            return value;
        }

        public override string CreatePresentationUniqueKey()
        {
            if (LibraryManager.GetLibraryOptions(this).EnableAutomaticSeriesGrouping)
            {
                var userdatakeys = GetUserDataKeys();

                if (userdatakeys.Count > 1)
                {
                    return AddLibrariesToPresentationUniqueKey(userdatakeys[0]);
                }
            }

            return base.CreatePresentationUniqueKey();
        }

        private string AddLibrariesToPresentationUniqueKey(string key)
        {
            var lang = GetPreferredMetadataLanguage();
            if (!string.IsNullOrWhiteSpace(lang))
            {
                key += "-" + lang;
            }

            var folders = LibraryManager.GetCollectionFolders(this)
                .Select(i => i.Id.ToString("N"))
                .ToArray();

            if (folders.Length == 0)
            {
                return key;
            }

            return key + "-" + string.Join("-", folders);
        }

        private static string GetUniqueSeriesKey(BaseItem series)
        {
            return series.GetPresentationUniqueKey();
        }

        public override int GetChildCount(User user)
        {
            var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;
            var seriesKey = GetUniqueSeriesKey(this);

            var result = LibraryManager.GetCount(new InternalItemsQuery(user)
            {
                AncestorWithPresentationUniqueKey = enableSeriesPresentationKey ? null : seriesKey,
                SeriesPresentationUniqueKey = enableSeriesPresentationKey ? seriesKey : null,
                IncludeItemTypes = new[] { typeof(Season).Name },
                IsVirtualItem = false,
                Limit = 0,
                DtoOptions = new Dto.DtoOptions
                {
                    Fields = new List<ItemFields>
                    {

                    },
                    EnableImages = false
                }
            });

            return result;
        }

        public override int GetRecursiveChildCount(User user)
        {
            var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;
            var seriesKey = GetUniqueSeriesKey(this);

            var query = new InternalItemsQuery(user)
            {
                AncestorWithPresentationUniqueKey = enableSeriesPresentationKey ? null : seriesKey,
                SeriesPresentationUniqueKey = enableSeriesPresentationKey ? seriesKey : null,
                DtoOptions = new Dto.DtoOptions
                {
                    Fields = new List<ItemFields>
                    {
                        
                    },
                    EnableImages = false
                }
            };

            if (query.IncludeItemTypes.Length == 0)
            {
                query.IncludeItemTypes = new[] { typeof(Episode).Name, typeof(Season).Name };
            }
            query.IsVirtualItem = false;
            query.Limit = 0;
            var totalRecordCount = LibraryManager.GetCount(query);

            return totalRecordCount;
        }

        /// <summary>
        /// Gets the user data key.
        /// </summary>
        /// <returns>System.String.</returns>
        public override List<string> GetUserDataKeys()
        {
            var list = base.GetUserDataKeys();

            var key = this.GetProviderId(MetadataProviders.Imdb);
            if (!string.IsNullOrWhiteSpace(key))
            {
                list.Insert(0, key);
            }

            key = this.GetProviderId(MetadataProviders.Tvdb);
            if (!string.IsNullOrWhiteSpace(key))
            {
                list.Insert(0, key);
            }

            return list;
        }

        /// <summary>
        /// Gets the trailer ids.
        /// </summary>
        /// <returns>List&lt;Guid&gt;.</returns>
        public List<Guid> GetTrailerIds()
        {
            var list = LocalTrailerIds.ToList();
            list.AddRange(RemoteTrailerIds);
            return list;
        }

        // Studio, Genre and Rating will all be the same so makes no sense to index by these
        protected override IEnumerable<string> GetIndexByOptions()
        {
            return new List<string> {
                {"None"},
                {"Performer"},
                {"Director"},
                {"Year"},
            };
        }

        [IgnoreDataMember]
        public bool ContainsEpisodesWithoutSeasonFolders
        {
            get
            {
                return Children.OfType<Video>().Any();
            }
        }

        public override IEnumerable<BaseItem> GetChildren(User user, bool includeLinkedChildren)
        {
            return GetSeasons(user);
        }

        public IEnumerable<Season> GetSeasons(User user)
        {
            var query = new InternalItemsQuery(user);

            SetSeasonQueryOptions(query, user);

            return LibraryManager.GetItemList(query).Cast<Season>();
        }

        private void SetSeasonQueryOptions(InternalItemsQuery query, User user)
        {
            var config = user.Configuration;

            var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;
            var seriesKey = GetUniqueSeriesKey(this);

            query.AncestorWithPresentationUniqueKey = enableSeriesPresentationKey ? null : seriesKey;
            query.SeriesPresentationUniqueKey = enableSeriesPresentationKey ? seriesKey : null;
            query.IncludeItemTypes = new[] { typeof(Season).Name };
            query.SortBy = new[] {ItemSortBy.SortName};

            if (!config.DisplayMissingEpisodes && !config.DisplayUnairedEpisodes)
            {
                query.IsVirtualItem = false;
            }
            else if (!config.DisplayMissingEpisodes)
            {
                query.IsMissing = false;
            }
            else if (!config.DisplayUnairedEpisodes)
            {
                query.IsVirtualUnaired = false;
            }
        }

        protected override Task<QueryResult<BaseItem>> GetItemsInternal(InternalItemsQuery query)
        {
            if (query.User == null)
            {
                return base.GetItemsInternal(query);
            }

            var user = query.User;

            if (query.Recursive)
            {
                var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;
                var seriesKey = GetUniqueSeriesKey(this);

                query.AncestorWithPresentationUniqueKey = enableSeriesPresentationKey ? null : seriesKey;
                query.SeriesPresentationUniqueKey = enableSeriesPresentationKey ? seriesKey : null;
                if (query.SortBy.Length == 0)
                {
                    query.SortBy = new[] { ItemSortBy.SortName };
                }
                if (query.IncludeItemTypes.Length == 0)
                {
                    query.IncludeItemTypes = new[] { typeof(Episode).Name, typeof(Season).Name };
                }
                query.IsVirtualItem = false;
                return Task.FromResult(LibraryManager.GetItemsResult(query));
            }

            SetSeasonQueryOptions(query, user);

            return Task.FromResult(LibraryManager.GetItemsResult(query));
        }

        public IEnumerable<Episode> GetEpisodes(User user)
        {
            var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;
            var seriesKey = GetUniqueSeriesKey(this);

            var query = new InternalItemsQuery(user)
            {
                AncestorWithPresentationUniqueKey = enableSeriesPresentationKey ? null : seriesKey,
                SeriesPresentationUniqueKey = enableSeriesPresentationKey ? seriesKey : null,
                IncludeItemTypes = new[] { typeof(Episode).Name, typeof(Season).Name },
                SortBy = new[] { ItemSortBy.SortName }
            };
            var config = user.Configuration;
            if (!config.DisplayMissingEpisodes && !config.DisplayUnairedEpisodes)
            {
                query.IsVirtualItem = false;
            }
            else if (!config.DisplayMissingEpisodes)
            {
                query.IsMissing = false;
            }
            else if (!config.DisplayUnairedEpisodes)
            {
                query.IsVirtualUnaired = false;
            }

            var allItems = LibraryManager.GetItemList(query).ToList();

            var allSeriesEpisodes = allItems.OfType<Episode>().ToList();

            var allEpisodes = allItems.OfType<Season>()
                .SelectMany(i => i.GetEpisodes(this, user, allSeriesEpisodes))
                .Reverse()
                .ToList();

            // Specials could appear twice based on above - once in season 0, once in the aired season
            // This depends on settings for that series
            // When this happens, remove the duplicate from season 0

            return allEpisodes.DistinctBy(i => i.Id).Reverse();
        }

        public async Task RefreshAllMetadata(MetadataRefreshOptions refreshOptions, IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Refresh bottom up, children first, then the boxset
            // By then hopefully the  movies within will have Tmdb collection values
            var items = GetRecursiveChildren().ToList();

            var seasons = items.OfType<Season>().ToList();
            var otherItems = items.Except(seasons).ToList();

            var totalItems = seasons.Count + otherItems.Count;
            var numComplete = 0;

            // Refresh current item
            await RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);

            // Refresh seasons
            foreach (var item in seasons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await item.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);

                numComplete++;
                double percent = numComplete;
                percent /= totalItems;
                progress.Report(percent * 100);
            }

            // Refresh episodes and other children
            foreach (var item in otherItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var skipItem = false;

                var episode = item as Episode;

                if (episode != null
                    && refreshOptions.MetadataRefreshMode != MetadataRefreshMode.FullRefresh
                    && !refreshOptions.ReplaceAllMetadata
                    && episode.IsMissingEpisode
                    && episode.LocationType == LocationType.Virtual
                    && episode.PremiereDate.HasValue
                    && (DateTime.UtcNow - episode.PremiereDate.Value).TotalDays > 30)
                {
                    skipItem = true;
                }

                if (!skipItem)
                {
                    await item.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
                }

                numComplete++;
                double percent = numComplete;
                percent /= totalItems;
                progress.Report(percent * 100);
            }

            refreshOptions = new MetadataRefreshOptions(refreshOptions);
            refreshOptions.IsPostRecursiveRefresh = true;
            await ProviderManager.RefreshSingleItem(this, refreshOptions, cancellationToken).ConfigureAwait(false);

            progress.Report(100);
        }

        public IEnumerable<Episode> GetSeasonEpisodes(Season parentSeason, User user)
        {
            var enableSeriesPresentationKey = ConfigurationManager.Configuration.EnableSeriesPresentationUniqueKey;

            var queryFromSeries = ConfigurationManager.Configuration.DisplaySpecialsWithinSeasons;

            // add optimization when this setting is not enabled
            var seriesKey = queryFromSeries ?
                GetUniqueSeriesKey(this) :
                GetUniqueSeriesKey(parentSeason);

            var query = new InternalItemsQuery(user)
            {
                AncestorWithPresentationUniqueKey = queryFromSeries && enableSeriesPresentationKey ? null : seriesKey,
                SeriesPresentationUniqueKey = queryFromSeries && enableSeriesPresentationKey ? seriesKey : null,
                IncludeItemTypes = new[] { typeof(Episode).Name },
                SortBy = new[] { ItemSortBy.SortName }
            };
            if (user != null)
            {
                var config = user.Configuration;
                if (!config.DisplayMissingEpisodes && !config.DisplayUnairedEpisodes)
                {
                    query.IsVirtualItem = false;
                }
                else if (!config.DisplayMissingEpisodes)
                {
                    query.IsMissing = false;
                }
                else if (!config.DisplayUnairedEpisodes)
                {
                    query.IsVirtualUnaired = false;
                }
            }

            var allItems = LibraryManager.GetItemList(query).OfType<Episode>();

            return GetSeasonEpisodes(parentSeason, user, allItems);
        }

        public IEnumerable<Episode> GetSeasonEpisodes(Season parentSeason, User user, IEnumerable<Episode> allSeriesEpisodes)
        {
            if (allSeriesEpisodes == null)
            {
                return GetSeasonEpisodes(parentSeason, user);
            }

            var episodes = FilterEpisodesBySeason(allSeriesEpisodes, parentSeason, ConfigurationManager.Configuration.DisplaySpecialsWithinSeasons);

            var sortBy = (parentSeason.IndexNumber ?? -1) == 0 ? ItemSortBy.SortName : ItemSortBy.AiredEpisodeOrder;

            return LibraryManager.Sort(episodes, user, new[] { sortBy }, SortOrder.Ascending)
                .Cast<Episode>();
        }

        /// <summary>
        /// Filters the episodes by season.
        /// </summary>
        public static IEnumerable<Episode> FilterEpisodesBySeason(IEnumerable<Episode> episodes, Season parentSeason, bool includeSpecials)
        {
            var seasonNumber = parentSeason.IndexNumber;
            var seasonPresentationKey = GetUniqueSeriesKey(parentSeason);

            var supportSpecialsInSeason = includeSpecials && seasonNumber.HasValue && seasonNumber.Value != 0;

            return episodes.Where(episode =>
            {
                var currentSeasonNumber = supportSpecialsInSeason ? episode.AiredSeasonNumber : episode.ParentIndexNumber;
                if (currentSeasonNumber.HasValue && seasonNumber.HasValue && currentSeasonNumber.Value == seasonNumber.Value)
                {
                    return true;
                }

                if (!currentSeasonNumber.HasValue && !seasonNumber.HasValue && parentSeason.LocationType == LocationType.Virtual)
                {
                    return true;
                }

                var season = episode.Season;
                return season != null && string.Equals(GetUniqueSeriesKey(season), seasonPresentationKey, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Filters the episodes by season.
        /// </summary>
        public static IEnumerable<Episode> FilterEpisodesBySeason(IEnumerable<Episode> episodes, int seasonNumber, bool includeSpecials)
        {
            if (!includeSpecials || seasonNumber < 1)
            {
                return episodes.Where(i => (i.ParentIndexNumber ?? -1) == seasonNumber);
            }

            return episodes.Where(i =>
            {
                var episode = i;

                if (episode != null)
                {
                    var currentSeasonNumber = episode.AiredSeasonNumber;

                    return currentSeasonNumber.HasValue && currentSeasonNumber.Value == seasonNumber;
                }

                return false;
            });
        }


        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            return config.BlockUnratedItems.Contains(UnratedItem.Series);
        }

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Series;
        }

        public SeriesInfo GetLookupInfo()
        {
            var info = GetItemLookupInfo<SeriesInfo>();

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
                    Url = string.Format("https://trakt.tv/shows/{0}", imdbId)
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

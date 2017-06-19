﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Manager
{
    public abstract class MetadataService<TItemType, TIdType> : IMetadataService
        where TItemType : IHasMetadata, IHasLookupInfo<TIdType>, new()
        where TIdType : ItemLookupInfo, new()
    {
        protected readonly IServerConfigurationManager ServerConfigurationManager;
        protected readonly ILogger Logger;
        protected readonly IProviderManager ProviderManager;
        protected readonly IFileSystem FileSystem;
        protected readonly IUserDataManager UserDataManager;
        protected readonly ILibraryManager LibraryManager;

        protected MetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IFileSystem fileSystem, IUserDataManager userDataManager, ILibraryManager libraryManager)
        {
            ServerConfigurationManager = serverConfigurationManager;
            Logger = logger;
            ProviderManager = providerManager;
            FileSystem = fileSystem;
            UserDataManager = userDataManager;
            LibraryManager = libraryManager;
        }

        public async Task<ItemUpdateType> RefreshMetadata(IHasMetadata item, MetadataRefreshOptions refreshOptions, CancellationToken cancellationToken)
        {
            var itemOfType = (TItemType)item;
            var config = ProviderManager.GetMetadataOptions(item);

            var updateType = ItemUpdateType.None;
            var requiresRefresh = false;

            var libraryOptions = LibraryManager.GetLibraryOptions((BaseItem)item);

            if (refreshOptions.MetadataRefreshMode != MetadataRefreshMode.None)
            {
                // TODO: If this returns true, should we instead just change metadata refresh mode to Full?
                requiresRefresh = item.RequiresRefresh();
            }

            if (!requiresRefresh && 
                libraryOptions.AutomaticRefreshIntervalDays > 0 && 
                (DateTime.UtcNow - item.DateLastRefreshed).TotalDays >= libraryOptions.AutomaticRefreshIntervalDays)
            {
                requiresRefresh = true;
            }

            var itemImageProvider = new ItemImageProvider(Logger, ProviderManager, ServerConfigurationManager, FileSystem);
            var localImagesFailed = false;

            var allImageProviders = ((ProviderManager)ProviderManager).GetImageProviders(item, refreshOptions).ToList();

            // Start by validating images
            try
            {
                // Always validate images and check for new locally stored ones.
                if (itemImageProvider.ValidateImages(item, allImageProviders.OfType<ILocalImageProvider>(), refreshOptions.DirectoryService))
                {
                    updateType = updateType | ItemUpdateType.ImageUpdate;
                }
            }
            catch (Exception ex)
            {
                localImagesFailed = true;
                Logger.ErrorException("Error validating images for {0}", ex, item.Path ?? item.Name ?? "Unknown name");
            }

            var metadataResult = new MetadataResult<TItemType>
            {
                Item = itemOfType
            };

            bool hasRefreshedMetadata = true;
            bool hasRefreshedImages = true;
            var isFirstRefresh = item.DateLastRefreshed == default(DateTime);

            // Next run metadata providers
            if (refreshOptions.MetadataRefreshMode != MetadataRefreshMode.None)
            {
                var providers = GetProviders(item, refreshOptions, isFirstRefresh, requiresRefresh)
                    .ToList();

                if (providers.Count > 0 || isFirstRefresh)
                {
                    if (item.BeforeMetadataRefresh())
                    {
                        updateType = updateType | ItemUpdateType.MetadataImport;
                    }
                }

                if (providers.Count > 0)
                {
                    var id = itemOfType.GetLookupInfo();

                    if (refreshOptions.SearchResult != null)
                    {
                        ApplySearchResult(id, refreshOptions.SearchResult);
                    }

                    //await FindIdentities(id, cancellationToken).ConfigureAwait(false);
                    id.IsAutomated = refreshOptions.IsAutomated;

                    var result = await RefreshWithProviders(metadataResult, id, refreshOptions, providers, itemImageProvider, cancellationToken).ConfigureAwait(false);

                    updateType = updateType | result.UpdateType;
                    if (result.Failures > 0)
                    {
                        hasRefreshedMetadata = false;
                    }
                }
            }

            // Next run remote image providers, but only if local image providers didn't throw an exception
            if (!localImagesFailed && refreshOptions.ImageRefreshMode != ImageRefreshMode.ValidationOnly)
            {
                var providers = GetNonLocalImageProviders(item, allImageProviders, refreshOptions).ToList();

                if (providers.Count > 0)
                {
                    var result = await itemImageProvider.RefreshImages(itemOfType, libraryOptions, providers, refreshOptions, config, cancellationToken).ConfigureAwait(false);

                    updateType = updateType | result.UpdateType;
                    if (result.Failures > 0)
                    {
                        hasRefreshedImages = false;
                    }
                }
            }

            var beforeSaveResult = await BeforeSave(itemOfType, isFirstRefresh || refreshOptions.ReplaceAllMetadata || refreshOptions.MetadataRefreshMode == MetadataRefreshMode.FullRefresh || requiresRefresh, updateType).ConfigureAwait(false);
            updateType = updateType | beforeSaveResult;

            if (item.LocationType == LocationType.FileSystem)
            {
                var file = refreshOptions.DirectoryService.GetFile(item.Path);
                if (file != null)
                {
                    var fileLastWriteTime = file.LastWriteTimeUtc;
                    if (item.EnableRefreshOnDateModifiedChange && fileLastWriteTime != item.DateModified)
                    {
                        Logger.Debug("Date modified for {0}. Old date {1} new date {2} Id {3}", item.Path, item.DateModified, fileLastWriteTime, item.Id);
                        requiresRefresh = true;
                    }

                    item.DateModified = fileLastWriteTime;
                }
            }

            // Save if changes were made, or it's never been saved before
            if (refreshOptions.ForceSave || updateType > ItemUpdateType.None || isFirstRefresh || refreshOptions.ReplaceAllMetadata || requiresRefresh)
            {
                // If any of these properties are set then make sure the updateType is not None, just to force everything to save
                if (refreshOptions.ForceSave || refreshOptions.ReplaceAllMetadata)
                {
                    updateType = updateType | ItemUpdateType.MetadataDownload;
                }

                if (hasRefreshedMetadata && hasRefreshedImages)
                {
                    item.DateLastRefreshed = DateTime.UtcNow;
                }
                else
                {
                    item.DateLastRefreshed = default(DateTime);
                }

                // Save to database
                await SaveItem(metadataResult, libraryOptions, updateType, cancellationToken).ConfigureAwait(false);
            }

            await AfterMetadataRefresh(itemOfType, refreshOptions, cancellationToken).ConfigureAwait(false);

            return updateType;
        }

        private void ApplySearchResult(ItemLookupInfo lookupInfo, RemoteSearchResult result)
        {
            lookupInfo.ProviderIds = result.ProviderIds;
            lookupInfo.Name = result.Name;
            lookupInfo.Year = result.ProductionYear;
        }

        protected async Task SaveItem(MetadataResult<TItemType> result, LibraryOptions libraryOptions, ItemUpdateType reason, CancellationToken cancellationToken)
        {
            if (result.Item.SupportsPeople && result.People != null)
            {
                var baseItem = result.Item as BaseItem;

                await LibraryManager.UpdatePeople(baseItem, result.People.ToList());
                await SavePeopleMetadata(result.People, libraryOptions, cancellationToken).ConfigureAwait(false);
            }
            await result.Item.UpdateToRepository(reason, cancellationToken).ConfigureAwait(false);
        }

        private async Task SavePeopleMetadata(List<PersonInfo> people, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            foreach (var person in people)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (person.ProviderIds.Any() || !string.IsNullOrWhiteSpace(person.ImageUrl))
                {
                    var updateType = ItemUpdateType.MetadataDownload;

                    var saveEntity = false;
                    var personEntity = LibraryManager.GetPerson(person.Name);
                    foreach (var id in person.ProviderIds)
                    {
                        if (!string.Equals(personEntity.GetProviderId(id.Key), id.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            personEntity.SetProviderId(id.Key, id.Value);
                            saveEntity = true;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(person.ImageUrl) && !personEntity.HasImage(ImageType.Primary))
                    {
                        await AddPersonImage(personEntity, libraryOptions, person.ImageUrl, cancellationToken).ConfigureAwait(false);

                        saveEntity = true;
                        updateType = updateType | ItemUpdateType.ImageUpdate;
                    }

                    if (saveEntity)
                    {
                        await personEntity.UpdateToRepository(updateType, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task AddPersonImage(Person personEntity, LibraryOptions libraryOptions, string imageUrl, CancellationToken cancellationToken)
        {
            if (libraryOptions.DownloadImagesInAdvance)
            {
                try
                {
                    await ProviderManager.SaveImage(personEntity, imageUrl, ImageType.Primary, null, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in AddPersonImage", ex);
                }
            }

            personEntity.SetImage(new ItemImageInfo
            {
                Path = imageUrl,
                Type = ImageType.Primary,
                IsPlaceholder = true
            }, 0);
        }

        private readonly Task _cachedTask = Task.FromResult(true);
        protected virtual Task AfterMetadataRefresh(TItemType item, MetadataRefreshOptions refreshOptions, CancellationToken cancellationToken)
        {
            item.AfterMetadataRefresh();
            return _cachedTask;
        }

        /// <summary>
        /// Befores the save.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="isFullRefresh">if set to <c>true</c> [is full refresh].</param>
        /// <param name="currentUpdateType">Type of the current update.</param>
        /// <returns>ItemUpdateType.</returns>
        protected virtual async Task<ItemUpdateType> BeforeSave(TItemType item, bool isFullRefresh, ItemUpdateType currentUpdateType)
        {
            var updateType = ItemUpdateType.None;

            updateType |= SaveCumulativeRunTimeTicks(item, isFullRefresh, currentUpdateType);
            updateType |= SaveDateLastMediaAdded(item, isFullRefresh, currentUpdateType);

            var presentationUniqueKey = item.CreatePresentationUniqueKey();
            if (!string.Equals(item.PresentationUniqueKey, presentationUniqueKey, StringComparison.Ordinal))
            {
                item.PresentationUniqueKey = presentationUniqueKey;
                updateType |= ItemUpdateType.MetadataImport;
            }

            var inheritedParentalRatingValue = item.GetInheritedParentalRatingValue() ?? 0;
            if (inheritedParentalRatingValue != item.InheritedParentalRatingValue)
            {
                item.InheritedParentalRatingValue = inheritedParentalRatingValue;
                updateType |= ItemUpdateType.MetadataImport;
            }

            var inheritedTags = item.GetInheritedTags();
            if (!inheritedTags.SequenceEqual(item.InheritedTags, StringComparer.Ordinal))
            {
                item.InheritedTags = inheritedTags;
                updateType |= ItemUpdateType.MetadataImport;
            }

            return updateType;
        }

        private ItemUpdateType SaveCumulativeRunTimeTicks(TItemType item, bool isFullRefresh, ItemUpdateType currentUpdateType)
        {
            var updateType = ItemUpdateType.None;

            if (isFullRefresh || currentUpdateType > ItemUpdateType.None)
            {
                var folder = item as Folder;
                if (folder != null && folder.SupportsCumulativeRunTimeTicks)
                {
                    var items = folder.GetRecursiveChildren(i => !i.IsFolder).ToList();
                    var ticks = items.Select(i => i.RunTimeTicks ?? 0).Sum();

                    if (!folder.RunTimeTicks.HasValue || folder.RunTimeTicks.Value != ticks)
                    {
                        folder.RunTimeTicks = ticks;
                        updateType = ItemUpdateType.MetadataEdit;
                    }
                }
            }

            return updateType;
        }

        private ItemUpdateType SaveDateLastMediaAdded(TItemType item, bool isFullRefresh, ItemUpdateType currentUpdateType)
        {
            var updateType = ItemUpdateType.None;

            var folder = item as Folder;
            if (folder != null && folder.SupportsDateLastMediaAdded)
            {
                var items = folder.GetRecursiveChildren(i => !i.IsFolder).Select(i => i.DateCreated).ToList();
                var date = items.Count == 0 ? (DateTime?)null : items.Max();

                if ((!folder.DateLastMediaAdded.HasValue && date.HasValue) || folder.DateLastMediaAdded != date)
                {
                    folder.DateLastMediaAdded = date;
                    updateType = ItemUpdateType.MetadataImport;
                }
            }

            return updateType;
        }

        /// <summary>
        /// Gets the providers.
        /// </summary>
        /// <returns>IEnumerable{`0}.</returns>
        protected IEnumerable<IMetadataProvider> GetProviders(IHasMetadata item, MetadataRefreshOptions options, bool isFirstRefresh, bool requiresRefresh)
        {
            // Get providers to refresh
            var providers = ((ProviderManager)ProviderManager).GetMetadataProviders<TItemType>(item).ToList();

            var metadataRefreshMode = options.MetadataRefreshMode;

            // Run all if either of these flags are true
            var runAllProviders = options.ReplaceAllMetadata ||
                metadataRefreshMode == MetadataRefreshMode.FullRefresh || 
                (isFirstRefresh && metadataRefreshMode >= MetadataRefreshMode.Default) ||
                (requiresRefresh && metadataRefreshMode >= MetadataRefreshMode.Default);

            if (!runAllProviders)
            {
                var providersWithChanges = providers
                    .Where(i =>
                    {
                        var hasFileChangeMonitor = i as IHasItemChangeMonitor;
                        if (hasFileChangeMonitor != null)
                        {
                            return HasChanged(item, hasFileChangeMonitor, options.DirectoryService);
                        }

                        return false;
                    })
                    .ToList();

                if (providersWithChanges.Count == 0)
                {
                    providers = new List<IMetadataProvider<TItemType>>();
                }
                else
                {
                    var anyRemoteProvidersChanged = providersWithChanges.OfType<IRemoteMetadataProvider>()
                        .Any();

                    providers = providers.Where(i =>
                    {
                        // If any provider reports a change, always run local ones as well
                        if (i is ILocalMetadataProvider)
                        {
                            return true;
                        }

                        // If any remote providers changed, run them all so that priorities can be honored
                        if (i is IRemoteMetadataProvider)
                        {
                            if (options.MetadataRefreshMode == MetadataRefreshMode.ValidationOnly)
                            {
                                return false;
                            }

                            return anyRemoteProvidersChanged;
                        }

                        // Run custom providers if they report a change or any remote providers change
                        return anyRemoteProvidersChanged || providersWithChanges.Contains(i);

                    }).ToList();
                }
            }

            return providers;
        }

        protected virtual IEnumerable<IImageProvider> GetNonLocalImageProviders(IHasMetadata item, IEnumerable<IImageProvider> allImageProviders, ImageRefreshOptions options)
        {
            // Get providers to refresh
            var providers = allImageProviders.Where(i => !(i is ILocalImageProvider)).ToList();

            var dateLastImageRefresh = item.DateLastRefreshed;

            // Run all if either of these flags are true
            var runAllProviders = options.ImageRefreshMode == ImageRefreshMode.FullRefresh || dateLastImageRefresh == default(DateTime);

            if (!runAllProviders)
            {
                providers = providers
                    .Where(i =>
                    {
                        var hasFileChangeMonitor = i as IHasItemChangeMonitor;
                        if (hasFileChangeMonitor != null)
                        {
                            return HasChanged(item, hasFileChangeMonitor, options.DirectoryService);
                        }

                        return false;
                    })
                    .ToList();
            }

            return providers;
        }

        public bool CanRefresh(IHasMetadata item)
        {
            return item is TItemType;
        }

        protected virtual async Task<RefreshResult> RefreshWithProviders(MetadataResult<TItemType> metadata,
            TIdType id,
            MetadataRefreshOptions options,
            List<IMetadataProvider> providers,
            ItemImageProvider imageService,
            CancellationToken cancellationToken)
        {
            var refreshResult = new RefreshResult
            {
                UpdateType = ItemUpdateType.None,
                Providers = providers.Select(i => i.GetType().FullName.GetMD5()).ToList()
            };

            var item = metadata.Item;

            var customProviders = providers.OfType<ICustomMetadataProvider<TItemType>>().ToList();
            var logName = item.LocationType == LocationType.Remote ? item.Name ?? item.Path : item.Path ?? item.Name;

            foreach (var provider in customProviders.Where(i => i is IPreRefreshProvider))
            {
                await RunCustomProvider(provider, item, logName, options, refreshResult, cancellationToken).ConfigureAwait(false);
            }

            var temp = new MetadataResult<TItemType>
            {
                Item = CreateNew()
            };
            temp.Item.Path = item.Path;

            var userDataList = new List<UserItemData>();

            // If replacing all metadata, run internet providers first
            if (options.ReplaceAllMetadata)
            {
                var remoteResult = await ExecuteRemoteProviders(temp, logName, id, providers.OfType<IRemoteMetadataProvider<TItemType, TIdType>>(), cancellationToken)
                    .ConfigureAwait(false);

                refreshResult.UpdateType = refreshResult.UpdateType | remoteResult.UpdateType;
                refreshResult.ErrorMessage = remoteResult.ErrorMessage;
                refreshResult.Failures += remoteResult.Failures;
            }

            var hasLocalMetadata = false;

            foreach (var provider in providers.OfType<ILocalMetadataProvider<TItemType>>().ToList())
            {
                var providerName = provider.GetType().Name;
                Logger.Debug("Running {0} for {1}", providerName, logName);

                var itemInfo = new ItemInfo(item);

                try
                {
                    var localItem = await provider.GetMetadata(itemInfo, options.DirectoryService, cancellationToken).ConfigureAwait(false);

                    if (localItem.HasMetadata)
                    {
                        if (imageService.MergeImages(item, localItem.Images))
                        {
                            refreshResult.UpdateType = refreshResult.UpdateType | ItemUpdateType.ImageUpdate;
                        }

                        if (localItem.UserDataList != null)
                        {
                            userDataList.AddRange(localItem.UserDataList);
                        }

                        MergeData(localItem, temp, new List<MetadataFields>(), !options.ReplaceAllMetadata, true);
                        refreshResult.UpdateType = refreshResult.UpdateType | ItemUpdateType.MetadataImport;

                        // Only one local provider allowed per item
                        if (item.IsLocked || localItem.Item.IsLocked || IsFullLocalMetadata(localItem.Item))
                        {
                            hasLocalMetadata = true;
                        }
                        break;
                    }

                    Logger.Debug("{0} returned no metadata for {1}", providerName, logName);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in {0}", ex, provider.Name);

                    // If a local provider fails, consider that a failure
                    refreshResult.ErrorMessage = ex.Message;
                }
            }

            // Local metadata is king - if any is found don't run remote providers
            if (!options.ReplaceAllMetadata && (!hasLocalMetadata || options.MetadataRefreshMode == MetadataRefreshMode.FullRefresh || !item.StopRefreshIfLocalMetadataFound))
            {
                var remoteResult = await ExecuteRemoteProviders(temp, logName, id, providers.OfType<IRemoteMetadataProvider<TItemType, TIdType>>(), cancellationToken)
                    .ConfigureAwait(false);

                refreshResult.UpdateType = refreshResult.UpdateType | remoteResult.UpdateType;
                refreshResult.ErrorMessage = remoteResult.ErrorMessage;
                refreshResult.Failures += remoteResult.Failures;
            }

            if (providers.Any(i => !(i is ICustomMetadataProvider)))
            {
                if (refreshResult.UpdateType > ItemUpdateType.None)
                {
                    if (hasLocalMetadata)
                    {
                        MergeData(temp, metadata, item.LockedFields, true, true);
                    }
                    else
                    {
                        // TODO: If the new metadata from above has some blank data, this can cause old data to get filled into those empty fields
                        MergeData(metadata, temp, new List<MetadataFields>(), false, false);
                        MergeData(temp, metadata, item.LockedFields, true, false);
                    }
                }
            }

            //var isUnidentified = failedProviderCount > 0 && successfulProviderCount == 0;

            foreach (var provider in customProviders.Where(i => !(i is IPreRefreshProvider)))
            {
                await RunCustomProvider(provider, item, logName, options, refreshResult, cancellationToken).ConfigureAwait(false);
            }

            await ImportUserData(item, userDataList, cancellationToken).ConfigureAwait(false);

            return refreshResult;
        }

        protected virtual bool IsFullLocalMetadata(TItemType item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return false;
            }

            return true;
        }

        private async Task ImportUserData(TItemType item, List<UserItemData> userDataList, CancellationToken cancellationToken)
        {
            var hasUserData = item as IHasUserData;

            if (hasUserData != null)
            {
                foreach (var userData in userDataList)
                {
                    await UserDataManager.SaveUserData(userData.UserId, hasUserData, userData, UserDataSaveReason.Import, cancellationToken)
                            .ConfigureAwait(false);
                }
            }
        }

        private async Task RunCustomProvider(ICustomMetadataProvider<TItemType> provider, TItemType item, string logName, MetadataRefreshOptions options, RefreshResult refreshResult, CancellationToken cancellationToken)
        {
            Logger.Debug("Running {0} for {1}", provider.GetType().Name, logName);

            try
            {
                refreshResult.UpdateType = refreshResult.UpdateType | await provider.FetchAsync(item, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                refreshResult.ErrorMessage = ex.Message;
                Logger.ErrorException("Error in {0}", ex, provider.Name);
            }
        }

        protected virtual TItemType CreateNew()
        {
            return new TItemType();
        }

        private async Task<RefreshResult> ExecuteRemoteProviders(MetadataResult<TItemType> temp, string logName, TIdType id, IEnumerable<IRemoteMetadataProvider<TItemType, TIdType>> providers, CancellationToken cancellationToken)
        {
            var refreshResult = new RefreshResult();

            var results = new List<MetadataResult<TItemType>>();

            foreach (var provider in providers)
            {
                var providerName = provider.GetType().Name;
                Logger.Debug("Running {0} for {1}", providerName, logName);

                if (id != null)
                {
                    MergeNewData(temp.Item, id);
                }

                try
                {
                    var result = await provider.GetMetadata(id, cancellationToken).ConfigureAwait(false);

                    if (result.HasMetadata)
                    {
                        results.Add(result);

                        refreshResult.UpdateType = refreshResult.UpdateType | ItemUpdateType.MetadataDownload;
                    }
                    else
                    {
                        Logger.Debug("{0} returned no metadata for {1}", providerName, logName);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    refreshResult.Failures++;
                    refreshResult.ErrorMessage = ex.Message;
                    Logger.ErrorException("Error in {0}", ex, provider.Name);
                }
            }

            var orderedResults = new List<MetadataResult<TItemType>>();
            var preferredLanguage = NormalizeLanguage(id.MetadataLanguage);

            // prioritize results with matching ResultLanguage
            foreach (var result in results)
            {
                if (!result.QueriedById)
                {
                    break;
                }

                if (string.Equals(NormalizeLanguage(result.ResultLanguage), preferredLanguage, StringComparison.OrdinalIgnoreCase) && result.QueriedById)
                {
                    orderedResults.Add(result);
                }
            }

            // add all other results
            foreach (var result in results)
            {
                if (!orderedResults.Contains(result))
                {
                    orderedResults.Add(result);
                }
            }

            foreach (var result in results)
            {
                MergeData(result, temp, new List<MetadataFields>(), false, false);
            }

            return refreshResult;
        }

        private string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "en";
            }
            return language;
        }

        private void MergeNewData(TItemType source, TIdType lookupInfo)
        {
            // Copy new provider id's that may have been obtained
            foreach (var providerId in source.ProviderIds)
            {
                var key = providerId.Key;

                // Don't replace existing Id's.
                if (!lookupInfo.ProviderIds.ContainsKey(key))
                {
                    lookupInfo.ProviderIds[key] = providerId.Value;
                }
            }
        }

        protected abstract void MergeData(MetadataResult<TItemType> source,
            MetadataResult<TItemType> target,
            List<MetadataFields> lockedFields,
            bool replaceData,
            bool mergeMetadataSettings);

        public virtual int Order
        {
            get
            {
                return 0;
            }
        }

        private bool HasChanged(IHasMetadata item, IHasItemChangeMonitor changeMonitor, IDirectoryService directoryService)
        {
            try
            {
                var hasChanged = changeMonitor.HasChanged(item, directoryService);

                //if (hasChanged)
                //{
                //    Logger.Debug("{0} reports change to {1}", changeMonitor.GetType().Name, item.Path ?? item.Name);
                //}

                return hasChanged;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error in {0}.HasChanged", ex, changeMonitor.GetType().Name);
                return false;
            }
        }
    }

    public class RefreshResult
    {
        public ItemUpdateType UpdateType { get; set; }
        public string ErrorMessage { get; set; }
        public List<Guid> Providers { get; set; }
        public int Failures { get; set; }
    }
}

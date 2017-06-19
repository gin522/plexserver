﻿using System.Collections.Generic;
using MediaBrowser.Common.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;

namespace MediaBrowser.Providers.Folders
{
    public class CollectionFolderMetadataService : MetadataService<CollectionFolder, ItemLookupInfo>
    {
        protected override void MergeData(MetadataResult<CollectionFolder> source, MetadataResult<CollectionFolder> target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }

        public CollectionFolderMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IFileSystem fileSystem, IUserDataManager userDataManager, ILibraryManager libraryManager) : base(serverConfigurationManager, logger, providerManager, fileSystem, userDataManager, libraryManager)
        {
        }
    }

    public class ManualCollectionsFolderMetadataService : MetadataService<ManualCollectionsFolder, ItemLookupInfo>
    {
        protected override void MergeData(MetadataResult<ManualCollectionsFolder> source, MetadataResult<ManualCollectionsFolder> target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }

        public ManualCollectionsFolderMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IFileSystem fileSystem, IUserDataManager userDataManager, ILibraryManager libraryManager) : base(serverConfigurationManager, logger, providerManager, fileSystem, userDataManager, libraryManager)
        {
        }
    }

}

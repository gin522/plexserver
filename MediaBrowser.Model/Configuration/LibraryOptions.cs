﻿namespace MediaBrowser.Model.Configuration
{
    public class LibraryOptions
    {
        public bool EnableArchiveMediaFiles { get; set; }
        public bool EnablePhotos { get; set; }
        public bool EnableRealtimeMonitor { get; set; }
        public bool EnableChapterImageExtraction { get; set; }
        public bool ExtractChapterImagesDuringLibraryScan { get; set; }
        public bool DownloadImagesInAdvance { get; set; }
        public MediaPathInfo[] PathInfos { get; set; }

        public bool SaveLocalMetadata { get; set; }
        public bool EnableInternetProviders { get; set; }
        public bool ImportMissingEpisodes { get; set; }
        public bool EnableAutomaticSeriesGrouping { get; set; }
        public bool EnableEmbeddedTitles { get; set; }

        public int AutomaticRefreshIntervalDays { get; set; }

        /// <summary>
        /// Gets or sets the preferred metadata language.
        /// </summary>
        /// <value>The preferred metadata language.</value>
        public string PreferredMetadataLanguage { get; set; }

        /// <summary>
        /// Gets or sets the metadata country code.
        /// </summary>
        /// <value>The metadata country code.</value>
        public string MetadataCountryCode { get; set; }

        public LibraryOptions()
        {
            EnablePhotos = true;
            EnableRealtimeMonitor = true;
            PathInfos = new MediaPathInfo[] { };
            EnableInternetProviders = true;
            EnableAutomaticSeriesGrouping = true;
        }
    }

    public class MediaPathInfo
    {
        public string Path { get; set; }
        public string NetworkPath { get; set; }
    }
}

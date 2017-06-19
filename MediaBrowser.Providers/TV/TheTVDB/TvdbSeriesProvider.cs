﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Xml;

namespace MediaBrowser.Providers.TV
{
    public class TvdbSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        internal readonly SemaphoreSlim TvDbResourcePool = new SemaphoreSlim(2, 2);
        internal static TvdbSeriesProvider Current { get; private set; }
        private readonly IZipClient _zipClient;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IXmlReaderSettingsFactory _xmlSettings;
        private readonly IServerConfigurationManager _config;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IMemoryStreamFactory _memoryStreamProvider;
        private readonly ILocalizationManager _localizationManager;

        public TvdbSeriesProvider(IZipClient zipClient, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager config, ILogger logger, ILibraryManager libraryManager, IMemoryStreamFactory memoryStreamProvider, IXmlReaderSettingsFactory xmlSettings, ILocalizationManager localizationManager)
        {
            _zipClient = zipClient;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _config = config;
            _logger = logger;
            _libraryManager = libraryManager;
            _memoryStreamProvider = memoryStreamProvider;
            _xmlSettings = xmlSettings;
            _localizationManager = localizationManager;
            Current = this;
        }

        private const string SeriesSearchUrl = "https://www.thetvdb.com/api/GetSeries.php?seriesname={0}&language={1}";
        private const string SeriesGetZip = "https://www.thetvdb.com/api/{0}/series/{1}/all/{2}.zip";
        private const string GetSeriesByImdbId = "https://www.thetvdb.com/api/GetSeriesByRemoteID.php?imdbid={0}&language={1}";

        private string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return language;
            }

            // pt-br is just pt to tvdb
            return language.Split('-')[0].ToLower();
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            if (IsValidSeries(searchInfo.ProviderIds))
            {
                var metadata = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

                if (metadata.HasMetadata)
                {
                    return new List<RemoteSearchResult>
                    {
                        new RemoteSearchResult
                        {
                            Name = metadata.Item.Name,
                            PremiereDate = metadata.Item.PremiereDate,
                            ProductionYear = metadata.Item.ProductionYear,
                            ProviderIds = metadata.Item.ProviderIds,
                            SearchProviderName = Name
                        }
                    };
                }
            }

            return await FindSeries(searchInfo.Name, searchInfo.Year, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo itemId, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();
            result.QueriedById = true;

            if (!IsValidSeries(itemId.ProviderIds))
            {
                result.QueriedById = false;
                await Identify(itemId).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (IsValidSeries(itemId.ProviderIds))
            {
                await EnsureSeriesInfo(itemId.ProviderIds, itemId.Name, itemId.Year, itemId.MetadataLanguage, cancellationToken).ConfigureAwait(false);

                result.Item = new Series();
                result.HasMetadata = true;

                FetchSeriesData(result, itemId.MetadataLanguage, itemId.ProviderIds, cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Fetches the series data.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="metadataLanguage">The metadata language.</param>
        /// <param name="seriesProviderIds">The series provider ids.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        private void FetchSeriesData(MetadataResult<Series> result, string metadataLanguage, Dictionary<string, string> seriesProviderIds, CancellationToken cancellationToken)
        {
            var series = result.Item;

            string id;
            if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.Tvdb, id);
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.Imdb, id);
            }

            var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

            var seriesXmlPath = GetSeriesXmlPath(seriesProviderIds, metadataLanguage);
            var actorsXmlPath = Path.Combine(seriesDataPath, "actors.xml");

            FetchSeriesInfo(result, seriesXmlPath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            result.ResetPeople();

            FetchActors(result, actorsXmlPath);
        }

        /// <summary>
        /// Downloads the series zip.
        /// </summary>
        internal async Task DownloadSeriesZip(string seriesId, string idType, string seriesName, int? seriesYear, string seriesDataPath, long? lastTvDbUpdateTime, string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            try
            {
                await DownloadSeriesZip(seriesId, idType, seriesName, seriesYear, seriesDataPath, lastTvDbUpdateTime, preferredMetadataLanguage, preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpException ex)
            {
                if (!ex.StatusCode.HasValue || ex.StatusCode.Value != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            if (!string.Equals(preferredMetadataLanguage, "en", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadSeriesZip(seriesId, idType, seriesName, seriesYear, seriesDataPath, lastTvDbUpdateTime, "en", preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task DownloadSeriesZip(string seriesId, string idType, string seriesName, int? seriesYear, string seriesDataPath, long? lastTvDbUpdateTime, string preferredMetadataLanguage, string saveAsMetadataLanguage, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                throw new ArgumentNullException("seriesId");
            }

            if (!string.Equals(idType, "tvdb", StringComparison.OrdinalIgnoreCase))
            {
                seriesId = await GetSeriesByRemoteId(seriesId, idType, preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);
            }

            // If searching by remote id came up empty, then do a regular search
            if (string.IsNullOrWhiteSpace(seriesId) && !string.IsNullOrWhiteSpace(seriesName))
            {
                var searchInfo = new SeriesInfo
                {
                    Name = seriesName,
                    Year = seriesYear,
                    MetadataLanguage = preferredMetadataLanguage
                };
                var results = await GetSearchResults(searchInfo, cancellationToken).ConfigureAwait(false);
                var result = results.FirstOrDefault();
                if (result != null)
                {
                    seriesId = result.GetProviderId(MetadataProviders.Tvdb);
                }
            }

            if (string.IsNullOrWhiteSpace(seriesId))
            {
                throw new ArgumentNullException("seriesId");
            }

            var url = string.Format(SeriesGetZip, TVUtils.TvdbApiKey, seriesId, NormalizeLanguage(preferredMetadataLanguage));

            using (var zipStream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvDbResourcePool,
                CancellationToken = cancellationToken,
                BufferContent = false

            }).ConfigureAwait(false))
            {
                // Delete existing files
                DeleteXmlFiles(seriesDataPath);

                // Copy to memory stream because we need a seekable stream
                using (var ms = _memoryStreamProvider.CreateNew())
                {
                    await zipStream.CopyToAsync(ms).ConfigureAwait(false);

                    ms.Position = 0;
                    _zipClient.ExtractAllFromZip(ms, seriesDataPath, true);
                }
            }

            // Sanitize all files, except for extracted episode files
            foreach (var file in _fileSystem.GetFilePaths(seriesDataPath, true).ToList()
                .Where(i => string.Equals(Path.GetExtension(i), ".xml", StringComparison.OrdinalIgnoreCase))
                .Where(i => !Path.GetFileName(i).StartsWith("episode-", StringComparison.OrdinalIgnoreCase)))
            {
                await SanitizeXmlFile(file).ConfigureAwait(false);
            }

            var downloadLangaugeXmlFile = Path.Combine(seriesDataPath, NormalizeLanguage(preferredMetadataLanguage) + ".xml");
            var saveAsLanguageXmlFile = Path.Combine(seriesDataPath, saveAsMetadataLanguage + ".xml");

            if (!string.Equals(downloadLangaugeXmlFile, saveAsLanguageXmlFile, StringComparison.OrdinalIgnoreCase))
            {
                _fileSystem.CopyFile(downloadLangaugeXmlFile, saveAsLanguageXmlFile, true);
            }

            await ExtractEpisodes(seriesDataPath, downloadLangaugeXmlFile, lastTvDbUpdateTime).ConfigureAwait(false);
        }

        private async Task<string> GetSeriesByRemoteId(string id, string idType, string language, CancellationToken cancellationToken)
        {
            var url = string.Format(GetSeriesByImdbId, id, NormalizeLanguage(language));

            using (var result = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvDbResourcePool,
                CancellationToken = cancellationToken,
                BufferContent = false

            }).ConfigureAwait(false))
            {
                return FindSeriesId(result);
            }
        }

        private string FindSeriesId(Stream stream)
        {
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
            {
                var settings = _xmlSettings.Create(false);

                settings.CheckCharacters = false;
                settings.IgnoreProcessingInstructions = true;
                settings.IgnoreComments = true;

                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();
                    reader.Read();

                    // Loop through each element
                    while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "Series":
                                    {
                                        if (reader.IsEmptyElement)
                                        {
                                            reader.Read();
                                            continue;
                                        }
                                        using (var subtree = reader.ReadSubtree())
                                        {
                                            return FindSeriesId(subtree);
                                        }
                                    }

                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                        else
                        {
                            reader.Read();
                        }
                    }
                }
            }

            return null;
        }

        private string FindSeriesId(XmlReader reader)
        {
            reader.MoveToContent();
            reader.Read();

            // Loop through each element
            while (!reader.EOF && reader.ReadState == ReadState.Interactive)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "seriesid":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    return val;
                                }

                                return null;
                            }

                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                }
            }

            return null;
        }

        internal static bool IsValidSeries(Dictionary<string, string> seriesProviderIds)
        {
            string id;
            if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out id))
            {
                // This check should ideally never be necessary but we're seeing some cases of this and haven't tracked them down yet.
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return true;
                }
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out id))
            {
                // This check should ideally never be necessary but we're seeing some cases of this and haven't tracked them down yet.
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return true;
                }
            }
            return false;
        }

        private SemaphoreSlim _ensureSemaphore = new SemaphoreSlim(1, 1);
        internal async Task<string> EnsureSeriesInfo(Dictionary<string, string> seriesProviderIds, string seriesName, int? seriesYear, string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            await _ensureSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string seriesId;
                if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out seriesId) && !string.IsNullOrWhiteSpace(seriesId))
                {
                    var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

                    // Only download if not already there
                    // The post-scan task will take care of updates so we don't need to re-download here
                    if (!IsCacheValid(seriesDataPath, preferredMetadataLanguage))
                    {
                        await DownloadSeriesZip(seriesId, MetadataProviders.Tvdb.ToString(), seriesName, seriesYear, seriesDataPath, null, preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);
                    }

                    return seriesDataPath;
                }

                if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out seriesId) && !string.IsNullOrWhiteSpace(seriesId))
                {
                    var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

                    // Only download if not already there
                    // The post-scan task will take care of updates so we don't need to re-download here
                    if (!IsCacheValid(seriesDataPath, preferredMetadataLanguage))
                    {
                        await DownloadSeriesZip(seriesId, MetadataProviders.Imdb.ToString(), seriesName, seriesYear, seriesDataPath, null, preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);
                    }

                    return seriesDataPath;
                }

                return null;
            }
            finally
            {
                _ensureSemaphore.Release();
            }
        }

        private bool IsCacheValid(string seriesDataPath, string preferredMetadataLanguage)
        {
            try
            {
                var files = _fileSystem.GetFiles(seriesDataPath, new[] { ".xml" }, true, false)
                    .ToList();

                var seriesXmlFilename = preferredMetadataLanguage + ".xml";

                const int cacheDays = 1;

                var seriesFile = files.FirstOrDefault(i => string.Equals(seriesXmlFilename, i.Name, StringComparison.OrdinalIgnoreCase));
                // No need to check age if automatic updates are enabled
                if (seriesFile == null || !seriesFile.Exists || (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(seriesFile)).TotalDays > cacheDays)
                {
                    return false;
                }

                var actorsXml = files.FirstOrDefault(i => string.Equals("actors.xml", i.Name, StringComparison.OrdinalIgnoreCase));
                // No need to check age if automatic updates are enabled
                if (actorsXml == null || !actorsXml.Exists || (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(actorsXml)).TotalDays > cacheDays)
                {
                    return false;
                }

                var bannersXml = files.FirstOrDefault(i => string.Equals("banners.xml", i.Name, StringComparison.OrdinalIgnoreCase));
                // No need to check age if automatic updates are enabled
                if (bannersXml == null || !bannersXml.Exists || (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(bannersXml)).TotalDays > cacheDays)
                {
                    return false;
                }
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the series.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="year">The year.</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.String}.</returns>
        private async Task<IEnumerable<RemoteSearchResult>> FindSeries(string name, int? year, string language, CancellationToken cancellationToken)
        {
            var results = (await FindSeriesInternal(name, language, cancellationToken).ConfigureAwait(false)).ToList();

            if (results.Count == 0)
            {
                var parsedName = _libraryManager.ParseName(name);
                var nameWithoutYear = parsedName.Name;

                if (!string.IsNullOrWhiteSpace(nameWithoutYear) && !string.Equals(nameWithoutYear, name, StringComparison.OrdinalIgnoreCase))
                {
                    results = (await FindSeriesInternal(nameWithoutYear, language, cancellationToken).ConfigureAwait(false)).ToList();
                }
            }

            return results.Where(i =>
            {
                if (year.HasValue && i.ProductionYear.HasValue)
                {
                    // Allow one year tolerance
                    return Math.Abs(year.Value - i.ProductionYear.Value) <= 1;
                }

                return true;
            });
        }

        private async Task<IEnumerable<RemoteSearchResult>> FindSeriesInternal(string name, string language, CancellationToken cancellationToken)
        {
            var url = string.Format(SeriesSearchUrl, WebUtility.UrlEncode(name), NormalizeLanguage(language));
            var searchResults = new List<RemoteSearchResult>();

            var comparableName = GetComparableName(name);

            using (var stream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvDbResourcePool,
                CancellationToken = cancellationToken,
                BufferContent = false

            }).ConfigureAwait(false))
            {
                var settings = _xmlSettings.Create(false);

                settings.CheckCharacters = false;
                settings.IgnoreProcessingInstructions = true;
                settings.IgnoreComments = true;

                using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                {
                    // Use XmlReader for best performance
                    using (var reader = XmlReader.Create(streamReader, settings))
                    {
                        reader.MoveToContent();
                        reader.Read();

                        // Loop through each element
                        while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                switch (reader.Name)
                                {
                                    case "Series":
                                        {
                                            if (reader.IsEmptyElement)
                                            {
                                                reader.Read();
                                                continue;
                                            }
                                            using (var subtree = reader.ReadSubtree())
                                            {
                                                var searchResult = GetSeriesSearchResultFromSubTree(subtree, comparableName);
                                                if (searchResult != null)
                                                {
                                                    searchResult.SearchProviderName = Name;
                                                    searchResults.Add(searchResult);
                                                }
                                            }
                                            break;
                                        }

                                    default:
                                        reader.Skip();
                                        break;
                                }
                            }
                            else
                            {
                                reader.Read();
                            }
                        }
                    }
                }
            }

            if (searchResults.Count == 0)
            {
                _logger.Info("TVDb Provider - Could not find " + name + ". Check name on Thetvdb.org.");
            }

            return searchResults;
        }

        private RemoteSearchResult GetSeriesSearchResultFromSubTree(XmlReader reader, string comparableName)
        {
            var searchResult = new RemoteSearchResult
            {
                SearchProviderName = Name
            };

            var titles = new List<string>();
            string seriesId = null;

            reader.MoveToContent();
            reader.Read();

            // Loop through each element
            while (!reader.EOF && reader.ReadState == ReadState.Interactive)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "SeriesName":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    titles.Add(GetComparableName(val));
                                }
                                break;
                            }

                        case "AliasNames":
                            {
                                var val = reader.ReadElementContentAsString();

                                var alias = (val ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(GetComparableName);
                                titles.AddRange(alias);
                                break;
                            }

                        case "IMDB_ID":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    searchResult.SetProviderId(MetadataProviders.Imdb, val);
                                }
                                break;
                            }

                        case "banner":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    searchResult.ImageUrl = TVUtils.BannerUrl + val;
                                }
                                break;
                            }

                        case "FirstAired":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, out date))
                                    {
                                        searchResult.ProductionYear = date.Year;
                                    }
                                }
                                break;
                            }

                        case "id":
                        case "seriesid":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    seriesId = val;
                                }
                                break;
                            }

                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                }
            }

            foreach (var title in titles)
            {
                if (string.Equals(title, comparableName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(seriesId))
                    {
                        searchResult.Name = title;
                        searchResult.SetProviderId(MetadataProviders.Tvdb, seriesId);
                        return searchResult;
                    }
                    break;
                }
                _logger.Info("TVDb Provider - " + title + " did not match " + comparableName);
            }

            return null;
        }

        /// <summary>
        /// The remove
        /// </summary>
        const string remove = "\"'!`?";
        /// <summary>
        /// The spacers
        /// </summary>
        const string spacers = "/,.:;\\(){}[]+-_=–*";  // (there are not actually two - in the they are different char codes)

        /// <summary>
        /// Gets the name of the comparable.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        private string GetComparableName(string name)
        {
            name = name.ToLower();
            name = _localizationManager.NormalizeFormKD(name);
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if ((int)c >= 0x2B0 && (int)c <= 0x0333)
                {
                    // skip char modifier and diacritics 
                }
                else if (remove.IndexOf(c) > -1)
                {
                    // skip chars we are removing
                }
                else if (spacers.IndexOf(c) > -1)
                {
                    sb.Append(" ");
                }
                else if (c == '&')
                {
                    sb.Append(" and ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            name = sb.ToString();
            name = name.Replace(", the", "");
            name = name.Replace("the ", " ");
            name = name.Replace(" the ", " ");

            string prevName;
            do
            {
                prevName = name;
                name = name.Replace("  ", " ");
            } while (name.Length != prevName.Length);

            return name.Trim();
        }

        private void FetchSeriesInfo(MetadataResult<Series> result, string seriesXmlPath, CancellationToken cancellationToken)
        {
            var settings = _xmlSettings.Create(false);

            settings.CheckCharacters = false;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreComments = true;

            var episiodeAirDates = new List<DateTime>();

            using (var fileStream = _fileSystem.GetFileStream(seriesXmlPath, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    // Use XmlReader for best performance
                    using (var reader = XmlReader.Create(streamReader, settings))
                    {
                        reader.MoveToContent();
                        reader.Read();

                        // Loop through each element
                        while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                switch (reader.Name)
                                {
                                    case "Series":
                                        {
                                            if (reader.IsEmptyElement)
                                            {
                                                reader.Read();
                                                continue;
                                            }
                                            using (var subtree = reader.ReadSubtree())
                                            {
                                                FetchDataFromSeriesNode(result, subtree, cancellationToken);
                                            }
                                            break;
                                        }

                                    case "Episode":
                                        {
                                            if (reader.IsEmptyElement)
                                            {
                                                reader.Read();
                                                continue;
                                            }
                                            using (var subtree = reader.ReadSubtree())
                                            {
                                                var date = GetFirstAiredDateFromEpisodeNode(subtree, cancellationToken);

                                                if (date.HasValue)
                                                {
                                                    episiodeAirDates.Add(date.Value);
                                                }
                                            }
                                            break;
                                        }

                                    default:
                                        reader.Skip();
                                        break;
                                }
                            }
                            else
                            {
                                reader.Read();
                            }
                        }
                    }
                }
            }

            if (result.Item.Status.HasValue && result.Item.Status.Value == SeriesStatus.Ended && episiodeAirDates.Count > 0)
            {
                result.Item.EndDate = episiodeAirDates.Max();
            }
        }

        private DateTime? GetFirstAiredDateFromEpisodeNode(XmlReader reader, CancellationToken cancellationToken)
        {
            DateTime? airDate = null;
            int? seasonNumber = null;

            reader.MoveToContent();
            reader.Read();

            // Loop through each element
            while (!reader.EOF && reader.ReadState == ReadState.Interactive)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "FirstAired":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, out date))
                                    {
                                        airDate = date.ToUniversalTime();
                                    }
                                }

                                break;
                            }

                        case "SeasonNumber":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    int rval;

                                    // int.TryParse is local aware, so it can be probamatic, force us culture
                                    if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                    {
                                        seasonNumber = rval;
                                    }
                                }

                                break;
                            }

                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                }
            }

            if (seasonNumber.HasValue && seasonNumber.Value != 0)
            {
                return airDate;
            }

            return null;
        }

        /// <summary>
        /// Fetches the actors.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="actorsXmlPath">The actors XML path.</param>
        private void FetchActors(MetadataResult<Series> result, string actorsXmlPath)
        {
            var settings = _xmlSettings.Create(false);

            settings.CheckCharacters = false;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreComments = true;

            using (var fileStream = _fileSystem.GetFileStream(actorsXmlPath, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    // Use XmlReader for best performance
                    using (var reader = XmlReader.Create(streamReader, settings))
                    {
                        reader.MoveToContent();
                        reader.Read();

                        // Loop through each element
                        while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                switch (reader.Name)
                                {
                                    case "Actor":
                                        {
                                            if (reader.IsEmptyElement)
                                            {
                                                reader.Read();
                                                continue;
                                            }
                                            using (var subtree = reader.ReadSubtree())
                                            {
                                                FetchDataFromActorNode(result, subtree);
                                            }
                                            break;
                                        }
                                    default:
                                        reader.Skip();
                                        break;
                                }
                            }
                            else
                            {
                                reader.Read();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fetches the data from actor node.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="reader">The reader.</param>
        private void FetchDataFromActorNode(MetadataResult<Series> result, XmlReader reader)
        {
            reader.MoveToContent();

            var personInfo = new PersonInfo();

            reader.MoveToContent();
            reader.Read();

            // Loop through each element
            while (!reader.EOF && reader.ReadState == ReadState.Interactive)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Name":
                            {
                                personInfo.Name = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                break;
                            }

                        case "Role":
                            {
                                personInfo.Role = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                break;
                            }

                        case "id":
                            {
                                reader.Skip();
                                break;
                            }

                        case "Image":
                            {
                                var url = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    personInfo.ImageUrl = TVUtils.BannerUrl + url;
                                }
                                break;
                            }

                        case "SortOrder":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    int rval;

                                    // int.TryParse is local aware, so it can be probamatic, force us culture
                                    if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                    {
                                        personInfo.SortOrder = rval;
                                    }
                                }
                                break;
                            }

                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                }
            }

            personInfo.Type = PersonType.Actor;

            if (!string.IsNullOrWhiteSpace(personInfo.Name))
            {
                result.AddPerson(personInfo);
            }
        }

        private void FetchDataFromSeriesNode(MetadataResult<Series> result, XmlReader reader, CancellationToken cancellationToken)
        {
            Series item = result.Item;

            reader.MoveToContent();
            reader.Read();

            // Loop through each element
            while (!reader.EOF && reader.ReadState == ReadState.Interactive)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "SeriesName":
                            {
                                item.Name = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                break;
                            }

                        case "Overview":
                            {
                                item.Overview = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                break;
                            }

                        case "Language":
                            {
                                result.ResultLanguage = (reader.ReadElementContentAsString() ?? string.Empty).Trim();
                                break;
                            }

                        case "Airs_DayOfWeek":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    item.AirDays = TVUtils.GetAirDays(val);
                                }
                                break;
                            }

                        case "Airs_Time":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    item.AirTime = val;
                                }
                                break;
                            }

                        case "ContentRating":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    item.OfficialRating = val;
                                }
                                break;
                            }

                        case "Rating":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    float rval;

                                    // float.TryParse is local aware, so it can be probamatic, force us culture
                                    if (float.TryParse(val, NumberStyles.AllowDecimalPoint, _usCulture, out rval))
                                    {
                                        item.CommunityRating = rval;
                                    }
                                }
                                break;
                            }
                        case "RatingCount":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    int rval;

                                    // int.TryParse is local aware, so it can be probamatic, force us culture
                                    if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                    {
                                        item.VoteCount = rval;
                                    }
                                }

                                break;
                            }

                        case "IMDB_ID":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    item.SetProviderId(MetadataProviders.Imdb, val);
                                }

                                break;
                            }

                        case "zap2it_id":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    item.SetProviderId(MetadataProviders.Zap2It, val);
                                }

                                break;
                            }

                        case "Status":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    SeriesStatus seriesStatus;

                                    if (Enum.TryParse(val, true, out seriesStatus))
                                        item.Status = seriesStatus;
                                }

                                break;
                            }

                        case "FirstAired":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, out date))
                                    {
                                        date = date.ToUniversalTime();

                                        item.PremiereDate = date;
                                        item.ProductionYear = date.Year;
                                    }
                                }

                                break;
                            }

                        case "Runtime":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    int rval;

                                    // int.TryParse is local aware, so it can be probamatic, force us culture
                                    if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                    {
                                        item.RunTimeTicks = TimeSpan.FromMinutes(rval).Ticks;
                                    }
                                }

                                break;
                            }

                        case "Genre":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    var vals = val
                                        .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(i => i.Trim())
                                        .Where(i => !string.IsNullOrWhiteSpace(i))
                                        .ToList();

                                    if (vals.Count > 0)
                                    {
                                        item.Genres.Clear();

                                        foreach (var genre in vals)
                                        {
                                            item.AddGenre(genre);
                                        }
                                    }
                                }

                                break;
                            }

                        case "Network":
                            {
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    var vals = val
                                        .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(i => i.Trim())
                                        .Where(i => !string.IsNullOrWhiteSpace(i))
                                        .ToList();

                                    if (vals.Count > 0)
                                    {
                                        item.Studios.Clear();

                                        foreach (var genre in vals)
                                        {
                                            item.AddStudio(genre);
                                        }
                                    }
                                }

                                break;
                            }

                        default:
                            reader.Skip();
                            break;
                    }
                }
                else
                {
                    reader.Read();
                }
            }
        }

        /// <summary>
        /// Extracts info for each episode into invididual xml files so that they can be easily accessed without having to step through the entire series xml
        /// </summary>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="xmlFile">The XML file.</param>
        /// <param name="lastTvDbUpdateTime">The last tv db update time.</param>
        /// <returns>Task.</returns>
        private async Task ExtractEpisodes(string seriesDataPath, string xmlFile, long? lastTvDbUpdateTime)
        {
            var settings = _xmlSettings.Create(false);

            settings.CheckCharacters = false;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreComments = true;

            using (var fileStream = _fileSystem.GetFileStream(xmlFile, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    // Use XmlReader for best performance
                    using (var reader = XmlReader.Create(streamReader, settings))
                    {
                        reader.MoveToContent();
                        reader.Read();

                        // Loop through each element
                        while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                switch (reader.Name)
                                {
                                    case "Episode":
                                        {
                                            var outerXml = reader.ReadOuterXml();

                                            await SaveEpsiodeXml(seriesDataPath, outerXml, lastTvDbUpdateTime).ConfigureAwait(false);
                                            break;
                                        }

                                    default:
                                        reader.Skip();
                                        break;
                                }
                            }
                            else
                            {
                                reader.Read();
                            }
                        }
                    }
                }
            }
        }

        private async Task SaveEpsiodeXml(string seriesDataPath, string xml, long? lastTvDbUpdateTime)
        {
            var settings = _xmlSettings.Create(false);

            settings.CheckCharacters = false;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreComments = true;

            var seasonNumber = -1;
            var episodeNumber = -1;
            var absoluteNumber = -1;
            var lastUpdateString = string.Empty;

            using (var streamReader = new StringReader(xml))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();
                    reader.Read();

                    // Loop through each element
                    while (!reader.EOF && reader.ReadState == ReadState.Interactive)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "lastupdated":
                                    {
                                        lastUpdateString = reader.ReadElementContentAsString();
                                        break;
                                    }

                                case "EpisodeNumber":
                                    {
                                        var val = reader.ReadElementContentAsString();
                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            int num;
                                            if (int.TryParse(val, NumberStyles.Integer, _usCulture, out num))
                                            {
                                                episodeNumber = num;
                                            }
                                        }
                                        break;
                                    }

                                case "absolute_number":
                                    {
                                        var val = reader.ReadElementContentAsString();
                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            int num;
                                            if (int.TryParse(val, NumberStyles.Integer, _usCulture, out num))
                                            {
                                                absoluteNumber = num;
                                            }
                                        }
                                        break;
                                    }

                                case "SeasonNumber":
                                    {
                                        var val = reader.ReadElementContentAsString();
                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            int num;
                                            if (int.TryParse(val, NumberStyles.Integer, _usCulture, out num))
                                            {
                                                seasonNumber = num;
                                            }
                                        }
                                        break;
                                    }

                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                        else
                        {
                            reader.Read();
                        }
                    }
                }
            }

            var hasEpisodeChanged = true;
            if (!string.IsNullOrWhiteSpace(lastUpdateString) && lastTvDbUpdateTime.HasValue)
            {
                long num;
                if (long.TryParse(lastUpdateString, NumberStyles.Any, _usCulture, out num))
                {
                    hasEpisodeChanged = num >= lastTvDbUpdateTime.Value;
                }
            }

            var file = Path.Combine(seriesDataPath, string.Format("episode-{0}-{1}.xml", seasonNumber, episodeNumber));

            // Only save the file if not already there, or if the episode has changed
            if (hasEpisodeChanged || !_fileSystem.FileExists(file))
            {
                using (var fileStream = _fileSystem.GetFileStream(file, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.None, true))
                {
                    using (var writer = XmlWriter.Create(fileStream, new XmlWriterSettings
                    {
                        Encoding = Encoding.UTF8,
                        Async = true
                    }))
                    {
                        await writer.WriteRawAsync(xml).ConfigureAwait(false);
                    }
                }
            }

            if (absoluteNumber != -1)
            {
                file = Path.Combine(seriesDataPath, string.Format("episode-abs-{0}.xml", absoluteNumber));

                // Only save the file if not already there, or if the episode has changed
                if (hasEpisodeChanged || !_fileSystem.FileExists(file))
                {
                    using (var fileStream = _fileSystem.GetFileStream(file, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.None, true))
                    {
                        using (var writer = XmlWriter.Create(fileStream, new XmlWriterSettings
                        {
                            Encoding = Encoding.UTF8,
                            Async = true
                        }))
                        {
                            await writer.WriteRawAsync(xml).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesProviderIds">The series provider ids.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, Dictionary<string, string> seriesProviderIds)
        {
            string seriesId;
            if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
            {
                var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

                return seriesDataPath;
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
            {
                var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

                return seriesDataPath;
            }

            return null;
        }

        public string GetSeriesXmlPath(Dictionary<string, string> seriesProviderIds, string language)
        {
            var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

            var seriesXmlFilename = language.ToLower() + ".xml";

            return Path.Combine(seriesDataPath, seriesXmlFilename);
        }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "tvdb");

            return dataPath;
        }

        private void DeleteXmlFiles(string path)
        {
            try
            {
                foreach (var file in _fileSystem.GetFilePaths(path, true)
                    .ToList())
                {
                    _fileSystem.DeleteFile(file);
                }
            }
            catch (IOException)
            {
                // No biggie
            }
        }

        /// <summary>
        /// Sanitizes the XML file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>Task.</returns>
        private async Task SanitizeXmlFile(string file)
        {
            string validXml;

            using (var fileStream = _fileSystem.GetFileStream(file, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read, true))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    var xml = await reader.ReadToEndAsync().ConfigureAwait(false);

                    validXml = StripInvalidXmlCharacters(xml);
                }
            }

            using (var fileStream = _fileSystem.GetFileStream(file, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read, true))
            {
                using (var writer = new StreamWriter(fileStream))
                {
                    await writer.WriteAsync(validXml).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Strips the invalid XML characters.
        /// </summary>
        /// <param name="inString">The in string.</param>
        /// <returns>System.String.</returns>
        public static string StripInvalidXmlCharacters(string inString)
        {
            if (inString == null) return null;

            var sbOutput = new StringBuilder();
            char ch;

            for (int i = 0; i < inString.Length; i++)
            {
                ch = inString[i];
                if ((ch >= 0x0020 && ch <= 0xD7FF) ||
                    (ch >= 0xE000 && ch <= 0xFFFD) ||
                    ch == 0x0009 ||
                    ch == 0x000A ||
                    ch == 0x000D)
                {
                    sbOutput.Append(ch);
                }
            }
            return sbOutput.ToString();
        }

        public string Name
        {
            get { return "TheTVDB"; }
        }

        public async Task Identify(SeriesInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.GetProviderId(MetadataProviders.Tvdb)))
            {
                return;
            }

            var srch = await FindSeries(info.Name, info.Year, info.MetadataLanguage, CancellationToken.None).ConfigureAwait(false);

            var entry = srch.FirstOrDefault();

            if (entry != null)
            {
                var id = entry.GetProviderId(MetadataProviders.Tvdb);
                info.SetProviderId(MetadataProviders.Tvdb, id);
            }
        }

        public int Order
        {
            get
            {
                // After Omdb
                return 1;
            }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = TvDbResourcePool,
                BufferContent = false
            });
        }
    }
}

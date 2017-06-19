﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Emby.Dlna.Didl;
using Emby.Dlna.Server;
using Emby.Dlna.Service;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Xml;

namespace Emby.Dlna.ContentDirectory
{
    public class ControlHandler : BaseControlHandler
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IChannelManager _channelManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IServerConfigurationManager _config;
        private readonly User _user;
        private readonly IUserViewManager _userViewManager;

        private const string NS_DC = "http://purl.org/dc/elements/1.1/";
        private const string NS_DIDL = "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/";
        private const string NS_DLNA = "urn:schemas-dlna-org:metadata-1-0/";
        private const string NS_UPNP = "urn:schemas-upnp-org:metadata-1-0/upnp/";

        private readonly int _systemUpdateId;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        private readonly DidlBuilder _didlBuilder;

        private readonly DeviceProfile _profile;

        public ControlHandler(ILogger logger, ILibraryManager libraryManager, DeviceProfile profile, string serverAddress, string accessToken, IImageProcessor imageProcessor, IUserDataManager userDataManager, User user, int systemUpdateId, IServerConfigurationManager config, ILocalizationManager localization, IChannelManager channelManager, IMediaSourceManager mediaSourceManager, IUserViewManager userViewManager, IMediaEncoder mediaEncoder, IXmlReaderSettingsFactory xmlReaderSettingsFactory)
            : base(config, logger, xmlReaderSettingsFactory)
        {
            _libraryManager = libraryManager;
            _userDataManager = userDataManager;
            _user = user;
            _systemUpdateId = systemUpdateId;
            _channelManager = channelManager;
            _userViewManager = userViewManager;
            _profile = profile;
            _config = config;

            _didlBuilder = new DidlBuilder(profile, user, imageProcessor, serverAddress, accessToken, userDataManager, localization, mediaSourceManager, Logger, libraryManager, mediaEncoder);
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetResult(string methodName, IDictionary<string, string> methodParams)
        {
            var deviceId = "test";

            var user = _user;

            if (string.Equals(methodName, "GetSearchCapabilities", StringComparison.OrdinalIgnoreCase))
                return HandleGetSearchCapabilities();

            if (string.Equals(methodName, "GetSortCapabilities", StringComparison.OrdinalIgnoreCase))
                return HandleGetSortCapabilities();

            if (string.Equals(methodName, "GetSortExtensionCapabilities", StringComparison.OrdinalIgnoreCase))
                return HandleGetSortExtensionCapabilities();

            if (string.Equals(methodName, "GetSystemUpdateID", StringComparison.OrdinalIgnoreCase))
                return HandleGetSystemUpdateID();

            if (string.Equals(methodName, "Browse", StringComparison.OrdinalIgnoreCase))
                return HandleBrowse(methodParams, user, deviceId).Result;

            if (string.Equals(methodName, "X_GetFeatureList", StringComparison.OrdinalIgnoreCase))
                return HandleXGetFeatureList();

            if (string.Equals(methodName, "GetFeatureList", StringComparison.OrdinalIgnoreCase))
                return HandleGetFeatureList();

            if (string.Equals(methodName, "X_SetBookmark", StringComparison.OrdinalIgnoreCase))
                return HandleXSetBookmark(methodParams, user);

            if (string.Equals(methodName, "Search", StringComparison.OrdinalIgnoreCase))
                return HandleSearch(methodParams, user, deviceId).Result;

            if (string.Equals(methodName, "X_BrowseByLetter", StringComparison.OrdinalIgnoreCase))
                return HandleX_BrowseByLetter(methodParams, user, deviceId).Result;

            throw new ResourceNotFoundException("Unexpected control request name: " + methodName);
        }

        private IEnumerable<KeyValuePair<string, string>> HandleXSetBookmark(IDictionary<string, string> sparams, User user)
        {
            var id = sparams["ObjectID"];

            var serverItem = GetItemFromObjectId(id, user);

            var item = serverItem.Item;

            var newbookmark = int.Parse(sparams["PosSecond"], _usCulture);

            var userdata = _userDataManager.GetUserData(user, item);

            userdata.PlaybackPositionTicks = TimeSpan.FromSeconds(newbookmark).Ticks;

            _userDataManager.SaveUserData(user.Id, item, userdata, UserDataSaveReason.TogglePlayed,
                CancellationToken.None);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<KeyValuePair<string, string>> HandleGetSearchCapabilities()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SearchCaps", "res@resolution,res@size,res@duration,dc:title,dc:creator,upnp:actor,upnp:artist,upnp:genre,upnp:album,dc:date,upnp:class,@id,@refID,@protocolInfo,upnp:author,dc:description,pv:avKeywords" }
            };
        }

        private IEnumerable<KeyValuePair<string, string>> HandleGetSortCapabilities()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SortCaps", "res@duration,res@size,res@bitrate,dc:date,dc:title,dc:size,upnp:album,upnp:artist,upnp:albumArtist,upnp:episodeNumber,upnp:genre,upnp:originalTrackNumber,upnp:rating" }
            };
        }

        private IEnumerable<KeyValuePair<string, string>> HandleGetSortExtensionCapabilities()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SortExtensionCaps", "res@duration,res@size,res@bitrate,dc:date,dc:title,dc:size,upnp:album,upnp:artist,upnp:albumArtist,upnp:episodeNumber,upnp:genre,upnp:originalTrackNumber,upnp:rating" }
            };
        }

        private IEnumerable<KeyValuePair<string, string>> HandleGetSystemUpdateID()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers.Add("Id", _systemUpdateId.ToString(_usCulture));
            return headers;
        }

        private IEnumerable<KeyValuePair<string, string>> HandleGetFeatureList()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "FeatureList", GetFeatureListXml() }
            };
        }

        private IEnumerable<KeyValuePair<string, string>> HandleXGetFeatureList()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "FeatureList", GetFeatureListXml() }
            };
        }

        private string GetFeatureListXml()
        {
            var builder = new StringBuilder();

            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            builder.Append("<Features xmlns=\"urn:schemas-upnp-org:av:avs\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"urn:schemas-upnp-org:av:avs http://www.upnp.org/schemas/av/avs.xsd\">");

            builder.Append("<Feature name=\"samsung.com_BASICVIEW\" version=\"1\">");
            builder.Append("<container id=\"I\" type=\"object.item.imageItem\"/>");
            builder.Append("<container id=\"A\" type=\"object.item.audioItem\"/>");
            builder.Append("<container id=\"V\" type=\"object.item.videoItem\"/>");
            builder.Append("</Feature>");

            builder.Append("</Features>");

            return builder.ToString();
        }

        public string GetValueOrDefault(IDictionary<string, string> sparams, string key, string defaultValue)
        {
            string val;

            if (sparams.TryGetValue(key, out val))
            {
                return val;
            }

            return defaultValue;
        }

        private async Task<IEnumerable<KeyValuePair<string, string>>> HandleBrowse(IDictionary<string, string> sparams, User user, string deviceId)
        {
            var id = sparams["ObjectID"];
            var flag = sparams["BrowseFlag"];
            var filter = new Filter(GetValueOrDefault(sparams, "Filter", "*"));
            var sortCriteria = new SortCriteria(GetValueOrDefault(sparams, "SortCriteria", ""));

            var provided = 0;

            // Default to null instead of 0
            // Upnp inspector sends 0 as requestedCount when it wants everything
            int? requestedCount = null;
            int? start = 0;

            int requestedVal;
            if (sparams.ContainsKey("RequestedCount") && int.TryParse(sparams["RequestedCount"], out requestedVal) && requestedVal > 0)
            {
                requestedCount = requestedVal;
            }

            int startVal;
            if (sparams.ContainsKey("StartingIndex") && int.TryParse(sparams["StartingIndex"], out startVal) && startVal > 0)
            {
                start = startVal;
            }

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };

            StringWriter builder = new StringWriterWithEncoding(Encoding.UTF8);

            int totalCount;

            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                //writer.WriteStartDocument();

                writer.WriteStartElement(string.Empty, "DIDL-Lite", NS_DIDL);

                writer.WriteAttributeString("xmlns", "dc", null, NS_DC);
                writer.WriteAttributeString("xmlns", "dlna", null, NS_DLNA);
                writer.WriteAttributeString("xmlns", "upnp", null, NS_UPNP);
                //didl.SetAttribute("xmlns:sec", NS_SEC);

                DidlBuilder.WriteXmlRootAttributes(_profile, writer);

                var serverItem = GetItemFromObjectId(id, user);
                var item = serverItem.Item;

                if (string.Equals(flag, "BrowseMetadata"))
                {
                    totalCount = 1;

                    if (item.IsDisplayedAsFolder || serverItem.StubType.HasValue)
                    {
                        var childrenResult = (await GetUserItems(item, serverItem.StubType, user, sortCriteria, start, requestedCount).ConfigureAwait(false));

                        _didlBuilder.WriteFolderElement(writer, item, serverItem.StubType, null, childrenResult.TotalRecordCount, filter, id);
                    }
                    else
                    {
                        _didlBuilder.WriteItemElement(_config.GetDlnaConfiguration(), writer, item, user, null, null, deviceId, filter);
                    }

                    provided++;
                }
                else
                {
                    var childrenResult = (await GetUserItems(item, serverItem.StubType, user, sortCriteria, start, requestedCount).ConfigureAwait(false));
                    totalCount = childrenResult.TotalRecordCount;

                    provided = childrenResult.Items.Length;

                    foreach (var i in childrenResult.Items)
                    {
                        var childItem = i.Item;
                        var displayStubType = i.StubType;

                        if (childItem.IsDisplayedAsFolder || displayStubType.HasValue)
                        {
                            var childCount = (await GetUserItems(childItem, displayStubType, user, sortCriteria, null, 0).ConfigureAwait(false))
                                .TotalRecordCount;

                            _didlBuilder.WriteFolderElement(writer, childItem, displayStubType, item, childCount, filter);
                        }
                        else
                        {
                            _didlBuilder.WriteItemElement(_config.GetDlnaConfiguration(), writer, childItem, user, item, serverItem.StubType, deviceId, filter);
                        }
                    }
                }
                writer.WriteFullEndElement();
                //writer.WriteEndDocument();
            }

            var resXML = builder.ToString();

            return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string,string>("Result", resXML),
                    new KeyValuePair<string,string>("NumberReturned", provided.ToString(_usCulture)),
                    new KeyValuePair<string,string>("TotalMatches", totalCount.ToString(_usCulture)),
                    new KeyValuePair<string,string>("UpdateID", _systemUpdateId.ToString(_usCulture))
                };
        }

        private Task<IEnumerable<KeyValuePair<string, string>>> HandleX_BrowseByLetter(IDictionary<string, string> sparams, User user, string deviceId)
        {
            // TODO: Implement this method
            return HandleSearch(sparams, user, deviceId);
        }

        private async Task<IEnumerable<KeyValuePair<string, string>>> HandleSearch(IDictionary<string, string> sparams, User user, string deviceId)
        {
            var searchCriteria = new SearchCriteria(GetValueOrDefault(sparams, "SearchCriteria", ""));
            var sortCriteria = new SortCriteria(GetValueOrDefault(sparams, "SortCriteria", ""));
            var filter = new Filter(GetValueOrDefault(sparams, "Filter", "*"));

            // sort example: dc:title, dc:date

            // Default to null instead of 0
            // Upnp inspector sends 0 as requestedCount when it wants everything
            int? requestedCount = null;
            int? start = 0;

            int requestedVal;
            if (sparams.ContainsKey("RequestedCount") && int.TryParse(sparams["RequestedCount"], out requestedVal) && requestedVal > 0)
            {
                requestedCount = requestedVal;
            }

            int startVal;
            if (sparams.ContainsKey("StartingIndex") && int.TryParse(sparams["StartingIndex"], out startVal) && startVal > 0)
            {
                start = startVal;
            }

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };

            StringWriter builder = new StringWriterWithEncoding(Encoding.UTF8);
            int totalCount = 0;
            int provided = 0;

            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                //writer.WriteStartDocument();

                writer.WriteStartElement(string.Empty, "DIDL-Lite", NS_DIDL);

                writer.WriteAttributeString("xmlns", "dc", null, NS_DC);
                writer.WriteAttributeString("xmlns", "dlna", null, NS_DLNA);
                writer.WriteAttributeString("xmlns", "upnp", null, NS_UPNP);
                //didl.SetAttribute("xmlns:sec", NS_SEC);

                DidlBuilder.WriteXmlRootAttributes(_profile, writer);

                var serverItem = GetItemFromObjectId(sparams["ContainerID"], user);

                var item = serverItem.Item;

                var childrenResult = (await GetChildrenSorted(item, user, searchCriteria, sortCriteria, start, requestedCount).ConfigureAwait(false));

                totalCount = childrenResult.TotalRecordCount;

                provided = childrenResult.Items.Length;

                foreach (var i in childrenResult.Items)
                {
                    if (i.IsDisplayedAsFolder)
                    {
                        var childCount = (await GetChildrenSorted(i, user, searchCriteria, sortCriteria, null, 0).ConfigureAwait(false))
                            .TotalRecordCount;

                        _didlBuilder.WriteFolderElement(writer, i, null, item, childCount, filter);
                    }
                    else
                    {
                        _didlBuilder.WriteItemElement(_config.GetDlnaConfiguration(), writer, i, user, item, serverItem.StubType, deviceId, filter);
                    }
                }

                writer.WriteFullEndElement();
                //writer.WriteEndDocument();
            }

            var resXML = builder.ToString();

            return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string,string>("Result", resXML),
                    new KeyValuePair<string,string>("NumberReturned", provided.ToString(_usCulture)),
                    new KeyValuePair<string,string>("TotalMatches", totalCount.ToString(_usCulture)),
                    new KeyValuePair<string,string>("UpdateID", _systemUpdateId.ToString(_usCulture))
                };
        }

        private Task<QueryResult<BaseItem>> GetChildrenSorted(BaseItem item, User user, SearchCriteria search, SortCriteria sort, int? startIndex, int? limit)
        {
            var folder = (Folder)item;

            var sortOrders = new List<string>();
            if (!folder.IsPreSorted)
            {
                sortOrders.Add(ItemSortBy.SortName);
            }

            var mediaTypes = new List<string>();
            bool? isFolder = null;

            if (search.SearchType == SearchType.Audio)
            {
                mediaTypes.Add(MediaType.Audio);
                isFolder = false;
            }
            else if (search.SearchType == SearchType.Video)
            {
                mediaTypes.Add(MediaType.Video);
                isFolder = false;
            }
            else if (search.SearchType == SearchType.Image)
            {
                mediaTypes.Add(MediaType.Photo);
                isFolder = false;
            }
            else if (search.SearchType == SearchType.Playlist)
            {
                //items = items.OfType<Playlist>();
                isFolder = true;
            }
            else if (search.SearchType == SearchType.MusicAlbum)
            {
                //items = items.OfType<MusicAlbum>();
                isFolder = true;
            }

            return folder.GetItems(new InternalItemsQuery
            {
                Limit = limit,
                StartIndex = startIndex,
                SortBy = sortOrders.ToArray(),
                SortOrder = sort.SortOrder,
                User = user,
                Recursive = true,
                IsMissing = false,
                ExcludeItemTypes = new[] { typeof(Game).Name, typeof(Book).Name },
                IsFolder = isFolder,
                MediaTypes = mediaTypes.ToArray()
            });
        }

        private async Task<QueryResult<ServerItem>> GetUserItems(BaseItem item, StubType? stubType, User user, SortCriteria sort, int? startIndex, int? limit)
        {
            if (item is MusicGenre)
            {
                return GetMusicGenreItems(item, null, user, sort, startIndex, limit);
            }

            if (item is MusicArtist)
            {
                return GetMusicArtistItems(item, null, user, sort, startIndex, limit);
            }

            if (stubType.HasValue)
            {
                if (stubType.Value == StubType.People)
                {
                    var items = _libraryManager.GetPeopleItems(new InternalPeopleQuery
                    {
                        ItemId = item.Id

                    }).ToArray();

                    var result = new QueryResult<ServerItem>
                    {
                        Items = items.Select(i => new ServerItem(i)).ToArray(),
                        TotalRecordCount = items.Length
                    };

                    return ApplyPaging(result, startIndex, limit);
                }

                var person = item as Person;
                if (person != null)
                {
                    return GetItemsFromPerson(person, user, startIndex, limit);
                }

                return ApplyPaging(new QueryResult<ServerItem>(), startIndex, limit);
            }

            var folder = (Folder)item;

            var query = new InternalItemsQuery
            {
                Limit = limit,
                StartIndex = startIndex,
                User = user,
                IsMissing = false,
                PresetViews = new[] {CollectionType.Movies, CollectionType.TvShows, CollectionType.Music},
                ExcludeItemTypes = new[] {typeof (Game).Name, typeof (Book).Name},
                IsPlaceHolder = false
            };

            SetSorting(query, sort, folder.IsPreSorted);

            var queryResult = await folder.GetItems(query).ConfigureAwait(false);

            return ToResult(queryResult);
        }

        private QueryResult<ServerItem> GetMusicArtistItems(BaseItem item, Guid? parentId, User user, SortCriteria sort, int? startIndex, int? limit)
        {
            var query = new InternalItemsQuery(user)
            {
                Recursive = true,
                ParentId = parentId,
                ArtistIds = new[] { item.Id.ToString("N") },
                IncludeItemTypes = new[] { typeof(MusicAlbum).Name },
                Limit = limit,
                StartIndex = startIndex
            };

            SetSorting(query, sort, false);

            var result = _libraryManager.GetItemsResult(query);

            return ToResult(result);
        }

        private QueryResult<ServerItem> GetMusicGenreItems(BaseItem item, Guid? parentId, User user, SortCriteria sort, int? startIndex, int? limit)
        {
            var query = new InternalItemsQuery(user)
            {
                Recursive = true,
                ParentId = parentId,
                GenreIds = new[] {item.Id.ToString("N")},
                IncludeItemTypes = new[] {typeof (MusicAlbum).Name},
                Limit = limit,
                StartIndex = startIndex
            };

            SetSorting(query, sort, false);

            var result = _libraryManager.GetItemsResult(query);

            return ToResult(result);
        }

        private QueryResult<ServerItem> ToResult(QueryResult<BaseItem> result)
        {
            var serverItems = result
                .Items
                .Select(i => new ServerItem(i))
                .ToArray();

            return new QueryResult<ServerItem>
            {
                TotalRecordCount = result.TotalRecordCount,
                Items = serverItems
            };
        }

        private void SetSorting(InternalItemsQuery query, SortCriteria sort, bool isPreSorted)
        {
            var sortOrders = new List<string>();
            if (!isPreSorted)
            {
                sortOrders.Add(ItemSortBy.SortName);
            }

            query.SortBy = sortOrders.ToArray();
            query.SortOrder = sort.SortOrder;
        }

        private QueryResult<ServerItem> GetItemsFromPerson(Person person, User user, int? startIndex, int? limit)
        {
            var itemsResult = _libraryManager.GetItemsResult(new InternalItemsQuery(user)
            {
                PersonIds = new[] { person.Id.ToString("N") },
                IncludeItemTypes = new[] { typeof(Movie).Name, typeof(Series).Name, typeof(Trailer).Name },
                SortBy = new[] { ItemSortBy.SortName },
                Limit = limit,
                StartIndex = startIndex

            });

            var serverItems = itemsResult.Items.Select(i => new ServerItem(i))
            .ToArray();

            return new QueryResult<ServerItem>
            {
                TotalRecordCount = itemsResult.TotalRecordCount,
                Items = serverItems
            };
        }

        private QueryResult<ServerItem> ApplyPaging(QueryResult<ServerItem> result, int? startIndex, int? limit)
        {
            result.Items = result.Items.Skip(startIndex ?? 0).Take(limit ?? int.MaxValue).ToArray();

            return result;
        }

        private ServerItem GetItemFromObjectId(string id, User user)
        {
            return DidlBuilder.IsIdRoot(id)

                 ? new ServerItem(user.RootFolder)
                 : ParseItemId(id, user);
        }

        private ServerItem ParseItemId(string id, User user)
        {
            Guid itemId;
            StubType? stubType = null;

            // After using PlayTo, MediaMonkey sends a request to the server trying to get item info
            const string paramsSrch = "Params=";
            var paramsIndex = id.IndexOf(paramsSrch, StringComparison.OrdinalIgnoreCase);
            if (paramsIndex != -1)
            {
                id = id.Substring(paramsIndex + paramsSrch.Length);

                var parts = id.Split(';');
                id = parts[23];
            }

            if (id.StartsWith("folder_", StringComparison.OrdinalIgnoreCase))
            {
                stubType = StubType.Folder;
                id = id.Split(new[] { '_' }, 2)[1];
            }
            else if (id.StartsWith("people_", StringComparison.OrdinalIgnoreCase))
            {
                stubType = StubType.People;
                id = id.Split(new[] { '_' }, 2)[1];
            }

            if (Guid.TryParse(id, out itemId))
            {
                var item = _libraryManager.GetItemById(itemId);

                return new ServerItem(item)
                {
                    StubType = stubType
                };
            }

            Logger.Error("Error parsing item Id: {0}. Returning user root folder.", id);

            return new ServerItem(user.RootFolder);
        }
    }

    internal class ServerItem
    {
        public BaseItem Item { get; set; }
        public StubType? StubType { get; set; }

        public ServerItem(BaseItem item)
        {
            Item = item;

            if (item is IItemByName && !(item is Folder))
            {
                StubType = Dlna.ContentDirectory.StubType.Folder;
            }
        }
    }

    public enum StubType
    {
        Folder = 0,
        People = 1
    }
}

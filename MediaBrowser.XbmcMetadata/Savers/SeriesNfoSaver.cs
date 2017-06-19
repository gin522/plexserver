﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Xml;

namespace MediaBrowser.XbmcMetadata.Savers
{
    public class SeriesNfoSaver : BaseNfoSaver
    {
        protected override string GetLocalSavePath(IHasMetadata item)
        {
            return Path.Combine(item.Path, "tvshow.nfo");
        }

        protected override string GetRootElementName(IHasMetadata item)
        {
            return "tvshow";
        }

        public override bool IsEnabledFor(IHasMetadata item, ItemUpdateType updateType)
        {
            if (!item.SupportsLocalMetadata)
            {
                return false;
            }

            return item is Series && updateType >= MinimumUpdateType;
        }

        protected override void WriteCustomElements(IHasMetadata item, XmlWriter writer)
        {
            var series = (Series)item;

            var tvdb = item.GetProviderId(MetadataProviders.Tvdb);

            if (!string.IsNullOrEmpty(tvdb))
            {
                writer.WriteElementString("id", tvdb);

                writer.WriteStartElement("episodeguide");

                var language = item.GetPreferredMetadataLanguage();
                language = string.IsNullOrEmpty(language)
                    ? "en"
                    : language;

                writer.WriteStartElement("url");
                writer.WriteAttributeString("cache", string.Format("{0}.xml", tvdb));
                writer.WriteString(string.Format("http://www.thetvdb.com/api/1D62F2F90030C444/series/{0}/all/{1}.zip", tvdb, language));
                writer.WriteEndElement();
                
                writer.WriteEndElement();
            }

            writer.WriteElementString("season", "-1");
            writer.WriteElementString("episode", "-1");

            if (series.Status.HasValue)
            {
                writer.WriteElementString("status", series.Status.Value.ToString());
            }

            if (!string.IsNullOrEmpty(series.AirTime))
            {
                writer.WriteElementString("airs_time", series.AirTime);
            }

            if (series.AirDays.Count == 7)
            {
                writer.WriteElementString("airs_dayofweek", "Daily");
            }
            else if (series.AirDays.Count > 0)
            {
                writer.WriteElementString("airs_dayofweek", series.AirDays[0].ToString());
            }
        }

        protected override List<string> GetTagsUsed(IHasMetadata item)
        {
            var list = base.GetTagsUsed(item);
            list.AddRange(new string[]
            {
                "id",
                "episodeguide",
                "season",
                "episode",
                "status",
                "airs_time",
                "airs_dayofweek"
            });
            return list;
        }

        public SeriesNfoSaver(IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, ILogger logger, IXmlReaderSettingsFactory xmlReaderSettingsFactory) : base(fileSystem, configurationManager, libraryManager, userManager, userDataManager, logger, xmlReaderSettingsFactory)
        {
        }
    }
}

﻿using MediaBrowser.Model.Drawing;
using System.Linq;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Controller.Entities
{
    public class Photo : BaseItem
    {
        [IgnoreDataMember]
        public override bool SupportsLocalMetadata
        {
            get
            {
                return false;
            }
        }

        [IgnoreDataMember]
        public override string MediaType
        {
            get
            {
                return Model.Entities.MediaType.Photo;
            }
        }

        [IgnoreDataMember]
        public override Folder LatestItemsIndexContainer
        {
            get
            {
                return AlbumEntity;
            }
        }


        [IgnoreDataMember]
        public PhotoAlbum AlbumEntity
        {
            get
            {
                return GetParents().OfType<PhotoAlbum>().FirstOrDefault();
            }
        }

        [IgnoreDataMember]
        public override bool EnableRefreshOnDateModifiedChange
        {
            get { return true; }
        }

        public override bool CanDownload()
        {
            return true;
        }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public string Software { get; set; }
        public double? ExposureTime { get; set; }
        public double? FocalLength { get; set; }
        public ImageOrientation? Orientation { get; set; }
        public double? Aperture { get; set; }
        public double? ShutterSpeed { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Altitude { get; set; }
        public int? IsoSpeedRating { get; set; }
    }
}

﻿using MediaBrowser.Model.Dlna;
using System.Xml.Serialization;

namespace Emby.Dlna.Profiles
{
    [XmlRoot("Profile")]
    public class VlcProfile : DefaultProfile
    {
        public VlcProfile()
        {
            Name = "Vlc";


            TimelineOffsetSeconds = 5;

            Identification = new DeviceIdentification
            {
                ModelName = "Vlc",

                Headers = new[]
                {
                    new HttpHeaderInfo {Name = "User-Agent", Value = "vlc", Match = HeaderMatchType.Substring}
                }
            };

            TranscodingProfiles = new[]
            {
                new TranscodingProfile
                {
                    Container = "mp3",
                    AudioCodec = "mp3",
                    Type = DlnaProfileType.Audio
                },

                new TranscodingProfile
                {
                    Container = "ts",
                    Type = DlnaProfileType.Video,
                    AudioCodec = "aac",
                    VideoCodec = "h264"
                },

                new TranscodingProfile
                {
                    Container = "jpeg",
                    Type = DlnaProfileType.Photo
                }
            };

            DirectPlayProfiles = new[]
            {
                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Video
                },

                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Audio
                },

                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Photo,
                }
            };

            ResponseProfiles = new ResponseProfile[] { };

            ContainerProfiles = new ContainerProfile[] { };

            CodecProfiles = new CodecProfile[] { };

            SubtitleProfiles = new[]
            {
                new SubtitleProfile
                {
                    Format = "srt",
                    Method = SubtitleDeliveryMethod.External,
                },

                new SubtitleProfile
                {
                    Format = "sub",
                    Method = SubtitleDeliveryMethod.External,
                },

                new SubtitleProfile
                {
                    Format = "srt",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "ass",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "ssa",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "smi",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "dvdsub",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "pgs",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "pgssub",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                },

                new SubtitleProfile
                {
                    Format = "sub",
                    Method = SubtitleDeliveryMethod.Embed,
                    DidlMode = "",
                }
            };
        }
    }
}

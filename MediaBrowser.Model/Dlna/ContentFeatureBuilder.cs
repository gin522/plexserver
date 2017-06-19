﻿using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.Dlna
{
    public class ContentFeatureBuilder
    {
        private readonly DeviceProfile _profile;

        public ContentFeatureBuilder(DeviceProfile profile)
        {
            _profile = profile;
        }

        public string BuildImageHeader(string container,
            int? width,
            int? height,
            bool isDirectStream,
            string orgPn = null)
        {
            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetImageOrgOpValue();

            // 0 = native, 1 = transcoded
            var orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            DlnaFlags flagValue = DlnaFlags.BackgroundTransferMode |
                            DlnaFlags.InteractiveTransferMode |
                            DlnaFlags.DlnaV15;
            
            string dlnaflags = string.Format(";DLNA.ORG_FLAGS={0}",
             DlnaMaps.FlagsToString(flagValue));

            ResponseProfile mediaProfile = _profile.GetImageMediaProfile(container,
                width,
                height);

            if (string.IsNullOrEmpty(orgPn))
            {
                orgPn = mediaProfile == null ? null : mediaProfile.OrgPn;
            }

            if (string.IsNullOrEmpty(orgPn))
            {
                orgPn = GetImageOrgPnValue(container, width, height);
            }

            string contentFeatures = string.IsNullOrEmpty(orgPn) ? string.Empty : "DLNA.ORG_PN=" + orgPn;

            return (contentFeatures + orgOp + orgCi + dlnaflags).Trim(';');
        }

        public string BuildAudioHeader(string container,
            string audioCodec,
            int? audioBitrate,
            int? audioSampleRate,
            int? audioChannels,
            bool isDirectStream,
            long? runtimeTicks,
            TranscodeSeekInfo transcodeSeekInfo)
        {
            // first bit means Time based seek supported, second byte range seek supported (not sure about the order now), so 01 = only byte seek, 10 = time based, 11 = both, 00 = none
            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetOrgOpValue(runtimeTicks.HasValue, isDirectStream, transcodeSeekInfo);

            // 0 = native, 1 = transcoded
            string orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            DlnaFlags flagValue = DlnaFlags.StreamingTransferMode |
                            DlnaFlags.BackgroundTransferMode |
                            DlnaFlags.InteractiveTransferMode |
                            DlnaFlags.DlnaV15;

            //if (isDirectStream)
            //{
            //    flagValue = flagValue | DlnaFlags.ByteBasedSeek;
            //}
            //else if (runtimeTicks.HasValue)
            //{
            //    flagValue = flagValue | DlnaFlags.TimeBasedSeek;
            //}

            string dlnaflags = string.Format(";DLNA.ORG_FLAGS={0}",
             DlnaMaps.FlagsToString(flagValue));

            ResponseProfile mediaProfile = _profile.GetAudioMediaProfile(container,
                audioCodec,
                audioChannels,
                audioBitrate);

            string orgPn = mediaProfile == null ? null : mediaProfile.OrgPn;

            if (string.IsNullOrEmpty(orgPn))
            {
                orgPn = GetAudioOrgPnValue(container, audioBitrate, audioSampleRate, audioChannels);
            }

            string contentFeatures = string.IsNullOrEmpty(orgPn) ? string.Empty : "DLNA.ORG_PN=" + orgPn;

            return (contentFeatures + orgOp + orgCi + dlnaflags).Trim(';');
        }

        public List<string> BuildVideoHeader(string container,
            string videoCodec,
            string audioCodec,
            int? width,
            int? height,
            int? bitDepth,
            int? videoBitrate,
            TransportStreamTimestamp timestamp,
            bool isDirectStream,
            long? runtimeTicks,
            string videoProfile,
            double? videoLevel,
            float? videoFramerate,
            int? packetLength,
            TranscodeSeekInfo transcodeSeekInfo,
            bool? isAnamorphic,
            int? refFrames,
            int? numVideoStreams,
            int? numAudioStreams,
            string videoCodecTag,
            bool? isAvc)
        {
            // first bit means Time based seek supported, second byte range seek supported (not sure about the order now), so 01 = only byte seek, 10 = time based, 11 = both, 00 = none
            string orgOp = ";DLNA.ORG_OP=" + DlnaMaps.GetOrgOpValue(runtimeTicks.HasValue, isDirectStream, transcodeSeekInfo);

            // 0 = native, 1 = transcoded
            string orgCi = isDirectStream ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            DlnaFlags flagValue = DlnaFlags.StreamingTransferMode |
                            DlnaFlags.BackgroundTransferMode |
                            DlnaFlags.InteractiveTransferMode |
                            DlnaFlags.DlnaV15;

            //if (isDirectStream)
            //{
            //    flagValue = flagValue | DlnaFlags.ByteBasedSeek;
            //}
            //else if (runtimeTicks.HasValue)
            //{
            //    flagValue = flagValue | DlnaFlags.TimeBasedSeek;
            //}

            string dlnaflags = string.Format(";DLNA.ORG_FLAGS={0}",
             DlnaMaps.FlagsToString(flagValue));

            ResponseProfile mediaProfile = _profile.GetVideoMediaProfile(container,
                audioCodec,
                videoCodec,
                width,
                height,
                bitDepth,
                videoBitrate,
                videoProfile,
                videoLevel,
                videoFramerate,
                packetLength,
                timestamp,
                isAnamorphic,
                refFrames,
                numVideoStreams,
                numAudioStreams,
                videoCodecTag,
                isAvc);

            List<string> orgPnValues = new List<string>();

            if (mediaProfile != null && !string.IsNullOrEmpty(mediaProfile.OrgPn))
            {
                orgPnValues.AddRange(mediaProfile.OrgPn.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                foreach (string s in GetVideoOrgPnValue(container, videoCodec, audioCodec, width, height, timestamp))
                {
                    orgPnValues.Add(s);
                    break;
                }
            }

            List<string> contentFeatureList = new List<string>();

            foreach (string orgPn in orgPnValues)
            {
                string contentFeatures = string.IsNullOrEmpty(orgPn) ? string.Empty : "DLNA.ORG_PN=" + orgPn;

                var value = (contentFeatures + orgOp + orgCi + dlnaflags).Trim(';');

                contentFeatureList.Add(value);
            }

            if (orgPnValues.Count == 0)
            {
                string contentFeatures = string.Empty;

                var value = (contentFeatures + orgOp + orgCi + dlnaflags).Trim(';');

                contentFeatureList.Add(value);
            }

            return contentFeatureList;
        }

        private string GetImageOrgPnValue(string container, int? width, int? height)
        {
            MediaFormatProfile? format = new MediaFormatProfileResolver()
                .ResolveImageFormat(container,
                width,
                height);

            return format.HasValue ? format.Value.ToString() : null;
        }

        private string GetAudioOrgPnValue(string container, int? audioBitrate, int? audioSampleRate, int? audioChannels)
        {
            MediaFormatProfile? format = new MediaFormatProfileResolver()
                .ResolveAudioFormat(container,
                audioBitrate,
                audioSampleRate,
                audioChannels);

            return format.HasValue ? format.Value.ToString() : null;
        }

        private List<string> GetVideoOrgPnValue(string container, string videoCodec, string audioCodec, int? width, int? height, TransportStreamTimestamp timestamp)
        {
            List<string> list = new List<string>();
            foreach (MediaFormatProfile i in new MediaFormatProfileResolver().ResolveVideoFormat(container, videoCodec, audioCodec, width, height, timestamp))
                list.Add(i.ToString());
            return list;
        }
    }
}

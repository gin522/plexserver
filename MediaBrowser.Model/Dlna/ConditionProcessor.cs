﻿using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MediaBrowser.Model.Dlna
{
    public class ConditionProcessor
    {
        public bool IsVideoConditionSatisfied(ProfileCondition condition,
            int? width,
            int? height,
            int? bitDepth,
            int? videoBitrate,
            string videoProfile,
            double? videoLevel,
            float? videoFramerate,
            int? packetLength,
            TransportStreamTimestamp? timestamp,
            bool? isAnamorphic,
            int? refFrames,
            int? numVideoStreams,
            int? numAudioStreams,
            string videoCodecTag,
            bool? isAvc )
        {
            switch (condition.Property)
            {
                case ProfileConditionValue.IsAnamorphic:
                    return IsConditionSatisfied(condition, isAnamorphic);
                case ProfileConditionValue.IsAvc:
                    return IsConditionSatisfied(condition, isAvc);
                case ProfileConditionValue.VideoFramerate:
                    return IsConditionSatisfied(condition, videoFramerate);
                case ProfileConditionValue.VideoLevel:
                    return IsConditionSatisfied(condition, videoLevel);
                case ProfileConditionValue.VideoProfile:
                    return IsConditionSatisfied(condition, videoProfile);
                case ProfileConditionValue.VideoCodecTag:
                    return IsConditionSatisfied(condition, videoCodecTag);
                case ProfileConditionValue.PacketLength:
                    return IsConditionSatisfied(condition, packetLength);
                case ProfileConditionValue.VideoBitDepth:
                    return IsConditionSatisfied(condition, bitDepth);
                case ProfileConditionValue.VideoBitrate:
                    return IsConditionSatisfied(condition, videoBitrate);
                case ProfileConditionValue.Height:
                    return IsConditionSatisfied(condition, height);
                case ProfileConditionValue.Width:
                    return IsConditionSatisfied(condition, width);
                case ProfileConditionValue.RefFrames:
                    return IsConditionSatisfied(condition, refFrames);
                case ProfileConditionValue.NumAudioStreams:
                    return IsConditionSatisfied(condition, numAudioStreams);
                case ProfileConditionValue.NumVideoStreams:
                    return IsConditionSatisfied(condition, numVideoStreams);
                case ProfileConditionValue.VideoTimestamp:
                    return IsConditionSatisfied(condition, timestamp);
                default:
                    return true;
            }
        }

        public bool IsImageConditionSatisfied(ProfileCondition condition, int? width, int? height)
        {
            switch (condition.Property)
            {
                case ProfileConditionValue.Height:
                    return IsConditionSatisfied(condition, height);
                case ProfileConditionValue.Width:
                    return IsConditionSatisfied(condition, width);
                default:
                    throw new ArgumentException("Unexpected condition on image file: " + condition.Property);
            }
        }

        public bool IsAudioConditionSatisfied(ProfileCondition condition, int? audioChannels, int? audioBitrate)
        {
            switch (condition.Property)
            {
                case ProfileConditionValue.AudioBitrate:
                    return IsConditionSatisfied(condition, audioBitrate);
                case ProfileConditionValue.AudioChannels:
                    return IsConditionSatisfied(condition, audioChannels);
                default:
                    throw new ArgumentException("Unexpected condition on audio file: " + condition.Property);
            }
        }

        public bool IsVideoAudioConditionSatisfied(ProfileCondition condition,
            int? audioChannels,
            int? audioBitrate,
            string audioProfile,
            bool? isSecondaryTrack)
        {
            switch (condition.Property)
            {
                case ProfileConditionValue.AudioProfile:
                    return IsConditionSatisfied(condition, audioProfile);
                case ProfileConditionValue.AudioBitrate:
                    return IsConditionSatisfied(condition, audioBitrate);
                case ProfileConditionValue.AudioChannels:
                    return IsConditionSatisfied(condition, audioChannels);
                case ProfileConditionValue.IsSecondaryAudio:
                    return IsConditionSatisfied(condition, isSecondaryTrack);
                default:
                    throw new ArgumentException("Unexpected condition on audio file: " + condition.Property);
            }
        }

        private bool IsConditionSatisfied(ProfileCondition condition, int? currentValue)
        {
            if (!currentValue.HasValue)
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            int expected;
            if (int.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out expected))
            {
                switch (condition.Condition)
                {
                    case ProfileConditionType.Equals:
                    case ProfileConditionType.EqualsAny:
                        return currentValue.Value.Equals(expected);
                    case ProfileConditionType.GreaterThanEqual:
                        return currentValue.Value >= expected;
                    case ProfileConditionType.LessThanEqual:
                        return currentValue.Value <= expected;
                    case ProfileConditionType.NotEquals:
                        return !currentValue.Value.Equals(expected);
                    default:
                        throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
                }
            }

            return false;
        }

        private bool IsConditionSatisfied(ProfileCondition condition, string currentValue)
        {
            if (string.IsNullOrEmpty(currentValue))
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            string expected = condition.Value;

            switch (condition.Condition)
            {
                case ProfileConditionType.EqualsAny:
                    {
                        return ListHelper.ContainsIgnoreCase(expected.Split('|'), currentValue);
                    }
                case ProfileConditionType.Equals:
                    return StringHelper.EqualsIgnoreCase(currentValue, expected);
                case ProfileConditionType.NotEquals:
                    return !StringHelper.EqualsIgnoreCase(currentValue, expected);
                default:
                    throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
            }
        }

        private bool IsConditionSatisfied(ProfileCondition condition, bool? currentValue)
        {
            if (!currentValue.HasValue)
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            bool expected;
            if (bool.TryParse(condition.Value, out expected))
            {
                switch (condition.Condition)
                {
                    case ProfileConditionType.Equals:
                        return currentValue.Value == expected;
                    case ProfileConditionType.NotEquals:
                        return currentValue.Value != expected;
                    default:
                        throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
                }
            }

            return false;
        }

        private bool IsConditionSatisfied(ProfileCondition condition, float? currentValue)
        {
            if (!currentValue.HasValue)
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            float expected;
            if (float.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out expected))
            {
                switch (condition.Condition)
                {
                    case ProfileConditionType.Equals:
                        return currentValue.Value.Equals(expected);
                    case ProfileConditionType.GreaterThanEqual:
                        return currentValue.Value >= expected;
                    case ProfileConditionType.LessThanEqual:
                        return currentValue.Value <= expected;
                    case ProfileConditionType.NotEquals:
                        return !currentValue.Value.Equals(expected);
                    default:
                        throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
                }
            }

            return false;
        }

        private bool IsConditionSatisfied(ProfileCondition condition, double? currentValue)
        {
            if (!currentValue.HasValue)
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            double expected;
            if (double.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out expected))
            {
                switch (condition.Condition)
                {
                    case ProfileConditionType.Equals:
                        return currentValue.Value.Equals(expected);
                    case ProfileConditionType.GreaterThanEqual:
                        return currentValue.Value >= expected;
                    case ProfileConditionType.LessThanEqual:
                        return currentValue.Value <= expected;
                    case ProfileConditionType.NotEquals:
                        return !currentValue.Value.Equals(expected);
                    default:
                        throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
                }
            }

            return false;
        }

        private bool IsConditionSatisfied(ProfileCondition condition, TransportStreamTimestamp? timestamp)
        {
            if (!timestamp.HasValue)
            {
                // If the value is unknown, it satisfies if not marked as required
                return !condition.IsRequired;
            }

            TransportStreamTimestamp expected = (TransportStreamTimestamp)Enum.Parse(typeof(TransportStreamTimestamp), condition.Value, true);

            switch (condition.Condition)
            {
                case ProfileConditionType.Equals:
                    return timestamp == expected;
                case ProfileConditionType.NotEquals:
                    return timestamp != expected;
                default:
                    throw new InvalidOperationException("Unexpected ProfileConditionType: " + condition.Condition);
            }
        }
    }
}

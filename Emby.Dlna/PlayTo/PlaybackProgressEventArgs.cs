﻿using System;

namespace Emby.Dlna.PlayTo
{
    public class PlaybackProgressEventArgs : EventArgs
    {
        public uBaseObject MediaInfo { get; set; }
    }
}
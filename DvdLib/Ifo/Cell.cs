﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DvdLib.Ifo
{
    public class Cell
    {
        public CellPlaybackInfo PlaybackInfo { get; private set; }
        public CellPositionInfo PositionInfo { get; private set; }

        internal void ParsePlayback(BinaryReader br)
        {
            PlaybackInfo = new CellPlaybackInfo(br);
        }

        internal void ParsePosition(BinaryReader br)
        {
            PositionInfo = new CellPositionInfo(br);
        }
    }
}

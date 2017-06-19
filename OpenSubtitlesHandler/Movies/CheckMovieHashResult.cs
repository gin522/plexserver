﻿/* This file is part of OpenSubtitles Handler
   A library that handle OpenSubtitles.org XML-RPC methods.

   Copyright © Ala Ibrahim Hadid 2013

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace OpenSubtitlesHandler
{
    public struct CheckMovieHashResult
    {
        private string name;
        private string _MovieHash;
        private string _MovieImdbID;
        private string _MovieName;
        private string _MovieYear;

        public string MovieHash { get { return _MovieHash; } set { _MovieHash = value; } }
        public string MovieImdbID { get { return _MovieImdbID; } set { _MovieImdbID = value; } }
        public string MovieName { get { return _MovieName; } set { _MovieName = value; } }
        public string MovieYear { get { return _MovieYear; } set { _MovieYear = value; } }
        public string Name { get { return name; } set { name = value; } }

        public override string ToString()
        {
            return name;
        }
    }
}

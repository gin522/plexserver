﻿
namespace MediaBrowser.Model.Search
{
    public class SearchQuery
    {
        /// <summary>
        /// The user to localize search results for
        /// </summary>
        /// <value>The user id.</value>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the search term.
        /// </summary>
        /// <value>The search term.</value>
        public string SearchTerm { get; set; }

        /// <summary>
        /// Skips over a given number of items within the results. Use for paging.
        /// </summary>
        /// <value>The start index.</value>
        public int? StartIndex { get; set; }

        /// <summary>
        /// The maximum number of items to return
        /// </summary>
        /// <value>The limit.</value>
        public int? Limit { get; set; }

        public bool IncludePeople { get; set; }
        public bool IncludeMedia { get; set; }
        public bool IncludeGenres { get; set; }
        public bool IncludeStudios { get; set; }
        public bool IncludeArtists { get; set; }

        public string[] IncludeItemTypes { get; set; }

        public SearchQuery()
        {
            IncludeArtists = true;
            IncludeGenres = true;
            IncludeMedia = true;
            IncludePeople = true;
            IncludeStudios = true;

            IncludeItemTypes = new string[] { };
        }
    }
}

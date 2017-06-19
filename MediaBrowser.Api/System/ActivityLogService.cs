﻿using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Querying;
using System;
using System.Globalization;
using MediaBrowser.Model.Services;

namespace MediaBrowser.Api.System
{
    [Route("/System/ActivityLog/Entries", "GET", Summary = "Gets activity log entries")]
    public class GetActivityLogs : IReturn<QueryResult<ActivityLogEntry>>
    {
        /// <summary>
        /// Skips over a given number of items within the results. Use for paging.
        /// </summary>
        /// <value>The start index.</value>
        [ApiMember(Name = "StartIndex", Description = "Optional. The record index to start at. All items with a lower index will be dropped from the results.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? StartIndex { get; set; }

        /// <summary>
        /// The maximum number of items to return
        /// </summary>
        /// <value>The limit.</value>
        [ApiMember(Name = "Limit", Description = "Optional. The maximum number of records to return", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? Limit { get; set; }

        [ApiMember(Name = "MinDate", Description = "Optional. The minimum date. Format = ISO", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string MinDate { get; set; }
    }

    [Authenticated(Roles = "Admin")]
    public class ActivityLogService : BaseApiService
    {
        private readonly IActivityManager _activityManager;

        public ActivityLogService(IActivityManager activityManager)
        {
            _activityManager = activityManager;
        }

        public object Get(GetActivityLogs request)
        {
            DateTime? minDate = string.IsNullOrWhiteSpace(request.MinDate) ?
                (DateTime?)null :
                DateTime.Parse(request.MinDate, null, DateTimeStyles.RoundtripKind).ToUniversalTime();

            var result = _activityManager.GetActivityLogEntries(minDate, request.StartIndex, request.Limit);

            return ToOptimizedResult(result);
        }
    }
}

﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.Library;
using MediaBrowser.Model.Tasks;

namespace Emby.Server.Implementations.ScheduledTasks
{
    /// <summary>
    /// Class RefreshMediaLibraryTask
    /// </summary>
    public class RefreshMediaLibraryTask : IScheduledTask
    {
        /// <summary>
        /// The _library manager
        /// </summary>
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshMediaLibraryTask" /> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        public RefreshMediaLibraryTask(ILibraryManager libraryManager, IServerConfigurationManager config)
        {
            _libraryManager = libraryManager;
            _config = config;
        }

        /// <summary>
        /// Creates the triggers that define when the task will run
        /// </summary>
        /// <returns>IEnumerable{BaseTaskTrigger}.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[] { 
            
                // Every so often
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(12).Ticks}
            };
        }

        /// <summary>
        /// Executes the internal.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress.Report(0);

            return ((LibraryManager)_libraryManager).ValidateMediaLibraryInternal(progress, cancellationToken);
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Scan media library"; }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get { return "Scans your media library and refreshes metatata based on configuration."; }
        }

        /// <summary>
        /// Gets the category.
        /// </summary>
        /// <value>The category.</value>
        public string Category
        {
            get
            {
                return "Library";
            }
        }

        public string Key
        {
            get { return "RefreshLibrary"; }
        }
    }
}

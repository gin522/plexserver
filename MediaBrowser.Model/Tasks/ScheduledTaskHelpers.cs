﻿using System;
using System.Linq;

namespace MediaBrowser.Model.Tasks
{
    /// <summary>
    /// Class ScheduledTaskHelpers
    /// </summary>
    public static class ScheduledTaskHelpers
    {
        /// <summary>
        /// Gets the task info.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <returns>TaskInfo.</returns>
        public static TaskInfo GetTaskInfo(IScheduledTaskWorker task)
        {
            var isHidden = false;

            var configurableTask = task.ScheduledTask as IConfigurableScheduledTask;

            if (configurableTask != null)
            {
                isHidden = configurableTask.IsHidden;
            }

            string key = task.ScheduledTask.Key;

            var triggers = task.Triggers
                .OrderBy(i => i.Type)
                .ThenBy(i => i.DayOfWeek ?? DayOfWeek.Sunday)
                .ThenBy(i => i.TimeOfDayTicks ?? 0)
                .ToList();

            return new TaskInfo
            {
                Name = task.Name,
                CurrentProgressPercentage = task.CurrentProgress,
                State = task.State,
                Id = task.Id,
                LastExecutionResult = task.LastExecutionResult,

                Triggers = triggers,

                Description = task.Description,
                Category = task.Category,
                IsHidden = isHidden,
                Key = key
            };
        }
    }
}

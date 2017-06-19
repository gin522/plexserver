﻿using System;

namespace MediaBrowser.Model.Tasks
{
    public class TaskCompletionEventArgs : EventArgs
    {
        public IScheduledTaskWorker Task { get; set; }

        public TaskResult Result { get; set; }
    }
}

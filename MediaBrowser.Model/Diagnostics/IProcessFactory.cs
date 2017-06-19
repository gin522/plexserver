﻿using System;

namespace MediaBrowser.Model.Diagnostics
{
    public interface IProcessFactory
    {
        IProcess Create(ProcessOptions options);
    }

    public class ProcessOptions
    {
        public String FileName { get; set; }
        public String Arguments { get; set; }
        public String WorkingDirectory { get; set; }
        public bool CreateNoWindow { get; set; }
        public bool UseShellExecute { get; set; }
        public bool EnableRaisingEvents { get; set; }
        public bool ErrorDialog { get; set; }
        public bool RedirectStandardError { get; set; }
        public bool RedirectStandardInput { get; set; }
        public bool RedirectStandardOutput { get; set; }
        public bool IsHidden { get; set; }
    }
}

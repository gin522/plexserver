﻿using MediaBrowser.Model.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;

namespace MediaBrowser.Controller.Providers
{
    public class DirectoryService : IDirectoryService
    {
        private readonly ILogger _logger;
		private readonly IFileSystem _fileSystem;

        private readonly ConcurrentDictionary<string, Dictionary<string, FileSystemMetadata>> _cache =
            new ConcurrentDictionary<string, Dictionary<string, FileSystemMetadata>>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, FileSystemMetadata> _fileCache =
        new ConcurrentDictionary<string, FileSystemMetadata>(StringComparer.OrdinalIgnoreCase);

        public DirectoryService(ILogger logger, IFileSystem fileSystem)
        {
            _logger = logger;
			_fileSystem = fileSystem;
        }

        public DirectoryService(IFileSystem fileSystem)
            : this(new NullLogger(), fileSystem)
        {
        }

        public IEnumerable<FileSystemMetadata> GetFileSystemEntries(string path)
        {
            return GetFileSystemEntries(path, false);
        }

        public Dictionary<string, FileSystemMetadata> GetFileSystemDictionary(string path)
        {
            return GetFileSystemDictionary(path, false);
        }

        private Dictionary<string, FileSystemMetadata> GetFileSystemDictionary(string path, bool clearCache)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }

            Dictionary<string, FileSystemMetadata> entries;

            if (clearCache)
            {
                Dictionary<string, FileSystemMetadata> removed;

                _cache.TryRemove(path, out removed);
            }

            if (!_cache.TryGetValue(path, out entries))
            {
                //_logger.Debug("Getting files for " + path);

                entries = new Dictionary<string, FileSystemMetadata>(StringComparer.OrdinalIgnoreCase);
                
                try
                {
                    // using EnumerateFileSystemInfos doesn't handle reparse points (symlinks)
					var list = _fileSystem.GetFileSystemEntries(path)
                        .ToList();

                    // Seeing dupes on some users file system for some reason
                    foreach (var item in list)
                    {
                        entries[item.FullName] = item;
                    }
                }
                catch (IOException)
                {
                }

                //var group = entries.ToLookup(i => Path.GetDirectoryName(i.FullName)).ToList();

                _cache.TryAdd(path, entries);
            }

            return entries;
        }

        private IEnumerable<FileSystemMetadata> GetFileSystemEntries(string path, bool clearCache)
        {
            return GetFileSystemDictionary(path, clearCache).Values;
        }

        public IEnumerable<FileSystemMetadata> GetFiles(string path)
        {
            return GetFiles(path, false);
        }

        public IEnumerable<FileSystemMetadata> GetFiles(string path, bool clearCache)
        {
            return GetFileSystemEntries(path, clearCache).Where(i => !i.IsDirectory);
        }

        public IEnumerable<string> GetFilePaths(string path)
        {
            return _fileSystem.GetFilePaths(path);
        }

        public IEnumerable<string> GetFilePaths(string path, bool clearCache)
        {
            return _fileSystem.GetFilePaths(path);
        }

        public FileSystemMetadata GetFile(string path)
        {
            FileSystemMetadata file;
            if (!_fileCache.TryGetValue(path, out file))
            {
                file = _fileSystem.GetFileInfo(path);

                if (file != null)
                {
                    _fileCache.TryAdd(path, file);
                }
            }

            return file;
            //return _fileSystem.GetFileInfo(path);
        }

        public IEnumerable<FileSystemMetadata> GetDirectories(string path)
        {
            return GetFileSystemEntries(path, false).Where(i => i.IsDirectory);
        }
    }
}

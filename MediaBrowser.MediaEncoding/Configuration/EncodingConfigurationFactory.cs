﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;

namespace MediaBrowser.MediaEncoding.Configuration
{
    public class EncodingConfigurationFactory : IConfigurationFactory
    {
        private readonly IFileSystem _fileSystem;

        public EncodingConfigurationFactory(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new[]
            {
                new EncodingConfigurationStore(_fileSystem)
            };
        }
    }

    public class EncodingConfigurationStore : ConfigurationStore, IValidatingConfiguration
    {
        private readonly IFileSystem _fileSystem;

        public EncodingConfigurationStore(IFileSystem fileSystem)
        {
            ConfigurationType = typeof(EncodingOptions);
            Key = "encoding";
            _fileSystem = fileSystem;
        }

        public void Validate(object oldConfig, object newConfig)
        {
            var oldEncodingConfig = (EncodingOptions)oldConfig;
            var newEncodingConfig = (EncodingOptions)newConfig;

            var newPath = newEncodingConfig.TranscodingTempPath;

            if (!string.IsNullOrWhiteSpace(newPath)
                && !string.Equals(oldEncodingConfig.TranscodingTempPath ?? string.Empty, newPath))
            {
                // Validate
                if (!_fileSystem.DirectoryExists(newPath))
                {
                    throw new FileNotFoundException(string.Format("{0} does not exist.", newPath));
                }
            }
        }
    }
}

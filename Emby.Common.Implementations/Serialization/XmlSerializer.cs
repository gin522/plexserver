﻿using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using MediaBrowser.Common.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

namespace Emby.Common.Implementations.Serialization
{
    /// <summary>
    /// Provides a wrapper around third party xml serialization.
    /// </summary>
    public class MyXmlSerializer : IXmlSerializer
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public MyXmlSerializer(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }

        // Need to cache these
        // http://dotnetcodebox.blogspot.com/2013/01/xmlserializer-class-may-result-in.html
        private readonly Dictionary<string, XmlSerializer> _serializers =
            new Dictionary<string, XmlSerializer>();

        private XmlSerializer GetSerializer(Type type)
        {
            var key = type.FullName;
            lock (_serializers)
            {
                XmlSerializer serializer;
                if (!_serializers.TryGetValue(key, out serializer))
                {
                    serializer = new XmlSerializer(type);
                    _serializers[key] = serializer;
                }
                return serializer;
            }
        }

        /// <summary>
        /// Serializes to writer.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="writer">The writer.</param>
        private void SerializeToWriter(object obj, XmlWriter writer)
        {
            var netSerializer = GetSerializer(obj.GetType());
            netSerializer.Serialize(writer, obj);
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="stream">The stream.</param>
        /// <returns>System.Object.</returns>
        public object DeserializeFromStream(Type type, Stream stream)
        {
            using (var reader = XmlReader.Create(stream))
            {
                var netSerializer = GetSerializer(type);
                return netSerializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Serializes to stream.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="stream">The stream.</param>
        public void SerializeToStream(object obj, Stream stream)
        {
#if NET46
            using (var writer = new XmlTextWriter(stream, null))            
            {
                writer.Formatting = Formatting.Indented;
                SerializeToWriter(obj, writer);
            }
#else
            using (var writer = XmlWriter.Create(stream))
            {
                SerializeToWriter(obj, writer);
            }
#endif
        }

        /// <summary>
        /// Serializes to file.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="file">The file.</param>
        public void SerializeToFile(object obj, string file)
        {
            _logger.Debug("Serializing to file {0}", file);
            using (var stream = new FileStream(file, FileMode.Create))
            {
                SerializeToStream(obj, stream);
            }
        }

        /// <summary>
        /// Deserializes from file.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="file">The file.</param>
        /// <returns>System.Object.</returns>
        public object DeserializeFromFile(Type type, string file)
        {
            _logger.Debug("Deserializing file {0}", file);
            using (var stream = _fileSystem.OpenRead(file))
            {
                return DeserializeFromStream(type, stream);
            }
        }

        /// <summary>
        /// Deserializes from bytes.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>System.Object.</returns>
        public object DeserializeFromBytes(Type type, byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                return DeserializeFromStream(type, stream);
            }
        }
    }
}

﻿using System.IO;
using MediaBrowser.Model.IO;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;

namespace Emby.Common.Implementations.Archiving
{
    /// <summary>
    /// Class DotNetZipClient
    /// </summary>
    public class ZipClient : IZipClient
    {
		private readonly IFileSystem _fileSystem;

		public ZipClient(IFileSystem fileSystem) 
		{
			_fileSystem = fileSystem;
		}

		/// <summary>
        /// Extracts all.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAll(string sourceFile, string targetPath, bool overwriteExistingFiles)
        {
			using (var fileStream = _fileSystem.OpenRead(sourceFile))
            {
                ExtractAll(fileStream, targetPath, overwriteExistingFiles);
            }
        }

        /// <summary>
        /// Extracts all.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAll(Stream source, string targetPath, bool overwriteExistingFiles)
        {
            using (var reader = ReaderFactory.Open(source))
            {
                var options = new ExtractionOptions();
                options.ExtractFullPath = true;

                if (overwriteExistingFiles)
                {
                    options.Overwrite = true;
                }

                reader.WriteAllToDirectory(targetPath, options);
            }
        }

        public void ExtractAllFromZip(Stream source, string targetPath, bool overwriteExistingFiles)
        {
            using (var reader = ZipReader.Open(source))
            {
                var options = new ExtractionOptions();
                options.ExtractFullPath = true;

                if (overwriteExistingFiles)
                {
                    options.Overwrite = true;
                }

                reader.WriteAllToDirectory(targetPath, options);
            }
        }

        /// <summary>
        /// Extracts all from7z.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFrom7z(string sourceFile, string targetPath, bool overwriteExistingFiles)
        {
			using (var fileStream = _fileSystem.OpenRead(sourceFile))
            {
                ExtractAllFrom7z(fileStream, targetPath, overwriteExistingFiles);
            }
        }

        /// <summary>
        /// Extracts all from7z.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFrom7z(Stream source, string targetPath, bool overwriteExistingFiles)
        {
            using (var archive = SevenZipArchive.Open(source))
            {
                using (var reader = archive.ExtractAllEntries())
                {
                    var options = new ExtractionOptions();
                    options.ExtractFullPath = true;

                    if (overwriteExistingFiles)
                    {
                        options.Overwrite = true;
                    }

                    reader.WriteAllToDirectory(targetPath, options);
                }
            }
        }


        /// <summary>
        /// Extracts all from tar.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFromTar(string sourceFile, string targetPath, bool overwriteExistingFiles)
        {
			using (var fileStream = _fileSystem.OpenRead(sourceFile))
            {
                ExtractAllFromTar(fileStream, targetPath, overwriteExistingFiles);
            }
        }

        /// <summary>
        /// Extracts all from tar.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFromTar(Stream source, string targetPath, bool overwriteExistingFiles)
        {
            using (var archive = TarArchive.Open(source))
            {
                using (var reader = archive.ExtractAllEntries())
                {
                    var options = new ExtractionOptions();
                    options.ExtractFullPath = true;

                    if (overwriteExistingFiles)
                    {
                        options.Overwrite = true;
                    }

                    reader.WriteAllToDirectory(targetPath, options);
                }
            }
        }

        /// <summary>
        /// Extracts all from rar.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFromRar(string sourceFile, string targetPath, bool overwriteExistingFiles)
        {
			using (var fileStream = _fileSystem.OpenRead(sourceFile))
            {
                ExtractAllFromRar(fileStream, targetPath, overwriteExistingFiles);
            }
        }

        /// <summary>
        /// Extracts all from rar.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="overwriteExistingFiles">if set to <c>true</c> [overwrite existing files].</param>
        public void ExtractAllFromRar(Stream source, string targetPath, bool overwriteExistingFiles)
        {
            using (var archive = RarArchive.Open(source))
            {
                using (var reader = archive.ExtractAllEntries())
                {
                    var options = new ExtractionOptions();
                    options.ExtractFullPath = true;

                    if (overwriteExistingFiles)
                    {
                        options.Overwrite = true;
                    }

                    reader.WriteAllToDirectory(targetPath, options);
                }
            }
        }
    }
}

﻿using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.Library.Resolvers.Books
{
    /// <summary>
    /// 
    /// </summary>
    public class BookResolver : MediaBrowser.Controller.Resolvers.ItemResolver<Book>
    {
        private readonly string[] _validExtensions = {".pdf", ".epub", ".mobi", ".cbr", ".cbz"};

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override Book Resolve(ItemResolveArgs args)
        {
            var collectionType = args.GetCollectionType();

            // Only process items that are in a collection folder containing books
            if (!string.Equals(collectionType, CollectionType.Books, StringComparison.OrdinalIgnoreCase))
                return null;
            
            if (args.IsDirectory)
            {
                return GetBook(args);
            }

            var extension = Path.GetExtension(args.Path);

            if (extension != null && _validExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                // It's a book
                return new Book
                {
                    Path = args.Path,
                    IsInMixedFolder = true
                };
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Book GetBook(ItemResolveArgs args)
        {
            var bookFiles = args.FileSystemChildren.Where(f =>
            {
                var fileExtension = Path.GetExtension(f.FullName) ??
                                    string.Empty;

                return _validExtensions.Contains(fileExtension,
                                                StringComparer
                                                    .OrdinalIgnoreCase);
            }).ToList();

            // Don't return a Book if there is more (or less) than one document in the directory
            if (bookFiles.Count != 1)
                return null;

            return new Book
                       {
                           Path = bookFiles[0].FullName
                       };
        }
    }
}

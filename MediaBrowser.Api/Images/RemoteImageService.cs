﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Services;

namespace MediaBrowser.Api.Images
{
    public class BaseRemoteImageRequest : IReturn<RemoteImageResult>
    {
        [ApiMember(Name = "Type", Description = "The image type", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public ImageType? Type { get; set; }

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

        [ApiMember(Name = "ProviderName", Description = "Optional. The image provider to use", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ProviderName { get; set; }

        [ApiMember(Name = "IncludeAllLanguages", Description = "Optional.", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "GET")]
        public bool IncludeAllLanguages { get; set; }
    }

    [Route("/Items/{Id}/RemoteImages", "GET", Summary = "Gets available remote images for an item")]
    [Authenticated]
    public class GetRemoteImages : BaseRemoteImageRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }
    }

    [Route("/Items/{Id}/RemoteImages/Providers", "GET", Summary = "Gets available remote image providers for an item")]
    [Authenticated]
    public class GetRemoteImageProviders : IReturn<List<ImageProviderInfo>>
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }
    }

    public class BaseDownloadRemoteImage : IReturnVoid
    {
        [ApiMember(Name = "Type", Description = "The image type", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
        public ImageType Type { get; set; }

        [ApiMember(Name = "ProviderName", Description = "The image provider", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ProviderName { get; set; }

        [ApiMember(Name = "ImageUrl", Description = "The image url", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ImageUrl { get; set; }
    }

    [Route("/Items/{Id}/RemoteImages/Download", "POST", Summary = "Downloads a remote image for an item")]
    [Authenticated(Roles="Admin")]
    public class DownloadRemoteImage : BaseDownloadRemoteImage
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }
    }

    [Route("/Images/Remote", "GET", Summary = "Gets a remote image")]
    public class GetRemoteImage
    {
        [ApiMember(Name = "ImageUrl", Description = "The image url", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ImageUrl { get; set; }
    }

    public class RemoteImageService : BaseApiService
    {
        private readonly IProviderManager _providerManager;

        private readonly IServerApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;

        private readonly IDtoService _dtoService;
        private readonly ILibraryManager _libraryManager;

        public RemoteImageService(IProviderManager providerManager, IDtoService dtoService, IServerApplicationPaths appPaths, IHttpClient httpClient, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            _providerManager = providerManager;
            _dtoService = dtoService;
            _appPaths = appPaths;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
        }

        public object Get(GetRemoteImageProviders request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            var result = GetImageProviders(item);

            return ToOptimizedSerializedResultUsingCache(result);
        }

        private List<ImageProviderInfo> GetImageProviders(BaseItem item)
        {
            return _providerManager.GetRemoteImageProviderInfo(item).ToList();
        }

        public async Task<object> Get(GetRemoteImages request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            return await GetRemoteImageResult(item, request).ConfigureAwait(false);
        }

        private async Task<RemoteImageResult> GetRemoteImageResult(BaseItem item, BaseRemoteImageRequest request)
        {
            var images = await _providerManager.GetAvailableRemoteImages(item, new RemoteImageQuery
            {
                ProviderName = request.ProviderName,
                IncludeAllLanguages = request.IncludeAllLanguages,
                IncludeDisabledProviders = true,
                ImageType = request.Type

            }, CancellationToken.None).ConfigureAwait(false);

            var imagesList = images.ToList();

            var allProviders = _providerManager.GetRemoteImageProviderInfo(item);

            if (request.Type.HasValue)
            {
                allProviders = allProviders.Where(i => i.SupportedImages.Contains(request.Type.Value));
            }

            var result = new RemoteImageResult
            {
                TotalRecordCount = imagesList.Count,
                Providers = allProviders.Select(i => i.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            };

            if (request.StartIndex.HasValue)
            {
                imagesList = imagesList.Skip(request.StartIndex.Value)
                    .ToList();
            }

            if (request.Limit.HasValue)
            {
                imagesList = imagesList.Take(request.Limit.Value)
                    .ToList();
            }

            result.Images = imagesList;

            return result;
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        public void Post(DownloadRemoteImage request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            var task = DownloadRemoteImage(item, request);

            Task.WaitAll(task);
        }

        /// <summary>
        /// Downloads the remote image.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="request">The request.</param>
        /// <returns>Task.</returns>
        private async Task DownloadRemoteImage(BaseItem item, BaseDownloadRemoteImage request)
        {
            await _providerManager.SaveImage(item, request.ImageUrl, request.Type, null, CancellationToken.None).ConfigureAwait(false);

            await item.UpdateToRepository(ItemUpdateType.ImageUpdate, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetRemoteImage request)
        {
            return GetAsync(request).Result;
        }

        public async Task<object> GetAsync(GetRemoteImage request)
        {
            var urlHash = request.ImageUrl.GetMD5();
            var pointerCachePath = GetFullCachePath(urlHash.ToString());

            string contentPath;

            try
            {
                contentPath = _fileSystem.ReadAllText(pointerCachePath);

                if (_fileSystem.FileExists(contentPath))
				{
				    return await ResultFactory.GetStaticFileResult(Request, contentPath).ConfigureAwait(false);
				}
            }
            catch (FileNotFoundException)
            {
                // Means the file isn't cached yet
            }
            catch (IOException)
            {
                // Means the file isn't cached yet
            }

            await DownloadImage(request.ImageUrl, urlHash, pointerCachePath).ConfigureAwait(false);

            // Read the pointer file again
            contentPath = _fileSystem.ReadAllText(pointerCachePath);

            return await ResultFactory.GetStaticFileResult(Request, contentPath).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads the image.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="urlHash">The URL hash.</param>
        /// <param name="pointerCachePath">The pointer cache path.</param>
        /// <returns>Task.</returns>
        private async Task DownloadImage(string url, Guid urlHash, string pointerCachePath)
        {
            var result = await _httpClient.GetResponse(new HttpRequestOptions
            {
                Url = url,
                BufferContent = false

            }).ConfigureAwait(false);

            var ext = result.ContentType.Split('/').Last();

            var fullCachePath = GetFullCachePath(urlHash + "." + ext);

			_fileSystem.CreateDirectory(Path.GetDirectoryName(fullCachePath));
            using (var stream = result.Content)
            {
                using (var filestream = _fileSystem.GetFileStream(fullCachePath, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read, true))
                {
                    await stream.CopyToAsync(filestream).ConfigureAwait(false);
                }
            }

			_fileSystem.CreateDirectory(Path.GetDirectoryName(pointerCachePath));
            _fileSystem.WriteAllText(pointerCachePath, fullCachePath);
        }

        /// <summary>
        /// Gets the full cache path.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        private string GetFullCachePath(string filename)
        {
            return Path.Combine(_appPaths.CachePath, "remote-images", filename.Substring(0, 1), filename);
        }
    }
}

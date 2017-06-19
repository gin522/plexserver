﻿using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Model.Services;

namespace MediaBrowser.Api
{
    /// <summary>
    /// Class BaseApiService
    /// </summary>
    public class BaseApiService : IService, IRequiresRequest
    {
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public ILogger Logger
        {
            get
            {
                return ApiEntryPoint.Instance.Logger;
            }
        }

        /// <summary>
        /// Gets or sets the HTTP result factory.
        /// </summary>
        /// <value>The HTTP result factory.</value>
        public IHttpResultFactory ResultFactory
        {
            get
            {
                return ApiEntryPoint.Instance.ResultFactory;
            }
        }

        /// <summary>
        /// Gets or sets the request context.
        /// </summary>
        /// <value>The request context.</value>
        public IRequest Request { get; set; }

        public string GetHeader(string name)
        {
            return Request.Headers[name];
        }

        /// <summary>
        /// To the optimized result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result">The result.</param>
        /// <returns>System.Object.</returns>
        protected object ToOptimizedResult<T>(T result)
            where T : class
        {
            return ResultFactory.GetOptimizedResult(Request, result);
        }

        protected void AssertCanUpdateUser(IAuthorizationContext authContext, IUserManager userManager, string userId)
        {
            var auth = authContext.GetAuthorizationInfo(Request);

            var authenticatedUser = userManager.GetUserById(auth.UserId);

            // If they're going to update the record of another user, they must be an administrator
            if (!string.Equals(userId, auth.UserId, StringComparison.OrdinalIgnoreCase))
            {
                if (!authenticatedUser.Policy.IsAdministrator)
                {
                    throw new SecurityException("Unauthorized access.");
                }
            }
            else
            {
                if (!authenticatedUser.Policy.EnableUserPreferenceAccess)
                {
                    throw new SecurityException("Unauthorized access.");
                }
            }
        }

        /// <summary>
        /// To the optimized serialized result using cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result">The result.</param>
        /// <returns>System.Object.</returns>
        protected object ToOptimizedSerializedResultUsingCache<T>(T result)
           where T : class
        {
            return ToOptimizedResult(result);
        }

        /// <summary>
        /// Gets the session.
        /// </summary>
        /// <returns>SessionInfo.</returns>
        protected async Task<SessionInfo> GetSession(ISessionContext sessionContext)
        {
            var session = await sessionContext.GetSession(Request).ConfigureAwait(false);

            if (session == null)
            {
                throw new ArgumentException("Session not found.");
            }

            return session;
        }

        protected DtoOptions GetDtoOptions(IAuthorizationContext authContext, object request)
        {
            var options = new DtoOptions();

            var authInfo = authContext.GetAuthorizationInfo(Request);

            options.DeviceId = authInfo.DeviceId;

            var hasFields = request as IHasItemFields;
            if (hasFields != null)
            {
                options.Fields = hasFields.GetItemFields().ToList();
            }

            var client = authInfo.Client ?? string.Empty;
            if (client.IndexOf("kodi", StringComparison.OrdinalIgnoreCase) != -1 ||
                client.IndexOf("wmc", StringComparison.OrdinalIgnoreCase) != -1 ||
                client.IndexOf("media center", StringComparison.OrdinalIgnoreCase) != -1 ||
                client.IndexOf("classic", StringComparison.OrdinalIgnoreCase) != -1)
            {
                options.Fields.Add(Model.Querying.ItemFields.RecursiveItemCount);
            }

            if (client.IndexOf("kodi", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("wmc", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("media center", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("classic", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("roku", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("samsung", StringComparison.OrdinalIgnoreCase) != -1 ||
               client.IndexOf("androidtv", StringComparison.OrdinalIgnoreCase) != -1)
            {
                options.Fields.Add(Model.Querying.ItemFields.ChildCount);
            }

            if (client.IndexOf("web", StringComparison.OrdinalIgnoreCase) == -1 &&

                // covers both emby mobile and emby for android mobile
                client.IndexOf("mobile", StringComparison.OrdinalIgnoreCase) == -1 &&
                client.IndexOf("ios", StringComparison.OrdinalIgnoreCase) == -1 &&
                client.IndexOf("theater", StringComparison.OrdinalIgnoreCase) == -1)
            {
                options.Fields.Add(Model.Querying.ItemFields.ChildCount);
            }

            var hasDtoOptions = request as IHasDtoOptions;
            if (hasDtoOptions != null)
            {
                options.EnableImages = hasDtoOptions.EnableImages ?? true;

                if (hasDtoOptions.ImageTypeLimit.HasValue)
                {
                    options.ImageTypeLimit = hasDtoOptions.ImageTypeLimit.Value;
                }
                if (hasDtoOptions.EnableUserData.HasValue)
                {
                    options.EnableUserData = hasDtoOptions.EnableUserData.Value;
                }

                if (!string.IsNullOrWhiteSpace(hasDtoOptions.EnableImageTypes))
                {
                    options.ImageTypes = (hasDtoOptions.EnableImageTypes ?? string.Empty).Split(',').Where(i => !string.IsNullOrWhiteSpace(i)).Select(v => (ImageType)Enum.Parse(typeof(ImageType), v, true)).ToList();
                }
            }

            return options;
        }

        protected MusicArtist GetArtist(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(MusicArtist).Name }

                }).OfType<MusicArtist>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetArtist(name);
        }

        protected Studio GetStudio(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(Studio).Name }

                }).OfType<Studio>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetStudio(name);
        }

        protected Genre GetGenre(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(Genre).Name }

                }).OfType<Genre>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetGenre(name);
        }

        protected MusicGenre GetMusicGenre(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(MusicGenre).Name }

                }).OfType<MusicGenre>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetMusicGenre(name);
        }

        protected GameGenre GetGameGenre(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(GameGenre).Name }

                }).OfType<GameGenre>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetGameGenre(name);
        }

        protected Person GetPerson(string name, ILibraryManager libraryManager)
        {
            if (name.IndexOf(BaseItem.SlugChar) != -1)
            {
                var result = libraryManager.GetItemList(new InternalItemsQuery
                {
                    SlugName = name,
                    IncludeItemTypes = new[] { typeof(Person).Name }

                }).OfType<Person>().FirstOrDefault();

                if (result != null)
                {
                    return result;
                }
            }

            return libraryManager.GetPerson(name);
        }

        protected string GetPathValue(int index)
        {
            var pathInfo = Parse(Request.PathInfo);
            var first = pathInfo[0];

            // backwards compatibility
            if (string.Equals(first, "mediabrowser", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, "emby", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            return pathInfo[index];
        }

        private static List<string> Parse(string pathUri)
        {
            var actionParts = pathUri.Split(new[] { "://" }, StringSplitOptions.None);

            var pathInfo = actionParts[actionParts.Length - 1];

            var optionsPos = pathInfo.LastIndexOf('?');
            if (optionsPos != -1)
            {
                pathInfo = pathInfo.Substring(0, optionsPos);
            }

            var args = pathInfo.Split('/');

            return args.Skip(1).ToList();
        }

        /// <summary>
        /// Gets the name of the item by.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <returns>Task{BaseItem}.</returns>
        protected BaseItem GetItemByName(string name, string type, ILibraryManager libraryManager)
        {
            BaseItem item;

            if (type.IndexOf("Person", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetPerson(name, libraryManager);
            }
            else if (type.IndexOf("Artist", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetArtist(name, libraryManager);
            }
            else if (type.IndexOf("Genre", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetGenre(name, libraryManager);
            }
            else if (type.IndexOf("MusicGenre", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetMusicGenre(name, libraryManager);
            }
            else if (type.IndexOf("GameGenre", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetGameGenre(name, libraryManager);
            }
            else if (type.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = GetStudio(name, libraryManager);
            }
            else if (type.IndexOf("Year", StringComparison.OrdinalIgnoreCase) == 0)
            {
                item = libraryManager.GetYear(int.Parse(name));
            }
            else
            {
                throw new ArgumentException();
            }

            return item;
        }
    }
}

﻿using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Services;

namespace MediaBrowser.Api.UserLibrary
{
    /// <summary>
    /// Class GetYears
    /// </summary>
    [Route("/Years", "GET", Summary = "Gets all years from a given item, folder, or the entire library")]
    public class GetYears : GetItemsByName
    {
    }

    /// <summary>
    /// Class GetYear
    /// </summary>
    [Route("/Years/{Year}", "GET", Summary = "Gets a year")]
    public class GetYear : IReturn<BaseItemDto>
    {
        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        /// <value>The year.</value>
        [ApiMember(Name = "Year", Description = "The year", IsRequired = true, DataType = "int", ParameterType = "path", Verb = "GET")]
        public int Year { get; set; }

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        /// <value>The user id.</value>
        [ApiMember(Name = "UserId", Description = "Optional. Filter by user id, and attach user data", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string UserId { get; set; }
    }

    /// <summary>
    /// Class YearsService
    /// </summary>
    [Authenticated]
    public class YearsService : BaseItemsByNameService<Year>
    {
        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetYear request)
        {
            var result = GetItem(request);

            return ToOptimizedSerializedResultUsingCache(result);
        }

        /// <summary>
        /// Gets the item.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        private BaseItemDto GetItem(GetYear request)
        {
            var item = LibraryManager.GetYear(request.Year);
            
            var dtoOptions = GetDtoOptions(AuthorizationContext, request);

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                var user = UserManager.GetUserById(request.UserId);

                return DtoService.GetBaseItemDto(item, dtoOptions, user);
            }

            return DtoService.GetBaseItemDto(item, dtoOptions);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetYears request)
        {
            var result = GetResult(request);

            return ToOptimizedSerializedResultUsingCache(result);
        }

        /// <summary>
        /// Gets all items.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="items">The items.</param>
        /// <returns>IEnumerable{Tuple{System.StringFunc{System.Int32}}}.</returns>
        protected override IEnumerable<BaseItem> GetAllItems(GetItemsByName request, IEnumerable<BaseItem> items)
        {
            var itemsList = items.Where(i => i.ProductionYear != null).ToList();

            return itemsList
                .Select(i => i.ProductionYear ?? 0)
                .Where(i => i > 0)
                .Distinct()
                .Select(year => LibraryManager.GetYear(year));
        }

        public YearsService(IUserManager userManager, ILibraryManager libraryManager, IUserDataManager userDataRepository, IItemRepository itemRepository, IDtoService dtoService, IAuthorizationContext authorizationContext) : base(userManager, libraryManager, userDataRepository, itemRepository, dtoService, authorizationContext)
        {
        }
    }
}

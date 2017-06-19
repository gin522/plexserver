﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Server.Implementations.LiveTv
{
    public class ProgramImageProvider : IDynamicImageProvider, IHasItemChangeMonitor, IHasOrder
    {
        private readonly ILiveTvManager _liveTvManager;

        public ProgramImageProvider(ILiveTvManager liveTvManager)
        {
            _liveTvManager = liveTvManager;
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            return new[] { ImageType.Primary };
        }

        private string GetItemExternalId(BaseItem item)
        {
            var externalId = item.ExternalId;

            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = item.GetProviderId("ProviderExternalId");
            }

            return externalId;
        }

        public async Task<DynamicImageResponse> GetImage(IHasImages item, ImageType type, CancellationToken cancellationToken)
        {
            var liveTvItem = (LiveTvProgram)item;

            var imageResponse = new DynamicImageResponse();

            var service = _liveTvManager.Services.FirstOrDefault(i => string.Equals(i.Name, liveTvItem.ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service != null)
            {
                try
                {
                    var channel = _liveTvManager.GetInternalChannel(liveTvItem.ChannelId);

                    if (channel != null)
                    {
                        var response = await service.GetProgramImageAsync(GetItemExternalId(liveTvItem), GetItemExternalId(channel), cancellationToken).ConfigureAwait(false);

                        if (response != null)
                        {
                            imageResponse.HasImage = true;
                            imageResponse.Stream = response.Stream;
                            imageResponse.Format = response.Format;
                        }
                    }
                }
                catch (NotImplementedException)
                {
                }
            }

            return imageResponse;
        }

        public string Name
        {
            get { return "Live TV Service Provider"; }
        }

        public bool Supports(IHasImages item)
        {
            return item is LiveTvProgram;
        }

        public int Order
        {
            get
            {
                // Let the better providers run first
                return 100;
            }
        }

        public bool HasChanged(IHasMetadata item, IDirectoryService directoryService)
        {
            var liveTvItem = item as LiveTvProgram;

            if (liveTvItem != null)
            {
                return !liveTvItem.HasImage(ImageType.Primary);
            }
            return false;
        }
    }
}

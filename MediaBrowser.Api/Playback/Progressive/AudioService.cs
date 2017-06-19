﻿using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Services;

namespace MediaBrowser.Api.Playback.Progressive
{
    /// <summary>
    /// Class GetAudioStream
    /// </summary>
    [Route("/Audio/{Id}/stream.{Container}", "GET", Summary = "Gets an audio stream")]
    [Route("/Audio/{Id}/stream", "GET", Summary = "Gets an audio stream")]
    [Route("/Audio/{Id}/stream.{Container}", "HEAD", Summary = "Gets an audio stream")]
    [Route("/Audio/{Id}/stream", "HEAD", Summary = "Gets an audio stream")]
    public class GetAudioStream : StreamRequest
    {
    }

    /// <summary>
    /// Class AudioService
    /// </summary>
    // TODO: In order to autheneticate this in the future, Dlna playback will require updating
    //[Authenticated]
    public class AudioService : BaseProgressiveStreamingService
    {
        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public Task<object> Get(GetAudioStream request)
        {
            return ProcessRequest(request, false);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public Task<object> Head(GetAudioStream request)
        {
            return ProcessRequest(request, true);
        }

        protected override string GetCommandLineArguments(string outputPath, StreamState state, bool isEncoding)
        {
            var encodingOptions = ApiEntryPoint.Instance.GetEncodingOptions();

            var audioTranscodeParams = new List<string>();

            var bitrate = state.OutputAudioBitrate;

            if (bitrate.HasValue)
            {
                audioTranscodeParams.Add("-ab " + bitrate.Value.ToString(UsCulture));
            }

            if (state.OutputAudioChannels.HasValue)
            {
                audioTranscodeParams.Add("-ac " + state.OutputAudioChannels.Value.ToString(UsCulture));
            }

            // opus will fail on 44100
            if (!string.Equals(state.OutputAudioCodec, "opus", global::System.StringComparison.OrdinalIgnoreCase))
            {
                if (state.OutputAudioSampleRate.HasValue)
                {
                    audioTranscodeParams.Add("-ar " + state.OutputAudioSampleRate.Value.ToString(UsCulture));
                }
            }

            const string vn = " -vn";

            var threads = EncodingHelper.GetNumberOfThreads(state, encodingOptions, false);

            var inputModifier = EncodingHelper.GetInputModifier(state, encodingOptions);

            return string.Format("{0} {1} -threads {2}{3} {4} -id3v2_version 3 -write_id3v1 1 -y \"{5}\"",
                inputModifier,
                EncodingHelper.GetInputArgument(state, encodingOptions),
                threads,
                vn,
                string.Join(" ", audioTranscodeParams.ToArray()),
                outputPath).Trim();
        }

        public AudioService(IServerConfigurationManager serverConfig, IUserManager userManager, ILibraryManager libraryManager, IIsoManager isoManager, IMediaEncoder mediaEncoder, IFileSystem fileSystem, IDlnaManager dlnaManager, ISubtitleEncoder subtitleEncoder, IDeviceManager deviceManager, IMediaSourceManager mediaSourceManager, IZipClient zipClient, IJsonSerializer jsonSerializer, IAuthorizationContext authorizationContext, IImageProcessor imageProcessor) : base(serverConfig, userManager, libraryManager, isoManager, mediaEncoder, fileSystem, dlnaManager, subtitleEncoder, deviceManager, mediaSourceManager, zipClient, jsonSerializer, authorizationContext, imageProcessor)
        {
        }
    }
}

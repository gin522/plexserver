using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.MediaEncoding.Probing;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Diagnostics;
using MediaBrowser.Model.System;

namespace MediaBrowser.MediaEncoding.Encoder
{
    /// <summary>
    /// Class MediaEncoder
    /// </summary>
    public class MediaEncoder : IMediaEncoder, IDisposable
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// The _thumbnail resource pool
        /// </summary>
        private readonly SemaphoreSlim _thumbnailResourcePool = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The FF probe resource pool
        /// </summary>
        private readonly SemaphoreSlim _ffProbeResourcePool = new SemaphoreSlim(2, 2);

        public string FFMpegPath { get; private set; }

        public string FFProbePath { get; private set; }

        protected readonly IServerConfigurationManager ConfigurationManager;
        protected readonly IFileSystem FileSystem;
        protected readonly ILiveTvManager LiveTvManager;
        protected readonly IIsoManager IsoManager;
        protected readonly ILibraryManager LibraryManager;
        protected readonly IChannelManager ChannelManager;
        protected readonly ISessionManager SessionManager;
        protected readonly Func<ISubtitleEncoder> SubtitleEncoder;
        protected readonly Func<IMediaSourceManager> MediaSourceManager;
        private readonly IHttpClient _httpClient;
        private readonly IZipClient _zipClient;
        private readonly IProcessFactory _processFactory;
        private readonly IMemoryStreamFactory _memoryStreamProvider;

        private readonly List<ProcessWrapper> _runningProcesses = new List<ProcessWrapper>();
        private readonly bool _hasExternalEncoder;
        private readonly string _originalFFMpegPath;
        private readonly string _originalFFProbePath;
        private readonly int DefaultImageExtractionTimeoutMs;
        private readonly bool EnableEncoderFontFile;

        private readonly IEnvironmentInfo _environmentInfo;

        public MediaEncoder(ILogger logger, IJsonSerializer jsonSerializer, string ffMpegPath, string ffProbePath, bool hasExternalEncoder, IServerConfigurationManager configurationManager, IFileSystem fileSystem, ILiveTvManager liveTvManager, IIsoManager isoManager, ILibraryManager libraryManager, IChannelManager channelManager, ISessionManager sessionManager, Func<ISubtitleEncoder> subtitleEncoder, Func<IMediaSourceManager> mediaSourceManager, IHttpClient httpClient, IZipClient zipClient, IMemoryStreamFactory memoryStreamProvider, IProcessFactory processFactory,
            int defaultImageExtractionTimeoutMs,
            bool enableEncoderFontFile, IEnvironmentInfo environmentInfo)
        {
            if (jsonSerializer == null)
            {
                throw new ArgumentNullException("jsonSerializer");
            }

            _logger = logger;
            _jsonSerializer = jsonSerializer;
            ConfigurationManager = configurationManager;
            FileSystem = fileSystem;
            LiveTvManager = liveTvManager;
            IsoManager = isoManager;
            LibraryManager = libraryManager;
            ChannelManager = channelManager;
            SessionManager = sessionManager;
            SubtitleEncoder = subtitleEncoder;
            MediaSourceManager = mediaSourceManager;
            _httpClient = httpClient;
            _zipClient = zipClient;
            _memoryStreamProvider = memoryStreamProvider;
            _processFactory = processFactory;
            DefaultImageExtractionTimeoutMs = defaultImageExtractionTimeoutMs;
            EnableEncoderFontFile = enableEncoderFontFile;
            _environmentInfo = environmentInfo;
            FFProbePath = ffProbePath;
            FFMpegPath = ffMpegPath;
            _originalFFProbePath = ffProbePath;
            _originalFFMpegPath = ffMpegPath;

            _hasExternalEncoder = hasExternalEncoder;

            SetEnvironmentVariable();
        }

        private readonly object _logLock = new object();
        public void SetLogFilename(string name)
        {
            lock (_logLock)
            {
                try
                {
                    _environmentInfo.SetProcessEnvironmentVariable("FFREPORT", "file=" + name + ":level=32");
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error setting FFREPORT environment variable", ex);
                }
            }
        }

        public void ClearLogFilename()
        {
            lock (_logLock)
            {
                try
                {
                    _environmentInfo.SetProcessEnvironmentVariable("FFREPORT", null);
                }
                catch (Exception ex)
                {
                    //_logger.ErrorException("Error setting FFREPORT environment variable", ex);
                }
            }
        }

        private void SetEnvironmentVariable()
        {
            try
            {
                //_environmentInfo.SetProcessEnvironmentVariable("FFREPORT", "file=program-YYYYMMDD-HHMMSS.txt:level=32");
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error setting FFREPORT environment variable", ex);
            }
            try
            {
                //_environmentInfo.SetUserEnvironmentVariable("FFREPORT", "file=program-YYYYMMDD-HHMMSS.txt:level=32");
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error setting FFREPORT environment variable", ex);
            }
        }

        public string EncoderLocationType
        {
            get
            {
                if (_hasExternalEncoder)
                {
                    return "External";
                }

                if (string.IsNullOrWhiteSpace(FFMpegPath))
                {
                    return null;
                }

                if (IsSystemInstalledPath(FFMpegPath))
                {
                    return "System";
                }

                return "Custom";
            }
        }

        public bool IsDefaultEncoderPath
        {
            get
            {
                var path = FFMpegPath;

                var parentPath = Path.Combine(ConfigurationManager.ApplicationPaths.ProgramDataPath, "ffmpeg", "20160410");

                return FileSystem.ContainsSubPath(parentPath, path);
            }
        }

        private bool IsSystemInstalledPath(string path)
        {
            if (path.IndexOf("/", StringComparison.Ordinal) == -1 && path.IndexOf("\\", StringComparison.Ordinal) == -1)
            {
                return true;
            }

            return false;
        }

        public async Task Init()
        {
            InitPaths();

            if (!string.IsNullOrWhiteSpace(FFMpegPath))
            {
                var result = new EncoderValidator(_logger, _processFactory).Validate(FFMpegPath);

                SetAvailableDecoders(result.Item1);
                SetAvailableEncoders(result.Item2);

                if (EnableEncoderFontFile)
                {
                    var directory = Path.GetDirectoryName(FFMpegPath);

                    if (!string.IsNullOrWhiteSpace(directory) && FileSystem.ContainsSubPath(ConfigurationManager.ApplicationPaths.ProgramDataPath, directory))
                    {
                        await new FontConfigLoader(_httpClient, ConfigurationManager.ApplicationPaths, _logger, _zipClient,
                                FileSystem).DownloadFonts(directory).ConfigureAwait(false);
                    }
                }
            }
        }

        private void InitPaths()
        {
            ConfigureEncoderPaths();

            if (_hasExternalEncoder)
            {
                LogPaths();
                return;
            }

            // If the path was passed in, save it into config now.
            var encodingOptions = GetEncodingOptions();
            var appPath = encodingOptions.EncoderAppPath;

            var valueToSave = FFMpegPath;

            if (!string.IsNullOrWhiteSpace(valueToSave))
            {
                // if using system variable, don't save this.
                if (IsSystemInstalledPath(valueToSave) || _hasExternalEncoder)
                {
                    valueToSave = null;
                }
            }

            if (!string.Equals(valueToSave, appPath, StringComparison.Ordinal))
            {
                encodingOptions.EncoderAppPath = valueToSave;
                ConfigurationManager.SaveConfiguration("encoding", encodingOptions);
            }
        }

        public async Task UpdateEncoderPath(string path, string pathType)
        {
            if (_hasExternalEncoder)
            {
                return;
            }

            _logger.Info("Attempting to update encoder path to {0}. pathType: {1}", path ?? string.Empty, pathType ?? string.Empty);

            Tuple<string, string> newPaths;

            if (string.Equals(pathType, "system", StringComparison.OrdinalIgnoreCase))
            {
                path = "ffmpeg";

                newPaths = TestForInstalledVersions();
            }
            else if (string.Equals(pathType, "custom", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentNullException("path");
                }

                if (!FileSystem.FileExists(path) && !FileSystem.DirectoryExists(path))
                {
                    throw new ResourceNotFoundException();
                }
                newPaths = GetEncoderPaths(path);
            }
            else
            {
                throw new ArgumentException("Unexpected pathType value");
            }

            if (string.IsNullOrWhiteSpace(newPaths.Item1))
            {
                throw new ResourceNotFoundException("ffmpeg not found");
            }
            if (string.IsNullOrWhiteSpace(newPaths.Item2))
            {
                throw new ResourceNotFoundException("ffprobe not found");
            }

            path = newPaths.Item1;

            if (!ValidateVersion(path, true))
            {
                throw new ResourceNotFoundException("ffmpeg version 3.0 or greater is required.");
            }

            var config = GetEncodingOptions();
            config.EncoderAppPath = path;
            ConfigurationManager.SaveConfiguration("encoding", config);

            Init();
        }

        private bool ValidateVersion(string path, bool logOutput)
        {
            return new EncoderValidator(_logger, _processFactory).ValidateVersion(path, logOutput);
        }

        private void ConfigureEncoderPaths()
        {
            if (_hasExternalEncoder)
            {
                return;
            }

            var appPath = GetEncodingOptions().EncoderAppPath;

            if (string.IsNullOrWhiteSpace(appPath))
            {
                appPath = Path.Combine(ConfigurationManager.ApplicationPaths.ProgramDataPath, "ffmpeg");
            }

            var newPaths = GetEncoderPaths(appPath);
            if (string.IsNullOrWhiteSpace(newPaths.Item1) || string.IsNullOrWhiteSpace(newPaths.Item2))
            {
                newPaths = TestForInstalledVersions();
            }

            if (!string.IsNullOrWhiteSpace(newPaths.Item1) && !string.IsNullOrWhiteSpace(newPaths.Item2))
            {
                FFMpegPath = newPaths.Item1;
                FFProbePath = newPaths.Item2;
            }

            LogPaths();
        }

        private Tuple<string, string> GetEncoderPaths(string configuredPath)
        {
            var appPath = configuredPath;

            if (!string.IsNullOrWhiteSpace(appPath))
            {
                if (FileSystem.DirectoryExists(appPath))
                {
                    return GetPathsFromDirectory(appPath);
                }

                if (FileSystem.FileExists(appPath))
                {
                    return new Tuple<string, string>(appPath, GetProbePathFromEncoderPath(appPath));
                }
            }

            return new Tuple<string, string>(null, null);
        }

        private Tuple<string, string> TestForInstalledVersions()
        {
            string encoderPath = null;
            string probePath = null;

            if (_hasExternalEncoder && ValidateVersion(_originalFFMpegPath, true))
            {
                encoderPath = _originalFFMpegPath;
                probePath = _originalFFProbePath;
            }

            if (string.IsNullOrWhiteSpace(encoderPath))
            {
                if (ValidateVersion("ffmpeg", true) && ValidateVersion("ffprobe", false))
                {
                    encoderPath = "ffmpeg";
                    probePath = "ffprobe";
                }
            }

            return new Tuple<string, string>(encoderPath, probePath);
        }

        private Tuple<string, string> GetPathsFromDirectory(string path)
        {
            // Since we can't predict the file extension, first try directly within the folder 
            // If that doesn't pan out, then do a recursive search
            var files = FileSystem.GetFilePaths(path);

            var excludeExtensions = new[] { ".c" };

            var ffmpegPath = files.FirstOrDefault(i => string.Equals(Path.GetFileNameWithoutExtension(i), "ffmpeg", StringComparison.OrdinalIgnoreCase) && !excludeExtensions.Contains(Path.GetExtension(i) ?? string.Empty));
            var ffprobePath = files.FirstOrDefault(i => string.Equals(Path.GetFileNameWithoutExtension(i), "ffprobe", StringComparison.OrdinalIgnoreCase) && !excludeExtensions.Contains(Path.GetExtension(i) ?? string.Empty));

            if (string.IsNullOrWhiteSpace(ffmpegPath) || !FileSystem.FileExists(ffmpegPath))
            {
                files = FileSystem.GetFilePaths(path, true);

                ffmpegPath = files.FirstOrDefault(i => string.Equals(Path.GetFileNameWithoutExtension(i), "ffmpeg", StringComparison.OrdinalIgnoreCase) && !excludeExtensions.Contains(Path.GetExtension(i) ?? string.Empty));

                if (!string.IsNullOrWhiteSpace(ffmpegPath))
                {
                    ffprobePath = GetProbePathFromEncoderPath(ffmpegPath);
                }
            }

            return new Tuple<string, string>(ffmpegPath, ffprobePath);
        }

        private string GetProbePathFromEncoderPath(string appPath)
        {
            return FileSystem.GetFilePaths(Path.GetDirectoryName(appPath))
                .FirstOrDefault(i => string.Equals(Path.GetFileNameWithoutExtension(i), "ffprobe", StringComparison.OrdinalIgnoreCase));
        }

        private void LogPaths()
        {
            _logger.Info("FFMpeg: {0}", FFMpegPath ?? "not found");
            _logger.Info("FFProbe: {0}", FFProbePath ?? "not found");
        }

        private EncodingOptions GetEncodingOptions()
        {
            return ConfigurationManager.GetConfiguration<EncodingOptions>("encoding");
        }

        private List<string> _encoders = new List<string>();
        public void SetAvailableEncoders(List<string> list)
        {
            _encoders = list.ToList();
            //_logger.Info("Supported encoders: {0}", string.Join(",", list.ToArray()));
        }

        private List<string> _decoders = new List<string>();
        public void SetAvailableDecoders(List<string> list)
        {
            _decoders = list.ToList();
            //_logger.Info("Supported decoders: {0}", string.Join(",", list.ToArray()));
        }

        public bool SupportsEncoder(string encoder)
        {
            return _encoders.Contains(encoder, StringComparer.OrdinalIgnoreCase);
        }

        public bool SupportsDecoder(string decoder)
        {
            return _decoders.Contains(decoder, StringComparer.OrdinalIgnoreCase);
        }

        public bool CanEncodeToAudioCodec(string codec)
        {
            if (string.Equals(codec, "opus", StringComparison.OrdinalIgnoreCase))
            {
                codec = "libopus";
            }
            else if (string.Equals(codec, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                codec = "libmp3lame";
            }

            return SupportsEncoder(codec);
        }

        public bool CanEncodeToSubtitleCodec(string codec)
        {
            // TODO
            return true;
        }

        /// <summary>
        /// Gets the encoder path.
        /// </summary>
        /// <value>The encoder path.</value>
        public string EncoderPath
        {
            get { return FFMpegPath; }
        }

        /// <summary>
        /// Gets the media info.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task<MediaInfo> GetMediaInfo(MediaInfoRequest request, CancellationToken cancellationToken)
        {
            var extractChapters = request.MediaType == DlnaProfileType.Video && request.ExtractChapters;

            var inputFiles = MediaEncoderHelpers.GetInputArgument(FileSystem, request.InputPath, request.Protocol, request.MountedIso, request.PlayableStreamFileNames);

            var probeSize = EncodingHelper.GetProbeSizeArgument(inputFiles.Length);
            string analyzeDuration;

            if (request.AnalyzeDurationMs > 0)
            {
                analyzeDuration = "-analyzeduration " +
                                  (request.AnalyzeDurationMs * 1000).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                analyzeDuration = EncodingHelper.GetAnalyzeDurationArgument(inputFiles.Length);
            }

            probeSize = probeSize + " " + analyzeDuration;
            probeSize = probeSize.Trim();

            var forceEnableLogging = request.Protocol != MediaProtocol.File;

            return GetMediaInfoInternal(GetInputArgument(inputFiles, request.Protocol), request.InputPath, request.Protocol, extractChapters,
                probeSize, request.MediaType == DlnaProfileType.Audio, request.VideoType, forceEnableLogging, cancellationToken);
        }

        /// <summary>
        /// Gets the input argument.
        /// </summary>
        /// <param name="inputFiles">The input files.</param>
        /// <param name="protocol">The protocol.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentException">Unrecognized InputType</exception>
        public string GetInputArgument(string[] inputFiles, MediaProtocol protocol)
        {
            return EncodingUtils.GetInputArgument(inputFiles.ToList(), protocol);
        }

        /// <summary>
        /// Gets the media info internal.
        /// </summary>
        /// <returns>Task{MediaInfoResult}.</returns>
        private async Task<MediaInfo> GetMediaInfoInternal(string inputPath,
            string primaryPath,
            MediaProtocol protocol,
            bool extractChapters,
            string probeSizeArgument,
            bool isAudio,
            VideoType videoType,
            bool forceEnableLogging,
            CancellationToken cancellationToken)
        {
            var args = extractChapters
                ? "{0} -i {1} -threads 0 -v info -print_format json -show_streams -show_chapters -show_format"
                : "{0} -i {1} -threads 0 -v info -print_format json -show_streams -show_format";

            var process = _processFactory.Create(new ProcessOptions
            {
                CreateNoWindow = true,
                UseShellExecute = false,

                // Must consume both or ffmpeg may hang due to deadlocks. See comments below.   
                RedirectStandardOutput = true,
                FileName = FFProbePath,
                Arguments = string.Format(args, probeSizeArgument, inputPath).Trim(),

                IsHidden = true,
                ErrorDialog = false,
                EnableRaisingEvents = true
            });

            if (forceEnableLogging)
            {
                _logger.Info("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
            }
            else
            {
                _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
            }

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                await _ffProbeResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    StartProcess(processWrapper);
                }
                catch (Exception ex)
                {
                    _ffProbeResourcePool.Release();

                    _logger.ErrorException("Error starting ffprobe", ex);

                    throw;
                }

                try
                {
                    //process.BeginErrorReadLine();

                    var result = _jsonSerializer.DeserializeFromStream<InternalMediaInfoResult>(process.StandardOutput.BaseStream);

                    if (result == null || (result.streams == null && result.format == null))
                    {
                        throw new Exception("ffprobe failed - streams and format are both null.");
                    }

                    if (result.streams != null)
                    {
                        // Normalize aspect ratio if invalid
                        foreach (var stream in result.streams)
                        {
                            if (string.Equals(stream.display_aspect_ratio, "0:1", StringComparison.OrdinalIgnoreCase))
                            {
                                stream.display_aspect_ratio = string.Empty;
                            }
                            if (string.Equals(stream.sample_aspect_ratio, "0:1", StringComparison.OrdinalIgnoreCase))
                            {
                                stream.sample_aspect_ratio = string.Empty;
                            }
                        }
                    }

                    var mediaInfo = new ProbeResultNormalizer(_logger, FileSystem, _memoryStreamProvider).GetMediaInfo(result, videoType, isAudio, primaryPath, protocol);

                    var videoStream = mediaInfo.MediaStreams.FirstOrDefault(i => i.Type == MediaStreamType.Video);

                    if (videoStream != null && !videoStream.IsInterlaced)
                    {
                        var isInterlaced = DetectInterlaced(mediaInfo, videoStream);

                        if (isInterlaced)
                        {
                            videoStream.IsInterlaced = true;
                        }
                    }

                    return mediaInfo;
                }
                catch
                {
                    StopProcess(processWrapper, 100);

                    throw;
                }
                finally
                {
                    _ffProbeResourcePool.Release();
                }
            }
        }

        private bool DetectInterlaced(MediaSourceInfo video, MediaStream videoStream)
        {
            // If it's mpeg based, assume true
            if ((videoStream.Codec ?? string.Empty).IndexOf("mpeg", StringComparison.OrdinalIgnoreCase) != -1)
            {
                var formats = (video.Container ?? string.Empty).Split(',').ToList();
                return formats.Contains("vob", StringComparer.OrdinalIgnoreCase) ||
                                              formats.Contains("m2ts", StringComparer.OrdinalIgnoreCase) ||
                                              formats.Contains("ts", StringComparer.OrdinalIgnoreCase) ||
                                              formats.Contains("mpegts", StringComparer.OrdinalIgnoreCase) ||
                                              formats.Contains("wtv", StringComparer.OrdinalIgnoreCase);

            }

            return false;
        }

        /// <summary>
        /// The us culture
        /// </summary>
        protected readonly CultureInfo UsCulture = new CultureInfo("en-US");

        public Task<string> ExtractAudioImage(string path, int? imageStreamIndex, CancellationToken cancellationToken)
        {
            return ExtractImage(new[] { path }, null, imageStreamIndex, MediaProtocol.File, true, null, null, cancellationToken);
        }

        public Task<string> ExtractVideoImage(string[] inputFiles, string container, MediaProtocol protocol, Video3DFormat? threedFormat, TimeSpan? offset, CancellationToken cancellationToken)
        {
            return ExtractImage(inputFiles, container, null, protocol, false, threedFormat, offset, cancellationToken);
        }

        public Task<string> ExtractVideoImage(string[] inputFiles, string container, MediaProtocol protocol, int? imageStreamIndex, CancellationToken cancellationToken)
        {
            return ExtractImage(inputFiles, container, imageStreamIndex, protocol, false, null, null, cancellationToken);
        }

        private async Task<string> ExtractImage(string[] inputFiles, string container, int? imageStreamIndex, MediaProtocol protocol, bool isAudio,
            Video3DFormat? threedFormat, TimeSpan? offset, CancellationToken cancellationToken)
        {
            var inputArgument = GetInputArgument(inputFiles, protocol);

            if (isAudio)
            {
                if (imageStreamIndex.HasValue && imageStreamIndex.Value > 0)
                {
                    // It seems for audio files we need to subtract 1 (for the audio stream??)
                    imageStreamIndex = imageStreamIndex.Value - 1;
                }
            }
            else
            {
                try
                {
                    return await ExtractImageInternal(inputArgument, container, imageStreamIndex, protocol, threedFormat, offset, true, cancellationToken).ConfigureAwait(false);
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch
                {
                    _logger.Error("I-frame image extraction failed, will attempt standard way. Input: {0}", inputArgument);
                }
            }

            return await ExtractImageInternal(inputArgument, container, imageStreamIndex, protocol, threedFormat, offset, false, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> ExtractImageInternal(string inputPath, string container, int? imageStreamIndex, MediaProtocol protocol, Video3DFormat? threedFormat, TimeSpan? offset, bool useIFrame, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }

            var tempExtractPath = Path.Combine(ConfigurationManager.ApplicationPaths.TempDirectory, Guid.NewGuid() + ".jpg");
            FileSystem.CreateDirectory(Path.GetDirectoryName(tempExtractPath));

            // apply some filters to thumbnail extracted below (below) crop any black lines that we made and get the correct ar then scale to width 600. 
            // This filter chain may have adverse effects on recorded tv thumbnails if ar changes during presentation ex. commercials @ diff ar
            var vf = "scale=600:trunc(600/dar/2)*2";

            if (threedFormat.HasValue)
            {
                switch (threedFormat.Value)
                {
                    case Video3DFormat.HalfSideBySide:
                        vf = "crop=iw/2:ih:0:0,scale=(iw*2):ih,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        // hsbs crop width in half,scale to correct size, set the display aspect,crop out any black bars we may have made the scale width to 600. Work out the correct height based on the display aspect it will maintain the aspect where -1 in this case (3d) may not.
                        break;
                    case Video3DFormat.FullSideBySide:
                        vf = "crop=iw/2:ih:0:0,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        //fsbs crop width in half,set the display aspect,crop out any black bars we may have made the scale width to 600.
                        break;
                    case Video3DFormat.HalfTopAndBottom:
                        vf = "crop=iw:ih/2:0:0,scale=(iw*2):ih),setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        //htab crop heigh in half,scale to correct size, set the display aspect,crop out any black bars we may have made the scale width to 600
                        break;
                    case Video3DFormat.FullTopAndBottom:
                        vf = "crop=iw:ih/2:0:0,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        // ftab crop heigt in half, set the display aspect,crop out any black bars we may have made the scale width to 600
                        break;
                    default:
                        break;
                }
            }

            var mapArg = imageStreamIndex.HasValue ? (" -map 0:v:" + imageStreamIndex.Value.ToString(CultureInfo.InvariantCulture)) : string.Empty;

            var enableThumbnail = !new List<string> { "wtv" }.Contains(container ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var thumbnail = enableThumbnail ? ",thumbnail=24" : string.Empty;

            // Use ffmpeg to sample 100 (we can drop this if required using thumbnail=50 for 50 frames) frames and pick the best thumbnail. Have a fall back just in case.
            var args = useIFrame ? string.Format("-i {0}{3} -threads 0 -v quiet -vframes 1 -vf \"{2}{4}\" -f image2 \"{1}\"", inputPath, tempExtractPath, vf, mapArg, thumbnail) :
                string.Format("-i {0}{3} -threads 0 -v quiet -vframes 1 -vf \"{2}\" -f image2 \"{1}\"", inputPath, tempExtractPath, vf, mapArg);

            var probeSizeArgument = EncodingHelper.GetProbeSizeArgument(1);
            var analyzeDurationArgument = EncodingHelper.GetAnalyzeDurationArgument(1);

            if (!string.IsNullOrWhiteSpace(probeSizeArgument))
            {
                args = probeSizeArgument + " " + args;
            }

            if (!string.IsNullOrWhiteSpace(analyzeDurationArgument))
            {
                args = analyzeDurationArgument + " " + args;
            }

            if (offset.HasValue)
            {
                args = string.Format("-ss {0} ", GetTimeParameter(offset.Value)) + args;
            }

            var process = _processFactory.Create(new ProcessOptions
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = FFMpegPath,
                Arguments = args,
                IsHidden = true,
                ErrorDialog = false
            });

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                bool ranToCompletion;

                StartProcess(processWrapper);

                var timeoutMs = ConfigurationManager.Configuration.ImageExtractionTimeoutMs;
                if (timeoutMs <= 0)
                {
                    timeoutMs = DefaultImageExtractionTimeoutMs;
                }

                ranToCompletion = process.WaitForExit(timeoutMs);

                if (!ranToCompletion)
                {
                    StopProcess(processWrapper, 1000);
                }

                var exitCode = ranToCompletion ? processWrapper.ExitCode ?? 0 : -1;
                var file = FileSystem.GetFileInfo(tempExtractPath);

                if (exitCode == -1 || !file.Exists || file.Length == 0)
                {
                    var msg = string.Format("ffmpeg image extraction failed for {0}", inputPath);

                    _logger.Error(msg);

                    throw new Exception(msg);
                }

                return tempExtractPath;
            }
        }

        public string GetTimeParameter(long ticks)
        {
            var time = TimeSpan.FromTicks(ticks);

            return GetTimeParameter(time);
        }

        public string GetTimeParameter(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss\.fff", UsCulture);
        }

        public async Task ExtractVideoImagesOnInterval(string[] inputFiles,
            MediaProtocol protocol,
            Video3DFormat? threedFormat,
            TimeSpan interval,
            string targetDirectory,
            string filenamePrefix,
            int? maxWidth,
            CancellationToken cancellationToken)
        {
            var resourcePool = _thumbnailResourcePool;

            var inputArgument = GetInputArgument(inputFiles, protocol);

            var vf = "fps=fps=1/" + interval.TotalSeconds.ToString(UsCulture);

            if (maxWidth.HasValue)
            {
                var maxWidthParam = maxWidth.Value.ToString(UsCulture);

                vf += string.Format(",scale=min(iw\\,{0}):trunc(ow/dar/2)*2", maxWidthParam);
            }

            FileSystem.CreateDirectory(targetDirectory);
            var outputPath = Path.Combine(targetDirectory, filenamePrefix + "%05d.jpg");

            var args = string.Format("-i {0} -threads 0 -v quiet -vf \"{2}\" -f image2 \"{1}\"", inputArgument, outputPath, vf);

            var probeSizeArgument = EncodingHelper.GetProbeSizeArgument(1);
            var analyzeDurationArgument = EncodingHelper.GetAnalyzeDurationArgument(1);

            if (!string.IsNullOrWhiteSpace(probeSizeArgument))
            {
                args = probeSizeArgument + " " + args;
            }

            if (!string.IsNullOrWhiteSpace(analyzeDurationArgument))
            {
                args = analyzeDurationArgument + " " + args;
            }

            var process = _processFactory.Create(new ProcessOptions
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = FFMpegPath,
                Arguments = args,
                IsHidden = true,
                ErrorDialog = false
            });

            _logger.Info(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

            await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool ranToCompletion = false;

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                try
                {
                    StartProcess(processWrapper);

                    // Need to give ffmpeg enough time to make all the thumbnails, which could be a while,
                    // but we still need to detect if the process hangs.
                    // Making the assumption that as long as new jpegs are showing up, everything is good.

                    bool isResponsive = true;
                    int lastCount = 0;

                    while (isResponsive)
                    {
                        if (process.WaitForExit(30000))
                        {
                            ranToCompletion = true;
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var jpegCount = FileSystem.GetFilePaths(targetDirectory)
                            .Count(i => string.Equals(Path.GetExtension(i), ".jpg", StringComparison.OrdinalIgnoreCase));

                        isResponsive = (jpegCount > lastCount);
                        lastCount = jpegCount;
                    }

                    if (!ranToCompletion)
                    {
                        StopProcess(processWrapper, 1000);
                    }
                }
                finally
                {
                    resourcePool.Release();
                }

                var exitCode = ranToCompletion ? processWrapper.ExitCode ?? 0 : -1;

                if (exitCode == -1)
                {
                    var msg = string.Format("ffmpeg image extraction failed for {0}", inputArgument);

                    _logger.Error(msg);

                    throw new Exception(msg);
                }
            }
        }

        public async Task<string> EncodeAudio(EncodingJobOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var job = await new AudioEncoder(this,
                _logger,
                ConfigurationManager,
                FileSystem,
                IsoManager,
                LibraryManager,
                SessionManager,
                SubtitleEncoder(),
                MediaSourceManager(),
                _processFactory)
                .Start(options, progress, cancellationToken).ConfigureAwait(false);

            await job.TaskCompletionSource.Task.ConfigureAwait(false);

            return job.OutputFilePath;
        }

        public async Task<string> EncodeVideo(EncodingJobOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var job = await new VideoEncoder(this,
                _logger,
                ConfigurationManager,
                FileSystem,
                IsoManager,
                LibraryManager,
                SessionManager,
                SubtitleEncoder(),
                MediaSourceManager(),
                _processFactory)
                .Start(options, progress, cancellationToken).ConfigureAwait(false);

            await job.TaskCompletionSource.Task.ConfigureAwait(false);

            return job.OutputFilePath;
        }

        private void StartProcess(ProcessWrapper process)
        {
            process.Process.Start();

            lock (_runningProcesses)
            {
                _runningProcesses.Add(process);
            }
        }
        private void StopProcess(ProcessWrapper process, int waitTimeMs)
        {
            try
            {
                if (process.Process.WaitForExit(waitTimeMs))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error in WaitForExit", ex);
            }

            try
            {
                _logger.Info("Killing ffmpeg process");

                process.Process.Kill();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error killing process", ex);
            }
        }

        private void StopProcesses()
        {
            List<ProcessWrapper> proceses;
            lock (_runningProcesses)
            {
                proceses = _runningProcesses.ToList();
                _runningProcesses.Clear();
            }

            foreach (var process in proceses)
            {
                if (!process.HasExited)
                {
                    StopProcess(process, 500);
                }
            }
        }

        public string EscapeSubtitleFilterPath(string path)
        {
            // https://ffmpeg.org/ffmpeg-filters.html#Notes-on-filtergraph-escaping
            // We need to double escape

            return path.Replace('\\', '/').Replace(":", "\\:").Replace("'", "'\\\\\\''");
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                StopProcesses();
            }
        }

        private class ProcessWrapper : IDisposable
        {
            public readonly IProcess Process;
            public bool HasExited;
            public int? ExitCode;
            private readonly MediaEncoder _mediaEncoder;
            private readonly ILogger _logger;

            public ProcessWrapper(IProcess process, MediaEncoder mediaEncoder, ILogger logger)
            {
                Process = process;
                _mediaEncoder = mediaEncoder;
                _logger = logger;
                Process.Exited += Process_Exited;
            }

            void Process_Exited(object sender, EventArgs e)
            {
                var process = (IProcess)sender;

                HasExited = true;

                try
                {
                    ExitCode = process.ExitCode;
                }
                catch
                {
                }

                DisposeProcess(process);
            }

            private void DisposeProcess(IProcess process)
            {
                lock (_mediaEncoder._runningProcesses)
                {
                    _mediaEncoder._runningProcesses.Remove(this);
                }

                try
                {
                    process.Dispose();
                }
                catch
                {
                }
            }

            private bool _disposed;
            private readonly object _syncLock = new object();
            public void Dispose()
            {
                lock (_syncLock)
                {
                    if (!_disposed)
                    {
                        if (Process != null)
                        {
                            Process.Exited -= Process_Exited;
                            DisposeProcess(Process);
                        }
                    }

                    _disposed = true;
                }
            }
        }
    }
}
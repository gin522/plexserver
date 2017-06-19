﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace Emby.Server.Implementations.LiveTv.TunerHosts
{
    public class QueueStream
    {
        private readonly Stream _outputStream;
        private readonly ConcurrentQueue<Tuple<byte[], int, int>> _queue = new ConcurrentQueue<Tuple<byte[], int, int>>();
        private CancellationToken _cancellationToken;
        public TaskCompletionSource<bool> TaskCompletion { get; private set; }

        public Action<QueueStream> OnFinished { get; set; }
        private readonly ILogger _logger;
        public Guid Id = Guid.NewGuid();

        public QueueStream(Stream outputStream, ILogger logger)
        {
            _outputStream = outputStream;
            _logger = logger;
            TaskCompletion = new TaskCompletionSource<bool>();
        }

        public void Queue(byte[] bytes, int offset, int count)
        {
            _queue.Enqueue(new Tuple<byte[], int, int>(bytes, offset, count));
        }

        public void Start(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            Task.Run(() => StartInternal());
        }

        private Tuple<byte[], int, int> Dequeue()
        {
            Tuple<byte[], int, int> result;
            if (_queue.TryDequeue(out result))
            {
                return result;
            }

            return null;
        }

        private void OnClosed()
        {
            GC.Collect();
            if (OnFinished != null)
            {
                OnFinished(this);
            }
        }

        public async Task WriteAsync(byte[] bytes, int offset, int count)
        {
            //return _outputStream.WriteAsync(bytes, offset, count, cancellationToken);
            var cancellationToken = _cancellationToken;

            try
            {
                await _outputStream.WriteAsync(bytes, offset, count, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("QueueStream cancelled");
                TaskCompletion.TrySetCanceled();
                OnClosed();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error in QueueStream", ex);
                TaskCompletion.TrySetException(ex);
                OnClosed();
            }
        }

        private async Task StartInternal()
        {
            var cancellationToken = _cancellationToken;

            try
            {
                while (true)
                {
                    var result = Dequeue();
                    if (result != null)
                    {
                        await _outputStream.WriteAsync(result.Item1, result.Item2, result.Item3, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("QueueStream cancelled");
                TaskCompletion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error in QueueStream", ex);
                TaskCompletion.TrySetException(ex);
            }
            finally
            {
                OnClosed();
            }
        }
    }
}

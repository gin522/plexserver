﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Model.Net
{
    public interface IAcceptSocket : IDisposable
    {
        bool DualMode { get; }
        IpEndPointInfo LocalEndPoint { get; }
        IpEndPointInfo RemoteEndPoint { get; }
        void Close();
        void Shutdown(bool both);
        void Listen(int backlog);
        void Bind(IpEndPointInfo endpoint);
        void Connect(IpEndPointInfo endPoint);
        void StartAccept(Action<IAcceptSocket> onAccept, Func<bool> isClosed);
        Task SendFile(string path, byte[] preBuffer, byte[] postBuffer, CancellationToken cancellationToken);
    }

    public class SocketCreateException : Exception
    {
        public SocketCreateException(string errorCode, Exception originalException)
            : base(errorCode, originalException)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; private set; }
    }
}

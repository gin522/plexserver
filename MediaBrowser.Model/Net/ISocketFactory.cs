﻿
namespace MediaBrowser.Model.Net
{
    /// <summary>
    /// Implemented by components that can create a platform specific UDP socket implementation, and wrap it in the cross platform <see cref="ISocket"/> interface.
    /// </summary>
    public interface ISocketFactory
	{

		/// <summary>
		/// Createa a new unicast socket using the specified local port number.
		/// </summary>
		/// <param name="localPort">The local port to bind to.</param>
		/// <returns>A <see cref="ISocket"/> implementation.</returns>
		ISocket CreateUdpSocket(int localPort);

        ISocket CreateUdpBroadcastSocket(int localPort);

        ISocket CreateTcpSocket(IpAddressInfo remoteAddress, int remotePort);

        /// <summary>
        /// Createa a new unicast socket using the specified local port number.
        /// </summary>
        ISocket CreateSsdpUdpSocket(IpAddressInfo localIp, int localPort);

        /// <summary>
        /// Createa a new multicast socket using the specified multicast IP address, multicast time to live and local port.
        /// </summary>
        /// <param name="ipAddress">The multicast IP address to bind to.</param>
        /// <param name="multicastTimeToLive">The multicast time to live value. Actually a maximum number of network hops for UDP packets.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <returns>A <see cref="ISocket"/> implementation.</returns>
        ISocket CreateUdpMulticastSocket(string ipAddress, int multicastTimeToLive, int localPort);

        IAcceptSocket CreateSocket(IpAddressFamily family, SocketType socketType, ProtocolType protocolType, bool dualMode);
    }

    public enum SocketType
    {
        Stream
    }

    public enum ProtocolType
    {
        Tcp
    }
}

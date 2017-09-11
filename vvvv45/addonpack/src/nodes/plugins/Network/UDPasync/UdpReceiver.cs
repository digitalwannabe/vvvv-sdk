using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace MrVux.Network
{
    public class UdpServer : AbstractDataReceiver
    {
        public UdpServer(int port) : base(port) { }

        public UdpServer(int port, int buffersize) : base(port, buffersize) { }

        protected override void CreateSocket()
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.socket.Bind(this.endpoint);
        }
    }
}

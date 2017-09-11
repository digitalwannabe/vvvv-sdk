using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using FeralTic.DX11.Resources;
using FeralTic.DX11;
using System.Net;
using System.Net.Sockets;

namespace VVVV.DX11.Nodes.Texture
{
    public delegate void TextureReceivedDelegate(DX11Texture2D texture);

    public class ReceiverThread
    {
        private object m_lock = new object();

        private Thread thr;
        private bool running = false;

        private DX11RenderContext context;

        private int buffersize =1000000;
        private IPEndPoint endpoint;
        private Socket socket;

        public event TextureReceivedDelegate TextureReceived;

        private byte[] b;

        public void Start(DX11RenderContext context, int port)
        {
            b = new byte[this.buffersize];
            this.endpoint = new IPEndPoint(IPAddress.Any, port);
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.socket.Bind(this.endpoint);

            this.context = context;
            if (!this.running)
            {
                this.running = true;
                this.thr = new Thread(new ThreadStart(this.Run));
                this.thr.Start();
            }
        }

        public void Stop()
        {
            try
            {
                this.thr.Abort();
            }
            catch
            {

            }
        }

        private void Run()
        {
            while (this.running)
            {
                
                if (this.socket.Available > 0)
                {
                    // Shouldn't raise any error as data is available and socket should not be closed
                    int rec = this.socket.Receive(b);

                    byte[] crop = new byte[rec];

                    Array.Copy(b, 0, crop, 0, rec);

                    DX11Texture2D t = DX11Texture2D.FromMemory(context, crop);

                    if (this.TextureReceived != null)
                    {
                        this.TextureReceived(t);
                    }
                }


                // Sleep the receiving thread to give us a chance to update the isrunning
                Thread.Sleep(2);
            }
        }
    }
}

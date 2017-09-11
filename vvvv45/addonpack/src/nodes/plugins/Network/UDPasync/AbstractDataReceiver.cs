using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace MrVux.Network
{
    /// <summary>
    /// Delegate raised when we receive data
    /// </summary>
    /// <param name="data">Data in binary format</param>
    public delegate void DataReceivedDelegate(byte[] data);

    /// <summary>
    /// Base class for data Receiving
    /// </summary>
    public abstract class AbstractDataReceiver
    {
        protected Socket socket;
        protected IPEndPoint endpoint;
        private bool isrunning;
        private int buffersize;

        private Thread thr;
        private object m_lock = new object();

        public event DataReceivedDelegate DataReceived;

        protected abstract void CreateSocket();

        byte[] b;
        
        public AbstractDataReceiver(int port) : this(port, 8192) { }

        public AbstractDataReceiver(int port, int buffersize)
        {
            this.buffersize = buffersize;
            this.endpoint = new IPEndPoint(IPAddress.Any, port);
            b = new byte[this.buffersize];
        }

        public void Start()
        {
            if (!this.isrunning)
            {
                this.CreateSocket();
                thr = new Thread(new ThreadStart(this.Run));
                this.isrunning = true;
                thr.Start();
            }
        }

        public void Stop()
        {
            this.isrunning = false;
            try
            {
                // Block until we've finished sending/receiving current packet
                this.socket.Shutdown(SocketShutdown.Both);
                // Close down the socket
                this.socket.Close();
            }
            catch
            {
            }
        }

        private void Run()
        {
            while (isrunning)
            {
                

                int rec = 0;
                if (this.socket.Available > 0)
                {
                    try
                    {
                        // Shouldn't raise any error as data is available and socket should not be closed
                        rec = this.socket.Receive(b);
                        byte[] crop = new byte[rec];
                        Array.Copy(b, 0, crop, 0, rec);
                        if (this.DataReceived != null)
                        {
                            this.DataReceived(crop);
                        }
                    }
                    catch
                    {

                    }
                }

                // Sleep the receiving thread to give us a chance to update the isrunning
                Thread.Sleep(1);
            }
        }
    }
}

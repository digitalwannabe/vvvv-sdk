using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel.Composition;
using VVVV.PluginInterfaces.V2;
using MrVux.Network;

namespace VVVV.DX11.Tamschick.Network
{
    [PluginInfo(Name = "UdpReceiver", Category = "Network", Version = "", Author = "vux", Tags = "", AutoEvaluate = true)]
    public class UpdReceiverNode : IPluginEvaluate, IDisposable, IPartImportsSatisfiedNotification
    {
        [Input("Port", IsSingle = true, DefaultValue= 4444)]
        protected IDiffSpread<int> port;

        [Input("Enabled", IsSingle=true)]
        protected IDiffSpread<bool> enabled;

        [Output("Server",IsSingle=true)]
        protected ISpread<UdpServer> server;

        [Output("Output", IsSingle = true)]
        protected ISpread<Stream> FStreamOut;

        private UdpServer udp;
        private byte[] message;

        public void OnImportsSatisfied()
        {
            //start with an empty stream output
            FStreamOut.SliceCount = 0;
        }

        public void Evaluate(int SpreadMax)
        {
            FStreamOut.ResizeAndDispose(SpreadMax, () => new MemoryComStream());
            var outputStream = FStreamOut[0];

            bool reset = false;
            if (this.port.IsChanged)
            {
                this.KillServer();
                reset = true;
            }

            if (this.enabled.IsChanged || reset)
            {
                this.KillServer();

                if (this.enabled[0])
                {
                    this.udp = new UdpServer(this.port[0]);
                    this.udp.Start();
                    this.udp.DataReceived += Udp_DataReceived;
                    outputStream.Write(message, 0, message.Length);
                    
                }
            }
            this.server[0] = udp;

            
                    
        }

        private void Udp_DataReceived(byte[] data)
        {
            message = data;
        }

        private void KillServer()
        {
            if (udp != null)
            {
                udp.Stop();
                udp = null;
            }
        }

        public void Dispose()
        {
            this.KillServer();
        }
    }
}

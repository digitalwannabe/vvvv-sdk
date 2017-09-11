using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V2;

using System.Net.Sockets;
using System.Net;

namespace VVVV.Nodes.File
{
    [PluginInfo(
        Name = "UDP Receive",
        Category = "Network",
        Version = "Async",
        Tags = "boom",
        Author = "digitalWannabe",
        Help = "Receive async udp")]
    public class AsyncUDPReceiveNode : IPluginEvaluate, IDisposable, IPartImportsSatisfiedNotification
    {
        [Input("Port", IsSingle =true)]
        public IDiffSpread<int> Port;
        [Input("Listen", IsBang = true, IsSingle = true)]
        public ISpread<bool> DoListen;
//        [Output("Output")]
//        public ISpread<string> Output;
//        [Output("Output2")]
//        public ISpread<string> Output2;
//        [Output("Output Raw", IsSingle = true)]
//        public ISpread<Stream> OutputRaw;
//        [Output("Output Raw Oldest", IsSingle = true)]
 //       public ISpread<Stream> OutputRawOldest;
        [Output("Output Raw Spread")]
        public ISpread<Stream> OutputRawSpread;

        [Output("Count", IsSingle = true)]
        public ISpread<int> OutputDebug;
        [Import]
        public ILogger Logger;


        private Task Receiver;
//        private string Receivestring;
        private byte[] resultarray;
        UdpClient udp;
        private List<string> stringBuffer;
        private Stack<byte[]> byteStack;

        public async Task ReceiveMessage(int port)
        {

            var tcs = new TaskCompletionSource<bool>();
            using (udp = new UdpClient(port))
            {
                while (true)
                {                    
                    var receivedResult = await udp.ReceiveAsync();
                    resultarray = receivedResult.Buffer;
                    byteStack.Push(resultarray);               
                }
                
            }

        }

        public void Evaluate(int spreadMax)
        {
            //           stringBuffer = new List<string>();
            //            OutputRaw.SliceCount = 1;
            //            Output2.SliceCount = 1;

            OutputRawSpread.SliceCount = 1;

            var port = Port[0];
            var doListen = DoListen[0];
            if (doListen)
            {
                if (Receiver == null || Receiver.IsCompleted || Receiver.IsFaulted)
                {
                    Receiver = ReceiveMessage(port);
 //                   Receiver.Start();
                }

                if (Port.IsChanged)
                {
                    udp.Close();
//                    Receiver.Dispose();
                    Receiver = ReceiveMessage(port);
//                    Receiver.Start();
                }
                
            }

            if (!doListen)
            {
                if(Receiver != null )
                {
                    udp.Close();
                }

                if (Receiver.IsCompleted || Receiver.IsFaulted)
                {
                    Receiver.Dispose();
                }


           }
            
//            OutputRaw[0] = new MemoryComStream(resultarray);

            int byteArrayCount =  byteStack.Count;
            OutputDebug[0] = byteArrayCount;
            

           
            byteStack.Reverse();
            Stack<byte[]> buf2 = byteStack;


            if (byteArrayCount > 0)
            {
                //               Output.SliceCount = stringCount;
                OutputRawSpread.SliceCount = byteArrayCount;
                for (int i = 0; i < byteArrayCount; i++)
                {
                    OutputRawSpread[i] = new MemoryComStream(byteStack.ElementAt(i));
                }
                OutputRawSpread.Reverse();
/*                OutputRawOldest[0] = new MemoryComStream(byteStack.Last());
                if (byteArrayCount > 1)
                {
                    buf2.Pop();
                }
                */

            }/*
            else
            {
                if (buf2.Count > 0)
                {
//                    OutputRawSpread.SliceCount = 1;
//                    OutputRawSpread[0] = new MemoryComStream(byteStack.Last());
                    OutputRawOldest[0] = new MemoryComStream(buf2.Last());
/*                    int outCount = buf2.Count;
                    Output.SliceCount = outCount;
                    for (int i =0; i < outCount; i++)
                    {
                        Output[i] = buf2.Pop();
                    }*/
//                }
                



 //           }
           
            stringBuffer.Clear();
            byteStack.Clear();
        }

        public void Dispose()
        {

            udp.Close();
            udp.Dispose();
            Receiver.Dispose();
        }




        public void OnImportsSatisfied()
        {
 //           Output.SliceCount = 1;
//            OutputRaw.SliceCount = 1;
            OutputRawSpread.SliceCount = 1;
//            OutputRawOldest.SliceCount = 1;
            OutputDebug.SliceCount = 1;
            stringBuffer = new List<string>();
            byteStack = new Stack<byte[]>();

//            resultarray.
//            Receiver = ReceiveMessage(Port[0]);
        }
    }
}
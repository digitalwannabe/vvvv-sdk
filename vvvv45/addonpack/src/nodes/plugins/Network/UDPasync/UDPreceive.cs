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
    public sealed class DisposableScope : IDisposable
    {
        private readonly Action _closeScopeAction;
        public DisposableScope(Action closeScopeAction)
        {
            _closeScopeAction = closeScopeAction;
        }
        public void Dispose()
        {
            _closeScopeAction();
        }
    }


    public static class extensionMethod
    {
        public static IDisposable CreateTimeoutScope(this IDisposable disposable)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationTokenRegistration = cancellationTokenSource.Token.Register(disposable.Dispose);
            return new DisposableScope(
                () =>
                {
                    cancellationTokenRegistration.Dispose();
                    cancellationTokenSource.Dispose();
                    disposable.Dispose();
                });
        }

        public static IDisposable CreateTimeoutScope(this IDisposable disposable, int ms)
        {
            var cancellationTokenSource = new CancellationTokenSource(ms);
            var cancellationTokenRegistration = cancellationTokenSource.Token.Register(disposable.Dispose);
            return new DisposableScope(
                () =>
                {
                    cancellationTokenRegistration.Dispose();
                    cancellationTokenSource.Dispose();
                    disposable.Dispose();
                });
        }

    }




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
        [Output("Output Raw Newest", IsSingle = true)]
        public ISpread<Stream> OutputRawNewest;
        [Output("Output Raw Oldest", IsSingle = true)]
        public ISpread<Stream> OutputRawOldest;
        [Output("Output Raw Buffered", IsSingle = true)]
        public ISpread<Stream> OutputRawBuffered;
        [Output("Output Raw Spread")]
        public ISpread<Stream> OutputRawSpread;

        [Output("Count", IsSingle = true)]
        public ISpread<int> OutputDebug;
        [Import]
        public ILogger Logger;


        private Task Receiver;
        private byte[] resultarray;
        private Stack<byte[]> byteStack;
        private Stack<byte[]> byteStackSwap;
        int counter;



        public async Task ReceiveMessage(int port)
        {

            var udp = new UdpClient(port);
            using (udp.CreateTimeoutScope(100))
            {

                while (true)
                {
                    try {
                        var receivedResult = await udp.ReceiveAsync();                                               
                        resultarray = receivedResult.Buffer;
                        byteStack.Push(resultarray);
                    }
                    catch (ObjectDisposedException e)
                    {
//                        Logger.Log(LogType.Message,e.ToString());
                        return;
                    }
                    catch (Exception e)
                    {
                        //                       Logger.Log(LogType.Error, e.ToString());
                        return;
                    }
                                   
                }
                
            }

        }

        public void Evaluate(int spreadMax)
        {
            var port = Port[0];
            var doListen = DoListen[0];
            
            if (doListen)
            {
                if (Receiver == null || Receiver.IsCompleted || Receiver.IsFaulted)
                {
                    Receiver = ReceiveMessage(port);
                }


                if (Port.IsChanged)
                {
                    Receiver.CreateTimeoutScope();
                    Receiver = ReceiveMessage(port);
                }

                Stack<byte[]> stackNow = byteStack;

                int byteArrayCount = stackNow.Count;
                OutputDebug[0] = byteStackSwap.Count;
                stackNow.Reverse();
                


                if (byteArrayCount > 0)
                {

                    OutputRawNewest[0] = new MemoryComStream(stackNow.First());
                    
                    OutputRawOldest[0] = new MemoryComStream(stackNow.Last());
                    OutputRawBuffered[0] = new MemoryComStream(stackNow.Last());
                    OutputRawSpread.SliceCount = byteArrayCount;
                    for (int i = 0; i < byteArrayCount; i++)
                    {
                        byteStackSwap.Push(stackNow.ElementAt(i));
                        OutputRawSpread[i] = new MemoryComStream(stackNow.ElementAt(i));
                        
                    }
                    OutputRawSpread.Reverse();
                }

                else
                {
                    //
                    if (byteStackSwap.Count > 0)
                    {
                        //                        Logger.Log(LogType.Message, "i was here2");
                        byteStackSwap.Reverse();
                        byteStackSwap.Pop();
                        
                        OutputRawOldest[0] = new MemoryComStream(byteStackSwap.Last());

                    }
                        byteStackSwap.Clear();
                    
                    
                }
                byteStack.Clear();

            }
            else //if !doListen
            {
                if(Receiver == null)//this is only in case of init
                {
                    Receiver = ReceiveMessage(port);
                }
                if (Receiver != null)
                {
                    Receiver.CreateTimeoutScope();
                }
                if (Receiver.IsCompleted || Receiver.IsFaulted)
                {
                    Receiver.Dispose();
                }
            }

            
        }

        public void Dispose()
        {
            Receiver.CreateTimeoutScope();

            if (Receiver.IsCompleted || Receiver.IsFaulted)
            {
                Receiver.Dispose();
            }
        }


        public void OnImportsSatisfied()
        {
            OutputRawSpread.SliceCount = 1;
            OutputRawNewest.SliceCount = 1;
            OutputRawBuffered.SliceCount = 1;
            OutputDebug.SliceCount = 1;
            byteStack = new Stack<byte[]>();
            byteStackSwap = new Stack<byte[]>();
            counter = 0;
        }
    }
}
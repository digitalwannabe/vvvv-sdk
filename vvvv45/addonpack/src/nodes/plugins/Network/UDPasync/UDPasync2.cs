#region usings
using System;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;


using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.EX9;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

//using SlimDX;
//using SlimDX.Direct3D11;
using SlimDX.Direct3D9;
//using FeralTic.DX11;
//using FeralTic.DX11.Resources;

//using VVVV.DX11.Core;

using Mpir.NET;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{


    #region PluginInfo
    [PluginInfo(Name = "UDP Receiver", Category = "Network", Version = "Async", Help = "async udp receiver node")]
    #endregion PluginInfo
    public class UDPlisten : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
    {
        #region fields & pins

        [Input("Remote Port")]
        public IDiffSpread<int> FRemotePort;

        [Input("Cancel", IsBang = true, IsSingle = false)]
        public ISpread<bool> FCancel;

        [Input("Listen", IsSingle = true)]
        public ISpread<bool> FDoItIn;



        [Output("Success")]
        public ISpread<bool> FReadyOut;

        [Output("Output")]
        public ISpread<string> FStringOut;

        [Import()]
        public ILogger FLogger;

        #endregion fields & pins

        private readonly Spread<Task> FTasks = new Spread<Task>();
        private CancellationTokenSource FCts;
        UdpClient udpClient;
        bool completed;
        string returnData;
        int spMax;

        // Called when this plugin was created
        public void OnImportsSatisfied()
        {
            // Do any initialization logic here. In this example we won't need to
            // do anything special.

           udpClient = new UdpClient(4444);
           completed = true;


        }

        // Called when this plugin gets deleted
        public void Dispose()
        {
            // Should this plugin get deleted by the user or should vvvv shutdown
            // we need to wait until all still running tasks ran to a completion
            // state.
            CancelRunningTasks();
        }


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            
 //           if (!udpClient.Client.Connected) udpClient = new UdpClient(FRemotePort[0]);

            if (FCancel[0])
            {
                CancelRunningTasks();
            }

            FStringOut.SliceCount = SpreadMax;
            spMax = SpreadMax;

            if (FRemotePort.IsChanged)
            {
                udpClient.Close();
                udpClient = new UdpClient(FRemotePort[0]);
//                CancelRunningTasks();
            }


            if (FDoItIn[0])
            {
//                completed = false;
 //               udpClient.Close();
 //               udpClient = new UdpClient(FRemotePort[0]);


                // Let's first cancel all running tasks (if any).
//                CancelRunningTasks();
                // Create a new task cancellation source object.
 //               FCts = new CancellationTokenSource();
                // Retrieve the cancellation token from it which we'll use for
                // the new tasks we setup up now.
 //               var ct = FCts.Token;
                // Set the slice counts of our outputs.
                SpreadMax = SpreadUtils.SpreadMax(FRemotePort/*,FPA,FPM*/);

                FStringOut.SliceCount = SpreadMax;
                FReadyOut.SliceCount = SpreadMax;
                // Setup the new tasks.
                FTasks.SliceCount = SpreadMax;
                for (int i = 0; i < SpreadMax; i++)
                {
                    int index = i;
                    
                    // Reset the outputs and the ready state
                    FStringOut[index] = string.Empty;
                    returnData = string.Empty;
                    FReadyOut[index] = false;
                    // Create a Bitmap object from an image file.

                    // Get the color of a pixel within myBitmap.
                    //                   Color pixelColor = myBitmap.GetPixel(50, 50);


 //                   if (completed)
 //                   {
                        completed = false;
 //                       CancelRunningTasks();
                        ReceiverTask(index);
 //                   }
                    
                    // Now save the task in our internal task spread so we're able to cancel
                    // it later on.
                    
                }
            }

            //                for (int i = 0; i < SpreadMax; i++)
            //               {
            //                   int index = i;

            //                  int listenPort = FRemotePort[index];
            

            }

        private void ReceiverTask(int index)
        {


 //           CancelRunningTasks();
            // Create a new task cancellation source object.
            FCts = new CancellationTokenSource();
            // Retrieve the cancellation token from it which we'll use for
            // the new tasks we setup up now.
            var ct = FCts.Token;
            // Now setup a new task which will perform the long running
            // computation on the thread pool of the system.
            var task = Task.Factory.StartNew(() =>
            {
                // Should a cancellation be requested throw the task
                // canceled exception.
                // In this specific scenario this seems a little useless,
                // but imagine your long running computation runs in a loop 
                // you could call this method in each iteration. 
                // Also many asynchronously methods in .NET provide an overload
                // which takes a cancellation token.
                ct.ThrowIfCancellationRequested();

                try
                {


                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    returnData = Encoding.ASCII.GetString(receiveBytes);



                }
                catch (Exception e)
                {
                    FLogger.Log(e);
                }

                var result = returnData;

                //                        udpClient.Close();

                // Note that the ToString method will take the most time 
                // in this particular example, so we'll also compute it in
                // background.
                return new { ReturnString = result/*, ValueAsString = result.ToString()*/ };
                //                       result.Dispose();
            },
                // The cancellation token should also be passed to the StartNew method.
                // For details see http://msdn.microsoft.com/en-us/library/dd997396%28v=vs.110%29.aspx
                ct
            // Once the task is completed we want to write the result to the output.
            // Writing to pins is only allowed in the main thread of vvvv. To achieve
            // this we setup a so called continuation which we tell to run on the
            // task scheduler of the main thread, which is in fact the one who called
            // the Evaluate method of this plugin.
            ).ContinueWith(t =>
            {
                        // Write the result to the outputs

                        // Note that in this particular example writing out the string
                        // will take a very long time - so should more or less be seen
                        // as a debug output.
                        FStringOut[index] = t.Result.ReturnString;
                        // And set the ready state to true
                        FReadyOut[index] = true;
                //                        CancelRunningTasks();
                //                Task.WaitAll();
                //                this.
               Evaluate(spMax);
                        completed = true;

                        //                        udpClient.Close();
                    },
                // Same as in StartNew we pass the used cancellation token
                ct,
                // Here we can specify some options under which circumstances the 
                // continuation should run. In this case we only want it to run if
                // the task wasn't cancelled before.
                TaskContinuationOptions.OnlyOnRanToCompletion,
                // This way we tell the continuation to run on the main thread of vvvv.
                TaskScheduler.FromCurrentSynchronizationContext()
            );

            FTasks[index] = task;
        }


        private void CancelRunningTasks()
        {
            if (FCts != null)
            {
                // All our running tasks use the cancellation token of this cancellation
                // token source. Once we call cancel the ct.ThrowIfCancellationRequested()
                // will throw and the task will transition to the canceled state.
                FCts.Cancel();
                try
                {
                    // We need to wait for all tasks until they're either in the canceled
                    // or completion state.
                    Task.WaitAll(FTasks.ToArray());
                }
                catch (AggregateException e)
                {
                    // Log all exceptions which were thrown during the task execution.
                    foreach (var exception in e.InnerExceptions)
                        FLogger.Log(exception);
                }
                // Dispose the cancellation token source and set it to null so we know
                // to setup a new one in a next frame.
                FCts.Dispose();
                FCts = null;
                // And cleanup all the tasks
                foreach (var task in FTasks)
                    task.Dispose();
                FTasks.SliceCount = 0;
            }
        }
    }

        
    



    [PluginInfo(Name = "UDP Sender", Category = "Network", Version = "Async", Help = "async udp node")]
    public class UDPServerNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
    {
        

        [Input("Input")]
        public ISpread<string> FString;

        [Input("Remote Host")]
        public ISpread<string> FRemoteIP;

        [Input("Remote Port")]
        public ISpread<int> FRemotePort;


        [Input("Cancel", IsBang = true, IsSingle = false)]
		public ISpread<bool> FCancel;

        [Input("Do Send", IsBang = true, IsSingle = true)]
        public ISpread<bool> FDoItIn;


        [Output("Success")]
        public ISpread<bool> FReadyOut;

        [Output("Output String")]
        public ISpread<string> FStringOut;

        [Import]
        public ILogger FLogger;

        private readonly Spread<Task> FTasks = new Spread<Task>();
        private CancellationTokenSource FCts;

        // Called when this plugin was created
        public void OnImportsSatisfied()
        {
            // Do any initialization logic here. In this example we won't need to
            // do anything special.


        }

        // Called when this plugin gets deleted
        public void Dispose()
        {
            // Should this plugin get deleted by the user or should vvvv shutdown
            // we need to wait until all still running tasks ran to a completion
            // state.
            CancelRunningTasks();
        }


        // Called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {

            if (FCancel[0])
            {
                CancelRunningTasks();
            }

            if (FDoItIn[0])
            {
                // Let's first cancel all running tasks (if any).
                CancelRunningTasks();
                // Create a new task cancellation source object.
                FCts = new CancellationTokenSource();
                // Retrieve the cancellation token from it which we'll use for
                // the new tasks we setup up now.
                var ct = FCts.Token;
                // Set the slice counts of our outputs.
                SpreadMax = SpreadUtils.SpreadMax(FRemotePort, FRemoteIP, FString/*,FPA,FPM*/);

                FStringOut.SliceCount = SpreadMax;
                FReadyOut.SliceCount = SpreadMax;
                // Setup the new tasks.
                FTasks.SliceCount = SpreadMax;
                for (int i = 0; i < SpreadMax; i++)
                {
                    int index = i;
                    // Reset the outputs and the ready state
                    FStringOut[index] = string.Empty;
                    FReadyOut[index] = false;
                    // Create a Bitmap object from an image file.

                    // Get the color of a pixel within myBitmap.
                    //                   Color pixelColor = myBitmap.GetPixel(50, 50);



                    // Now setup a new task which will perform the long running
                    // computation on the thread pool of the system.
                    var task = Task.Factory.StartNew(() =>
                    {
                        // Should a cancellation be requested throw the task
                        // canceled exception.
                        // In this specific scenario this seems a little useless,
                        // but imagine your long running computation runs in a loop 
                        // you could call this method in each iteration. 
                        // Also many asynchronously methods in .NET provide an overload
                        // which takes a cancellation token.
                        ct.ThrowIfCancellationRequested();


                        // Here is the actual computation:
                        // This constructor arbitrarily assigns the local port number.

                        int port = FRemotePort[index];
//                        UdpClient sender = new UdpClient(port);
                        //                   UdpClient udpClient = new UdpClient(port);
                        try
                        {
                            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            IPAddress broadcast = IPAddress.Parse(FRemoteIP[index]);
                            byte[] sendbuf = Encoding.ASCII.GetBytes(FString[index]);
                            IPEndPoint ep = new IPEndPoint(broadcast, port);
                            s.SendTo(sendbuf, ep);
                            FLogger.Log(LogType.Message, "Message sent to the broadcast address");

                        }
                        catch (Exception e)
                        {
                            FLogger.Log(e);
                        }

                        var result = "OK";

                        // Note that the ToString method will take the most time 
                        // in this particular example, so we'll also compute it in
                        // background.
                        return new { Value = result/*, ValueAsString = result.ToString()*/ };
 //                       result.Dispose();
                    },
                        // The cancellation token should also be passed to the StartNew method.
                        // For details see http://msdn.microsoft.com/en-us/library/dd997396%28v=vs.110%29.aspx
                        ct
                    // Once the task is completed we want to write the result to the output.
                    // Writing to pins is only allowed in the main thread of vvvv. To achieve
                    // this we setup a so called continuation which we tell to run on the
                    // task scheduler of the main thread, which is in fact the one who called
                    // the Evaluate method of this plugin.
                    ).ContinueWith(t =>
                    {
                        // Write the result to the outputs

                        // Note that in this particular example writing out the string
                        // will take a very long time - so should more or less be seen
                        // as a debug output.
                        FStringOut[index] = t.Result.Value;
                        // And set the ready state to true
                        FReadyOut[index] = true;
                    },
                        // Same as in StartNew we pass the used cancellation token
                        ct,
                        // Here we can specify some options under which circumstances the 
                        // continuation should run. In this case we only want it to run if
                        // the task wasn't cancelled before.
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        // This way we tell the continuation to run on the main thread of vvvv.
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
                    // Now save the task in our internal task spread so we're able to cancel
                    // it later on.
                    FTasks[index] = task;
                }
            }
        }

        private void CancelRunningTasks()
        {
            if (FCts != null)
            {
                // All our running tasks use the cancellation token of this cancellation
                // token source. Once we call cancel the ct.ThrowIfCancellationRequested()
                // will throw and the task will transition to the canceled state.
                FCts.Cancel();
                try
                {
                    // We need to wait for all tasks until they're either in the canceled
                    // or completion state.
                    Task.WaitAll(FTasks.ToArray());
                }
                catch (AggregateException e)
                {
                    // Log all exceptions which were thrown during the task execution.
                    foreach (var exception in e.InnerExceptions)
                        FLogger.Log(exception);
                }
                // Dispose the cancellation token source and set it to null so we know
                // to setup a new one in a next frame.
                FCts.Dispose();
                FCts = null;
                // And cleanup all the tasks
                foreach (var task in FTasks)
                    task.Dispose();
                FTasks.SliceCount = 0;
            }
        }

    }
}
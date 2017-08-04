#region usings
using System;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

//using VVVV.DX11.Core;

using Mpir.NET;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
    [PluginInfo(Name = "SumProducts", Category = "MPIR BigInteger", Help = "mpir series")]
    public class MPIRSumProductsNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
    {
        [Input("Colors")]
        public ISpread<int> FColors;

        [Input("Pixel")]
        public ISpread<int> FPixel;

        [Input("Value")]
        public ISpread<int> FValue;

        [Input("Cancel", IsBang = true, IsSingle = false)]
		public ISpread<bool> FCancel;

        [Input("Calculate", IsBang = true, IsSingle = true)]
        public ISpread<bool> FDoItIn;


        [Output("Success")]
        public ISpread<bool> FReadyOut;

        [Output("Output")]
        public ISpread<mpz_t> FOutput;

        [Output("Output String")]
        public ISpread<string> FStringOut;


        [Import]
		public ILogger FLogger;

        private readonly Spread<Task> FTasks = new Spread<Task>();
        private readonly Spread<CancellationTokenSource> FCts = new Spread<CancellationTokenSource>();
        private readonly Spread<CancellationToken> ct = new Spread<CancellationToken>();
        private int TaskCount = 0;

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
            for (int i = 0; i < TaskCount; i++)
            {
                int index = i;
                FLogger.Log(LogType.Message, "Dispose task:" + index);
                CancelRunningTasks(index);
            }
        }

        // Called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {

            SpreadMax = SpreadUtils.SpreadMax(FColors, FPixel/*,FPA,FPM*/);
            // Set the slice counts of our outputs.
            FOutput.SliceCount = SpreadMax;
            FStringOut.SliceCount = SpreadMax;
            FReadyOut.SliceCount = SpreadMax;

            FTasks.SliceCount = SpreadMax;
            FCts.SliceCount = SpreadMax;
            ct.SliceCount = SpreadMax;
            TaskCount = SpreadMax;


            for (int i = 0; i < SpreadMax; i++)
            {
                // store i to a new variable so it won't change when tasks are running over longer period.
                int index = i;

                if (FCancel[index])
                {
                    CancelRunningTasks(index);
                }


                if (FDoItIn[index])
                {
                    // Let's first cancel all running tasks (if any).
                    CancelRunningTasks(index);

                    // Create a new task cancellation source object.
                    FCts[index] = new CancellationTokenSource();
                    // Retrieve the cancellation token from it which we'll use for
                    // the new tasks we setup up now.
                    ct[index] = FCts[index].Token;

                    // Reset the outputs and the ready state
                    FOutput[i] = default(mpz_t);
                    FStringOut[i] = string.Empty;
                    FReadyOut[i] = false;


                    // Now setup a new task which will perform the long running
                    FTasks[index] = Task.Factory.StartNew(() =>
                    {
                        // Should a cancellation be requested throw the task
                        // canceled exception.
                        // In this specific scenario this seems a little useless,
                        // but imagine your long running computation runs in a loop 
                        // you could call this method in each iteration. 
                        // Also many asynchronously methods in .NET provide an overload
                        // which takes a cancellation token.
                        ct[index].ThrowIfCancellationRequested();

                        // Here is the actual compution:
                        int numPixels = FPixel[index];
                        int numColors = FColors[index];

                        mpz_t result = new mpz_t(0);                       
                        mpz_t a = new mpz_t(numColors);

                        for (int j = 0; j < numPixels; j++)
                        {
                            int jndex = j;                           
                            mpz_t op = new mpz_t((a.Power(jndex)).Multiply(FValue[jndex]));
                            result.Add(op);
                        }


                        // Note that the ToString method will take the most time 
                        // in this particular example, so we'll also compute it in
                        // background.
                        return new { Value = result, ValueAsString = result.ToString() };
                    },
                    // The cancellation token should also be passed to the StartNew method.
                    // For details see http://msdn.microsoft.com/en-us/library/dd997396%28v=vs.110%29.aspx
                    ct[index]
                    // Once the task is completed we want to write the result to the output.
                    // Writing to pins is only allowed in the main thread of vvvv. To achieve
                    // this we setup a so called continuation which we tell to run on the
                    // task scheduler of the main thread, which is in fact the one who called
                    // the Evaluate method of this plugin.
                    ).ContinueWith(t =>
                    {
                        // Write the result to the outputs
                        FOutput[index] = t.Result.Value;
                        // Note that in this particular example writing out the string
                        // will take a very long time - so should more or less be seen
                        // as a debug output.
                        FStringOut[index] = t.Result.ValueAsString;
                        // And set the ready state to true
                        FReadyOut[index] = true;
                    },
                    // Same as in StartNew we pass the used cancellation token
                    ct[index],
                    // Here we can specify some options under which circumstances the 
                    // continuation should run. In this case we only want it to run if
                    // the task wasn't cancelled before.
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    // This way we tell the continuation to run on the main thread of vvvv.
                    TaskScheduler.FromCurrentSynchronizationContext()
                    );
                }
            }
        }

        private void CancelRunningTasks(int index)
        {
            if (FCts[index] != null)
            {
                // All our running tasks use the cancellation token of this cancellation
                // token source. Once we call cancel the ct.ThrowIfCancellationRequested()
                // will throw and the task will transition to the canceled state.
                FCts[index].Cancel();

                // Dispose the cancellation token source and set it to null so we know
                // to setup a new one in a next frame.
                FCts[index].Dispose();
                FCts[index] = null;

            }
        }

    }
}
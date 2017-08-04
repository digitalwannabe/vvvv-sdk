#region usings
using System;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using NodaTime;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
    [PluginInfo(Name = "DateTime", Category = "BigInteger", Help = "Compute dates > 10.000 AD")]
    public class BigSumProductsNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
    {
        [Input("Input")]
        public ISpread<BigInteger> FIn;

        [Input("Start Date", DefaultValue = 1)]
        public ISpread<DateTime> FStartTime;

        [Input("Calculate", IsBang = true, IsSingle = true)]
        public ISpread<bool> FDoItIn;



        [Output("Success")]
        public ISpread<bool> FReadyOut;

        [Output("TimeAndDate")]
        public ISpread<string> FStringDateTime;

        [Output("Year")]
        public ISpread<string> FStringYear;

        [Import]
        public ILogger FLogger;

        private readonly Spread<Task> FTasks = new Spread<Task>();
        private CancellationTokenSource FCts;

        const int hours = 24;
        const int minutes = 60;
        const int seconds = 60;
        const int milliseconds = 1000;
        const int ticks = 10000;
        const int frames = 60;
        const long hmsmt = hours * minutes * seconds * milliseconds * (long)ticks; //convert ticks to long to avoid oveflow
        const long hmsf = hours * minutes * seconds * frames;

        string[] months = {"January","February","March","April","May","June","July","August","September","October","November","December"};


        //every 4 years there is a leap year, every 100 years the leap year is skipped, every 400 years it is NOT skipped
        const long daysIn400years = 303 * 365 + 97 * 366;
        const long framesIn400years = daysIn400years * hmsf;
        //       const UInt64 daysIn400years = 303 * 365 + 97 * 366;

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

        //Convert frames to ticks
        private long FramesToTicks(BigInteger framesToConvert)
        {
            long result = ((long)framesToConvert / frames) * milliseconds * ticks;
            return result;
        }

        //Convert frames to date
        private BigInteger FramesToYear(BigInteger frames)
        {
            BigInteger year = (frames / seconds) * milliseconds * ticks;
            return year;
        }

        private void FramesToDateTime(BigInteger framesPassed, DateTime startTime, out BigInteger years, out string dateAndTime) {

            BigInteger remainder400s;
            BigInteger number400s = System.Numerics.BigInteger.DivRem(framesPassed, framesIn400years, out remainder400s);

            DateTime minus400sFromStart = new DateTime(startTime.Ticks + FramesToTicks(remainder400s));

            LocalDateTime minus400sFromStartLocal = NodaTime.Extensions.DateTimeExtensions.ToLocalDateTime(minus400sFromStart); //converting to NodaTime here to work with periods (which know years)
            LocalDateTime startTimeLocal = NodaTime.Extensions.DateTimeExtensions.ToLocalDateTime(startTime);

            Period period = Period.Between(startTimeLocal, minus400sFromStartLocal, PeriodUnits.AllUnits);

            years = number400s * 400 + period.Years;

            LocalDateTime finalMinus400s = startTimeLocal.Plus(period);
            
            dateAndTime = finalMinus400s.TimeOfDay.ToString() + ", " + finalMinus400s.DayOfWeek.ToString() + ", " + months[finalMinus400s.Month-1] + " " + finalMinus400s.Day.ToString();
        }

        // Called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
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
                FStringYear.SliceCount = SpreadMax;
                FStringDateTime.SliceCount = SpreadMax;
                FReadyOut.SliceCount = SpreadMax;
                // Setup the new tasks.
                FTasks.SliceCount = SpreadMax;
                for (int i = 0; i < FReadyOut.SliceCount; i++)
                {
                    int index = i;

                    // Reset the outputs and the ready state
                    FStringYear[index] = string.Empty;
                    FStringDateTime[index] = string.Empty;
                    FReadyOut[index] = false;

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

                        string dateAndTime;
                        BigInteger years;
                        FramesToDateTime(FIn[index], FStartTime[index], out years, out dateAndTime);

                        // Note that the ToString method will take the most time 
                        // in this particular example, so we'll also compute it in
                        // background.
                        return new { YearAsString = years.ToString(), DateAndTimeString = dateAndTime };
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
                        FStringYear[index] = t.Result.YearAsString;
                        // Note that in this particular example writing out the string
                        // will take a very long time - so should more or less be seen
                        // as a debug output.
                        FStringDateTime[index] = t.Result.DateAndTimeString;
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
                    FTasks[i] = task;
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
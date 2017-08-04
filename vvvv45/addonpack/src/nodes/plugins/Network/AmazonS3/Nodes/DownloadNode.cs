using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;


using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace VVVV.Nodes
{

    #region PluginInfo
    [PluginInfo(Name = "AmazonS3Download",
                Category = "Network",
                Help = "Download from S3",
                Author = "digitalWannabe",
                Credits = "elias, vux",
                Tags = "amazon,s3",
                AutoEvaluate = true)]
    #endregion PluginInfo
    public class AmazonS3DownloadNode : IPluginEvaluate
    {


        [Input("Key")]
        IDiffSpread<string> FPinInKey;

        [Input("Bucket")]
        IDiffSpread<string> FPinInBucket;

        [Input("Subdir")]
        IDiffSpread<string> FPinInSubdir;

        [Input("Destination Path", StringType = StringType.Directory)]
        IDiffSpread<string> FPinInDir;

        [Output("Success")]
        ISpread<bool> FPinOutSuccess;

        [Input("Download", IsBang = true, IsSingle = true)]
        IDiffSpread<bool> FPinInDoSend;

        [Import()]
        ILogger FLogger;

        string FError = "";

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


        public bool downloadMyFileFromS3(string localFileDestination, string bucketName, string subDirectoryInBucket, string fileNameInS3)
        {
            // input explained :
            // localFilePath = the full local file path e.g. "c:\mydir\mysubdir\myfilename.zip"
            // bucketName : the name of the bucket in S3 ,the bucket should be alreadt created
            // subDirectoryInBucket : if this string is not empty the file will be Downloaded to
            // a subdirectory with this name
            // fileNameInS3 = the file name in the S3
            try
            {
                // create an instance of IAmazonS3 class ,in my case i choose RegionEndpoint.EUWest1
                // you can change that to APNortheast1 , APSoutheast1 , APSoutheast2 , CNNorth1
                // SAEast1 , USEast1 , USGovCloudWest1 , USWest1 , USWest2 . this choice will not
                // store your file in a different cloud storage but (i think) it differ in performance
                // depending on your location
                var client = new AmazonS3Client(Amazon.RegionEndpoint.EUWest2);
                FLogger.Log(LogType.Debug, "Created client!");
                // create a TransferUtility instance passing it the IAmazonS3 created in the first step
                TransferUtility utility = new TransferUtility(client);
                // making a TransferUtilityDownloadRequest instance
                TransferUtilityDownloadRequest request = new TransferUtilityDownloadRequest();
                FLogger.Log(LogType.Debug, "Created Download request!");
                if (subDirectoryInBucket == "" || subDirectoryInBucket == null)
                {
                    request.BucketName = bucketName; //no subdirectory just bucket name
                }
                else
                {   // subdirectory and bucket name
                    request.BucketName = bucketName + @"/" + subDirectoryInBucket;
                }

                request.Key = fileNameInS3; //file name up in S3
                request.FilePath = localFileDestination; //local file name

                utility.Download(localFileDestination, request.BucketName, fileNameInS3) ; //commensing the transfer
                FLogger.Log(LogType.Debug, "Download successful!");
                return true; //indicate that the file was sent
            }
            catch (AmazonS3Exception e)
            {

                FLogger.Log(LogType.Debug, e.ToString());
                return false;
            }
        }

        #region Evaluate
        public void Evaluate(int SpreadMax)
        {

            if (FPinInDoSend[0])
            {
                SpreadMax = SpreadUtils.SpreadMax(FPinInDoSend, FPinInDir, FPinInBucket, FPinInKey, FPinInSubdir);


                // Let's first cancel all running tasks (if any).
                CancelRunningTasks();
                // Create a new task cancellation source object.
                FCts = new CancellationTokenSource();
                // Retrieve the cancellation token from it which we'll use for
                // the new tasks we setup up now.
                var ct = FCts.Token;

                FTasks.SliceCount = SpreadMax;
                FPinOutSuccess.SliceCount = SpreadMax;

                for (int i = 0; i < SpreadMax; i++)
                {
                    //                if (FPinInDoSend[i])
                    //                 {

                    // Reset the outputs and the ready state
                    //                       FOutput[i] = default(BigInteger);
                    //                       FStringOut[i] = string.Empty;
                    FPinOutSuccess[i] = false;

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

                        // Here is the actual compution:
                        //                            var @base = new BigInteger(FBaseIn[i]);
                        //                           var exponent = FExponentInt[i];
                        //                            var result = BigIntPow(@base, exponent);

                        // preparing our file and directory names
                        string localFileDestination = FPinInDir[i] + "/" + FPinInKey[i]; // test file
                        string myBucketName = FPinInBucket[i]; //your s3 bucket name goes here
                        string s3DirectoryName = FPinInSubdir[i];
                        string s3FileName = FPinInKey[i];

                        var result = downloadMyFileFromS3(localFileDestination, myBucketName, s3DirectoryName, s3FileName);

                        // Note that the ToString method will take the most time 
                        // in this particular example, so we'll also compute it in
                        // background.
                        return new { Value = result };
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
                            //                              FOutput[i] = t.Result.Value;
                            // Note that in this particular example writing out the string
                            // will take a very long time - so should more or less be seen
                            // as a debug output.
                            //                              FStringOut[i] = t.Result.ValueAsString;
                            // And set the ready state to true
                            FPinOutSuccess[i] = t.Result.Value;
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

                    //                       FLogger.Log(LogType.Debug, "hi tty!");
                    //            }

                }
            }

        }


        #endregion

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

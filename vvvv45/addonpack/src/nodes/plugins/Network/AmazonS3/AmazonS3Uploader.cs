﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace VVVV
{
    public class AmazonS3Uploader2
    {

        [Import()]
        ILogger FLogger;

        public bool sendMyFileToS3(string localFilePath, string bucketName, string subDirectoryInBucket, string fileNameInS3)
        {
            // input explained :
            // localFilePath = the full local file path e.g. "c:\mydir\mysubdir\myfilename.zip"
            // bucketName : the name of the bucket in S3 ,the bucket should be alreadt created
            // subDirectoryInBucket : if this string is not empty the file will be uploaded to
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
                FLogger.Log(LogType.Debug, "Created client");
                // create a TransferUtility instance passing it the IAmazonS3 created in the first step
                TransferUtility utility = new TransferUtility(client);
                // making a TransferUtilityUploadRequest instance
                TransferUtilityUploadRequest request = new TransferUtilityUploadRequest();
                FLogger.Log(LogType.Debug, "Created upload request");
                if (subDirectoryInBucket == "" || subDirectoryInBucket == null)
                {
                    request.BucketName = bucketName; //no subdirectory just bucket name
                }
                else
                {   // subdirectory and bucket name
                    request.BucketName = bucketName + @"/" + subDirectoryInBucket;
                }

                request.Key = fileNameInS3; //file name up in S3
                request.FilePath = localFilePath; //local file name
                utility.Upload(localFilePath,bucketName); //commensing the transfer
                FLogger.Log(LogType.Debug, "Upload successful!");
                return true; //indicate that the file was sent
            }
            catch (AmazonS3Exception e)
            {
                FLogger.Log(LogType.Debug, e.Message);
                FLogger.Log(LogType.Debug, e.ResponseBody);
                FLogger.Log(LogType.Debug, e.ToString());
                return false;
            }
        }

    }
}

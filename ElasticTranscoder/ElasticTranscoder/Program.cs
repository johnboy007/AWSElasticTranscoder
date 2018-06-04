using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using Amazon;
using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using Amazon.S3;
using Amazon.S3.Model;

namespace ElasticTranscoder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            PrintMenu();
        }

        private static void PrintMenu()
        {
            Console.WriteLine("Welcome to the POWER AWS Elastic Transcoder\n");

            Console.WriteLine("Upload and transcode video U");
            Console.WriteLine("Press the Escape (Esc) key to quit\n");
            ConsoleKeyInfo keyinfo;
            do
            {
                keyinfo = Console.ReadKey();
                if (keyinfo.Key == ConsoleKey.U) UploadVideo();
            } while (keyinfo.Key != ConsoleKey.Escape);
        }


        private static void UploadVideo()
        {
            Console.WriteLine(!UploadVideoToLocation() ? "There was a problem please check the log file\n" : "Upload of video completed\n");
            PrintMenu();
        }

        private static bool UploadFile(string strFilePath, string strKey)
        {
            try
            {
                Console.WriteLine("\n");
                var strAWSAccessKey = GetAppSetting("AWSAccessKeyID");
                var strAWSSecretKey = GetAppSetting("AWSSecretKey");
                var strContentDisposition = GetAppSetting("ContentDisposition");
                var strBucket_Input = GetAppSetting("Bucket");

                var objRegionEndpoint = RegionEndpoint.GetBySystemName(GetAppSetting("AWSRegion"));
                Console.WriteLine($"Uploading to {strBucket_Input}/{strKey}");
                using (var objAmazonS3Client = new AmazonS3Client(strAWSAccessKey, strAWSSecretKey, objRegionEndpoint))
                {
                    var objPutObjectRequest = new PutObjectRequest
                    {
                        BucketName = strBucket_Input,
                        FilePath =strFilePath,
                        Key =  strKey,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    objPutObjectRequest.Metadata.Add("Content-Disposition", strContentDisposition);
                    var response2 = objAmazonS3Client.PutObject(objPutObjectRequest);
                    Console.WriteLine($"Video Uploaded to bucket {strBucket_Input}");
                    return response2.HttpStatusCode == HttpStatusCode.OK;
                }
            }
            catch (AmazonS3Exception objException)
            {
                WriteToLog(objException.Message);
                return false;
            }
            catch (Exception objException)
            {
                WriteToLog(objException.Message);
                return false;
            }
        }


        private static bool UploadVideoToLocation()
        {
            try
            {
                var strSamplePath = GetAppSetting("SamplePath");
                var strFilePath = strSamplePath;
                var strFilename = Path.GetFileName(strSamplePath);
                var strExtension = Path.GetExtension(strFilePath);
                var strRand = GetRandomString();
                var strKey = $"mwstag/ElasticTranscoder/Video/{strRand}/{strFilename}";
                
                if (string.IsNullOrEmpty(strExtension))
                    strFilePath += ".mp4";

                //Upload the video to the input bucket
                if (!UploadFile(strFilePath, strKey))
                {
                    Console.WriteLine("The video upload did not go well!!");
                    return false;
                }

                //Create the pipelines
                var strAWSAccessKey = GetAppSetting("AWSAccessKeyID");
                var strAWSSecretKey = GetAppSetting("AWSSecretKey");
                var objAmazonElasticTranscoderClient = new AmazonElasticTranscoderClient(strAWSAccessKey, strAWSSecretKey, RegionEndpoint.EUWest1);
                var lstPipelines = objAmazonElasticTranscoderClient.ListPipelines();
                Pipeline objPipeline;
                if (!objAmazonElasticTranscoderClient.ListPipelines().Pipelines.Any())
                    objPipeline = objAmazonElasticTranscoderClient.CreatePipeline(new CreatePipelineRequest
                    {
                        Name = GetAppSetting("PipelineName"),
                        InputBucket = GetAppSetting("Bucket"),
                        OutputBucket = GetAppSetting("Bucket"),
                        Role = GetAppSetting("AWSRole")
                    }).Pipeline; //createpipelineresult
                else
                    objPipeline = objAmazonElasticTranscoderClient.ListPipelines().Pipelines.First();


                //Create the job
                objAmazonElasticTranscoderClient.CreateJob(new CreateJobRequest
                {
                    PipelineId = objPipeline.Id,
                    Input = new JobInput
                    {
                        AspectRatio = "auto",
                        Container = "mp4", //H.264
                        FrameRate = "auto",
                        Interlaced = "auto",
                        Resolution = "auto",
                        Key = strKey
                    },
                    Output = new CreateJobOutput
                    {
                        ThumbnailPattern = $"{GetRandomString()}{{count}}",
                        Rotate = "0",
                        PresetId = "1351620000001-000010", //Generic-720 px
                        Key = $"mwstag/ElasticTranscoder/Video/{strRand}/output/{strFilename}"
                    }
                });
                return true;
            }
            catch (Exception objException)
            {
                WriteToLog(objException.Message);
                return false;
            }
        }

        private static string GetAppSetting(string strKey)
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;
            return settings[strKey].Value;
        }


        private static void WriteToLog(string strValue)
        {
            using (var writer = new StreamWriter(GetAppSetting("LogPath"), true))
            {
                writer.WriteLine(strValue);
            }
        }

        /// <summary>
        ///     Get random string of 11 characters.
        /// </summary>
        /// <returns>Random string.</returns>
        private static string GetRandomString()
        {
            var path = Path.GetRandomFileName();
            path = path.Replace(".", ""); // Remove period.
            return path;
        }
    }
}
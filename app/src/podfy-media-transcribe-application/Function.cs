using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using System.Diagnostics;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace podfy_media_transcribe_application
{
    public class Function
    {
        public async Task TranscribeFunctionHandlerAsync(SNSEvent evnt, ILambdaContext context)
        {
            context.Logger.LogInformation($"Received message with Records {evnt.Records.Count}");
            foreach (var record in evnt.Records)
            {
                var messageEvent = JsonSerializer.Deserialize<MessageEvent>(record.Sns.Message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                context.Logger.LogInformation($"Processed record {messageEvent.Identifier}");

                await TranscribeMediaFile(messageEvent.FileUri, messageEvent.Identifier, context);
            }

            await Task.CompletedTask;
        }

        private async Task TranscribeMediaFile(string s3HttpUri, string identifier, ILambdaContext context, string languageCode = null)
        {
            try
            {
                context.Logger.LogInformation($"entry in transcribe");

                var bucketName = Debugger.IsAttached ? "you-bucket" : Environment.GetEnvironmentVariable("BUCKET_NAME");

                var pos = identifier.LastIndexOf(".");
                var transcriptFileName = (pos != -1) ? identifier.Substring(0, pos) + ".json" : identifier + ".json";

                var client = GetTrancribeClient();

                var startJobRequest = new StartTranscriptionJobRequest()
                {
                    Media = new Media()
                    {
                        MediaFileUri = s3HttpUri
                    },
                    OutputBucketName = bucketName,
                    OutputKey = transcriptFileName,
                    TranscriptionJobName = $"{DateTime.Now.Ticks}-{identifier}",
                    //LanguageCode = new LanguageCode(languageCode),
                    IdentifyLanguage = true,
                    Subtitles = new Subtitles()
                    {
                        Formats = new List<string> { "vtt" }
                    }
                };

                var startJobResponse = await client.StartTranscriptionJobAsync(startJobRequest);

                if (startJobResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    context.Logger.LogInformation($"job with identifier: {identifier} has started");
                else
                    context.Logger.LogError($"job failed with identifier: {identifier} ");



                //var getJobRequest = new GetTranscriptionJobRequest() { TranscriptionJobName = startJobRequest.TranscriptionJobName };


                //GetTranscriptionJobResponse getJobResponse;
                //do
                //{
                //    Thread.Sleep(15 * 1000);
                //    Console.Write(".");
                //    getJobResponse = await client.GetTranscriptionJobAsync(getJobRequest);
                //} while (getJobResponse.TranscriptionJob.TranscriptionJobStatus == "IN_PROGRESS");
                //Console.WriteLine($"Job complete, status: {getJobResponse.TranscriptionJob.TranscriptionJobStatus}");

                //Console.WriteLine($"Results saved to file {transcriptFileName}");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"job failed: {ex.Message} ");
                throw;
            }
        }

        private AmazonTranscribeServiceClient GetTrancribeClient()
        {
            if (Debugger.IsAttached)
                return new AmazonTranscribeServiceClient(Amazon.RegionEndpoint.USEast1);
            else
                return new AmazonTranscribeServiceClient(Environment.GetEnvironmentVariable("ACCESS_KEY"), Environment.GetEnvironmentVariable("SECRET_KEY"), Amazon.RegionEndpoint.USEast1);

        }
    }

    public class MessageEvent
    {
        public string Identifier { get; set; }
        public string FileUri { get; set; }
    }
}
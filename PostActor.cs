using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class PostActor : ReceiveActor
    {
        private class PostResult
        {
            public ImageProcessingPipelineActor.SnapRecord Snap;
            public bool Success;
        }

        private const int TIMEOUT_SECS = 10;

        private readonly string _baseUrl;

        private readonly string _token;

        private readonly IActorRef _listener;

        private readonly HttpClient _http;

        public PostActor(
            string baseUrl,
            string token,
            IActorRef listener)
        {
            _baseUrl = baseUrl;
            _token = token;

            _http = new HttpClient();
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_token}");

            Receive<PostResult>(msg =>
            {
                Log.Information("Http request returned.");
            });

            Receive<ImageProcessingPipelineActor.Post>(msg =>
            {
                // allow timeout
                var timeout = new CancellationTokenSource();
                timeout.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECS));

                // prepare request
                var multipartContent = new MultipartFormDataContent();
                var stream = File.OpenRead(msg.Snap.SrcPath);
                var content = new StreamContent(stream);
                content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                {
                    // Skipper requires quotes
                    Name = "\"file\"",
                    FileName = "\"test.png\""
                };
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

                multipartContent.Add(content);

                // we don't need to wait for response
                var url = $"{_baseUrl}/v1/org/{msg.Snap.OrgId}/snap/{msg.Snap.InstanceId}";

                Log.Information($"Starting POST to {url}.");

                _http
                    .PostAsync(
                        url,
                        multipartContent)
                    .ContinueWith(async responseMsg =>
                    {
                        var response = responseMsg.Result;
                        var bodyString = await response.Content.ReadAsStringAsync();

                        Log.Information($"Response String: {bodyString}");

                        stream.Dispose();
                        content.Dispose();
                        multipartContent.Dispose();
                        
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            return new PostResult
                            {
                                Snap = msg.Snap,
                                Success = true
                            };
                        }
                        else
                        {
                            return new PostResult
                            {
                                Snap = msg.Snap,
                                Success = false
                            };
                        }
                    }, timeout.Token)
                    .PipeTo(Self);
            });
        }
    }
}

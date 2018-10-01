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
    /// <summary>
    /// Actor that sends composite image to Trellis.
    /// </summary>
    public class PostActor : ReceiveActor
    {
        /// <summary>
        /// The result of the POST request, used internally.
        /// </summary>
        private class PostResult
        {
            /// <summary>
            /// The snap record.
            /// </summary>
            public ImageProcessingPipelineActor.SnapRecord Snap;

            /// <summary>
            /// True iff successful.
            /// </summary>
            public bool Success;
        }

        /// <summary>
        /// How long to wait until we timeout.
        /// </summary>
        private const int TIMEOUT_SECS = 10;

        /// <summary>
        /// The base URL of trellis.
        /// </summary>
        private readonly string _baseUrl;

        /// <summary>
        /// The token.
        /// </summary>
        private readonly string _token;

        /// <summary>
        /// An actor that listens for updates.
        /// </summary>
        private readonly IActorRef _listener;

        /// <summary>
        /// The http client to use repeatedly.
        /// </summary>
        private readonly HttpClient _http;

        /// <summary>
        /// Constructor.
        /// </summary>
        public PostActor(
            string baseUrl,
            string token,
            IActorRef listener)
        {
            _baseUrl = baseUrl;
            _token = token;
            _listener = listener;

            _http = new HttpClient();
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_token}");

            // received from self
            Receive<PostResult>(msg =>
            {
                Log.Information("Http request returned.");

                // finally, delete!
                if (msg.Success)
                {
                    File.Delete(msg.Snap.SrcPath);
                }

                _listener.Tell(new ImageProcessingPipelineActor.Complete
                {
                    Snap = msg.Snap
                });
            });

            // starts the action
            Receive<ImageProcessingPipelineActor.Start>(msg =>
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
                    // Trellis requires quotes
                    Name = "\"file\"",
                    FileName = "\"test.png\""
                };
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                multipartContent.Add(content);

                // construct the url
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
                    // instead of using await, PipeTo uses IActorRef::Tell
                    .PipeTo(Self);
            });
        }
    }
}
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                if (!msg.Success)
                {
                    Log.Error("Could not upload file.", msg.Snap);
                }

                // delete src
                File.Delete(msg.Snap.SrcPath);

                _listener.Tell(new ImageProcessingPipelineActor.Complete
                {
                    Snap = msg.Snap
                });
            });

            // starts the action
            Receive<ImageProcessingPipelineActor.Start>(msg =>
            {
                // post src
                PostImage(
                    msg.Snap.SrcPath,
                    $"{_baseUrl}/v1/org/{msg.Snap.OrgId}/snap/{msg.Snap.InstanceId}",
                    msg.Snap);
            });
        }

        /// <summary>
        /// POSTs an image to an endpoint.
        /// </summary>
        /// <param name="srcPath">The path to the image.</param>
        /// <param name="url">The URL to POST to.</param>
        /// <param name="snap">The snap to pass along.</param>
        private void PostImage(
            string srcPath,
            string url,
            ImageProcessingPipelineActor.SnapRecord snap)
        {
            // allow timeout
            var timeout = new CancellationTokenSource();
            timeout.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECS));

            // prepare request
            var multipartContent = new MultipartFormDataContent();
            
            // Session and User Content
            multipartContent.Add(new StringContent(snap.SessionId), "\"sessionId\"");
            multipartContent.Add(new StringContent(snap.UserId), "\"userId\"");
            multipartContent.Add(new StringContent(snap.Tag), "\"tag\"");
            
            // File Content Last
            var stream = File.OpenRead(srcPath);
            var content = new StreamContent(stream);
            multipartContent.Add(content, "\"file\"", "\"file.jpg\"");
            
            Log.Information($"POST to {url}.", snap);

            _http
                .PostAsync(url, multipartContent)
                .ContinueWith(responseMsg =>
                {
                    var response = responseMsg.Result;

                    stream.Dispose();
                    content.Dispose();
                    multipartContent.Dispose();
                    
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return new PostResult
                        {
                            Snap = snap,
                            Success = true
                        };
                    }
                    else
                    {
                        return new PostResult
                        {
                            Snap = snap,
                            Success = false
                        };
                    }
                }, timeout.Token)
                .PipeTo(Self);
        }
    }
}
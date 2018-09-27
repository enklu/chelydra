using Newtonsoft.Json;

namespace CreateAR.Snap
{
    /// <summary>
    /// Object that forms data how Trellis expects.
    /// </summary>
    public class WebSocketRequest
    {
        /// <summary>
        /// Contains header information.
        /// </summary>
        public class HeaderData
        {
            /// <summary>
            /// Auth header.
            /// </summary>
            public string Authorization;
        }
        
        /// <summary>
        /// Url to hit. This should not include the host.
        /// </summary>
        [JsonProperty("url")]
        public string Url;

        /// <summary>
        /// Http method.
        /// </summary>
        [JsonProperty("method")]
        public string Method;

        /// <summary>
        /// Header info.
        /// </summary>
        [JsonProperty("headers")]
        public HeaderData Headers;

        /// <summary>
        /// The data to send, if the http verb supports a body.
        /// </summary>
        [JsonProperty("data")]
        public object Data;

        /// <summary>
        /// Creates a request.
        /// </summary>
        public WebSocketRequest(string url, string method)
        {
            Url = url;
            Method = method;
        }

        /// <summary>
        /// Creates a request.
        /// </summary>
        public WebSocketRequest(string url, string method, object payload)
            : this(url, method)
        {
            Data = payload;
        }
    }
}
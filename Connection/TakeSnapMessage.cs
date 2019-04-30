using Newtonsoft.Json;

namespace CreateAR.Snap
{
    /// <summary>
    /// Message received from Trellis when a snap should be taken.
    /// </summary>
    public class TakeSnapMessage
    {
        /// <summary>
        /// The type.
        /// </summary>
        [JsonProperty("type")]
        public string Type;

        /// <summary>
        /// The associated instanceId.
        /// </summary>
        [JsonProperty("instanceId")]
        public string InstanceId;

        /// <summary>
        /// The tag to upload the image with.
        /// </summary>
        [JsonProperty("tag")]
        public string Tag;

        /// <summary>
        /// The session in which the take snap was initiated.
        /// </summary>
        [JsonProperty("sessionId")]
        public string SessionId;

        /// <summary>
        /// The user identifier which initiated the snap.
        /// </summary>
        [JsonProperty("userId")]
        public string UserId;

        public override string ToString()
        {
            return $"[TakeSnapMessage SessionId={SessionId}, UserId={UserId}, Type={Type}, InstanceId={InstanceId}, Tag={Tag}]";
        }
    }
}
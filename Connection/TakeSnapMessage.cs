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
    }
}
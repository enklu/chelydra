using CommandLine;

namespace CreateAR.Snap
{
    /// <summary>
    /// Command line options.
    /// </summary>
    public class ConfigurationOptions
    {
        /// <summary>
        /// Trellis url.
        /// </summary>
        [Option('u', "url",
            Default = "https://trellis.enklu.com:10001",
            HelpText = "Trellis URL. Defaults to cloud install.")]
        public string Url { get; set; }

        /// <summary>
        /// Organization id.
        /// </summary>
        [Option('o', "org",
            Required = true,
            HelpText = "Organization id.")]
        public string OrgId { get; set; }

        /// <summary>
        /// JWT.
        /// </summary>
        [Option('t', "token",
            Required = true,
            HelpText = "Valid token.")]
        public string Token { get; set; }
    }
}
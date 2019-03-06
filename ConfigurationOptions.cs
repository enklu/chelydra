using System.Collections.Generic;
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

        /// <summary>
        /// Dimensions of the screen to cut out.
        /// </summary>
        /// <value></value>
        [Option('d', "dimensions",
            Required = true,
            Separator = ',',
            HelpText = "X, Y, Width, Height of cutout.")]
        public IEnumerable<int> Dimensions { get; set; }

        /// <inheritdoc />
        override public string ToString()
        {
            return $"[ConfigurationOptions Url={Url}, OrgId={OrgId}]";
        }
    }
}
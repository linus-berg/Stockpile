using CommandLine;

namespace Stockpile.CLI {
  [Verb("blacklist", HelpText = "Blacklist package")]
  public class BlacklistOptions : CommonOptions {
    [Option('c', "channelId", Required = true)]
    public string ChannelId { get; set; }

    [Option('a', "artifactId", Required = true)]
    public string ArtifactId { get; set; }

    [Option('v', "version", Required = true)]
    public string Version { get; set; }
  }
}
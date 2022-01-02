using System;
using System.Threading.Tasks;
using Stockpile.Channels;
using Stockpile.CLI;
using Stockpile.Config;

namespace Stockpile.Services {
  public class ArtifactService {
    private static readonly DateTime RUNTIME = DateTime.UtcNow;
    private BaseChannel channel_;
    private readonly MainConfig config_;
    private readonly ChannelConfig fetcher_config_;
    private CommonOptions options_;

    public ArtifactService(MainConfig config, string channel_id) {
      config_ = config;
      fetcher_config_ = config_.GetChannelConfig(channel_id);
      RunSetup();
    }

    public ArtifactService(MainConfig config, ChannelConfig fetcher_config) {
      config_ = config;
      fetcher_config_ = fetcher_config;
      RunSetup();
    }

    private void RunSetup() {
      /* Setup database storage location */
      DatabaseService.SetDatabaseDirs(config_.db_path);
      SetChannel();
    }

    public async Task Start() {
      try {
        await channel_.Start();
      }
      catch (Exception e) {
        Console.WriteLine(e);
      }
    }

    public BaseChannel GetChannel() {
      return channel_;
    }

    private void SetChannel() {
      fetcher_config_.output.delta =
        $"{fetcher_config_.output.delta}{RUNTIME.ToString(config_.delta_format)}/";
      channel_ = fetcher_config_.type switch {
        "npm" => new Npm(config_, fetcher_config_),
        "nuget" => new Nuget(config_, fetcher_config_),
        "maven" => new Maven(config_, fetcher_config_),
        "git" => new Git(config_, fetcher_config_),
        _ => throw new ArgumentException("type")
      };
    }
  }
}
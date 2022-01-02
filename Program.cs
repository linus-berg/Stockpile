using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Stockpile.Channels;
using Stockpile.CLI;
using Stockpile.Config;
using Stockpile.Services;

namespace Stockpile {
  internal class Program {
    private const string DELTA_DIR_NAME = "{0}{1}";
    private static readonly DateTime RUNTIME = DateTime.UtcNow;
    private static string[] arguments;

    private static int Main(string[] args) {
      arguments = args;
      Parser.Default
        .ParseArguments<BlacklistOptions, StockpileOptions>(args)
        .MapResult(
          (BlacklistOptions options) => RunBlacklist(options),
          (StockpileOptions options) => RunStockpile(options),
          err => 1);
      return 0;
    }

    private static MainConfig GetConfigFile(string config) {
      if (!File.Exists(config)) throw new FileNotFoundException(config);
      return JsonSerializer.Deserialize<MainConfig>(File.ReadAllText(config));
    }

    private static int RunStockpile(StockpileOptions options) {
      MainConfig config = GetConfigFile(options.config);
      config.staging = options.staging || config.staging;
      List<ArtifactService> fetchers = config.channels
        .Select(fetcher => new ArtifactService(config, fetcher)).ToList();
      Task.WaitAll(fetchers.Select(a_s => a_s.Start()).ToArray());
      return 0;
    }

    private static int RunBlacklist(BlacklistOptions options) {
      MainConfig config = GetConfigFile(options.config);
      ArtifactService artifact_service = new(config, options.ChannelId);
      BaseChannel base_channel = artifact_service.GetChannel();
      return 0;
    }

    private static ArtifactService GetArtifactService(MainConfig config,
      string channel_id) {
      return new ArtifactService(config, channel_id);
    }
  }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using CommandLine;
using Stockpile.Channels;
using Stockpile.CLI;
using Stockpile.Config;
using Stockpile.Services;
using Stockpile.UI;

namespace Stockpile {
  internal class Program {
    private const string DELTA_DIR_NAME = "{0}{1}";
    private static readonly DateTime RUNTIME = DateTime.UtcNow;
    private static string[] arguments;

    private static int Main(string[] args) {
      arguments = args;
      Parser.Default
        .ParseArguments<ManagerOptions, StockpileOptions>(args)
        .MapResult(
          (ManagerOptions options) => RunWithUI(options),
          (StockpileOptions options) => Run(options),
          err => 1);
      return 0;
    }

    private static Main ReadConfigFile(string config) {
      if (!File.Exists(config)) throw new FileNotFoundException(config);
      return JsonSerializer.Deserialize<Main>(File.ReadAllText(config));
    }

    private static int Run(StockpileOptions options) {
      Main cfg = ReadConfigFile(options.config);
      cfg.staging = options.staging || cfg.staging;
      /* Setup database storage location */
      DatabaseService.SetDatabaseDirs(cfg.db_path);
      List<BaseChannel> fetchers = new();
      Task.WaitAll(cfg.fetchers
        .Select(cfg_fetcher => CreateChannelTask(cfg, cfg_fetcher)).ToArray());
      return 0;
    }

    private static int RunWithUI(ManagerOptions options) {
      AppBuilder.Configure<App>().UsePlatformDetect()
        .StartWithClassicDesktopLifetime(arguments);
      ManagerWindow mw = new();
      mw.Show();
      return 0;
    }

    private static async Task CreateChannelTask(Main cfg, Fetcher cfg_fetcher) {
      BaseChannel ch = GetChannel(cfg, cfg_fetcher);
      try {
        await ch.Start();
      }
      catch (Exception e) {
        Console.WriteLine(e);
      }
    }

    private static BaseChannel GetChannel(Main main_cfg, Fetcher cfg) {
      Output output = cfg.output;
      cfg.output.delta =
        $"{cfg.output.delta}{RUNTIME.ToString(main_cfg.delta_format)}/";

      return cfg.type switch {
        "npm" => new Npm(main_cfg, cfg),
        "nuget" => new Nuget(main_cfg, cfg),
        "maven" => new Maven(main_cfg, cfg),
        "git" => new Git(main_cfg, cfg),
        _ => throw new ArgumentException("type")
      };
    }
  }
}
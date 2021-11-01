using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Stockpile.Channels;
using Stockpile.Services;

namespace Stockpile {
  class Program {
    const string DELTA_DIR_NAME = "{0}{1}";
    static readonly DateTime RUNTIME = DateTime.UtcNow;

    static int Main(string[] args) {
      Parser.Default.ParseArguments<CLI.Options>(args).WithParsed(Run);
      return 0;
    }

    static Config.Main ReadConfigFile(string config) {
      if (!File.Exists(config)) {
        throw new FileNotFoundException(config);
      }
      return JsonSerializer.Deserialize<Config.Main>(File.ReadAllText(config));
    }

    static void Run(CLI.Options opt) {
      var cfg = ReadConfigFile(opt.config);
      cfg.staging = opt.staging || cfg.staging;
      cfg.progress_bars = opt.progress_bars;
      /* Setup database storage location */
      DatabaseService.SetDatabaseDirs(cfg.db_path);

      var fetchers = new List<BaseChannel>();
      var tasks = new List<Task>();
      foreach (var cfg_fetcher in cfg.fetchers) {
        tasks.Add(CreateChannelTask(cfg, cfg_fetcher));
      }
      Task.WaitAll(tasks.ToArray());
    }

    static async Task CreateChannelTask(Config.Main cfg, Config.Fetcher cfg_fetcher) {
      BaseChannel ch = GetChannel(cfg, cfg_fetcher);
      try {
        await ch.Start();
      } catch (Exception e) {
        Console.WriteLine(e);
      }
    }

    static BaseChannel GetChannel(Config.Main main_cfg, Config.Fetcher cfg) {
      Config.Output output = cfg.output;
      cfg.output.delta = cfg.output.delta + RUNTIME.ToString(main_cfg.delta_format) + '/';

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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommandLine;
using Stockpile.Fetchers;

namespace Stockpile {
  class Program {
    const string CFG_PATH = "./Config.json";
    const string DELTA_DIR_NAME = "{0}{1}";
    const string DATE_FMT = "yyyyMMddHHmmssff";
    static readonly DateTime RUNTIME = DateTime.UtcNow;

    static int Main(string[] args) {
      Parser.Default.ParseArguments<CLI.Options>(args).WithParsed(Run);
      return 0;
    }

    static Config.Main ReadConfigFile() {
      if (!File.Exists(CFG_PATH)) {
        throw new FileNotFoundException("config.json");
      }
      return JsonSerializer.Deserialize<Config.Main>(File.ReadAllText(CFG_PATH));
    }

    static void Run(CLI.Options opt) {
      var cfg = ReadConfigFile();
      cfg.staging = opt.staging || cfg.staging;

      /* Setup database storage location */
      Database.SetDatabaseDir(cfg.db_path);

      var fetchers = new List<BaseFetcher>();
      var tasks = new List<Task>();
      foreach (var cfg_fetcher in cfg.fetchers) {
        tasks.Add(CreateFetcherTask(cfg, cfg_fetcher));
      }
      Task.WaitAll(tasks.ToArray());
    }

    static Task CreateFetcherTask(Config.Main cfg, Config.Fetcher cfg_fetcher) {
      var fetcher = GetFetcherType(cfg, cfg_fetcher);
      return Task.Run(() => {
        try {
          fetcher.Start();
        } catch (Exception e) {
          Console.WriteLine(e);
        }
      });
    }

    static BaseFetcher GetFetcherType(Config.Main main_cfg, Config.Fetcher cfg) {
      var output = cfg.output;
      cfg.output.delta = cfg.output.delta + RUNTIME.ToString(DATE_FMT) + '/';

      return cfg.type switch {
        "npm" => new Npm(main_cfg, cfg),
        "nuget" => new Nuget(main_cfg, cfg),
        "git" => new Git(main_cfg, cfg),
        _ => throw new ArgumentException("type")
      };
    }

    static string GetDeltaDir(string dir) {
      return dir + RUNTIME.ToString(DATE_FMT);
    }
  }
}

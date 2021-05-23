using System;
using System.IO;
using Stockpile.Fetchers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
namespace Stockpile {
  class Program {
    const string CFG_PATH = "./Config.json";
    const string DELTA_DIR_NAME = "{0}{1}";
    const string DATE_FMT = "yyyyMMddHHmmssff"; 
    const bool STAGING = true;
    static readonly DateTime RUNTIME = DateTime.UtcNow;
    static int Main(string[] args) {
      if (!File.Exists(CFG_PATH)) {
        Console.WriteLine("NO CONFIG FILE PROVIDED");
        return 99;
      }
      Console.WriteLine("Getting Nuget and NPM packages!");
      List<BaseFetcher> fetchers = new List<BaseFetcher>();
      Config.Main cfg = JsonSerializer.Deserialize<Config.Main>(File.ReadAllText(CFG_PATH));
      List<Task> tasks = new List<Task>(); 
      foreach(Config.Fetcher fetch_cfg in cfg.fetchers) {
        BaseFetcher fetcher = GetFetcherType(fetch_cfg);
        tasks.Add(Task.Run(() => {
          try {
            StartFetcher(fetcher, fetch_cfg);
          } catch(Exception e) {
            Console.WriteLine(e);
          }
        }));
      }
      Task.WaitAll(tasks.ToArray());
      return 0;
    }

    static BaseFetcher GetFetcherType(Config.Fetcher cfg) {
      Config.Output output = cfg.output;
      CreateTypeDirs(output.full, output.delta);
      return cfg.type switch {
        "npm" => new Npm(cfg, RUNTIME, STAGING),
        "nuget" => new Nuget(cfg, RUNTIME, STAGING),
        _ => throw new ArgumentException("type")
      };
    }
    
    static void CreateTypeDirs(string full, string delta) {
      CreateDir(full);
      CreateDir(GetDeltaDir(delta));
    }
    
    static string GetDeltaDir(string dir) {
      return dir + RUNTIME.ToString(DATE_FMT);
    }

    static void CreateDir(string directory) {
      if (!Directory.Exists(directory)) {
        Directory.CreateDirectory(directory);
      }
    }

    static string[] GetPackageList(string filename) {
      return File.ReadAllLines(filename);
    }

    static void StartFetcher(BaseFetcher fetcher, Config.Fetcher cfg) {
      string[] pkg_list = GetPackageList(cfg.input);
      int pkg_count = pkg_list.Length;
      foreach(string line in pkg_list) {
        fetcher.Get(line);
        fetcher.ProcessIds();
      }
    }
  }
}

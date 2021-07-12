﻿using System;
using System.IO;
using Stockpile.Fetchers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using CommandLine;

namespace Stockpile {
  class Program {
    const string CFG_PATH = "./Config.json";
    const string DELTA_DIR_NAME = "{0}{1}";
    const string DATE_FMT = "yyyyMMddHHmmssff"; 
    static readonly DateTime RUNTIME = DateTime.UtcNow;

    static int Main(string[] args) {
      Console.WriteLine("Getting Nuget and NPM packages!");
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
      Config.Main cfg = ReadConfigFile();
      cfg.staging = opt.staging || cfg.staging;

      /* Setup database storage location */
      Database.SetDatabaseDir(cfg.db_path);

      List<BaseFetcher> fetchers = new List<BaseFetcher>();
      List<Task> tasks = new List<Task>(); 
      foreach(Config.Fetcher cfg_fetcher in cfg.fetchers) {
        tasks.Add(CreateFetcherTask(cfg, cfg_fetcher));
      }
      Task.WaitAll(tasks.ToArray());
    }

    static Task CreateFetcherTask(Config.Main cfg, Config.Fetcher cfg_fetcher) {
      BaseFetcher fetcher = GetFetcherType(cfg, cfg_fetcher);
      return Task.Run(() => {
        try {
          StartFetcher(fetcher, cfg_fetcher);
        } catch(Exception e) {
          Console.WriteLine(e);
        }
      });
    }


    static BaseFetcher GetFetcherType(Config.Main main_cfg, Config.Fetcher cfg) {
      Config.Output output = cfg.output;
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

    static void StartFetcher(BaseFetcher fetcher, Config.Fetcher cfg) {
      string[] pkg_list = File.ReadAllLines(cfg.input);
      int pkg_count = pkg_list.Length;
      foreach(string line in pkg_list) {
        fetcher.Get(line);
      }
      fetcher.ProcessIds();
    }
  }
}

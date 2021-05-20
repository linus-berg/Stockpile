﻿using System;
using System.IO;
using System.Threading.Tasks;
using CloneX.Fetchers;
using ShellProgressBar;

namespace CloneX {
  class Program {

    const string DELTA_DIR = "./DELTA/{0}/{1}/";
    const string OUT_DIR = "./PACKAGES/{0}/";
    
    const string NUGET_ID = "NUGET";
    const string NPM_ID = "NPM";
    const bool STAGING = true;
    
    static string RUNTIME = DateTime.UtcNow.ToString("yyyyMMddHHmmssff");
    static ProgressBarOptions p_opt = new ProgressBarOptions {
      ProgressCharacter = '-',
      DisplayTimeInRealTime = false
    };

    static int Main(string[] args) {
      Console.WriteLine("Getting Nuget and NPM packages!");
      CreateTypeDirs(NPM_ID);
      CreateTypeDirs(NUGET_ID);
      try {
        GetPackages();
      } catch (Exception e) {
        Console.WriteLine(e.ToString());
      }
      return 0;
    }
    
    static void CreateTypeDirs(string type) {
      CreateDir(GetOutDir(type));
      CreateDir(GetDeltaDir(type));
    }
    
    static string GetOutDir(string type) {
      return string.Format(OUT_DIR, type);
    }

    static string GetDeltaDir(string type) {
      return string.Format(DELTA_DIR, type, RUNTIME);
    }


    static void CreateDir(string directory) {
      if (!Directory.Exists(directory)) {
        Directory.CreateDirectory(directory);
      }
    }

    static void GetPackages() {
      using ProgressBar main_bar = new ProgressBar(0,"", p_opt);
      Task[] tasks = new Task[1];
      tasks[0] = GetNuGetPackages("./NUGET.txt", main_bar);
      tasks[1] = GetNpmPackages("./NPM.txt", main_bar);
      Task.WaitAll(tasks);
    }

    static string[] GetPackageList(string filename) {
      return File.ReadAllLines(filename);
    }

    static async Task GetNuGetPackages(string filename, ProgressBar bar) {
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      Nuget nuget = new(GetOutDir(NUGET_ID), GetDeltaDir(NUGET_ID), bar, STAGING);
      foreach(string line in pkg_list) {
        await nuget.Get(line);
      }
    }
    static async Task GetNpmPackages(string filename, ProgressBar bar) {
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      CloneX.Fetchers.Npm npm = new(GetOutDir(NPM_ID), GetDeltaDir(NPM_ID), bar, STAGING);
      foreach(string line in pkg_list) {
        await npm.Get(line);
        npm.ProcessAllTarballs();
      }
    }
  }
}

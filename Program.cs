using System;
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
    
    static string RUNTIME = DateTime.UtcNow.ToString("yyyyMMddHHmmssff");
    static ProgressBarOptions p_opt = new ProgressBarOptions {
      ProgressCharacter = '-',
      DisplayTimeInRealTime = false
    };
    static int Main(string[] args) {
      Console.WriteLine("Getting Nuget and NPM packages!");
      CreateTypeDirs(NPM_ID);
      CreateTypeDirs(NUGET_ID);
      GetPackages();
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
      using var main_bar = new ProgressBar(0,"", p_opt);
      Task[] tasks = new Task[2];
      tasks[0] = GetNuGetPackages("./NUGET.txt", main_bar);
      tasks[1] = GetNpmPackages("./NPM.txt", main_bar);
      Task.WaitAll(tasks);
    }

    static string[] GetPackageList(string filename) {
      return File.ReadAllLines(filename);
    }

    static async Task GetNuGetPackages(string filename, ProgressBar bar) {
      Nuget nuget = new(GetOutDir(NUGET_ID), GetDeltaDir(NUGET_ID));
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      using var ch = bar.Spawn(pkg_count, "NuGet Progress", p_opt);
      bar.MaxTicks = bar.MaxTicks + pkg_count;
      foreach(string line in pkg_list) {
        await nuget.Get(line);
        ch.Tick();
      }
    }
    static async Task GetNpmPackages(string filename, ProgressBar bar) {
      CloneX.Fetchers.Npm npm = new(GetOutDir(NPM_ID), GetDeltaDir(NPM_ID));
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      using var ch = bar.Spawn(pkg_count, "NPM Progress", p_opt);
      bar.MaxTicks = bar.MaxTicks + pkg_count;
      foreach(string line in pkg_list) {
        await npm.Get(line);
        ch.Tick();
      }
    }
  }
}

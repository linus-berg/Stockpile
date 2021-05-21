using System;
using System.IO;
using CloneX.Fetchers;
using System.Threading.Tasks;

namespace CloneX {
  class Program {

    const string DELTA_DIR = "./DELTA/{0}/{1}/";
    const string OUT_DIR = "./PACKAGES/{0}/";
    
    const string NUGET_ID = "NUGET";
    const string NPM_ID = "NPM";
    const bool STAGING = true;
    
    static string RUNTIME = DateTime.UtcNow.ToString("yyyyMMddHHmmssff");
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
      Task[] tasks = new Task[2];
      tasks[0] = Task.Run(() => GetNuGetPackages("./NUGET.txt"));
      tasks[1] = Task.Run(() => GetNpmPackages("./NPM.txt"));
      Task.WaitAll(tasks);
    }

    static string[] GetPackageList(string filename) {
      return File.ReadAllLines(filename);
    }

    static void GetNuGetPackages(string filename) {
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      Nuget nuget = new(GetOutDir(NUGET_ID), GetDeltaDir(NUGET_ID), STAGING);
      foreach(string line in pkg_list) {
        nuget.Get(line);
        nuget.ProcessIds();
      }
    }
    static void GetNpmPackages(string filename) {
      string[] pkg_list = GetPackageList(filename);
      int pkg_count = pkg_list.Length;
      CloneX.Fetchers.Npm npm = new(GetOutDir(NPM_ID), GetDeltaDir(NPM_ID), STAGING);
      foreach(string line in pkg_list) {
        npm.Get(line);
        npm.ProcessIds();
      }
    }
  }
}

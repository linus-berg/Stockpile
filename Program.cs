using System;
using System.IO;
using System.Threading.Tasks;
using CloneX.Fetchers;

namespace CloneX {
  class Program {

    const string DELTA_DIR = "./DELTA/{0}/{1}/";
    const string OUT_DIR = "./PACKAGES/{0}/";
    
    const string NUGET_ID = "NUGET";
    const string NPM_ID = "NPM";
    
    static string RUNTIME = DateTime.UtcNow.ToString("yyyyMMddHHmmssff");

    static async Task Main(string[] args) {
      Console.WriteLine("Getting Nuget and NPM packages!");
      CreateTypeDirs(NPM_ID);
      CreateTypeDirs(NUGET_ID);
      await GetPackages();
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

    static async Task GetPackages() {
      //await GetNuGetPackages("./NUGET.txt");
      await GetNpmPackages("./NPM.txt");
    }

    static async Task GetNuGetPackages(string file_path) {
      Nuget nuget = new(GetOutDir(NUGET_ID), GetDeltaDir(NUGET_ID));
      string[] f_txt = File.ReadAllLines(file_path);
      foreach(string line in f_txt) {
        await nuget.Get(line);
      }
    }
    static async Task GetNpmPackages(string file_path) {
      CloneX.Fetchers.Npm npm = new(GetOutDir(NPM_ID), GetDeltaDir(NPM_ID));
      string[] f_txt = File.ReadAllLines(file_path);
      foreach(string line in f_txt) {
        await npm.Get(line);
      }
    }
  }
}

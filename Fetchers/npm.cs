using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;
using ShellProgressBar;
using System;
namespace CloneX.Fetchers {
class Distribution {
  public string shasum {get; set;}
  public string tarball { get; set;}
}

class Manifest {
  public Dictionary<string, string> dependencies {get; set;}
  public Distribution dist {get; set;}
}

class Package {
  public Dictionary<string, Manifest> versions {get; set;}
}

public class Npm : BaseFetcher {
  public const string SYSTEM = "NPM";
  private const string REGISTRY = "https://registry.npmjs.org/";
  private readonly RestClient client_ = new RestClient(REGISTRY);
  public Npm(string out_dir, string delta_dir, ProgressBar pb, bool seeding = false) : base(out_dir, delta_dir, pb, SYSTEM, seeding) {
  }

  public override async Task Get(string id) {
    depth_++;
    Message(id, Status.CHECK);
    Package package = await GetPackage(id);
    this.Memorize(id);
    if (package == null || package.versions == null) {
      return;
    }
    this.AddPkgCount(package.versions.Count);
    foreach(var kv in package.versions) {
      Manifest manifest = kv.Value;
      if (manifest.dist.tarball != null) {
        try {
          Message(id + "@" + kv.Key, Status.FETCH);
          GetTarball(manifest.dist.tarball);
          this.Tick();
        } catch (Exception e) {
          Write(e.ToString());
        }
      }
      if (manifest.dependencies != null) {
        foreach(KeyValuePair<string, string> pkg in manifest.dependencies) {
          if (!this.InMemory(pkg.Key)) {
            await Get(pkg.Key);
          }
        }
      }
    }
    depth_--;
  }
  
  public void GetTarball(string url) {
    string filename = StripRegistry(url).Replace("/-/", "/");
    string out_file = this.GetOutPath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      this.AddBytes(out_file);
      return;
    }
    IRestRequest req = CreateRequest(url, DataFormat.None);
    using FileStream fs = File.OpenWrite(out_file);
    req.ResponseWriter = stream => {
      using (stream) {
        stream.CopyTo(fs);
      }
    };
    client_.DownloadData(req);
    fs.Close();
    this.AddBytes(out_file);
    this.CopyToDelta(filename);
  }
  
  private string StripRegistry(string url) {
    return url.Replace(REGISTRY, "");
  }

  private async Task<Package> GetPackage(string id) {
    return (await client_.GetAsync<Package>(CreateRequest($"{id}/"))); 
  }

  private IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
    return new RestRequest(url, fmt);
  }
}
}

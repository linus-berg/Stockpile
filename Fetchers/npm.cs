using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;
using ShellProgressBar;
using System;
namespace CloneX.Fetchers {

class Tarball {
  public string id {get; set;}
  public string url {get; set;}
}
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
  private List<Tarball> tarballs_;

  public Npm(string out_dir, string delta_dir,
             ProgressBar pb, bool seeding = false) : base(out_dir, delta_dir,
                                                          pb, SYSTEM, seeding) {
    this.tarballs_ = new List<Tarball>();
  }

  public override async Task Get(string id) {
    depth_++;
    Message(id, Status.CHECK);
    Package package = await GetPackage(id);
    this.Memorize(id);
    if (package == null) {
      WriteError($"{id} package metadata is null!");
      return;
    }
    await AddVersions(id, package);
    depth_--;
  }

  private async Task AddVersions(string id, Package package) {
    if (package.versions == null) {
      WriteError($"{id} package versions is null!");
      return;
    }
    /* For each version, add each version dependency (if not prev found)! */
    this.AddPkgCount(package.versions.Count);
    foreach(var kv in package.versions) {
      Manifest manifest = kv.Value;
      if (manifest.dist.tarball != null) {
        this.tarballs_.Add(new Tarball {
          id = id,
          url = manifest.dist.tarball
        });
      }
      if (manifest.dependencies == null) {
        continue;
      }
      foreach(KeyValuePair<string, string> pkg in manifest.dependencies) {
        if (!this.InMemory(pkg.Key)) {
          await Get(pkg.Key);
        }
      }
    }
  }
  
  public void ProcessAllTarballs() {
    Parallel.ForEach(this.tarballs_, po, (ball) => {
      try {
        Message(ball.url, Status.FETCH);
        GetTarball(ball);
        this.Tick();
      } catch (Exception e) {
        WriteError(e.ToString());
      }
    });
    this.tarballs_.Clear();
  }
  
  private void GetTarball(Tarball ball) {
    string filename = StripRegistry(ball.url).Replace("/-/", "/");
    string out_file = this.GetOutPath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      this.AddBytes(out_file);
      return;
    }
    IRestRequest req = CreateRequest(ball.url, DataFormat.None);
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
    try {
      return await client_.GetAsync<Package>(CreateRequest($"{id}/")); 
    } catch (Exception e) {
      WriteError(e.ToString());
      return null;
    }
  }

  private IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
    return new RestRequest(url, fmt);
  }
}
}

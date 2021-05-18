using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;

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
  const string REGISTRY = "https://registry.npmjs.org/";
  readonly RestClient client_ = new RestClient(REGISTRY);
  private HashSet<string> found_;
  private bool seeding_;
  
  public Npm(string out_dir, string delta_dir, bool seeding = false) : base(out_dir, delta_dir) {
    this.found_ = new(); 
    this.seeding_ = seeding;
  }


  public async Task Get(string id) {
    if (found_.Contains(id)) {
      return;
    }
    Package package = await GetPackage(id);
    found_.Add(id);
    foreach(var kv in package.versions) {
      Manifest manifest = kv.Value;
      if (manifest.dist.tarball != null) {
        GetTarball(manifest.dist.tarball);
      }
      if (manifest.dependencies != null) {
        foreach(KeyValuePair<string, string> pkg in manifest.dependencies) {
          await Get(pkg.Key);
        }
      }
    }
  }
  
  public void GetTarball(string url) {
    string file_path = StripRegistry(url).Replace("/-/", "/");
    string out_path = this.GetOutPath(file_path);
    string delta_path = this.GetDeltaPath(file_path);
    if (File.Exists(out_path)) {
      return;
    } else {
      Directory.CreateDirectory(Path.GetDirectoryName(out_path));
      Directory.CreateDirectory(Path.GetDirectoryName(delta_path));
    }

    IRestRequest req = CreateRequest(url, DataFormat.None);
    using var writer = File.OpenWrite(out_path);
    req.ResponseWriter = stream => {
      using (stream) {
        stream.CopyTo(writer);
      }
    };
    client_.DownloadData(req);
    writer.Close();
    if (!this.seeding_) {
      this.CopyToDelta(file_path);
    }
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

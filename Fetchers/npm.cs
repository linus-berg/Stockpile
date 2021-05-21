using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;
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

  public Npm(string out_dir, string delta_dir,
             DateTime runtime, bool seeding = false) : base(out_dir, delta_dir, SYSTEM, runtime, seeding) {
  }

  public override void Get(string id) {
    depth_++;
    SetStatus(id, Status.CHECK);
    Package pkg = GetPackage(id);
    /* Memorize to never visit this node again */
    this.Memorize(id);
    if (pkg == null || pkg.versions == null) {
      SetStatus(id, Status.ERROR);
      this.SetError(id);
    } else {
      AddTransient(id, pkg);
    }
    depth_--;
  }

  private void AddTransient(string id, Package pkg) {
    /* For each version, add each versions dependencies! */
    this.AddPkgCount(pkg.versions.Count);
    foreach(var kv in pkg.versions) {
      Manifest manifest = kv.Value;
      if (manifest.dependencies == null) {
        continue;
      }
      SetStatus($"{id}@{kv.Key} ({manifest.dependencies.Count})", Status.PARSE);
      foreach(KeyValuePair<string, string> p in manifest.dependencies) {
        if (!this.InMemory(p.Key)) {
          Get(p.Key);
        }
      }
    }
  }
  
  public void ProcessIds() {
    /* Parallel, max 5 concurrent fetchers */
    Parallel.ForEach(this.GetMemory(), po, (id) => {
      try {
        if (this.IsValid(id)) {
          ProcessVersions(id);
        }
      } catch (Exception) {
        // Processing error for {id}, for now ignore.
      }
    });
  }

  private void ProcessVersions(string id) {
    Package pkg = GetPackage(id);
    if (pkg == null || pkg.versions == null) {
      throw new ApplicationException($"{id} versions is null."); 
    }
    foreach(KeyValuePair<string, Manifest> kv in pkg.versions) {
      SetStatus($"{id}@{kv.Key}", Status.FETCH);
      TryGetTarball(id, kv.Value);
    }
  }

  private void TryGetTarball(string id, Manifest manifest) {
    try {
      if (manifest.dist.tarball == null) {
        throw new ArgumentNullException($"{id} tarball is null.");
      }
      this.GetTarball(manifest.dist.tarball);
    } catch (Exception) {
      // ignore
    }
  }
  
  private void GetTarball(string url) {
    string out_fp = this.GetOutFilePath(StripRegistry(url).Replace("/-/", "/"));
    this.CreateFilePath(out_fp);
    /* If not on disk and the download succeeded */
    if (!OnDisk(out_fp) && Download(url, out_fp)) {
      this.CopyToDelta(out_fp);
    }
    this.AddBytes(out_fp);
  }

  private bool Download(string url, string out_fp) {
    try {
      using FileStream fs = File.OpenWrite(out_fp);
      IRestRequest req = CreateRequest(url, DataFormat.None);
      req.ResponseWriter = stream => {
        using (stream) {
          stream.CopyTo(fs);
        }
      };
      client_.DownloadData(req);
      fs.Close();
    } catch (Exception) {
      return false;
    }
    return true;
  }
  

  private Package GetPackage(string id) {
    try {
      return client_.Get<Package>(CreateRequest($"{id}/")).Data; 
    } catch (Exception) {
      return null;
    }
  }

  private string StripRegistry(string url) {
    return url.Replace(REGISTRY, "");
  }

  private IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
    return new RestRequest(url, fmt);
  }
}
}

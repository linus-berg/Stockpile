using System.Threading.Tasks;
using System.Collections.Generic;
using Stockpile.Services;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using MavenNet;
using MavenNet.Models;
namespace Stockpile.Channels {
  class Maven : BaseChannel {
    private const string MAVEN_ = "https://repo1.maven.org/maven2";
    private record FileMap {
      public string postfix;
      public string ext;
    }
    private readonly FileMap[] FILE_MAPS = {
      new FileMap {
        postfix = "pom",
        ext =".pom"
      },
      new FileMap {
        postfix = "jar",
        ext =".jar"
      },
      new FileMap {
        postfix = "src",
        ext ="-sources.jar"
      },
      new FileMap {
        postfix = "doc",
        ext ="-javadoc.jar"
      },
    };

    private MavenCentralRepository repo_;
    public Maven(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
      repo_ = MavenRepository.FromMavenCentral();
    }

    protected override string GetFilePath(DBPackage pkg) {
      return pkg.url.Replace(MAVEN_ + "/", "");
    }

    public override async Task Get(string id) {
      Update(id, "INSPECT");
      Depth++;
      /* Memorize to not check again */
      ms_.Add(id);
      string[] parts = id.Split("::");
      string group_id = parts[0];
      string artifact_id = parts[1];
      Metadata metadata = await GetMetadata(group_id, artifact_id);
      if (metadata == null) {
        return;
      }

      foreach (string version in metadata.AllVersions) {
        Project artifact = await GetArtifact(group_id, artifact_id, version);
        if (artifact == null) {
          continue;
        }
        string base_url = $"{MAVEN_}/{group_id.Replace(".", "/")}/{artifact_id}/{version}";

        /* all the different names for fucking maven shit. */
        bool processed = false;
        foreach (FileMap fm in FILE_MAPS) {
          string fm_id = $"{id}::{fm.postfix}";
          string url = $"{base_url}/{artifact_id}-{version}{fm.ext}";
          if (IsProcessed(fm_id, version, url)) {
            processed = true;
            break;
          }
        }

        if (processed) {
          continue;
        }
        await ProcessDependencies(artifact.Dependencies);
        /* Set all filetypes as processed */
        foreach (FileMap fm in FILE_MAPS) {
          string fm_id = $"{id}::{fm.postfix}";
          db_.SetProcessed(fm_id, version);
        }
      }
      Depth--;
    }

    private async Task ProcessDependencies(List<Dependency> dependencies) {
      foreach (Dependency dep in dependencies) {
        string db_id = $"{dep.GroupId}::{dep.ArtifactId}";
        if (dep.GroupId.Contains("$") || dep.GroupId.Contains("{")) {
          continue;
        }
        if (!ms_.Exists(db_id)) {
          await Get(db_id);
        }
      }
    }


    public static Project ParsePOM(Stream stream) {
      Project result = null;
      var serializer = new XmlSerializer(typeof(Project));
      using (var sr = new StreamReader(stream))
        result = (Project)serializer.Deserialize(new XmlTextReader(sr) {
          Namespaces = false,
        });
      return result;
    }

    public static T Parse<T>(Stream stream) {
      T result = default(T);
      var serializer = new XmlSerializer(typeof(T));
      using (var sr = new StreamReader(stream))
        result = (T)serializer.Deserialize(sr);

      return result;
    }

    private async Task<Metadata> GetMetadata(string g, string id) {
      Metadata m = null;
      try {
        using Stream s = await repo_.OpenMavenMetadataFile(g, id);
        m = Parse<Metadata>(s);
      } catch { }
      return m;
    }

    private async Task<Project> GetArtifact(string g, string id, string v) {
      Project p = null;
      try {
        using Stream s = await repo_.OpenArtifactPomFile(g, id, v);
        p = ParsePOM(s);
      } catch { }
      return p;
    }
  }
}

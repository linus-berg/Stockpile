using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using MavenNet;
using MavenNet.Models;
using Stockpile.Config;
using Stockpile.Database;
using Stockpile.Services;
using Artifact = Stockpile.Database.Artifact;

namespace Stockpile.Channels {
  internal class Maven : BaseChannel {
    private const string MAVEN_ = "https://repo1.maven.org/maven2";

    private readonly FileMap[] FILE_MAPS = {
      new() {
        postfix = "pom",
        ext = ".pom"
      },
      new() {
        postfix = "jar",
        ext = ".jar"
      },
      new() {
        postfix = "src",
        ext = "-sources.jar"
      },
      new() {
        postfix = "doc",
        ext = "-javadoc.jar"
      }
    };

    private readonly MavenCentralRepository repo_;

    public Maven(Main main_cfg, Fetcher cfg) : base(main_cfg, cfg) {
      repo_ = MavenRepository.FromMavenCentral();
    }

    protected override string GetFilePath(ArtifactVersion version) {
      return version.Url.Replace(MAVEN_ + "/", "");
    }

    protected override async Task Get(string id) {
      Update(id, Operation.INSPECT);
      Depth++;
      /* Memorize to not check again */
      ms_.Add(id);
      string[] parts = id.Split("::");
      string group_id = parts[0];
      string artifact_id = parts[1];
      Metadata metadata = await GetMetadata(group_id, artifact_id);
      if (metadata == null) return;


      foreach (string version in metadata.AllVersions) {
        Project artifact = await GetArtifact(group_id, artifact_id, version);
        if (artifact == null) continue;
        string base_url =
          $"{MAVEN_}/{group_id.Replace(".", "/")}/{artifact_id}/{version}";

        /* all the different names for fucking maven shit. */
        bool processed = false;
        foreach (FileMap fm in FILE_MAPS) {
          string fm_id = $"{id}::{fm.postfix}";
          Artifact db_artifact = await db_.AddArtifact(fm_id);
          if (db_artifact.IsVersionProcessed(version)) {
            continue;
          }
          processed = true;
          break;
        }

        if (processed) continue;
        await ProcessDependencies(artifact.Dependencies);
        /* Set all filetypes as processed */
        foreach (FileMap fm in FILE_MAPS) {
          string fm_id = $"{id}::{fm.postfix}";
          Artifact db_artifact = await db_.GetArtifact(fm_id);
          string url = $"{base_url}/{artifact_id}-{version}{fm.ext}";
          db_artifact.SetVersionAsProcessed(fm_id, url);
        }
      }
      

      Depth--;
    }

    private async Task ProcessDependencies(List<Dependency> dependencies) {
      foreach (Dependency dep in dependencies) {
        string db_id = $"{dep.GroupId}::{dep.ArtifactId}";
        if (dep.GroupId.Contains("$") || dep.GroupId.Contains("{")) continue;
        if (!ms_.Exists(db_id)) await Get(db_id);
      }
    }


    public static Project ParsePOM(Stream stream) {
      Project result = null;
      XmlSerializer serializer = new(typeof(Project));
      using (StreamReader sr = new(stream)) {
        result = (Project) serializer.Deserialize(new XmlTextReader(sr) {
          Namespaces = false
        });
      }

      return result;
    }

    public static T Parse<T>(Stream stream) {
      T result = default;
      XmlSerializer serializer = new(typeof(T));
      using (StreamReader sr = new(stream)) {
        result = (T) serializer.Deserialize(sr);
      }

      return result;
    }

    private async Task<Metadata> GetMetadata(string g, string id) {
      Metadata m = null;
      try {
        using Stream s = await repo_.OpenMavenMetadataFile(g, id);
        m = Parse<Metadata>(s);
      }
      catch {
      }

      return m;
    }

    private async Task<Project> GetArtifact(string g, string id, string v) {
      Project p = null;
      try {
        using Stream s = await repo_.OpenArtifactPomFile(g, id, v);
        p = ParsePOM(s);
      }
      catch {
      }

      return p;
    }

    private record FileMap {
      public string ext;
      public string postfix;
    }
  }
}
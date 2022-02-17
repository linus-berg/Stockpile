using System;
using Stockpile.Constants;
using Stockpile.Infrastructure.Entities;

namespace Stockpile.Services {
  public class ConsoleDisplayService : IDisplayService {
    private readonly string id_;


    public ConsoleDisplayService(string id) {
      id_ = id;
    }

    public void Post(string msg, Operation op) {
      Console.WriteLine($"{GetPrefix(op)}->{msg}");
    }

    public void PostError(string msg) {
      Post(msg, Operation.ERROR);
    }

    public void PostWarning(string msg) {
      Post(msg, Operation.WARNING);
    }

    public void PostInfo(DisplayInfo info) {
      string msg = $"[{info.Packages}][{info.Versions}]";
      msg += $"[{info.CurrentTreeDepth}/{info.MaxTreeDepth}][{info.Message}]";
      Post(msg, info.Operation);
    }

    public void PostDownload(Artifact artifact, ArtifactVersion version, int c, int m) {
      Post($"[{artifact.Name}][{version.Version}][{c}/{m}]", Operation.DOWNLOAD);
    }

    ~ConsoleDisplayService() {
      Console.WriteLine($"[{id_}][FINISHED]");
    }

    private string GetPrefix(Operation op) {
      return $"[{id_}][{op.ToString()}]";
    }
  }
}
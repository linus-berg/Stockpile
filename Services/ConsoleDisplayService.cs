using System.Drawing;
using Console = Colorful.Console;
using Colorful;

namespace Stockpile.Services {
  public class ConsoleDisplayService : IDisplayService {
    private readonly string id_;
    
    private readonly StyleSheet SC_ = new StyleSheet(Color.Gray);

    public ConsoleDisplayService(string id) {
      id_ = id;
      SC_.AddStyle("npm", Color.Cyan);
      SC_.AddStyle("nuget", Color.Brown);
      SC_.AddStyle("maven", Color.Gold);
      SC_.AddStyle("git", Color.Lime);
      SC_.AddStyle(Operation.INSPECT.ToString(), Color.Coral);
      SC_.AddStyle(Operation.DOWNLOAD.ToString(), Color.GreenYellow);
      SC_.AddStyle(Operation.COMPLETED.ToString(), Color.Green);
      SC_.AddStyle(Operation.WARNING.ToString(), Color.Yellow);
      SC_.AddStyle(Operation.ERROR.ToString(), Color.Red);
    }

    ~ConsoleDisplayService() {
      Console.WriteLineStyled($"[{id_}][FINISHED]", SC_);
    }

    private string GetPrefix(Operation op) {
      return $"[{id_}][{op.ToString()}]";
    }

    public void Post(string msg, Operation op) {
      Console.WriteLineStyled($"{GetPrefix(op)}->{msg}", SC_);
    }

    public void PostError(string msg) {
      Post(msg, Operation.ERROR);
    }
    public void PostWarning(string msg) {
      Post(msg, Operation.WARNING);
    }

    public void PostInfo(DisplayInfo info) {
      string msg = $"[{info.Packages}][{info.Versions}]";
      msg += $"[{info.Depth}/{info.Max_Depth}][{info.Message}]";
      Post(msg, info.Operation);
    }

    public void PostDownload(string id, string v, int c, int m) {
      Post($"[{id}][{v}][{c}/{m}]", Operation.DOWNLOAD);
    }
  }
}

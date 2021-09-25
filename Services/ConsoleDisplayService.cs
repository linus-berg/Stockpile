using System;
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
      SC_.AddStyle("git", Color.Lime);
      SC_.AddStyle("INSPECT", Color.Coral);
      SC_.AddStyle("DOWNLOAD", Color.GreenYellow);
      SC_.AddStyle("COMPLETED", Color.Green);
    }

    ~ConsoleDisplayService() {
      Console.WriteLineStyled($"[{id_}][FINISHED]", SC_);
    }
    
    private string GetPrefix(DisplayInfo info) {
      var prefix = "";
      prefix += $"[{id_}][{info.Operation}]";
      prefix += $"[{info.Packages}][{info.Versions}]";
      prefix += $"[{info.Depth}/{info.Max_Depth}][{info.Message}]";
      return prefix;
    }

    private void Message(string msg) {
      Console.WriteLineStyled(msg, SC_);
    }

    public void Error(string msg) {
      Message(msg);
    }

    private static void Tick() {
    }

    public void Update(DisplayInfo info) {
      Message(GetPrefix(info));
    }

    public void UpdateChannel() {
    }

    public void UpdatePackage(string id, string v, int c, int m) {
      Message($"[{id_}][DOWNLOAD][{id}][{v}][{c}/{m}]");
    }

    public void SetChannelCount(int count) {
    }

    public void AddToCount(int count) {
    }
  }
}

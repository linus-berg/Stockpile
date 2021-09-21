using ShellProgressBar;
using System.Collections.Generic;

namespace Stockpile.Services {
  public class BarDisplayService : IDisplayService {
    
    private static readonly ProgressBarOptions bar_opts_ = new ProgressBarOptions {
      CollapseWhenFinished = true,
      ProgressCharacter = '─'
    };
    private readonly string id_;
    private static readonly IProgressBar main_bar_ = new ProgressBar(0, "Stockpiling...", bar_opts_);
    private readonly IProgressBar ch_bar_;
    private readonly Dictionary<string, IProgressBar> pkg_bars_;

    public BarDisplayService(string id) {
      id_ = id;
      ch_bar_ = main_bar_.Spawn(0, id_, bar_opts_);
      pkg_bars_ = new Dictionary<string, IProgressBar>();
    }

    ~BarDisplayService() {
      main_bar_.WriteLine($"[{id_}][FINISHED]");
      ch_bar_.Dispose();
    }
    
    private string GetPrefix(DisplayInfo info) {
      var prefix = "";
      prefix += $"{id_,-6}";
      prefix += $"Packages[{info.Packages}] Versions[{info.Versions}] ";
      prefix += $"Depth[{info.Depth}/{info.Max_Depth}] {info.Message}";
      return prefix;
    }
    

    private void Message(string msg) {
      ch_bar_.Message = msg;
    }

    public void Error(string msg) {
      main_bar_.WriteErrorLine(msg);
    }

    private static void Tick() {
      main_bar_.Tick();
    }

    public void Update(DisplayInfo info) {
      Message(GetPrefix(info));
    }

    public void UpdateChannel() {
      Tick();
      ch_bar_.Tick();
    }

    public void UpdatePackage(string id, string v, int c, int m) {
      if (!pkg_bars_.ContainsKey(id)) {
        pkg_bars_[id] = ch_bar_.Spawn(m, id, bar_opts_);
      }
      IProgressBar bar = pkg_bars_[id];
      bar.Tick($"[{id}][{v}][{c}/{m}]");
      if(c == m) {
        pkg_bars_.Remove(id);
      }
      UpdateChannel();
    }

    public void SetChannelCount(int count) {
      ch_bar_.MaxTicks = count;
    }

    public void AddToCount(int count) {
      main_bar_.MaxTicks = main_bar_.MaxTicks + count;
    }
  }
}

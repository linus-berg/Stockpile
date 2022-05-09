using System.IO;
using Stockpile.Config;

namespace Stockpile.Services {
  public class FileService {
    private readonly ChannelConfig ch_cfg_;
    private readonly MainConfig main_config_;

    public FileService(MainConfig main_config, ChannelConfig ch_cfg) {
      main_config_ = main_config;
      ch_cfg_ = ch_cfg;
    }

    public static void CreateDirectory(string file_path) {
      Directory.CreateDirectory(Path.GetDirectoryName(file_path));
    }

    public string GetMainFilePath(string filename) {
      return GetAbsolutePath(ch_cfg_.deposits.main, filename);
    }

    public string GetDeltaFilePath(string filepath) {
      return GetAbsolutePath(ch_cfg_.deposits.delta, filepath);
    }

    public static long GetSize(string path) {
      return new FileInfo(path).Length;
    }

    public static bool OnDisk(string path) {
      return File.Exists(path);
    }

    public static string GetAbsolutePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    public void CopyToDelta(string fp) {
      if (main_config_.staging) return;

      string out_fp = GetMainFilePath(fp);
      string delta_fp = GetDeltaFilePath(fp);
      CreateDirectory(delta_fp);
      File.CreateSymbolicLink(delta_fp, out_fp);
    }
  }
}

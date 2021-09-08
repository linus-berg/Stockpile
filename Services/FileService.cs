using System.IO;

namespace Stockpile.Services {
  public class FileService {
    private readonly Config.Main main_cfg_;
    private readonly Config.Fetcher ch_cfg_;

    public FileService(Config.Main main_cfg, Config.Fetcher ch_cfg) {
      main_cfg_ = main_cfg;
      ch_cfg_ = ch_cfg;
    }
    
    public static void CreateDirectory(string file_path) {
      Directory.CreateDirectory(Path.GetDirectoryName(file_path));
    }

    public string GetMainFilePath(string filename) {
      return GetAbsolutePath(ch_cfg_.output.full, filename);
    }

    public string GetDeltaFilePath(string filepath) {
      return GetAbsolutePath(ch_cfg_.output.delta, filepath);
    }
    
    public static bool OnDisk(string path) {
      return File.Exists(path);
    }
    
    public static string GetAbsolutePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }
    
    public void CopyToDelta(string fp) {
      if (!main_cfg_.staging) {
        string out_fp = GetMainFilePath(fp);
        string delta_fp = GetDeltaFilePath(fp);
        CreateDirectory(delta_fp);
        File.Copy(out_fp, delta_fp);
      }
    }
  }
}

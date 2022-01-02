﻿using System;
using System.Diagnostics;

namespace Stockpile.Channels {
  public class Message {
    public double bytes_delta;
    public double bytes_total;
    public int depth;
    public int packages;
    public int versions;
  }

  public class Utils {
    private const string PREFIX_ =
      "{0} - {8}, {1,-6} - [T/D={2:F2}/{3:F2}mb] Packages:{4, -5} Versions:{5, -5} Depth={6,-5}";

    private readonly DateTime START_TIME_;
    private readonly string SYSTEM_;

    public Utils(string system) {
      SYSTEM_ = system;
      START_TIME_ = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    public string GetPrefix(Message msg) {
      string prefix = "";
      prefix += $"{SYSTEM_,-6}";
      prefix += $"[T/D={msg.bytes_total:F2}/{msg.bytes_delta:F2}mb] ";
      prefix += $"Packages: {msg.packages,-5} Versions: {msg.versions,-5}";
      prefix += $"Dependency depth={msg.depth,-5}";
      return prefix;
    }
  }
}
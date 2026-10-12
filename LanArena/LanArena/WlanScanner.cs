using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LanArena;

public class WlanInfo
{
    public string Ssid = "", Auth = "", Encryption = "";
    public int Signal;
    public bool Connected;
}

/// <summary>Reads the available Wi-Fi networks through Windows' own "netsh wlan" command (no extra libraries needed).</summary>
public static class WlanScanner
{
    static string Netsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
            return o;
        }
        catch { return ""; }
    }

    static string CurrentSsid()
    {
        foreach (var raw in Netsh("wlan show interfaces").Split('\n'))
        {
            var m = Regex.Match(raw.TrimEnd('\r'), @"^\s*SSID\s*:\s*(.*)$");
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return "";
    }

    public static Task<List<WlanInfo>> ScanAsync() => Task.Run(() =>
    {
        var list = new List<WlanInfo>();
        string cur = CurrentSsid();
        WlanInfo w = null;
        int kv = 0;

        foreach (var raw in Netsh("wlan show networks mode=bssid").Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            var m = Regex.Match(line, @"^\s*SSID\s+\d+\s*:\s*(.*)$");
            if (m.Success)
            {
                w = new WlanInfo { Ssid = m.Groups[1].Value.Trim() };
                if (w.Ssid == "") w.Ssid = "(Hidden network)";
                w.Connected = cur != "" && w.Ssid == cur;
                list.Add(w);
                kv = 0;
                continue;
            }
            if (w == null) continue;
            int idx = line.IndexOf(':');
            if (idx < 0) continue;
            string val = line.Substring(idx + 1).Trim();

            if (line.TrimStart().StartsWith("BSSID", StringComparison.OrdinalIgnoreCase)) { kv = 99; continue; }
            if (kv < 3)
            {
                kv++;                       // 1 = network type, 2 = authentication, 3 = encryption (same order in every language)
                if (kv == 2) w.Auth = val;
                else if (kv == 3) w.Encryption = val;
                continue;
            }
            var sm = Regex.Match(val, @"^(\d+)\s*%$");
            if (sm.Success) w.Signal = Math.Max(w.Signal, int.Parse(sm.Groups[1].Value));
        }

        return list.OrderByDescending(x => x.Connected).ThenByDescending(x => x.Signal).ToList();
    });

    public static Task<string> ConnectAsync(string ssid) =>
        Task.Run(() => Netsh($"wlan connect name=\"{ssid}\"").Trim());
}

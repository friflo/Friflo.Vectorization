// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Utils;

internal static class OpenUrlUtils
{
    internal static bool OpenUrl(ReadOnlySpan<char> url)
    {
        var urlStr = url.ToString();

        // Validate that it is a valid absolute URI and uses safe protocols (http or https)
        if (Uri.TryCreate(urlStr, UriKind.Absolute, out Uri? uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            string safeUrl = uriResult.AbsoluteUri;
            try {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo { FileName = safeUrl, UseShellExecute = true });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start(new ProcessStartInfo { FileName = "open", Arguments = safeUrl, UseShellExecute = false });
                }
                else if (OperatingSystem.IsLinux())
                {
                    return OpenUrlLinux(safeUrl);
                }
                return true;
            }
            catch (Exception e) {
                Console.WriteLine($"OpenUrl failed: {e.Message}");
            }
        }
        return false;
    }
    
    private static bool OpenUrlLinux(string safeUrl)
    {
        bool isWsl = File.Exists("/proc/version") && 
                     File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);

        if (isWsl)
        {
            Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c start {safeUrl.Replace("&", "^&")}", UseShellExecute = false, CreateNoWindow = true });
        }
        else
        {
            string[] openers = { "xdg-open", "gio", "gnome-open", "kde-open" };
            bool opened = false;
            foreach (var opener in openers)
            {
                try {
                    Process.Start(new ProcessStartInfo { FileName = opener, Arguments = safeUrl, UseShellExecute = false });
                    opened = true;
                    break;
                } catch {}
            }
            if (!opened) return false;
        }
        return true;
    }
}
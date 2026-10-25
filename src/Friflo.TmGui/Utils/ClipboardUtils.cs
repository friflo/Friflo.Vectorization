// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Utils;

internal static class ClipboardUtils
{
    internal static void CopyToClipboard(ReadOnlySpan<char> text)
    {
        string textStr = text.ToString();

        if (OperatingSystem.IsWindows())
        {
            var psi = new ProcessStartInfo
            {
                FileName = "clip.exe",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                // Write text and explicitly close the stream so clip.exe knows input is done
                process.StandardInput.Write(textStr);
                process.StandardInput.Close(); 
                process.WaitForExit();
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pbcopy",
                RedirectStandardInput = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                process.StandardInput.Write(textStr);
                process.StandardInput.Close();
                process.WaitForExit();
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // Try Wayland first (wl-copy), then fallback to X11 (xclip)
            string[] tools = { "wl-copy", "xclip" };

            foreach (var tool in tools)
            {
                try
                {
                    var arguments = tool == "xclip" ? "-selection clipboard" : "";
                    var psi = new ProcessStartInfo
                    {
                        FileName = tool,
                        Arguments = arguments,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.StandardInput.Write(textStr);
                        process.StandardInput.Close(); // required: signal tool, input is ready
                        process.WaitForExit();
                        
                        if (process.ExitCode == 0) {
                            break;
                        }
                    }
                }
                catch
                {
                    // Tool not installed or failed, try next one
                }
            }
        }
    }
}
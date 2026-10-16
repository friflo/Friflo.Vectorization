// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Utils;

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
                process.StandardInput.Close(); // Wichtig: Signalisiert pbcopy, dass die Eingabe fertig ist
                process.WaitForExit();         // Wartet kurz, bis der Prozess sauber durchgelaufen ist
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux: Uses xclip via stdin
            var psi = new ProcessStartInfo
            {
                FileName = "xclip",
                Arguments = "-selection clipboard",
                RedirectStandardInput = true,
                UseShellExecute = false
            };
            try
            {
                using var process = Process.Start(psi);
                process?.StandardInput.Write(textStr);
            }
            catch
            {
                // Fallback or ignore if xclip is not installed
            }
        }
    }
}
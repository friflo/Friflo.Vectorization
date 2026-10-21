// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Win32;
using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Utils;


internal static class WindowSettingsUtils
{
    internal static readonly bool IsDarkMode = GetDarkMode();
    
    private static bool GetDarkMode()
    {
        if (OperatingSystem.IsWindows()) {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? value = key?.GetValue("AppsUseLightTheme");
                
                // 0 = Dark Mode, 1 = Light Mode
                if (value is int intValue) {
                    return intValue == 0;
                }
            } catch {
                // Fallback if registry cannot be read
            }
            return false;
        }
        
        if (OperatingSystem.IsMacOS()) {
            try {
                using var process = new Process();
                process.StartInfo.FileName = "defaults";
                process.StartInfo.Arguments = "read -g AppleInterfaceStyle";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(500); // Short timeout to avoid hanging
                
                // Exit code 0 means AppleInterfaceStyle is present (Dark Mode)
                return process.ExitCode == 0 && output.Contains("Dark", StringComparison.OrdinalIgnoreCase);
            } catch {
                // Fallback if defaults command fails
            }
            return false;
        }
        
        if (OperatingSystem.IsLinux()) {
            try {
                using var process = new Process();
                process.StartInfo.FileName = "gsettings";
                process.StartInfo.Arguments = "get org.gnome.desktop.interface color-scheme";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                
                if (process.Start()) {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(500);
                    
                    return process.ExitCode == 0 && output.Contains("prefer-dark", StringComparison.OrdinalIgnoreCase);
                }
            } catch {
                // Fallback if gsettings binary is missing or execution fails
            }
            return false;
        }

        return false; // Default fallback for unsupported platforms
    }
}
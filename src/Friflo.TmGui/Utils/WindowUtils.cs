// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Win32;
using System;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Utils;

internal static class WindowUtils
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
                // Fallback, cannot read registry
            }
            return false;
        }
        return false;
    }
}
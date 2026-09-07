// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.TUI.Terminal.Client;

public static class TerminalUtils
{
    /// <summary> Only required for Windows. VT100 always enabled on Linux/macOS. </summary>
    public static void EnableVT100()
    {
        if (OperatingSystem.IsWindows()) {
            EnableWindowsVt100();
        }
    }
    
    private static void EnableWindowsVt100()
    {
        const int   STD_OUTPUT_HANDLE = -11;
        const uint  ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(handle, out uint mode))
        {
            SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);
}

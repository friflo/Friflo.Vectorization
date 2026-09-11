// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.Client;



public static class TerminalUtils
{
    private const int STD_INPUT_HANDLE  = -10;
    private const int STD_OUTPUT_HANDLE = -11;

    private const uint ENABLE_LINE_INPUT                    = 0x0002;
    private const uint ENABLE_ECHO_INPUT                    = 0x0004;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT        = 0x0200; // Required for Arrow Keys
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING   = 0x0004;

    /// <summary> Configures the terminal for raw input and VT100 output. </summary>
    public static void EnableRawModeAndVT100()
    {
        if (OperatingSystem.IsWindows()) {
            EnableWindowsRawAndVt100();
        } else {
            // On Unix systems, line buffering is disabled via termios
            System.Diagnostics.Process.Start("stty", "-echo raw").WaitForExit();
        }
    }

    private static void EnableWindowsRawAndVt100()
    {
        // Enable VT100 Output
        IntPtr outHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(outHandle, out uint outMode)) {
            SetConsoleMode(outHandle, outMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }

        // Disable Line Input & Echo, enable VT100 Input for arrow keys
        IntPtr inHandle = GetStdHandle(STD_INPUT_HANDLE);
        if (GetConsoleMode(inHandle, out uint inMode)) {
            inMode &= ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT);
            inMode |= ENABLE_VIRTUAL_TERMINAL_INPUT; // Pass raw escape sequences to stdin
            SetConsoleMode(inHandle, inMode);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);
}
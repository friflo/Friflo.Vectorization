// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.Client;

internal sealed class Win32ConsoleInputStream : Stream
{
#region Win32 Console Input
    private const int       STD_INPUT_HANDLE    = -10;
    private const int       STD_OUTPUT_HANDLE   = -11;

    private const uint      ENABLE_LINE_INPUT                   = 0x0002;
    private const uint      ENABLE_ECHO_INPUT                   = 0x0004;
    private const uint      ENABLE_MOUSE_INPUT                  = 0x0010;
    private const uint      ENABLE_WINDOW_INPUT                 = 0x0008;
    private const uint      ENABLE_VIRTUAL_TERMINAL_INPUT       = 0x0200;
    private const uint      ENABLE_VIRTUAL_TERMINAL_PROCESSING  = 0x0004;

    private const ushort    KEY_EVENT                   = 0x0001;
    private const ushort    MOUSE_EVENT                 = 0x0002;
    private const ushort    WINDOW_BUFFER_SIZE_EVENT    = 0x0004;

    private readonly IntPtr _inHandle;

    private static void EnableWindowsRawAndVt100()
    {
        IntPtr outHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(outHandle, out uint outMode)) {
            SetConsoleMode(outHandle, outMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }

        IntPtr inHandle = GetStdHandle(STD_INPUT_HANDLE);
        if (GetConsoleMode(inHandle, out uint inMode)) {
            inMode &= ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT);
            inMode |= ENABLE_VIRTUAL_TERMINAL_INPUT | ENABLE_MOUSE_INPUT | ENABLE_WINDOW_INPUT;
            SetConsoleMode(inHandle, inMode);
        }
    }
#endregion

    public Win32ConsoleInputStream()
    {
        _inHandle = GetStdHandle(STD_INPUT_HANDLE);
        EnableWindowsRawAndVt100();
    }

#region Stream
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) {
            return ValueTask.FromResult(0);
        }

        Span<INPUT_RECORD> records = stackalloc INPUT_RECORD[16];
        if (!ReadConsoleInput(_inHandle, ref records[0], (uint)records.Length, out uint numRead) || numRead == 0) {
            return ValueTask.FromResult(0);
        }

        Span<byte> target = buffer.Span;
        int bytesWritten = 0;

        for (int i = 0; i < numRead; i++) {
            ref readonly var record = ref records[i];

            switch (record.EventType) {
                case KEY_EVENT when record.KeyEvent.bKeyDown != 0: {
                    char ch = record.KeyEvent.UnicodeChar;
                    if (ch != '\0' && bytesWritten < target.Length) {
                        target[bytesWritten++] = (byte)ch;
                    }
                    break;
                }
                case MOUSE_EVENT: {
                    // Encode Mouse Event data into buffer stream
                    var mouse = record.MouseEvent;
                    if (bytesWritten + 4 <= target.Length) {
                        target[bytesWritten++] = (byte)'\x1b';
                        target[bytesWritten++] = (byte)'[';
                        target[bytesWritten++] = (byte)'M';
                        target[bytesWritten++] = (byte)(mouse.dwButtonState & 0xFF);
                    }
                    break;
                }
                case WINDOW_BUFFER_SIZE_EVENT: {
                    OnWindowBufferSizeEvent(record.WindowBufferSizeEvent.dwSize);
                    break;
                }
            }
        }
        return ValueTask.FromResult(bytesWritten);
    }

    private void OnWindowBufferSizeEvent(COORD newSize)
    {
        // Dummy handler for window resize events
    }
    

    // Stream base boilerplate overrides
    public override     bool    CanRead => true;
    public override     bool    CanSeek => false;
    public override     bool    CanWrite => false;
    public override     long    Length => throw new NotSupportedException();
    public override     long    Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override     void    Flush() { }
    public override     int     Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override     long    Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override     void    SetLength(long value) => throw new NotSupportedException();
    public override     void    Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    #endregion
    
    
#region P/Invoke Definitions

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ReadConsoleInput(IntPtr hConsoleInput, ref INPUT_RECORD lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
        [FieldOffset(4)] public MOUSE_EVENT_RECORD MouseEvent;
        [FieldOffset(4)] public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSE_EVENT_RECORD
    {
        public COORD dwMousePosition;
        public uint dwButtonState;
        public uint dwControlKeyState;
        public uint dwEventFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOW_BUFFER_SIZE_RECORD
    {
        public COORD dwSize;
    }

#endregion
}
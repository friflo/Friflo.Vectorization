// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedMember.Local
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.Client;


internal sealed class Win32ConsoleInputStream : Stream
{
    private readonly struct Chunk
    {
        public readonly     byte[]  Buffer;
        public readonly     int     Length;

        public Chunk(byte[] buffer, int length) {
            Buffer = buffer;
            Length = length;
        }
        
        public void Return()
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }

    private readonly    IntPtr                  _inHandle;
    private readonly    Channel<Chunk>          _chunkChannel;
    private readonly    CancellationTokenSource _cts;
    private             Chunk                   _pendingChunk;
    private             int                     _pendingOffset;
    private             bool                    _isDisposed;

    internal Win32ConsoleInputStream()
    {
        _inHandle = GetStdHandle(STD_INPUT_HANDLE);
        EnableWindowsRawAndVt100();

        _cts = new CancellationTokenSource();
        var options = new UnboundedChannelOptions {
            SingleWriter = true,
            SingleReader = true
        };
        _chunkChannel = Channel.CreateUnbounded<Chunk>(options);

        var thread = new Thread(InputLoop) {
            IsBackground = true,
            Name = "Win32ConsoleInputWorker"
        };
        thread.Start();
    }

    private void InputLoop()
    {
        var records = new INPUT_RECORD[16];
        var writer = _chunkChannel.Writer;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        int writePos = 0;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (!ReadConsoleInput(_inHandle, records, (uint)records.Length, out uint numRead) || numRead == 0) {
                    continue;
                }

                for (int i = 0; i < numRead; i++)
                {
                    ref readonly var record = ref records[i];

                    switch (record.EventType) {
                        case EventType.KEY_EVENT when record.KeyEvent.bKeyDown != 0: {
                            char ch = record.KeyEvent.UnicodeChar;
                            if (ch != '\0') {
                                EnsureCapacity(ref buffer, writePos, 1);
                                buffer[writePos++] = (byte)ch;
                            }
                            break;
                        }
                        case EventType.WINDOW_BUFFER_SIZE_EVENT: {
                            var size = record.WindowBufferSizeEvent.dwSize;
                            EnsureCapacity(ref buffer, writePos, 16);
                            writePos += WriteVt100WindowSizeReport(buffer.AsSpan(writePos), size.X, size.Y);
                            break;
                        }
                        // mouse events are already handled by ENABLE_MOUSE_INPUT.
                        // case EventType.MOUSE_EVENT: { ... }  
                    }
                }

                if (writePos > 0) {
                    writer.TryWrite(new Chunk(buffer, writePos));
                    buffer = ArrayPool<byte>.Shared.Rent(256);
                    writePos = 0;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            writer.TryComplete();
        }
    }

    private static void EnsureCapacity(ref byte[] buffer, int currentPos, int required)
    {
        if (currentPos + required > buffer.Length) {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent((currentPos + required) * 2);
            buffer.AsSpan(0, currentPos).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }
    }

#region Stream
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (buffer.IsEmpty) {
            return 0;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var token = linkedCts.Token;

        var target = buffer;
        int bytesWritten = 0;

        // Drain pending chunk left over from previous read
        if (_pendingChunk.Buffer != null) {
            int remaining = _pendingChunk.Length - _pendingOffset;
            int toCopy = Math.Min(target.Length, remaining);

            _pendingChunk.Buffer.AsSpan(_pendingOffset, toCopy).CopyTo(target.Span);
            _pendingOffset += toCopy;
            bytesWritten += toCopy;

            if (_pendingOffset >= _pendingChunk.Length) {
                _pendingChunk.Return();
                _pendingChunk = default;
                _pendingOffset = 0;
            }

            if (bytesWritten == target.Length) {
                return bytesWritten;
            }
        }

        var reader = _chunkChannel.Reader;

        try
        {
            if (!await reader.WaitToReadAsync(token).ConfigureAwait(false)) {
                return bytesWritten;
            }

            while (bytesWritten < target.Length && reader.TryRead(out var chunk)) {
                int toCopy = Math.Min(target.Length - bytesWritten, chunk.Length);
                chunk.Buffer.AsSpan(0, toCopy).CopyTo(target.Span.Slice(bytesWritten));
                bytesWritten += toCopy;

                if (toCopy < chunk.Length) {
                    _pendingChunk = chunk;
                    _pendingOffset = toCopy;
                    break;
                }

                // return fully consumed chunk buffer back to pool
                chunk.Return();
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return bytesWritten;
        }

        return bytesWritten;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();
        _cts.Dispose();

        // ain remaining unread chunks to avoid leaking ArrayPool buffers
        if (_pendingChunk.Buffer != null) {
            _pendingChunk.Return();
            _pendingChunk = default;
        }

        while (_chunkChannel.Reader.TryRead(out var chunk)) {
            chunk.Return();
        }

        base.Dispose(disposing);
    }

    private static int WriteVt100WindowSizeReport(Span<byte> span, short width, short height)
    {
        int pos = 0;
        span[pos++] = (byte)'\x1b';
        span[pos++] = (byte)'[';
        span[pos++] = (byte)'8';
        span[pos++] = (byte)';';
        
        pos += WriteDecimalBytes(span.Slice(pos), height);
        span[pos++] = (byte)';';
        
        pos += WriteDecimalBytes(span.Slice(pos), width);
        span[pos++] = (byte)'t';
        
        return pos;
    }

    private static int WriteDecimalBytes(Span<byte> span, short value)
    {
        int pos = 0;
        if (value >= 10000) span[pos++] = (byte)('0' + (value / 10000 % 10));
        if (value >= 1000)  span[pos++] = (byte)('0' + (value / 1000 % 10));
        if (value >= 100)   span[pos++] = (byte)('0' + (value / 100 % 10));
        if (value >= 10)    span[pos++] = (byte)('0' + (value / 10 % 10));
        span[pos++] = (byte)('0' + (value % 10));
        return pos;
    }

    // Stream base boilerplate overrides
    public override     bool    CanRead => !_isDisposed;
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


#region Win32 Console setup
    private static uint _originalInMode;
    private static uint _originalOutMode;
    private static bool _modesSaved;
    
    private static void EnableWindowsRawAndVt100()
    {
        IntPtr outHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(outHandle, out uint outMode)) {
            _originalOutMode = outMode;
            SetConsoleMode(outHandle, outMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }

        IntPtr inHandle = GetStdHandle(STD_INPUT_HANDLE);
        if (GetConsoleMode(inHandle, out uint inMode)) {
            _originalInMode = inMode;
            inMode &= ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT);
            inMode |= ENABLE_VIRTUAL_TERMINAL_INPUT | ENABLE_MOUSE_INPUT | ENABLE_WINDOW_INPUT;
            SetConsoleMode(inHandle, inMode);
        }
        _modesSaved = true;
    }
    
    internal static void RestoreConsoleMode()
    {
        if (!_modesSaved) return;

        IntPtr outHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        SetConsoleMode(outHandle, _originalOutMode);

        IntPtr inHandle = GetStdHandle(STD_INPUT_HANDLE);
        SetConsoleMode(inHandle, _originalInMode);
    }
#endregion

#region P/Invoke Definitions / Win32 API

    private const int       STD_INPUT_HANDLE    = -10;
    private const int       STD_OUTPUT_HANDLE   = -11;

    private const uint      ENABLE_LINE_INPUT                   = 0x0002;
    private const uint      ENABLE_ECHO_INPUT                   = 0x0004;
    private const uint      ENABLE_MOUSE_INPUT                  = 0x0010;
    private const uint      ENABLE_WINDOW_INPUT                 = 0x0008;
    private const uint      ENABLE_VIRTUAL_TERMINAL_INPUT       = 0x0200;
    private const uint      ENABLE_VIRTUAL_TERMINAL_PROCESSING  = 0x0004;

    
    private enum EventType : ushort {
        KEY_EVENT                   = 0x0001,
        MOUSE_EVENT                 = 0x0002,
        WINDOW_BUFFER_SIZE_EVENT    = 0x0004,
        MENU_EVENT                  = 0x0008,
        FOCUS_EVENT                 = 0x0010,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public  short   X;
        public  short   Y;
    }

    [StructLayout(LayoutKind.Explicit, Size = 20)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)] public EventType                   EventType;
        [FieldOffset(4)] public KEY_EVENT_RECORD            KeyEvent;
        [FieldOffset(4)] public MOUSE_EVENT_RECORD          MouseEvent;
        [FieldOffset(4)] public WINDOW_BUFFER_SIZE_RECORD   WindowBufferSizeEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public  int     bKeyDown;
        public  ushort  wRepeatCount;
        public  ushort  wVirtualKeyCode;
        public  ushort  wVirtualScanCode;
        public  char    UnicodeChar;
        public  uint    dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSE_EVENT_RECORD
    {
        public  COORD   dwMousePosition;
        public  uint    dwButtonState;
        public  uint    dwControlKeyState;
        public  uint    dwEventFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOW_BUFFER_SIZE_RECORD
    {
        public  COORD   dwSize;
    }

#endregion
}
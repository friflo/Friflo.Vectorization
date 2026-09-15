// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Session;

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

internal sealed class AnsiConsoleIn : Stream
{
    private readonly struct Chunk
    {
        public readonly byte[] Buffer;
        public readonly int    Length;

        public Chunk(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }

        public void Return()
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }

    private readonly Channel<Chunk>          chunkChannel;
    private readonly CancellationTokenSource cts;
    private          Chunk                   pendingChunk;
    private          int                     pendingOffset;
    private          bool                    isDisposed;

    internal AnsiConsoleIn()
    {
        cts = new CancellationTokenSource();
        var options = new UnboundedChannelOptions {
            SingleWriter = true,
            SingleReader = true
        };
        chunkChannel = Channel.CreateUnbounded<Chunk>(options);

        var thread = new Thread(InputLoop) {
            IsBackground = true,
            Name = "MacOsConsoleInputWorker"
        };
        thread.Start();
    }

    private void InputLoop()
    {
        var writer = chunkChannel.Writer;

        try
        {
            while (!cts.IsCancellationRequested)
            {
                // Read single key without echoing it to console (blocks until a key is pressed)
                ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

                byte[] bytes = MapKeyToBytes(keyInfo);
                if (bytes.Length > 0)
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bytes.Length);
                    bytes.CopyTo(buffer, 0);
                    writer.TryWrite(new Chunk(buffer, bytes.Length));
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Thrown if stdin is redirected or closed
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static byte[] MapKeyToBytes(ConsoleKeyInfo keyInfo)
    {
        // Handle VT100 escape sequences for arrow keys and special keys
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:    return "\x1b[A"u8.ToArray();
            case ConsoleKey.DownArrow:  return "\x1b[B"u8.ToArray();
            case ConsoleKey.RightArrow: return "\x1b[C"u8.ToArray();
            case ConsoleKey.LeftArrow:  return "\x1b[D"u8.ToArray();
            case ConsoleKey.Home:       return "\x1b[H"u8.ToArray();
            case ConsoleKey.End:        return "\x1b[F"u8.ToArray();
            case ConsoleKey.Tab:        return "\t"u8.ToArray();
            case ConsoleKey.Enter:      return "\r"u8.ToArray();
            case ConsoleKey.Backspace:  return "\x7f"u8.ToArray();
            case ConsoleKey.Escape:     return "\x1b"u8.ToArray();
            default:
                if (keyInfo.KeyChar != '\0')
                {
                    return Encoding.UTF8.GetBytes(new[] { keyInfo.KeyChar });
                }
                return Array.Empty<byte>();
        }
    }

#region Stream
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (buffer.IsEmpty) {
            return 0;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
        var token = linkedCts.Token;

        var target = buffer;
        int bytesWritten = 0;

        if (pendingChunk.Buffer != null) {
            int remaining = pendingChunk.Length - pendingOffset;
            int toCopy = Math.Min(target.Length, remaining);

            pendingChunk.Buffer.AsSpan(pendingOffset, toCopy).CopyTo(target.Span);
            pendingOffset += toCopy;
            bytesWritten += toCopy;

            if (pendingOffset >= pendingChunk.Length) {
                pendingChunk.Return();
                pendingChunk = default;
                pendingOffset = 0;
            }

            if (bytesWritten == target.Length) {
                return bytesWritten;
            }
        }

        var reader = chunkChannel.Reader;

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
                    pendingChunk = chunk;
                    pendingOffset = toCopy;
                    break;
                }

                chunk.Return();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return bytesWritten;
        }

        return bytesWritten;
    }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed) return;
        isDisposed = true;

        cts.Cancel();
        cts.Dispose();

        // Restore main screen buffer
        Console.Write("\x1b[?1049l");
        Console.Out.Flush();

        if (pendingChunk.Buffer != null) {
            pendingChunk.Return();
            pendingChunk = default;
        }

        while (chunkChannel.Reader.TryRead(out var chunk)) {
            chunk.Return();
        }

        base.Dispose(disposing);
    }

    public override bool CanRead => !isDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int  Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
#endregion
}
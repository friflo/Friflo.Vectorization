// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable UseCollectionExpression
// ReSharper disable CheckNamespace
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
            Name = "AnsiConsoleInputWorker"
        };
        thread.Start();
    }

    private void InputLoop()
    {
        var writer = chunkChannel.Writer;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        int writePos = 0;

        try
        {
            Span<byte> scratch = stackalloc byte[4];
            
            while (!cts.IsCancellationRequested)
            {
                ConsoleKeyInfo keyInfo;
                try
                {
                    // Blocking read on Console.ReadKey() avoids high-CPU polling
                    keyInfo = Console.ReadKey(intercept: true);
                }
                catch (InvalidOperationException)
                {
                    // Handle stdin closure or redirection
                    break;
                }
                ReadOnlySpan<byte> keyBytes = MapKeyToBytes(keyInfo, scratch);
                if (!keyBytes.IsEmpty)
                {
                    EnsureCapacity(ref buffer, writePos, keyBytes.Length);
                    keyBytes.CopyTo(buffer.AsSpan(writePos));
                    writePos += keyBytes.Length;
                }

                if (writePos > 0)
                {
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
        if (currentPos + required > buffer.Length)
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent((currentPos + required) * 2);
            buffer.AsSpan(0, currentPos).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }
    }

    private static ReadOnlySpan<byte> MapKeyToBytes(ConsoleKeyInfo keyInfo, Span<byte> scratch)
    {
        // Convert special keys directly to UTF-8 ReadOnlySpan<byte> ANSI / VT100 sequence
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:    return "\x1b[A"u8;
            case ConsoleKey.DownArrow:  return "\x1b[B"u8;
            case ConsoleKey.RightArrow: return "\x1b[C"u8;
            case ConsoleKey.LeftArrow:  return "\x1b[D"u8;
            case ConsoleKey.Home:       return "\x1b[H"u8;
            case ConsoleKey.End:        return "\x1b[F"u8;
            case ConsoleKey.Tab:        return "\t"u8;
            case ConsoleKey.Enter:      return "\r"u8;
            case ConsoleKey.Backspace:  return "\x7f"u8;
            case ConsoleKey.Escape:     return "\x1b"u8;
            default:
                if (keyInfo.KeyChar != '\0')
                {
                    Span<char> charSpan = stackalloc char[1] { keyInfo.KeyChar };
                    int written = Encoding.UTF8.GetBytes(charSpan, scratch);
                    return scratch.Slice(0, written);
                }
                return ReadOnlySpan<byte>.Empty;
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

        // Drain pending chunk left over from previous read
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

                // Return fully consumed chunk buffer back to pool
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

        // Drain remaining unread chunks to avoid leaking ArrayPool buffers
        if (pendingChunk.Buffer != null) {
            pendingChunk.Return();
            pendingChunk = default;
        }

        while (chunkChannel.Reader.TryRead(out var chunk)) {
            chunk.Return();
        }

        base.Dispose(disposing);
    }

    // Stream base boilerplate overrides
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
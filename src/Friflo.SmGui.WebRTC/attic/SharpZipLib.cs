// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if USE_SharpZipLib

using System;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal partial class WebRtcClient
{
    private readonly Deflater               deflater = new (Deflater.DEFAULT_COMPRESSION, true);
    private readonly DeflaterOutputStream   compressor;
    
    private byte[] CompressMessageSharpZipLib(ReadOnlyMemory<byte> rawData, out int length)
    {
        if (!MemoryMarshal.TryGetArray(rawData, out ArraySegment<byte> segment)) {
            throw new InvalidOperationException();
        }
        
        MemoryMarshal.Write(lengthBuffer, rawData.Length);
        outputStream.Position = 0;
        outputStream.SetLength(0);
        outputStream.Write(lengthBuffer, 0, sizeof(int));
        
        compressor.Write(segment.Array!, 0, rawData.Length);
        compressor.Flush();

        byte[] buffer = outputStream.GetBuffer();
        length = checked((int)outputStream.Length);

        bool hasSyncTail =
            length >= 4 &&
            buffer[length - 4] == 0x00 &&
            buffer[length - 3] == 0x00 &&
            buffer[length - 2] == 0xFF &&
            buffer[length - 1] == 0xFF;

        if (!hasSyncTail) {
            // outputStream.Write(SyncFlushTail);
        }
        length = checked((int)outputStream.Length);
        
        // Console.WriteLine($"original: {rawData.Length}  compressed: {outputStream.Length}");
        return outputStream.GetBuffer();
    }
}

#endif

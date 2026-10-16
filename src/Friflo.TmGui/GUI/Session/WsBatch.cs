// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace

using System;

namespace Friflo.TmGui.Session;


internal class WsBatch : TmBatch, IHostAction
{
    internal WsBatch(TmGuiBackend backend, int maxVertices) : base(backend, maxVertices)
    {
    }

    protected internal override void InitBatch() { }
    
    internal void DrawCommandList()
    {
        EndBatch();
    }
    
    // --- IHostAction
    public void OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target)
    {
        var sb = StringBuilder();
        sb.Append("open-url");
        sb.Append('|');
        sb.Append(url);
        if (target.Length > 0) {
            sb.Append('|');
            sb.Append(target);
        }
        var command = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(command);
    }
    
    public void SetDisplayMode(TmDisplayMode mode) {
        var sb = StringBuilder();
        sb.Append("set-display-mode");
        sb.Append('|');
        sb.Append(mode.ToString());
        var command = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(command);
    }
    
    public void SetWindowTitle(ReadOnlySpan<char> title)
    {
        var sb = StringBuilder();
        sb.Append("set-title");
        sb.Append('|');
        sb.Append(title);
        var msg = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(msg);
    }
}
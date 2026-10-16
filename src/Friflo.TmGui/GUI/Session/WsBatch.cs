// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal class WsBatch : TmBatch
{
    internal WsBatch(TmGuiBackend backend, int maxVertices) : base(backend, maxVertices)
    {
    }

    protected internal override void InitBatch() { }
    
    protected internal override void SetWindowTitle(string title)
    {
        var sb = StringBuilder();
        sb.Append("set-title");
        sb.Append('|');
        sb.Append(title);
        var msg = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(msg);
    }
    
    internal void DrawCommandList()
    {
        EndBatch();
    }
}
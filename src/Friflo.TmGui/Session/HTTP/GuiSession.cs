// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;

namespace Friflo.TmGui.Session.HTTP;

internal sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    internal            IGuiView?       guiView;
    internal readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    
    
    internal GuiSession(TmClient client, IGuiAssets assets)
    {
        wsBackend   = new WsBackend(assets);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }

    public override void ProcessInput(ReadOnlySpan<byte> input)
    {
        throw new NotImplementedException("*********** TEST");
    }

    public override Memory<byte> IterateTui()
    {
        guiView!.RenderGui(wsBatch, 500, 300);
        return default;
    }
}
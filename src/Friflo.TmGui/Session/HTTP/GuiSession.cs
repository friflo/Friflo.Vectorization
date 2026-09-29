// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;
using System.Numerics;

namespace Friflo.TmGui.Session.HTTP;

internal sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    internal            IGuiView?       guiView;
    internal readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private             WsDrawCommand[] wsDrawList = [];
    
    
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

        var drawCommands = wsBatch.drawCommands;

        if (wsDrawList.Length < drawCommands.Count) {
            wsDrawList = new WsDrawCommand [drawCommands.Count];
        }
        for (int n = 0; n < drawCommands.Count; n++)
        {
            var cmd = drawCommands[n];
            wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
            };
        }
        return default;
    }
}

public struct WsDrawCommand
{
    public  ulong           zIndex;
    public  int             sequence;
    public  TmTexture       texture;
    public  MemoryView      vertexView;
    public  MemoryView      indexView;
    public  BlendState      blendState;
    public  Matrix4x4       projection;
    public  SamplerFilter   samplerFilter;
    public  RectVector2     scissor;
}
// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Recorder;
using Friflo.TmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal abstract class TmSession
{
    protected internal          IGuiView?       guiView;
    internal  readonly          TmClient        client;
    internal  readonly          SessionId       sessionId;
    internal                    bool            isDirty;
    
    public    override          string          ToString()          => $"session: {sessionId}";
    
    protected internal abstract TmGuiBackend    Backend             { get; }
    protected internal abstract TmBatch         Batch               { get; }
    internal           virtual  GuiReplay?      CreateReplay()      => null;
    internal           virtual  void            SendReplayCommands() { }
    
    internal           abstract void            ProcessInput(ReadOnlySpan<byte> input);
    internal           abstract TmSendMessage   IterateUI(in AssetResources resources);
    
    internal TmSession(TmClient client, SessionId sessionId) {
        this.client     = client;
        this.sessionId  = sessionId;
    }
}



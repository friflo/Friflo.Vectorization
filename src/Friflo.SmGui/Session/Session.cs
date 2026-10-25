// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.SmGui.Recorder;
using Friflo.SmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui;


internal abstract class SmSession
{
    protected internal          IGuiView?       guiView;
    internal  readonly          SmClient        client;
    internal  readonly          SessionKeys     keys;
    protected volatile          bool            isDirty;
    internal                    ViewSession?    viewSession;
    
    public    override          string          ToString()          => $"session: {keys.id}";
    
    protected internal abstract SmGuiBackend    Backend             { get; }
    protected internal abstract SmBatch         Batch               { get; }
    internal           virtual  GuiReplay?      CreateReplay()      => null;
    internal           virtual  void            SendReplayCommands() { }
    
    internal           abstract void            ProcessInput(ReadOnlySpan<byte> input);
    internal           abstract SmSendMessage   IterateUI(in AssetResources resources);
    
    internal SmSession(SmClient client, SessionKeys keys) {
        this.client = client;
        this.keys   = keys;
    }
    
    internal bool SetDirty()
    {
        if (isDirty) {
            return true;
        }
        isDirty = false;
        return false;
    }
}



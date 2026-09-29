// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;

namespace Friflo.TmGui.Session.HTTP;

internal sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    
    
    internal GuiSession(TmClient client)
    {
        this.client = client;
    }

    public override void ProcessInput(ReadOnlySpan<byte> input)
    {
        throw new NotImplementedException("*********** TEST");
    }

    public override Memory<byte> IterateTui()
    {
        throw new NotImplementedException("*********** TEST");
    }
}
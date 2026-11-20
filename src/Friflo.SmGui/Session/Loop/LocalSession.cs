// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


internal sealed class LocalSession : SmSession
{
    public LocalSession(SmClient client, SessionKeys keys) : base(client, keys) { }

    protected internal override SmGuiBackend    Backend => throw new NotImplementedException();
    protected internal override SmBatch         Batch   => throw new NotImplementedException();
    
    internal override           void            ProcessInput(ReadOnlySpan<byte> input) { }
    internal override           SmSendMessage   ProcessUI(in AssetResources resources) => default;
}

internal sealed class LocalClient : SmClient
{
    public LocalClient(SmSessionLoop loop) : base(loop) { }

    public override UiType UiType => UiType.NativeGui;
    
    protected internal override ValueTask<int>  SendAsync(SmSendMessage message, CancellationToken cancellationToken) => default;
    protected internal override int             Send(SmSendMessage message) => 0;
    protected internal override void            SendCommand(SmSendCommand command) { }

    public override void Dispose() { }
}


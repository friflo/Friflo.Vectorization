// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui;

public readonly struct SessionId
{
    public   readonly   Guid    value;
    internal readonly   string? str;

    public   override   string  ToString() => str ?? "none";
    
    internal SessionId(long id)
    {
        value   = Guid.Parse($"{id:D32}");
        str     = id.ToString();
    }
   
    private SessionId(Guid guid) {
        value   = guid;
        str     = value.ToString();
    }
    
    internal static SessionId FromSpan(ReadOnlySpan<char> sidSpan)
    {
        if (long.TryParse(sidSpan, out var sidInt)) {
            return new SessionId(sidInt);
        }
        if (Guid.TryParse(sidSpan, out Guid guid)) {
            return new SessionId(guid);
        }
        return default;
    }
}

public interface IGuiView
{
    void RenderGui(TmBatch batch, int targetWidth, int targetHeight);
}

public struct SessionInfo
{
    public  SessionId       sessionId;
    public  string[]        args;
    public  TmClient        client;
    public  TmGuiBackend    backend;
}

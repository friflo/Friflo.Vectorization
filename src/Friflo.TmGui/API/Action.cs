// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Utils;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public enum TmDisplayMode
{
    Windowed,
    Fullscreen,
}

public readonly ref struct TmAction : IHostAction
{
    private readonly    TmBatch batch;  //  8 bytes 
    
    internal TmAction(TmBatch batch)
    {
        this.batch = batch;
    }
    
    // --- IHostAction
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return batch.HostAction.OpenUrl(url, target);
    }

    public void SetDisplayMode(TmDisplayMode mode) {
        batch.HostAction.SetDisplayMode(mode);
    }

    public void SetWindowTitle(ReadOnlySpan<char> title) {
        batch.HostAction.SetWindowTitle(title);
    }

    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        batch.HostAction.CopyToClipboard(text);
    }
}

public interface IHostAction
{
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return OsUtils.OpenUrl(url);
    }
    
    public void SetDisplayMode(TmDisplayMode mode) { }
    
    public void SetWindowTitle(ReadOnlySpan<char> title) { }
    
    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        ClipboardUtils.CopyToClipboard(text);
    }
}


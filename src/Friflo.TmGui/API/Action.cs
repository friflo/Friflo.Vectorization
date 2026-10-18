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

public readonly ref struct TmAction : IClientAction
{
    private readonly    TmBatch batch;  //  8 bytes 
    
    internal TmAction(TmBatch batch)
    {
        this.batch = batch;
    }
    
    // --- IClientAction
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return batch.ClientAction.OpenUrl(url, target);
    }

    public void SetDisplayMode(TmDisplayMode mode) {
        batch.ClientAction.SetDisplayMode(mode);
    }

    public void SetWindowTitle(ReadOnlySpan<char> title) {
        batch.ClientAction.SetWindowTitle(title);
    }

    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        batch.ClientAction.CopyToClipboard(text);
    }
}

public interface IClientAction
{
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return OpenUrlUtils.OpenUrl(url);
    }
    
    public void SetDisplayMode(TmDisplayMode mode) { }
    
    public void SetWindowTitle(ReadOnlySpan<char> title) { }
    
    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        ClipboardUtils.CopyToClipboard(text);
    }
}


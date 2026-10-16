// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

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
    public void OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        batch.HostAction?.OpenUrl(url, target);
    }

    public void SetDisplayMode(TmDisplayMode mode) {
        batch.HostAction?.SetDisplayMode(mode);
    }

    public void SetWindowTitle(ReadOnlySpan<char> title) {
        batch.HostAction?.SetWindowTitle(title);
    }
}

public interface IHostAction
{
    public void OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        Process.Start(new ProcessStartInfo { FileName = url.ToString(), UseShellExecute = true });
    }
    
    public void SetDisplayMode(TmDisplayMode mode) { }
    
    public void SetWindowTitle(ReadOnlySpan<char> title) { }
}


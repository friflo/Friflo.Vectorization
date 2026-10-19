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

public interface IClientAction
{
    /// <summary>
    /// Opens the passed <paramref name="url"/> in a browser.<br/>
    /// In a browser GUI this method calls <c>window.open(url, target)</c>.
    /// </summary>
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


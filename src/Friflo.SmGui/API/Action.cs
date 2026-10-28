// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.SmGui.Utils;

// ReSharper disable EmptyConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui;


public enum SmDisplayMode
{
    Windowed,
    Fullscreen,
}

/// <summary>
/// Defines actions and commands that can be executed on a client interface.
/// </summary>
/// <remarks>
/// For a native application - e.g. based on SDL - you may create a custom implementation
/// and use it with <see cref="SmGuiBackend.SetClientActions"/>.
/// </remarks>
public abstract class ClientAction
{
    protected ClientAction() { }
    
    /// <summary>
    /// Opens the passed <paramref name="url"/> in a browser.<br/>
    /// In a browser GUI this method calls <c>window.open(url, target)</c>.
    /// </summary>
    public virtual bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return OpenUrlUtils.OpenUrl(url);
    }
    
    /// <summary>
    /// Sets the background color (GPU surface clear value) of a remote client.<br/>
    /// Has no effect on a native application (Vulkan, DX12, Metal). The application controls this color on its own.
    /// </summary>
    public virtual void SetClearColor(Color32 light, Color32 dark) { }
    
    public virtual void SetDisplayMode(SmDisplayMode mode) { }
    
    public virtual void SetWindowTitle(ReadOnlySpan<char> title) { }
    
    public virtual void CloseWindow() { }
    
    public virtual void CopyToClipboard(ReadOnlySpan<char> text)
    {
        ClipboardUtils.CopyToClipboard(text);
    }
}

internal sealed class NoopClientAction : ClientAction
{
    internal static readonly  NoopClientAction Instance = new();
    
    private         NoopClientAction() { }
    
    public override bool    OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) => false;
    public override void    SetClearColor(Color32 light, Color32 dark)  { }
    public override void    SetDisplayMode(SmDisplayMode mode)          { }
    public override void    SetWindowTitle(ReadOnlySpan<char> title)    { }
    public override void    CloseWindow()                               { }
    public override void    CopyToClipboard(ReadOnlySpan<char> text)    { }
}


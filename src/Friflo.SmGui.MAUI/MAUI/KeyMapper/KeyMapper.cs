// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;


// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


[Public]
public static class KeyMapperExtension
{
    [Public]
    public static MauiAppBuilder UseKeyHandler(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<SkiaGuiView, KeyMapper>();
        });
        return builder;
    }
}


internal sealed partial class KeyMapper
{
    private SkiaGuiView? Host => VirtualView as SkiaGuiView;

    // Sets IView.IsFocused on the SkiaView, which raises Focused/Unfocused.
    private void SetFocused(bool focused)
    {
        if (VirtualView is IView view && view.IsFocused != focused)
            view.IsFocused = focused;
    }
}


internal class HostKeyEventArgs : EventArgs
{
    internal    KeyCode Key { get; init; }

    /// <summary>Character produced by the key, if the platform provides one.</summary>
    internal    Rune?   Character { get; init; }

    internal    bool    IsDown  { get; init; }
    internal    bool    Shift   { get; init; }
    internal    bool    Ctrl    { get; init; }
    internal    bool    Alt     { get; init; }

    /// <summary>Set to true to stop the key from being passed on to the platform.</summary>
    internal    bool    Handled { get; set; }
}
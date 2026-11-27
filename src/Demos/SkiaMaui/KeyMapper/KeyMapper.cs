// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using Friflo.SmGui;
using SkiaMaui;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


public static class KeyMapperExtension
{
    public static MauiAppBuilder UseKeyHandler(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<SkiaView, KeyMapper>();
        });
        return builder;
    }
}


internal sealed partial class KeyMapper
{
    private SkiaView? Host => VirtualView as SkiaView;

    // Sets IView.IsFocused on the SkiaView, which raises Focused/Unfocused.
    private void SetFocused(bool focused)
    {
        if (VirtualView is IView view && view.IsFocused != focused)
            view.IsFocused = focused;
    }
}


public class HostKeyEventArgs : EventArgs
{
    
    public KeyCode Key { get; init; }

    /// <summary>Character produced by the key, if the platform provides one.</summary>
    public Rune? Character { get; init; }

    public bool IsDown { get; init; }
    public bool Shift { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }

    /// <summary>Set to true to stop the key from being passed on to the platform.</summary>
    public bool Handled { get; set; }
}
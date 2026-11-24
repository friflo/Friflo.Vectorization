// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.OpenTK;


public sealed class OpenTkClientAction : ClientAction
{
    private readonly NativeWindow window;

    public OpenTkClientAction(NativeWindow window)
    {
        this.window = window;
    }

    public override void SetDisplayMode(SmDisplayMode mode)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        switch (mode) {
            case SmDisplayMode.Fullscreen:
                if (window.WindowState != WindowState.Fullscreen) {
                    window.WindowState = WindowState.Fullscreen;
                }
                break;
            case SmDisplayMode.Windowed:
                if (window.WindowState == WindowState.Fullscreen) {
                    window.WindowState = WindowState.Normal;
                }
                break;
        }
    }

    public override void SetWindowTitle(ReadOnlySpan<char> value)
    {
        window.Title = value.ToString();
    }

    public override void CloseWindow()
    {
        window.Close();
    }
}

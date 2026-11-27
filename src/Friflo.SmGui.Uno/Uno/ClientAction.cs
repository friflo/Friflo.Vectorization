// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if TODO

using System;
using Microsoft.Xna.Framework;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Uno;

public sealed class UnoClientAction : ClientAction
{
    private readonly Game game;
    private readonly GraphicsDeviceManager graphics;

    public UnoClientAction(Game game, GraphicsDeviceManager graphics)
    {
        this.game = game;
        this.graphics = graphics;
    }

    public override void SetDisplayMode(SmDisplayMode mode)
    {
        game.GraphicsDevice.SetRenderTarget(null);
        switch (mode) {
            case SmDisplayMode.Fullscreen:
                if (!graphics.IsFullScreen) {
                    graphics.IsFullScreen = true;
                    graphics.ApplyChanges();
                }
                break;
            case SmDisplayMode.Windowed:
                if (graphics.IsFullScreen) {
                    graphics.IsFullScreen = false;
                    graphics.ApplyChanges();
                }
                break;
        }
    }

    public override void SetWindowTitle(ReadOnlySpan<char> value)
    {
        game.Window.Title = value.ToString();
    }

    public override void CloseWindow()
    {
        game.Exit();
    }
}

#endif

// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Rl = Raylib_cs.Raylib;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Raylib;

public sealed class RaylibClientAction : ClientAction
{
    public override void SetDisplayMode(SmDisplayMode mode)
    {
        switch (mode) {
            case SmDisplayMode.Fullscreen:
                if (!Rl.IsWindowFullscreen()) {
                    Rl.ToggleFullscreen();
                }
                break;
            case SmDisplayMode.Windowed:
                if (Rl.IsWindowFullscreen()) {
                    Rl.ToggleFullscreen();
                }
                break;
        }
    }

    public override void SetWindowTitle(ReadOnlySpan<char> value)
    {
        Rl.SetWindowTitle(value.ToString());
    }

    public override void CloseWindow()
    {
        Rl.CloseWindow();
    }
}
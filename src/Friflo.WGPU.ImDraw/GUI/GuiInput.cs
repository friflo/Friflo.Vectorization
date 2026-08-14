// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public enum ImEventType
{
    MouseMotion,
    MouseButtonUp,
    MouseButtonDown,
}

public struct ImEvent
{
    public ImEventType  type;
    public float        x;
    public float        y;
}

internal class GuiInput
{
    internal    bool    isMouseDown;
    internal    float   x;
    internal    float   y;
}

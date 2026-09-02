// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref partial struct GuiWidget
{
    internal void Spacer(float size)
    {
        var window      = Window;
        var spaceSize   = window.CurrentLayout.direction == LayoutDirection.Horizontal ? new Vector2(size, 0) : new Vector2(0, size);
        MoveCursor(spaceSize);
    }
    
   
    internal SpaceScope BeginSpace(Vector2 size, WidgetID id)
    {
        var window      = Window;
        var pos         = window.Cursor;
        var widgetState = WidgetState.None;
        var isFocused   = false;

        if (id.IsValid) {
            int parentHash  = window.GetCurrentScopeHash();
            int widgetId    = id.Resolve(parentHash);
            
            bool isHover    = window.IsHoverAtCapture(pos, size, draw);
            widgetState     = GetWidgetState(isHover, widgetId);
            isFocused       = RegisterFocusable(widgetId, pos, size);
        }
        MoveCursor(size);

        bool isFired = IsFired(widgetState, isFocused);
        return new SpaceScope(this, pos, size, isFired, isFocused, widgetState);
    }

    internal void EndSpace(in SpaceScope space)
    {
        if (!space.isFocused) return;
        DrawFocus(space.pos, space.size);
        Window.EnsureVisibleInScrollArea(space.pos, space.size);
    }
}
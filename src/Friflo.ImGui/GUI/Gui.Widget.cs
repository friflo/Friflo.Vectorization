// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct GuiWidget
{
    public  readonly    ImDraw          draw;           //  8 bytes
    public  readonly    GuiInput        input;          //  8 bytes
    private readonly    GuiState        guiState;       //  8 bytes
    private readonly    GuiStyle        currentStyle;   //  8 bytes
    
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public ref readonly GuiColors       Colors          { [DebuggerStepThrough] get => ref currentStyle.colors; }
    public ref readonly GuiSizes        Sizes           { [DebuggerStepThrough] get => ref currentStyle.sizes; }
    public              GuiWindow       Window          { [DebuggerStepThrough] get => guiState.window; }
    public              float           LineHeight      { [DebuggerStepThrough] get => draw.Font.lineHeight; }
    public              IFormatProvider FormatProvider  { [DebuggerStepThrough] get => draw.batch.formatProvider; }
    public              bool            IsSet           { [DebuggerStepThrough] get => currentStyle != null; }

    
    /// <summary> Clears and returns a cached <see cref="System.Text.StringBuilder"/> to prevent allocations. </summary>
    public              StringBuilder   StringBuilder() => draw.batch.StringBuilder();

    /// <summary> Registers a widget for keyboard/gamepad navigation.<br/> Keyboard: Tab and arrow keys (2D). </summary>
    /// <remarks> The frame a widget receives focus <see cref="GuiInput.JustNavigated"/> is set to true. </remarks>
    /// <returns>true if focused</returns>
    public bool RegisterFocusable(int widgetId, Vector2 pos, Vector2 size)
    {
        if (guiState.IsNewFrame) {
            return input.RegisterFocusable(Window, widgetId, pos, size);
        }
        return false;
    }
    
    public DragState GetDragState(bool isHover, int widgetId)
    {
        if (guiState.IsNewFrame) {
            return input.GetDragState(isHover, widgetId);    
        }
        return DragState.None;
    }
    
    public WidgetState GetWidgetState(bool isHover, int widgetId)
    {
        if (guiState.IsNewFrame) {
            return input.GetWidgetState(isHover, widgetId);    
        }
        return WidgetState.None;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFired(WidgetState widgetState, bool isFocused) {
        return widgetState == WidgetState.Clicked || (isFocused && input.IsSubmitFired);
    }
    
    public StyleScope UseStyle(GuiStyle? style)
    {
        if (style is null) return default;
        PushStyle(style);
        return new StyleScope(this);
    }
    
    internal GuiWidget(ImDraw draw, ImBatch batch) {
        this.draw       = draw;
        input           = batch.input;
        guiState        = batch.guiState;
        guiState.SetFrameCount(input.FrameCount);
        currentStyle    = guiState.currentStyle;
    }
#region Window
    internal WindowScope BeginWindow(string title, Vector2? pos, Vector2? size)
    {
        var host = draw.batch.host;
        if (!host.windows.TryGetValue(title, out guiState.window!)) {
            guiState.window = new GuiWindow(host, title) {
                bounds = new RectVector2(
                    pos  ?? new Vector2( 50,  50),
                    size ?? new Vector2(300, 200))
            };
            host.windows.Add(title, guiState.window);
            host.windowOrder.Add(guiState.window);
        }
        var window      = Window;
        
        // Hit test whole window
        bool isWindowHovered = !input.IsDragActive && window.IsHoverAt(window.Pos, window.Size, draw);

        // Focus window on click (WITHOUT capturing activeItem)
        if (isWindowHovered && input.IsMouseDown) {
            // Note: Moving window to front here ensures that subsequent child widgets 
            //       in this same frame pass the IsTopWindowAt() check and process clicks immediately.
            host.SetTopWindow(window);
        }
        var zindex = (uint)host.windowOrder.IndexOf(window) + 1;  // +1, so 0 is background;
        draw.PushZIndex(zindex);
        
        window.ResetScope();
        int parentHash = window.GetCurrentScopeHash();

        // Process window resize
        int resizeId 	= WidgetID.CombineHash(parentHash, "__resize".GetHashCode());
        bool isResizing = window.ProcessResize(this, resizeId);

        // Process title bar drag (strictly blocked while resizing)
        float titleBarHeight = LineHeight;
        var titleBarSize     = new Vector2(window.Size.X, titleBarHeight);
        int titleBarId       = WidgetID.CombineHash(parentHash, "__titlebar".GetHashCode());

        bool isTitleHover = !isResizing && window.IsHoverAt(window.Pos, titleBarSize, draw);
        var titleState    = GetDragState(isTitleHover, titleBarId);

        if (titleState == DragState.Down) {
            window.bounds = new RectVector2(window.Pos + input.MousePosDelta, window.Size);
        }

        // Render background & titlebar
        draw.FillRectRounded(window.Pos, window.Size, Sizes.CornerRadius, Colors.WindowColor, GuiSizes.CornerSegments);

        var headerColor = Colors.ButtonState(titleState);
        draw.FillRectRounded(window.Pos, titleBarSize, Sizes.CornerRadius, headerColor, GuiSizes.CornerSegments);

        var fontHeight = LineHeight;
        var textPos    = window.Pos + new Vector2(10f, (titleBarHeight - fontHeight) / 2f);
        draw.DrawText(title, textPos, Colors.TextColor);
        
        // --- Push content scissor rect (clips everything below titlebar) ---
        var titleOffset = new Vector2(0f, titleBarHeight);
        var innerSize   = Vector2.Max(Vector2.Zero, window.Size - titleOffset);
        var contentPos  = window.Pos + titleOffset + Sizes.WindowPadding.Min;
        var contentSize = Vector2.Max(Vector2.Zero, innerSize - Sizes.WindowPadding.Size);
        
        window.InitLayout(contentPos, contentSize);
        draw.PushScissor(window.Pos + titleOffset, innerSize);
        return new WindowScope(this, true);
    }
    
    internal void EndWindow()
    {
        draw.PopScissor();
        draw.PopZIndex();
        Window.ClearScope();
    }
#endregion


#region Widgets

    internal void Label(ReadOnlySpan<char> name, Color32 textColor)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = Colors.TextColor;
        
        var size = draw.DrawText(name, window.Cursor, textColor);
        
        MoveCursor(size);
    }
    
    internal void Spacer(float size)
    {
        var window      = Window;
        var spaceSize   = window.CurrentLayout.direction == LayoutDirection.Horizontal ? new Vector2(size, 0) : new Vector2(0, size);
        MoveCursor(spaceSize);
    }
    
    internal bool Button(ReadOnlySpan<char> name, Dim size, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        using var _ = UseStyle(style);

        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var pos      = window.Cursor;
        var textSize = draw.MeasureText(name);

        // Calculate final pixel footprint based on measured text size as content fallback
        var defaultSize = textSize + Sizes.FramePadding.Size;
        var finalSize   = window.WidgetSize(size, defaultSize);

        var isHover     = window.IsHoverAtCursor(finalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, finalSize);
        var widgetState = GetWidgetState(isHover, widgetId);

        // Background
        draw.FillRectRounded(pos, finalSize, Sizes.CornerRadius, Colors.ButtonState(widgetState), GuiSizes.CornerSegments);

        if (isFocused) {
            DrawFocus(pos, finalSize);
            window.EnsureVisibleInScrollArea(pos, finalSize);
        }

        draw.DrawTextInRect(name, pos + Sizes.FramePadding.Min, textSize, TextAlignment.Center, VerticalAlignment.Middle, Colors.ButtonText);
        
        MoveCursor(finalSize);
        
        return IsFired(widgetState, isFocused);
    }
    
    internal bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style, WidgetID id)
    {
        var window  = Window;
        using var _ = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        var padding = Sizes.FramePadding;
        
        float boxSize   = LineHeight + padding.Vertical; // quadratic box
        var pos         = window.Cursor;
        var textSize    = draw.MeasureText(name);

        var totalSize   = new Vector2(boxSize + padding.Size.X + textSize.X, boxSize);
        var isHover     = window.IsHoverAtCursor(totalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, totalSize);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool isToggled  = IsFired(widgetState, isFocused);
        if (isToggled) {
            value = !value;
        }
        var boxRectSize = new Vector2(boxSize, boxSize);
        draw.FillRectRounded(pos, boxRectSize, Sizes.CornerRadius, Colors.ButtonState(widgetState), GuiSizes.CornerSegments); // background

        if (isFocused) {
            DrawFocus(pos, boxRectSize);
            window.EnsureVisibleInScrollArea(pos, boxRectSize);
        }
        if (value) {
            var fillOffset = new Vector2(8, 8);
            draw.FillRectRounded(pos + fillOffset, boxRectSize - 2 * fillOffset, Sizes.CornerRadius, Colors.TextColor, GuiSizes.CornerSegments);
        }
        var textPos = new Vector2(pos.X + boxSize + padding.Min.X, pos.Y + padding.Min.Y);
        draw.DrawText(name, textPos, Colors.TextColor);

        MoveCursor(totalSize);
        return isToggled;
    }
    
    internal bool Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, ReadOnlySpan<char> format, GuiStyle? style, WidgetID id)
    {
        var window      = Window;
        using var _     = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        var padding     = Sizes.FramePadding;
        float height    = LineHeight + padding.Vertical;
        var pos         = window.Cursor;
        var totalSize   = new Vector2(width, height);
        var isHover     = window.IsHoverAtCursor(totalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, totalSize);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool changed    = false;
        
        if (widgetState == WidgetState.Down) {
            float t = Math.Clamp((input.MousePos.X - pos.X) / width, 0f, 1f);
            float newValue = min + t * (max - min);
            
            if (newValue != value) {
                value = newValue;
                changed = true;
            }
        }
        draw.FillRectRounded(pos, totalSize, Sizes.CornerRadius, Colors.ButtonState(widgetState), GuiSizes.CornerSegments); // background

        // Fill bar
        float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var fillSize = new Vector2(width * tVal, height);
        
        draw.FillRectRounded(pos, fillSize, Sizes.CornerRadius, Colors.SliderFill, GuiSizes.CornerSegments);

        // Render blue focus outline
        if (isFocused) {
            DrawFocus(pos, totalSize);
            window.EnsureVisibleInScrollArea(pos, totalSize);
        }
        var labelText = StringBuilder().AppendFloat(value, format.IsEmpty ? "F1" : format, FormatProvider);
        draw.DrawTextInRect(labelText.Span(), pos, totalSize, TextAlignment.Center, VerticalAlignment.Middle, Colors.TextColor);

        MoveCursor(totalSize);
        return changed;
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
            
            bool isHover    = window.IsHoverAt(pos, size, draw);
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
    }

#endregion

    
#region Layout
    internal VerticalScope BeginVertical(Dim size)
    {
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Vertical, boundsSize);
        return new VerticalScope(this);
    }

    internal void EndVertical() => PopLayout();
    
    internal HorizontalScope BeginHorizontal(Dim size)
    {
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        return new HorizontalScope(this);
    }
    internal void EndHorizontal() => PopLayout();
    
    
    
    internal HorizontalCenterScope BeginHorizontalAligned(int centerId, float align, Dim size)
    {
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        var oldMouseOffset = input.mouseOffset;
        guiState.mouseOffsets.TryGetValue(centerId, out input.mouseOffset);
        
        BeginHorizontal(size);
        return new HorizontalCenterScope(this, centerId, align, draw.batch.vertexCount, oldMouseOffset);
    }
    
    internal void EndHorizontalAligned(in HorizontalCenterScope scope)
    {
        EndHorizontal();
        
        input.mouseOffset = scope.oldMouseOffset;
        var maxSize     = PopLayout();
        var availableWidth = Window.CurrentLayout.boundsSize.X;
        var offset = (availableWidth - maxSize.X) * scope.align;
        // var offset      = (Window.Size.X - Sizes.WindowPadding.Size.X - maxSize.X) * scope.align;
        var batch       = draw.batch;
        var vertices    = batch.vertexBuffer.Span.Slice(scope.vertexStart, batch.vertexCount);
        
        foreach (ref var vertex in vertices) {
            vertex.position.X += offset;
        }
        guiState.mouseOffsets[scope.centerId] = new Vector2(offset, 0);
    }

    #endregion


#region Styles
    internal StyleScope PushStyle(GuiStyle style)
    {
        var revertStyles = guiState.revertStyles;
        var length       = revertStyles.Length;
        if (guiState.revertStylesCount >= length) {
            revertStyles = new RevertStyle[Math.Max(4, 2 * length)]; 
            Array.Copy(guiState.revertStyles,  revertStyles, length);
            guiState.revertStyles = revertStyles; 
        }
        ref var revertStyle = ref revertStyles[guiState.revertStylesCount++];
        guiState.currentStyle.PushOverrides(style, ref revertStyle);
        return new StyleScope(this);
    }
    
    internal void PopStyle()
    {
        ref var revertStyle = ref guiState.revertStyles[--guiState.revertStylesCount];
        guiState.currentStyle.PopOverrides(revertStyle);
    }
#endregion

    public void DrawFocus(Vector2 pos, Vector2 size)
    {
        var margin = new Vector2(6, 6);
        draw.PushZIndexLocal(draw.ZIndexLocal + 1);
        draw.StrokeRectRounded(pos - margin, size + 2 * margin, 12, 4, Colors.FocusColor);
        draw.PopZIndex();
    }
    
    public void MoveCursor(Vector2 size)
    {
        ref var node = ref Window.CurrentLayoutRef;

        if (node.direction == LayoutDirection.Vertical) {
            if (size.X > node.maxSize.X) node.maxSize.X = size.X;
            node.maxSize.Y  = node.cursor.Y + size.Y - node.startCursor.Y;
            node.cursor.Y  += size.Y + Sizes.ItemSpacing.Y;
        } else {
            node.maxSize.X  = node.cursor.X + size.X - node.startCursor.X;
            if (size.Y > node.maxSize.Y) node.maxSize.Y = size.Y;
            node.cursor.X  += size.X + Sizes.ItemSpacing.X;
        }
    }
    
    internal void PushLayout(LayoutDirection direction, Vector2 boundsSize)
    {
        Window.PushLayout(direction, boundsSize);
    }

    internal Vector2 PopLayout()
    {
        var maxSize = Window.PopLayout();
        MoveCursor(maxSize);
        return maxSize;
    }
}


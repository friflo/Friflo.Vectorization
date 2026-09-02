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
namespace Friflo.ImGui2D;


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
        draw.StrokeRectRounded(pos - margin, size + 2 * margin, Sizes.FocusRadius, 4, Colors.FocusColor);
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


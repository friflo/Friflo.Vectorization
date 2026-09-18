// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
	internal ChildScope BeginChild(WidgetID childId, Dim size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var outerSize = window.WidgetSize(size, default);
	    if (size.IsBounded) {
	        draw.PushScissor(parentStartCursor, outerSize);
	    }
	    window.SetCursor(parentStartCursor + Sizes.ChildPadding.Min);
	    var innerLayoutSize	= outerSize - Sizes.ChildPadding.Size;
	    PushLayout(LayoutDirection.Vertical, innerLayoutSize);

	    return new ChildScope(this, new ChildEnd(parentStartCursor, outerSize, size));
	}

	internal void EndChild(in ChildScope scope)
	{
		var window = Window;
	    var padding = Sizes.ChildPadding;
	    var contentSize = PopLayout();

	    if (scope.end.requestedSize.IsBounded) {
	        draw.PopScissor();
	    }
	    window.PopScope();

	    var finalChildSize = new Vector2(
	        scope.end.requestedSize.IsAutoWidth  ? contentSize.X + padding.Size.X : scope.end.outerSize.X,
	        scope.end.requestedSize.IsAutoHeight ? contentSize.Y + padding.Size.Y : scope.end.outerSize.Y
	    );
	    window.SetCursor(scope.end.startCursor);
	    MoveCursor(finalChildSize);
	}
}


// --------------------------------------------- Step-Rendering ---------------------------------------------


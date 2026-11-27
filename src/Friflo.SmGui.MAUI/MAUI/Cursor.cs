// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Maui.Controls;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;

public static partial class MauiCursor
{
     public static void SetMouseCursor(SmGuiBackend backend, View targetView) => SetCursor(backend, targetView);
}


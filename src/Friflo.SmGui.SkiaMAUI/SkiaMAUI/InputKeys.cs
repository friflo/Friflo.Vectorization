// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


#if !WINDOWS

using Microsoft.Maui.Controls;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaMAUI;


public static class MauiInputKeys
{
    public static void Attach(View targetView, SmGuiBackend backend)
    {
        // No polling keyboard API available on non-Windows MAUI targets without platform handlers.
    }

    public static void Unhook()
    {
    }
}

#endif
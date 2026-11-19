// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Raylib_cs;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;

public class RaylibTexture : IDisposable
{
    public   readonly   Texture2D   native;
    internal readonly   byte[]      pixels;
    internal readonly   string      name;
    private             bool        disposed;
    
    internal            uint        Id  => native.Id;

    public   override   string      ToString() => $"{name} ({Id}) {native.Width} x {native.Height}";

    internal RaylibTexture(Texture2D native, byte[] pixels, string name)
    {
        this.native = native;
        this.pixels = pixels;
        this.name   = name;
    }

    public void Dispose()
    {
        if (disposed) {
            return;
        }
        disposed = true;

        if (native.Id != 0) {
            Rlgl.UnloadTexture(native.Id);
        }
        GC.SuppressFinalize(this);
    }
}
// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.OpenTK;

internal sealed class OpenTkBuffer<T> : SmBuffer<T> where T : unmanaged
{
    internal readonly int          Handle;
    private  readonly BufferTarget target;
    private  readonly T[]          array;
    private  readonly Memory<T>      memory;

    public override Memory<T> Memory => memory;

    public OpenTkBuffer(int handle, BufferTarget target, T[] array)
    {
        Handle = handle;
        this.target = target;
        this.array  = array;
        memory      = new Memory<T>(array);
    }

    public override void Dispose()
    {
        if (Handle != 0)
            GL.DeleteBuffer(Handle);
    }

    public override void Write(int start, int length)
    {
        int elementSize = Marshal.SizeOf<T>();
        IntPtr offset   = (IntPtr)(start * elementSize);
        IntPtr size     = (IntPtr)(length * elementSize);

        GL.BindBuffer(target, Handle);
        GL.BufferSubData(target, offset, size, ref array[start]);
        GL.BindBuffer(target, 0);
    }
}
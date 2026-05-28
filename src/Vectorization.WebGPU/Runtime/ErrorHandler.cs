// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Friflo.Vectorization.WebGPU.Runtime;

internal sealed class WgpuErrorHandler
{
    internal ErrorType   errorType = ErrorType.NoError;
    private  string      message;
    
    internal unsafe void OnGpuError(ErrorType type, StringView errorMessage, void* userData)
    {
        errorType   = type;
        message     = Marshal.PtrToStringUTF8((IntPtr)errorMessage.data, (int)errorMessage.length);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("--- [WEBGPU CRITICAL ERROR] ---");
        Console.Error.WriteLine($"Type: {type}");
        Console.Error.WriteLine($"Message: {message}");
        Console.Error.WriteLine("-------------------------------");
        Console.ResetColor();
        if (Debugger.IsAttached) Debugger.Break();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)][StackTraceHidden]
    internal void ThrowOnError()
    {
        if (errorType == ErrorType.NoError) {
            return;
        }
        ThrowException();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    internal void ThrowException()
    {
        var error = errorType;
        errorType = ErrorType.NoError;
        throw new WgpuException (error, message);
    }
}

public sealed class WgpuException : Exception
{
    public readonly ErrorType errorType;
    
    internal WgpuException (ErrorType errorType, string message) : base(message) {
        this.errorType = errorType;
    }
}
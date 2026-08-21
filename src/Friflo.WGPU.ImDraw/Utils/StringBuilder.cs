// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public static class StringBuilderExtensions
{
    extension (StringBuilder builder)
    {
        public ReadOnlySpan<char> Span()
        {
            var enumerator = builder.GetChunks();
            if (!enumerator.MoveNext()) {
                return default;
            }
            return enumerator.Current.Span;
        }
        
        public StringBuilder AppendFormat(float value, ReadOnlySpan<char> format)
        {
            Span<char> floatBuffer = stackalloc char[32];
            value.TryFormat(floatBuffer, out int charsWritten, format);
            builder.Append(floatBuffer[..charsWritten]); 
            return builder;
        }
    }   
}

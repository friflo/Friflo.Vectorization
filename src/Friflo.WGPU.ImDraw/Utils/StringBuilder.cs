// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Globalization;
using System.Text;

// ReSharper disable ReplaceSliceWithRangeIndexer
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
        
        /// <summary> Append the passed value formatted without allocation. </summary>
        public StringBuilder AppendFloat(float value, ReadOnlySpan<char> format, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            Span<char> buffer = stackalloc char[32];
            value.TryFormat(buffer, out var charsWritten, format, provider);
            builder.Append(buffer.Slice(0, charsWritten));
            return builder;
        }
        
        /// <summary> Append the passed value formatted without allocation. </summary>
        public StringBuilder AppendDouble(double value, ReadOnlySpan<char> format, IFormatProvider? provider = null)
        {
            provider ??= CultureInfo.InvariantCulture;
            Span<char> buffer = stackalloc char[32];
            value.TryFormat(buffer, out var charsWritten, format, provider);
            builder.Append(buffer.Slice(0, charsWritten));
            return builder;
        }
    }   
}

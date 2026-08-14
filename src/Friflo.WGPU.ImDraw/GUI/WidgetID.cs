// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable NotAccessedField.Local
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;



public readonly struct WidgetID
{
    private readonly int? customId;

    public WidgetID(int id)     => customId = id;
    public WidgetID(string? id) => customId = id != null ? ComputeHash(id.AsSpan()) : null;

    public static implicit operator WidgetID(int id)    => new(id);
    public static implicit operator WidgetID(string id) => new(id);

    public int Resolve(ReadOnlySpan<char> label, int parentHash)
    {
        int baseId = customId ?? ComputeHash(label);
        return CombineHash(parentHash, baseId);
    }

    private static int ComputeHash(ReadOnlySpan<char> text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    public static int CombineHash(int h1, int h2)
    {
        unchecked
        {
            uint hash = (uint)h1;
            hash = (hash ^ (uint)h2) * 16777619;
            return (int)hash;
        }
    }
}
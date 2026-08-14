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
    private readonly int        id;
    private readonly string?    name;

    public WidgetID(int id) {
        this.id = id;
    }

    public WidgetID(string name)
    {
        this.id = ComputeHash(name);
        this.name = name;
    }

    public static implicit operator WidgetID(int id)    => new(id);
    public static implicit operator WidgetID(string id) => new(id);

    public int Resolve(ReadOnlySpan<char> label)
    {
        return id != 0 ? id : ComputeHash(label);
    }
    
    private static int ComputeHash(ReadOnlySpan<char> text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in text) {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public enum SizeId
{
    WindowPadding,
    FramePadding,
    ItemSpacing,
    CellPadding,
    ContainerPadding
}



public struct GuiSizes
{
    public float        CornerRadius    => 8;
    public Padding2D    ChildPadding    => new(16, right: 16, 6, 6); // right should be greater TrackThickness
    public float        TrackThickness  => 12;
    
    
    public Padding2D    WindowPadding    { readonly get => windowPadding;    set => windowPadding    = Add(SizeId.WindowPadding,    value); }
    public Padding2D    FramePadding     { readonly get => framePadding;     set => framePadding     = Add(SizeId.FramePadding,     value); }
    public Vector2      ItemSpacing      { readonly get => itemSpacing;      set => itemSpacing      = Add(SizeId.ItemSpacing,      value); }
    public Padding2D    CellPadding      { readonly get => cellPadding;      set => cellPadding      = Add(SizeId.CellPadding,      value); }
    public Padding2D    ContainerPadding { readonly get => containerPadding; set => containerPadding = Add(SizeId.ContainerPadding, value); }

    public readonly             Bitset64<SizeId>    Overrides                   => overrides;
    public readonly override    string              ToString()                  => $"overrides: {overrides.Count}";

    public                      void                RemoveOverride(SizeId id)   => overrides.Remove(id);
    public readonly             bool                HasOverride   (SizeId id)   => overrides.Contains(id);
    public                      void                ClearOverrides()            => overrides = default;

#region internal
    [Browse(Never)] private     Padding2D   windowPadding;
    [Browse(Never)] private     Padding2D   framePadding;
    [Browse(Never)] private     Vector2     itemSpacing;
    [Browse(Never)] private     Padding2D   cellPadding;
    [Browse(Never)] private     Padding2D   containerPadding;

    [Browse(Never)] internal Bitset64<SizeId> overrides;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Padding2D Add(SizeId id, Padding2D value) 
    {
        overrides.value |= 1UL << Unsafe.As<SizeId, int>(ref id);
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2 Add(SizeId id, Vector2 value) 
    {
        overrides.value |= 1UL << Unsafe.As<SizeId, int>(ref id);
        return value;
    }

    internal static void ApplyOverrides(in GuiSizes source, ref GuiSizes target, Bitset64<SizeId> overrides)
    {
        foreach (var id in overrides)
        {
            switch (id) {
                case SizeId.WindowPadding:      target.windowPadding    = source.windowPadding;     break;
                case SizeId.FramePadding:       target.framePadding     = source.framePadding;      break;
                case SizeId.ItemSpacing:        target.itemSpacing      = source.itemSpacing;       break;
                case SizeId.CellPadding:        target.cellPadding      = source.cellPadding;       break;
                case SizeId.ContainerPadding:   target.containerPadding = source.containerPadding;  break;
            }
        }
    }
#endregion

    public void AddOverrides(in GuiSizes source)
    {
        foreach (var id in source.overrides)
        {
            switch (id) {
                case SizeId.WindowPadding:      WindowPadding       = source.windowPadding;     break;
                case SizeId.FramePadding:       FramePadding        = source.framePadding;      break;
                case SizeId.ItemSpacing:        ItemSpacing         = source.itemSpacing;       break;
                case SizeId.CellPadding:        CellPadding         = source.cellPadding;       break;
                case SizeId.ContainerPadding:   ContainerPadding    = source.containerPadding;  break;
            }
        }
    }
}

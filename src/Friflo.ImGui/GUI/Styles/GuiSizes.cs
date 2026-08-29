// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public enum PaddingId
{
    WindowPadding,
    FramePadding,
    ItemSpacing,
    CellPadding,
    ContainerPadding
}



public struct GuiSizes
{
    public float     CornerRadius => 8;
    
    
    public Padding2D WindowPadding    { readonly get => windowPadding;    set => windowPadding    = Add(PaddingId.WindowPadding,    value); }
    public Padding2D FramePadding     { readonly get => framePadding;     set => framePadding     = Add(PaddingId.FramePadding,     value); }
    public Padding2D ItemSpacing      { readonly get => itemSpacing;      set => itemSpacing      = Add(PaddingId.ItemSpacing,      value); }
    public Padding2D CellPadding      { readonly get => cellPadding;      set => cellPadding      = Add(PaddingId.CellPadding,      value); }
    public Padding2D ContainerPadding { readonly get => containerPadding; set => containerPadding = Add(PaddingId.ContainerPadding, value); }

    public readonly             Bitset64<PaddingId> Overrides                       => overrides;
    public readonly override    string              ToString()                      => $"overrides: {overrides.Count}";

    public                      void                RemoveOverride(PaddingId id)    => overrides.Remove(id);
    public readonly             bool                HasOverride(PaddingId id)       => overrides.Contains(id);
    public                      void                ClearOverrides()                => overrides = default;

#region internal
    [Browse(Never)] private     Padding2D   windowPadding;
    [Browse(Never)] private     Padding2D   framePadding;
    [Browse(Never)] private     Padding2D   itemSpacing;
    [Browse(Never)] private     Padding2D   cellPadding;
    [Browse(Never)] private     Padding2D   containerPadding;

    [Browse(Never)] internal Bitset64<PaddingId> overrides;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Padding2D Add(PaddingId id, Padding2D padding) 
    {
        overrides.value |= 1UL << Unsafe.As<PaddingId, int>(ref id);
        return padding;
    }

    internal static void ApplyOverrides(in GuiSizes source, ref GuiSizes target, Bitset64<PaddingId> overrides)
    {
        foreach (var id in overrides)
        {
            switch (id) {
                case PaddingId.WindowPadding:       target.windowPadding    = source.windowPadding;     break;
                case PaddingId.FramePadding:        target.framePadding     = source.framePadding;      break;
                case PaddingId.ItemSpacing:         target.itemSpacing      = source.itemSpacing;       break;
                case PaddingId.CellPadding:         target.cellPadding      = source.cellPadding;       break;
                case PaddingId.ContainerPadding:    target.containerPadding = source.containerPadding;  break;
            }
        }
    }
#endregion

    public void AddOverrides(in GuiSizes source)
    {
        foreach (var id in source.overrides)
        {
            switch (id) {
                case PaddingId.WindowPadding:       WindowPadding       = source.windowPadding;     break;
                case PaddingId.FramePadding:        FramePadding        = source.framePadding;      break;
                case PaddingId.ItemSpacing:         ItemSpacing         = source.itemSpacing;       break;
                case PaddingId.CellPadding:         CellPadding         = source.cellPadding;       break;
                case PaddingId.ContainerPadding:    ContainerPadding    = source.containerPadding;  break;
            }
        }
    }
}

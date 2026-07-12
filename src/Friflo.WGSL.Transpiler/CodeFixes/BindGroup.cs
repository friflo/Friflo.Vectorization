// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public static partial class CodeFixer
{
    private readonly struct BindGroup
    {
        public readonly int     group;
        public readonly int     binding;
        public readonly string  attribute;
        public readonly string  type;
        public readonly string  parameter;

        public override string ToString() => parameter;

        internal BindGroup(WgslBinding binding, string attribute, string type)
        {
            group           = binding.Group;
            this.binding    = binding.Binding;
            this.attribute  = attribute;
            this.type       = type;
            parameter       = binding.Name;
        }
    }
    
    private static void AppendParameters(StringBuilder sb, List<BindGroup> bindGroups)
    {
        if (bindGroups.Count == 0) return;

        int maxMapLength = 0;
        int maxAttrLength = 0;

        var mapStrings = new string[bindGroups.Count];
        for (int i = 0; i < bindGroups.Count; i++)
        {
            var bg = bindGroups[i];
            string mapStr = bg.binding >= 10 ? $"[Map({bg.group},{bg.binding})]" : $"[Map({bg.group}, {bg.binding})]";
            mapStrings[i] = mapStr;

            if (mapStr.Length > maxMapLength) maxMapLength = mapStr.Length;
            if (bg.attribute.Length > maxAttrLength) maxAttrLength = bg.attribute.Length;
        }

        int mapTargetWidth  = ((maxMapLength + 3) / 4) * 4;
        int attrTargetWidth = maxAttrLength + 4; 

        for (int i = 0; i < bindGroups.Count; i++)
        {
            var bg = bindGroups[i];
            string mapStr = mapStrings[i];

            sb.Append("        "); 

            // column 1: [Map]
            sb.Append(mapStr);
            sb.Append(' ', mapTargetWidth - mapStr.Length);

            // column 2: WGSL-attribute (e.g. [texture_2d(ST.f32)])
            sb.Append(bg.attribute);
            
            // dynamic padding
            sb.Append(' ', attrTargetWidth - bg.attribute.Length);

            // column 3 & 4: C#-Type and Name
            sb.Append(bg.type);
            sb.Append(' ');
            sb.Append(bg.parameter);
            sb.Append(",\n");
        }
    }
}
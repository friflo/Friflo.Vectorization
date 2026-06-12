// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Linq;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;


namespace Friflo.WGSL.Transpiler;


public static class WgslParser
{
    // --- Basic Parsers ---
    private static readonly TextParser<char> Whitespace = Character.WhiteSpace;
    private static readonly TextParser<Unit> SkipSpaces = Whitespace.Many().Value(Unit.Value);
    
    private static readonly TextParser<string> Identifier = 
        Character.Letter.Or(Character.EqualTo('_'))
            .Then(c => Character.LetterOrDigit.Or(Character.EqualTo('_')).Many().Select(cs => c + new string(cs)));

    private static readonly TextParser<int> IntNumber = Numerics.IntegerInt32;

    // Matches types like `f32`, `vec3<f32>`, or `array<MyStruct, 16>`
    private static readonly TextParser<string> WgslTypeParser = 
        Span.WithAll(c => char.IsLetterOrDigit(c) || c == '_' || c == '<' || c == '>' || c == ',' || char.IsWhiteSpace(c))
            .Select(s => s.ToString().Trim());

    // --- Struct Parser ---
    private static readonly TextParser<WgslField> FieldParser =
        from name in Identifier
        from _1 in SkipSpaces.Then(_ => Character.EqualTo(':')).Then(_ => SkipSpaces)
        from type in WgslTypeParser
        from _2 in SkipSpaces.Then(_ => Character.EqualTo(',').Or(Character.EqualTo(';'))).OptionalOrDefault()
        select new WgslField { Name = name, WgslType = type };

    private static readonly TextParser<WgslStruct> StructParser =
        from _1 in Span.EqualTo("struct").Then(_ => SkipSpaces)
        from name in Identifier
        from _2 in SkipSpaces.Then(_ => Character.EqualTo('{')).Then(_ => SkipSpaces)
        from fields in FieldParser.Between(SkipSpaces, SkipSpaces).Many()
        from _3 in SkipSpaces.Then(_ => Character.EqualTo('}'))
        select new WgslStruct { Name = name, Fields = fields.ToList() };

    // --- Binding Parser ---
    private static TextParser<int> AttributeIndex(string attrName) =>
        from _1 in Character.EqualTo('@').Then(_ => Span.EqualTo(attrName)).Then(_ => SkipSpaces)
        from _2 in Character.EqualTo('(').Then(_ => SkipSpaces)
        from val in IntNumber
        from _3 in SkipSpaces.Then(_ => Character.EqualTo(')'))
        select val;

    private static readonly TextParser<WgslBinding> BindingParser =
        from g in AttributeIndex("group").Between(SkipSpaces, SkipSpaces)
        from b in AttributeIndex("binding").Between(SkipSpaces, SkipSpaces)
        from _1 in Span.EqualTo("var")
        // Optional access mode like <storage, read_write>
        from access in Character.EqualTo('<')
            .Then(_ => Span.WithAll(c => c != '>'))
            .Then(a => Character.EqualTo('>').Value(a.ToString()))
            .OptionalOrDefault(string.Empty)
        from name in SkipSpaces.Then(_ => Identifier)
        from _2 in SkipSpaces.Then(_ => Character.EqualTo(':')).Then(_ => SkipSpaces)
        from type in WgslTypeParser
        from _3 in SkipSpaces.Then(_ => Character.EqualTo(';'))
        select new WgslBinding { Group = g, Binding = b, AccessMode = access.Trim(), Name = name, WgslType = type };

    // --- Shader Method / Entry Point Parser ---
    private static readonly TextParser<string> StageParser =
        Character.EqualTo('@')
            .Then(_ => Span.EqualTo("compute").Or(Span.EqualTo("vertex")).Or(Span.EqualTo("fragment")))
            .Select(s => s.ToString());

    private static readonly TextParser<string> ParamAttributeParser =
        Character.EqualTo('@')
            .Then(_ => Identifier)
            .Then(attr => Character.EqualTo('(')
                .Then(_ => Identifier)
                .Then(id => Character.EqualTo(')').Value($"@{attr}({id})"))
                .Or(Parse.Return($"@{attr}"))
            );

    private static readonly TextParser<WgslParam> ParamParser =
        from attr in ParamAttributeParser.OptionalOrDefault(string.Empty).Between(SkipSpaces, SkipSpaces)
        from name in Identifier
        from _1 in SkipSpaces.Then(_ => Character.EqualTo(':')).Then(_ => SkipSpaces)
        from type in WgslTypeParser
        select new WgslParam { Attribute = attr, Name = name, WgslType = type };

    private static readonly TextParser<WgslEntryPoint> EntryPointParser =
        from stage in StageParser.Between(SkipSpaces, SkipSpaces)
        // Skip intermediate attributes like @workgroup_size(...) before 'fn'
        from _skip in Character.EqualTo('@').Then(_ => Span.WithAll(c => c != 'f')).OptionalOrDefault() 
        from _1 in Span.EqualTo("fn").Then(_ => SkipSpaces)
        from name in Identifier
        from _2 in SkipSpaces.Then(_ => Character.EqualTo('(')).Then(_ => SkipSpaces)
        from parameters in ParamParser.Between(SkipSpaces, SkipSpaces).ManyDelimitedBy(Character.EqualTo(','))
        from _3 in SkipSpaces.Then(_ => Character.EqualTo(')')).Then(_ => SkipSpaces)
        from retType in Span.EqualTo("->").Then(_ => SkipSpaces).Then(_ => WgslTypeParser).OptionalOrDefault(string.Empty)
        // Skip function body completely, look for closing '}'
        from _body in Character.EqualTo('{').Then(_ => Span.WithAll(c => c != '}')).Then(_ => Character.EqualTo('}'))
        select new WgslEntryPoint { Stage = stage, Name = name, Parameters = parameters.ToList(), ReturnType = retType };

    // --- Main Combinator Loop ---
    private static readonly TextParser<WgslShaderMetadata> ShaderParser =
        Parse.Ref(() => StructParser.Select(s => (object)s)
            .Or(BindingParser.Select(b => (object)b))
            .Or(EntryPointParser.Select(e => (object)e))
            // Fallback: Consume any unmapped character and move forward
            .Or(Character.ExceptIn().Value((object)null)) 
            .Many()
            .Select(results =>
            {
                var metadata = new WgslShaderMetadata();
                foreach (var item in results)
                {
                    if (item is WgslStruct s) metadata.Structs.Add(s);
                    else if (item is WgslBinding b) metadata.Bindings.Add(b);
                    else if (item is WgslEntryPoint e) metadata.EntryPoints.Add(e);
                }
                return metadata;
            }));

    public static WgslShaderMetadata ParseShader(string wgslCode)
    {
        // Strip single-line comments before parsing
        var lines = wgslCode.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
                            .Select(line => line.Contains("//") ? line.Substring(0, line.IndexOf("//", StringComparison.Ordinal)) : line);
        var cleanCode = string.Join("\n", lines);

        return ShaderParser.Parse(cleanCode);
    }
}
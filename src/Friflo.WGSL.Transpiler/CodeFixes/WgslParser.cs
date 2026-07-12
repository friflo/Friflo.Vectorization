// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;

namespace Friflo.WGSL.Transpiler.CodeFixes;

using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;


// ==========================================
// AST / DATA MODELS
// ==========================================

public class WgslModule
{
    public List<WgslStruct> Structs { get; set; } = new();
    public List<WgslBinding> Bindings { get; set; } = new();
    public List<WgslEntryPoint> EntryPoints { get; set; } = new();
}

public record WgslType
{
    public string Name { get; set; } = string.Empty;
    public ValueArray<WgslType> Generics { get; set; } = new(); // Geändert von ValueArray zu List für einfacheres Parsen, falls nötig im Code anpassen

    public override string ToString()
    {
        if (Generics.Length == 0) return Name;
        return $"{Name}<{string.Join(", ", Generics.Select(g => g.ToString()))}>";
    }
}

public class WgslStruct
{
    public string Name { get; set; } = string.Empty;
    public List<WgslField> Fields { get; set; } = new();
    
    public override string ToString() => Name;
}

public class WgslField
{
    public string Name { get; set; } = string.Empty;
    public WgslType WgslType { get; set; } = new();
    
    public override string ToString() => Name;
}

public record WgslBinding
{
    public int Group { get; set; }
    public int Binding { get; set; }
    public string Name { get; set; } = string.Empty;
    public WgslType WgslType { get; set; } = new();
    
    public string AddressSpace { get; set; } = string.Empty; // e.g. "storage", "uniform", "private"
    public string AccessMode { get; set; } = string.Empty;   // e.g. "read", "write", "read_write"
    
    public override string ToString() => $"{Name} : {WgslType}{(AddressSpace == "" ? "" : $"  <{AddressSpace}>")}";
}

public class WgslEntryPoint
{
    public string Stage { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<WgslParam> Parameters { get; set; } = new();
    public WgslType ReturnType { get; set; } = new();

    public override string ToString() => $"{Name}  @{Stage}";
}

public class WgslParam
{
    public string Attribute { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WgslType WgslType { get; set; } = new();
    
    public override string ToString() => Name;
}

// ==========================================
// TOKEN ENUM DEFINITION
// ==========================================
public enum WgslToken
{
    None,
    Identifier,
    Number,
    Struct,
    Var,
    Fn,
    ReturnArrow,
    At,
    Colon,
    Semicolon,
    Comma,
    LParen,
    RParen,
    LBrace,
    RBrace,
    LAngle,
    RAngle
}

// ==========================================
// WGSL TOKENIZER DEFINITION
// ==========================================
public static class WgslTokenizer
{
    public static readonly Tokenizer<WgslToken> Instance = new TokenizerBuilder<WgslToken>()
        // 1. Whitespaces und einzeilige Kommentare überlesen
        .Ignore(Span.WhiteSpace)
        .Ignore(Span.EqualTo("//").Then(s => Span.WithAll(c => c != '\n' && c != '\r')))
        
        // 2. Schlüsselwörter (Keywords)
        .Match(Span.EqualTo("struct"), WgslToken.Struct)
        .Match(Span.EqualTo("var"), WgslToken.Var)
        .Match(Span.EqualTo("fn"), WgslToken.Fn)
        .Match(Span.EqualTo("->"), WgslToken.ReturnArrow)
        
        // 3. Kontroll- und Strukturzeichen
        .Match(Character.EqualTo('@'), WgslToken.At)
        .Match(Character.EqualTo(':'), WgslToken.Colon)
        .Match(Character.EqualTo(';'), WgslToken.Semicolon)
        .Match(Character.EqualTo(','), WgslToken.Comma)
        .Match(Character.EqualTo('('), WgslToken.LParen)
        .Match(Character.EqualTo(')'), WgslToken.RParen)
        .Match(Character.EqualTo('{'), WgslToken.LBrace)
        .Match(Character.EqualTo('}'), WgslToken.RBrace)
        .Match(Character.EqualTo('<'), WgslToken.LAngle)
        .Match(Character.EqualTo('>'), WgslToken.RAngle)
        
        // 4. Literale (Zahlen) und Bezeichner (Identifier)
        .Match(Numerics.IntegerInt32.Select(_ => _), WgslToken.Number)
        .Match(Character.Letter.Or(Character.EqualTo('_'))
            .Then(c => Character.LetterOrDigit.Or(Character.EqualTo('_')).Many().Select(cs => c + new string(cs))), WgslToken.Identifier)
        
        // 5. Fallback für alles andere (=, +, *, [, ], etc.)
        .Match(Character.AnyChar, WgslToken.None) 
        .Build();
}

// ==========================================
// ROBUST TOKEN PARSER
// ==========================================
public static class WgslSuperpowerParser
{
    // --- Primitive Token Matchers ---
    private static readonly TokenListParser<WgslToken, string> Id = 
        Token.EqualTo(WgslToken.Identifier).Select(t => t.ToStringValue());

    private static readonly TokenListParser<WgslToken, int> Num = 
        Token.EqualTo(WgslToken.Number).Select(t => int.Parse(t.ToStringValue()));

    private static readonly TokenListParser<WgslToken, Token<WgslToken>> AnyToken =
        Token.Matching<WgslToken>(_ => true, "any token");

    private static readonly TokenListParser<WgslToken, WgslType> WgslTypeParser = 
        Parse.Ref(() =>
            from baseId in Id
            from generics in (
                from open in Token.EqualTo(WgslToken.LAngle)
                from inner in Parse.Ref(() => WgslTypeParser).ManyDelimitedBy(Token.EqualTo(WgslToken.Comma))
                from close in Token.EqualTo(WgslToken.RAngle)
                select inner.ToList()
            ).OptionalOrDefault(new List<WgslType>())
            select new WgslType { Name = baseId, Generics = generics.ToValueArray() }
        );

    // Discards attribute blocks like @location(0) or @vertex robustly (now allows Numbers inside)
    private static readonly TokenListParser<WgslToken, Unit> SkipAttribute =
        from at in Token.EqualTo(WgslToken.At)
        from name in Id
        from parens in (
            from open in Token.EqualTo(WgslToken.LParen)
            from inner in Token.EqualTo(WgslToken.Identifier).Or(Token.EqualTo(WgslToken.Number)).Many()
            from close in Token.EqualTo(WgslToken.RParen)
            select Unit.Value
        ).OptionalOrDefault()
        select Unit.Value;

    // Skips whole function or struct bodies safely
    private static readonly TokenListParser<WgslToken, Unit> SkipBracedBlock =
        from open in Token.EqualTo(WgslToken.LBrace)
        from content in Token.Matching<WgslToken>(t => t != WgslToken.RBrace, "not RBrace").Value(Unit.Value)
            .Or(Parse.Ref(() => SkipBracedBlock))
            .Many()
        from close in Token.EqualTo(WgslToken.RBrace)
        select Unit.Value;

    // --- Core WGSL Parsers ---

    private static readonly TokenListParser<WgslToken, WgslField> FieldParser =
        from attrs in SkipAttribute.Many()
        from name in Id
        from colon in Token.EqualTo(WgslToken.Colon)
        from type in WgslTypeParser
        from comma in Token.EqualTo(WgslToken.Comma).Or(Token.EqualTo(WgslToken.Semicolon)).OptionalOrDefault()
        select new WgslField { Name = name, WgslType = type };

    private static readonly TokenListParser<WgslToken, WgslStruct> StructParser =
        from keyword in Token.EqualTo(WgslToken.Struct)
        from name in Id
        from open in Token.EqualTo(WgslToken.LBrace)
        from fields in FieldParser.Many()
        from close in Token.EqualTo(WgslToken.RBrace)
        select new WgslStruct { Name = name, Fields = fields.ToList() };

    // @group(X)
    private static readonly TokenListParser<WgslToken, int> GroupAttrParser =
        Token.EqualTo(WgslToken.At)
            .Then(_ => Id).Where(id => id == "group")
            .Then(_ => Token.EqualTo(WgslToken.LParen))
            .Then(_ => Num)
            .Then(n => Token.EqualTo(WgslToken.RParen).Value(n));

    // @binding(Y)
    private static readonly TokenListParser<WgslToken, int> BindingAttrParser =
        Token.EqualTo(WgslToken.At)
            .Then(_ => Id).Where(id => id == "binding")
            .Then(_ => Token.EqualTo(WgslToken.LParen))
            .Then(_ => Num)
            .Then(n => Token.EqualTo(WgslToken.RParen).Value(n));

    // any combination  @group(X)  @binding(Y)
    private static readonly TokenListParser<WgslToken, WgslBinding> BindingParser =
        (from first in GroupAttrParser
         from second in BindingAttrParser
         select (Group: first, Binding: second))
        .Or(
         from first in BindingAttrParser
         from second in GroupAttrParser
         select (Group: second, Binding: first))
        .Then(attrs => 
            from varKeyword in Token.EqualTo(WgslToken.Var)
            from details in AccessDetailsParser.OptionalOrDefault((AddressSpace: string.Empty, AccessMode: string.Empty))
            from name in Id
            from colon in Token.EqualTo(WgslToken.Colon)
            from type in WgslTypeParser
            from semi in Token.EqualTo(WgslToken.Semicolon)
            
            select new WgslBinding 
            { 
                Group = attrs.Group, 
                Binding = attrs.Binding, 
                Name = name, 
                WgslType = type,
                AddressSpace = details.AddressSpace,
                AccessMode = details.AccessMode
            }
        );
    
    private static readonly TokenListParser<WgslToken, (string AddressSpace, string AccessMode)> AccessDetailsParser =
        from open in Token.EqualTo(WgslToken.LAngle)
        from parts in Id.ManyDelimitedBy(Token.EqualTo(WgslToken.Comma))
        from close in Token.EqualTo(WgslToken.RAngle)
        select parts.Length switch
        {
            >= 2    => (AddressSpace: parts[0], AccessMode: parts[1]),
            1       => (AddressSpace: parts[0], AccessMode: string.Empty),
            _       => (AddressSpace: string.Empty, AccessMode: string.Empty)
        };

    // Parses function arguments, e.g., @location(0) fragUV: vec2f
    private static readonly TokenListParser<WgslToken, WgslParam> ParamParser =
        from attr in (
            from at in Token.EqualTo(WgslToken.At)
            from attrName in Id
            from inner in (
                from o in Token.EqualTo(WgslToken.LParen) 
                from innerVal in Token.EqualTo(WgslToken.Identifier).Or(Token.EqualTo(WgslToken.Number)).Select(t => t.ToStringValue()) 
                from c in Token.EqualTo(WgslToken.RParen) 
                select innerVal
            ).OptionalOrDefault()
            select inner != null ? $"@{attrName}({inner})" : $"@{attrName}"
        ).OptionalOrDefault(string.Empty)
        from name in Id
        from colon in Token.EqualTo(WgslToken.Colon)
        from type in WgslTypeParser
        select new WgslParam { Attribute = attr, Name = name, WgslType = type };

    // Consumes everything between the parameter list close ')' and the body open '{'
    private static readonly TokenListParser<WgslToken, Unit> SkipUntilLBrace =
        Token.Matching<WgslToken>(t => t != WgslToken.LBrace, "anything before body")
            .Many().Value(Unit.Value);

    // The core EntryPoint parser for @vertex, @fragment, @compute
    private static readonly TokenListParser<WgslToken, WgslEntryPoint> EntryPointParser =
        from stage in Token.EqualTo(WgslToken.At).Then(_ => Id).Where(id => id == "vertex" || id == "fragment" || id == "compute")
        from intermediateAttrs in SkipAttribute.Many()
        from fnKeyword in Token.EqualTo(WgslToken.Fn)
        from name in Id
        from open in Token.EqualTo(WgslToken.LParen)
        from parameters in ParamParser.ManyDelimitedBy(Token.EqualTo(WgslToken.Comma))
        from close in Token.EqualTo(WgslToken.RParen)
        from returnTrivia in SkipUntilLBrace
        from body in SkipBracedBlock
        select new WgslEntryPoint { 
            Stage = stage, 
            Name = name, 
            Parameters = parameters.ToList(), 
            ReturnType = new WgslType() 
        };

    // --- Global Top-Level Parser ---
    private static readonly TokenListParser<WgslToken, WgslModule> GlobalShaderParser =
        (
            StructParser.Select(s => (object)s).Try()
            .Or(BindingParser.Select(b => (object)b).Try())
            .Or(EntryPointParser.Select(e => (object)e).Try())
            .Or(AnyToken.Value((object)null))
        ).Many()
        .Select(results =>
        {
            var metadata = new WgslModule();
            foreach (var item in results)
            {
                if (item is WgslStruct s) metadata.Structs.Add(s);
                else if (item is WgslBinding b) metadata.Bindings.Add(b);
                else if (item is WgslEntryPoint e) metadata.EntryPoints.Add(e);
            }
            return metadata;
        });

    // --- Main API Entry Point ---
    public static WgslModule ParseShader(string wgslCode)
    {
        TokenList<WgslToken> tokenList = WgslTokenizer.Instance.Tokenize(wgslCode);
        var result = GlobalShaderParser.Parse(tokenList);
        return result;
    }
}
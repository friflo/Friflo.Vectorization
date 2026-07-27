// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.WGSL.Transpiler.WGSL;


// ==========================================
// SCANNER (Zero-Allocation Lexer)
// ==========================================
public ref struct WgslScanner
{
    private readonly ReadOnlySpan<char> _source;
    private int _position;

    public WgslScanner(ReadOnlySpan<char> source)
    {
        _source = source;
        _position = 0;
    }

    public readonly bool IsEof => _position >= _source.Length;

    public void SkipWhitespaceAndComments()
    {
        while (_position < _source.Length)
        {
            char c = _source[_position];

            // skip whitespace
            if (char.IsWhiteSpace(c))
            {
                _position++;
                continue;
            }

            // skip one line comments
            if (c == '/' && _position + 1 < _source.Length && _source[_position + 1] == '/')
            {
                _position += 2;
                while (_position < _source.Length && _source[_position] != '\n')
                {
                    _position++;
                }
                continue;
            }

            break;
        }
    }

    public bool Match(char c)
    {
        SkipWhitespaceAndComments();
        if (_position < _source.Length && _source[_position] == c)
        {
            _position++;
            return true;
        }
        return false;
    }

    public bool Match(ReadOnlySpan<char> keyword)
    {
        SkipWhitespaceAndComments();
        if (_position + keyword.Length <= _source.Length &&
            _source.Slice(_position, keyword.Length).SequenceEqual(keyword))
        {
            // ensure keyword is not part of a longer symbol (e.g. "var_name")
            int nextPos = _position + keyword.Length;
            if (nextPos < _source.Length && (char.IsLetterOrDigit(_source[nextPos]) || _source[nextPos] == '_'))
            {
                return false;
            }

            _position += keyword.Length;
            return true;
        }
        return false;
    }

    public ReadOnlySpan<char> ReadIdentifier()
    {
        SkipWhitespaceAndComments();
        int start = _position;

        if (_position < _source.Length && (char.IsLetter(_source[_position]) || _source[_position] == '_'))
        {
            _position++;
            while (_position < _source.Length && (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
            {
                _position++;
            }
            return _source.Slice(start, _position - start);
        }

        return ReadOnlySpan<char>.Empty;
    }

    public int ReadInteger()
    {
        SkipWhitespaceAndComments();
        int start = _position;

        while (_position < _source.Length && char.IsDigit(_source[_position]))
        {
            _position++;
        }

        if (start == _position) return -1;
        return int.Parse(_source.Slice(start, _position - start).ToString());
    }

    public void SkipChar()
    {
        if (_position < _source.Length) _position++;
    }
}

// ==========================================
// 2. PARSER (Recursive Descent)
// ==========================================
public static class FastWgslParser
{
    public static WgslModule Parse(string wgslCode, string sourcePath)
    {
        var module = new WgslModule();
        var scanner = new WgslScanner(wgslCode.AsSpan());

        while (!scanner.IsEof)
        {
            scanner.SkipWhitespaceAndComments();
            if (scanner.IsEof) break;

            if (TryParseStruct(ref scanner, sourcePath, out var wgslStruct))
            {
                module.Structs.Add(wgslStruct);
            }
            else if (TryParseBinding(ref scanner, out var binding))
            {
                module.Bindings.Add(binding);
            }
            else if (TryParseEntryPoint(ref scanner, out var entryPoint))
            {
                module.EntryPoints.Add(entryPoint);
            }
            else
            {
                // unknown / ignored token  -> skip 1 char
                scanner.SkipChar();
            }
        }

        return module;
    }

    // --- Type Parser (support generics vec4<f32>) ---
    private static WgslType ParseType(ref WgslScanner scanner)
    {
        var nameSpan = scanner.ReadIdentifier();
        if (nameSpan.IsEmpty) return new WgslType();

        var wgslType = new WgslType { Name = nameSpan.ToString() };

        if (scanner.Match('<'))
        {
            var genericsList = new List<WgslType>();
            do
            {
                var genericType = ParseType(ref scanner);
                if (!string.IsNullOrEmpty(genericType.Name))
                {
                    genericsList.Add(genericType);
                }
            } 
            while (scanner.Match(','));

            scanner.Match('>');
            wgslType.Generics = genericsList.ToValueArray();
        }

        return wgslType;
    }

    // --- attributes (@group(0), @binding(1), @stage) ---
    private static void SkipAttributes(ref WgslScanner scanner)
    {
        while (scanner.Match('@'))
        {
            scanner.ReadIdentifier(); // Attribute Name
            if (scanner.Match('('))
            {
                while (!scanner.IsEof && !scanner.Match(')'))
                {
                    scanner.SkipChar();
                }
            }
        }
    }

    // --- Struct Parser ---
    private static bool TryParseStruct(ref WgslScanner scanner, string sourcePath, out WgslStruct result)
    {
        result = null!;
        if (!scanner.Match("struct")) return false;

        var nameSpan = scanner.ReadIdentifier();
        if (nameSpan.IsEmpty || !scanner.Match('{')) return false;

        var fields = new List<WgslField>();

        while (!scanner.IsEof && !scanner.Match('}'))
        {
            SkipAttributes(ref scanner);

            var fieldName = scanner.ReadIdentifier();
            if (fieldName.IsEmpty) break;

            if (scanner.Match(':'))
            {
                var fieldType = ParseType(ref scanner);
                fields.Add(new WgslField { Name = fieldName.ToString(), WgslType = fieldType });
            }

            scanner.Match(',');
            scanner.Match(';');
        }

        result = new WgslStruct
        {
            Name = nameSpan.ToString(),
            Fields = fields,
            sourcePath = sourcePath
        };
        return true;
    }

    // --- Binding Parser (@group(x) @binding(y) var...) ---
    private static bool TryParseBinding(ref WgslScanner scanner, out WgslBinding result)
    {
        result = null!;

        int group = -1, binding = -1;

        // parse Group & Binding Attribute
        for (int i = 0; i < 2; i++)
        {
            if (scanner.Match("@group"))
            {
                if (scanner.Match('(')) { group = scanner.ReadInteger(); scanner.Match(')'); }
            }
            else if (scanner.Match("@binding"))
            {
                if (scanner.Match('(')) { binding = scanner.ReadInteger(); scanner.Match(')'); }
            }
        }

        if (group == -1 || binding == -1) return false;

        if (!scanner.Match("var")) return false;

        string addressSpace = "", accessMode = "";
        if (scanner.Match('<'))
        {
            addressSpace = scanner.ReadIdentifier().ToString();
            if (scanner.Match(','))
            {
                accessMode = scanner.ReadIdentifier().ToString();
            }
            scanner.Match('>');
        }

        var name = scanner.ReadIdentifier().ToString();
        if (!scanner.Match(':')) return false;

        var type = ParseType(ref scanner);
        scanner.Match(';');

        result = new WgslBinding
        {
            Group = group,
            Binding = binding,
            Name = name,
            WgslType = type,
            AddressSpace = addressSpace,
            AccessMode = accessMode
        };
        return true;
    }

    // --- Entry Point Parser (@vertex, @fragment, @compute) ---
    private static bool TryParseEntryPoint(ref WgslScanner scanner, out WgslEntryPoint result)
    {
        result = null!;
        if (!scanner.Match('@')) return false;

        var stageSpan = scanner.ReadIdentifier();
        string stage = stageSpan.ToString();

        if (stage != "vertex" && stage != "fragment" && stage != "compute")
            return false;

        SkipAttributes(ref scanner);

        if (!scanner.Match("fn")) return false;

        var name = scanner.ReadIdentifier().ToString();
        if (!scanner.Match('(')) return false;

        var parameters = new List<WgslParam>();

        while (!scanner.IsEof && !scanner.Match(')'))
        {
            SkipAttributes(ref scanner);
            var paramName = scanner.ReadIdentifier();
            if (!paramName.IsEmpty && scanner.Match(':'))
            {
                var paramType = ParseType(ref scanner);
                parameters.Add(new WgslParam { Name = paramName.ToString(), WgslType = paramType });
            }
            scanner.Match(',');
        }

        WgslType returnType = new WgslType();
        if (scanner.Match("->"))
        {
            SkipAttributes(ref scanner);
            returnType = ParseType(ref scanner);
        }

        // skip curly brackets of function body { ... }
        SkipBracedBlock(ref scanner);

        result = new WgslEntryPoint
        {
            Stage = stage,
            Name = name,
            Parameters = parameters,
            ReturnType = returnType
        };
        return true;
    }

    private static void SkipBracedBlock(ref WgslScanner scanner)
    {
        if (!scanner.Match('{')) return;

        int depth = 1;
        while (!scanner.IsEof && depth > 0)
        {
            if (scanner.Match('{')) depth++;
            else if (scanner.Match('}')) depth--;
            else scanner.SkipChar();
        }
    }
}

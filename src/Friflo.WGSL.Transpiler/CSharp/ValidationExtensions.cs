// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.WGSL;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.CSharp;


public enum DiagType
{
    Error,
    Warn
}

public readonly struct ValidationDiag
{
    public readonly     SrcLoc      srcLoc;
    public readonly     string      message;
    public readonly     DiagType    type;

    public override string  ToString() => message;

    public ValidationDiag(SrcLoc srcLoc, string  message,  DiagType type)
    {
        this.srcLoc     = srcLoc;
        this.message    = message;
        this.type       = type;
    }
}


internal class FieldPath
{
    private readonly   string[]    path        = new string[10];
    private            int         pathLength;
    internal           string?     type;
    
    internal void Push()                => pathLength++;
    internal void Push(string field)    => path[pathLength++]   = field;
    internal void Pop ()                => pathLength--;
    internal void SetTail(string field) => path[pathLength - 1] = field;
    
    internal void Reset() {
        pathLength  = 0;
        type        = null;
    }
    
    public override string ToString() {
        var sb = new StringBuilder();
        for (int n = 0; n < pathLength; n++) {
            sb.Append('.');
            sb.Append(path[n]);
        }
        return sb.ToString();
    }
}


internal static class ValidationExtensions
{
    extension(List<ValidationDiag> diags)
    {
        internal void Shader(SrcLoc srcLoc, in CsShader shader, string message, DiagType type) {
            var error = $"[Shader(\"{shader.path}\")] - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
        
        internal void WorkgroupSize(CsWorkgroupSize workgroupSize, string message, DiagType type) {
            var error = $"[WorkgroupSize()] - {message}";
            diags.Add(new ValidationDiag(workgroupSize.attrLoc, error, type));
        }
                
        internal void Method(SrcLoc srcLoc, CsMethod method, string message, DiagType type) {
            var error = $"{method.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }

        internal void Map(SrcLoc srcLoc, in CsParameter parameter, string message, DiagType type) {
            var bg = parameter.BindGroup;
            var error = $"[Map({bg.group}, {bg.binding})] {parameter.Name} - {message}";
            diags.Add(new ValidationDiag(srcLoc, error, type));
        }
        
        internal void Mismatch(SrcLoc loc, in CsParameter parameter, WgslBinding wgslBinding, string message)
        {
            var sb = new StringBuilder();
            sb.Append($"wgsl {message}: C# [");
            sb.Append(parameter.ParamAttribute);
            var start = sb.Length;
            var arg_0 = parameter.AttrEnum.enum1.Name;
            if (!string.IsNullOrEmpty(arg_0)) {
                sb.Append("(");
                sb.Append(arg_0);
            }
            var arg_1 = parameter.AttrEnum.enum2.Name;
            if (!string.IsNullOrEmpty(arg_1)) {
                sb.Append(", ");
                sb.Append(arg_1);
            }
            if (sb.Length > start) {
                sb.Append(")");
            }
            sb.Append("]  ->  ");
            sb.Append(wgslBinding.AsString());
            diags.Map(loc, parameter, sb.ToString(), DiagType.Warn);
        }
        
        internal void TypeRequirement(in CsParameter parameter, string expectedType)
        {
            var error = $"[{parameter.ParamAttribute}] {parameter.Name} - Type requirement: {expectedType} - was: {parameter.Type.Name}";
            diags.Add(new ValidationDiag(parameter.TypeLoc, error, DiagType.Error));
        }
        
        internal void WgslTypeRequirement(in CsParameter parameter, SrcLoc typeLoc, ValueArray<CsTypeInfo> typeInfos)
        {
            var error = GetWgslTypeError(parameter.Type, typeInfos);
            var msg = $"[{parameter.ParamAttribute}] {parameter.Name} - require WGSL Type (int, float, Vector3, ...) - was: {error}";
            diags.Add(new ValidationDiag(typeLoc, msg, DiagType.Error));
        }
    }
        
    private static string? GetWgslTypeError(CsType type, ValueArray<CsTypeInfo> typeInfos)
    {
        if (type.TypeCode.IsBuffer) {
            var generic = type.Generics;
            if (generic.Length == 1) {
                type = generic[0];
            }
        }
        if (type.TypeCode.IsWgslType) {
            return null;
        }
        var path        = new Stack<string>();
        var errorType   = GetErrorPath(type, path, typeInfos);
        if (path.Count == 0) {
            return errorType.Name;
        }
        return $"{errorType.Name} at {type.Name}.{string.Join(".", path.Reverse())}";
    }
    
    private static CsType GetErrorPath(in CsType type, Stack<string> path, ValueArray<CsTypeInfo> typeInfos)
    {
        if (type.TypeCode is CsTypeCode.WgslStruct or CsTypeCode.CSharpStruct && path.Count < 10)
        {
            var ti = typeInfos.FindTypeInfo(type.Namespace, type.Name);
            foreach (var field in ti.Fields) {
                path.Push(field.Name);
                var fieldType = GetErrorPath(field.Type, path, typeInfos);
                if (!fieldType.TypeCode.IsWgslType) {
                    return fieldType;
                }
                path.Pop();
            }
        }
        return type;
    }
}

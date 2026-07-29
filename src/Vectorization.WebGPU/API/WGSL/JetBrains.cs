// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable CheckNamespace
namespace JetBrains.Annotations;

/// <summary>
/// Redefinition of <c>PathReferenceAttribute</c> from https://www.nuget.org/packages/Jetbrains.Annotations<br/>
/// Its only purpose:<br/>
/// Enable navigation to a project file within Rider with: <b>CTRL + Left Mouse Click</b> on a path string. E.g.
/// <code>
///     [Shader("~/shaders/basic.vert.wgsl",                                vertex:   "main")]
///     [Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",   fragment: "main")]
///     private static partial void RenderCube(RenderPass pass, RenderConfig config,
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class PathReferenceAttribute : Attribute;


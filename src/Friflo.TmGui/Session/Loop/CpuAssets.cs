// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.TmGui.Headless;


// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

internal class CpuAssets : IGuiAssets
{
    public TmFont CreateDefaultFont(TmGuiBackend backend)
    {
        return HeadlessAssets.CreateHeadlessFont();
    }

    public TmImageAsset LoadImage(Stream stream, TmColorComponents colorComponents)
    {
        throw Requires_Friflo_TmGui_Assets_Exception(nameof(LoadImage));
    }
    
    public Stream CreatePng(TmImageAsset asset)
    {
        throw Requires_Friflo_TmGui_Assets_Exception(nameof(CreatePng));
    }

    public TmTrueTypeFontAsset LoadTrueTypeFont(Stream ttfStream, float fontSize, int atlasWidth, int atlasHeight, byte[] alphaBitmapTarget, int firstChar, int charCount)
    {
        throw Requires_Friflo_TmGui_Assets_Exception(nameof(LoadTrueTypeFont));
    }
    
    private static InvalidOperationException Requires_Friflo_TmGui_Assets_Exception(string symbol)
    {
        return new InvalidOperationException($"{symbol}() requires IGuiAssets from package: Friflo.TmGui.Assets - instance: new DefaultGuiAssets()");
    }
}
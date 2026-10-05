// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Headless;

internal static class HeadlessExtensions
{
    internal static TmTexture AsImTexture(this HeadlessTexture texture)
    {
        return new TmTexture(texture, 0);
    }
    
    internal static HeadlessTexture AsGpuTexture(in this TmTexture tmTexture)
    {
        return (HeadlessTexture)tmTexture.native!;
    }
}
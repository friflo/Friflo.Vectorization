// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace
namespace Friflo.ImGui2D.Headless;

public static class HeadlessExtensions
{
    public static ImTexture AsImTexture(this HeadlessTexture texture)
    {
        return new ImTexture(texture, 0);
    }
    
    public static HeadlessTexture AsGpuTexture(in this ImTexture imTexture)
    {
        return (HeadlessTexture)imTexture.native!;
    }
}
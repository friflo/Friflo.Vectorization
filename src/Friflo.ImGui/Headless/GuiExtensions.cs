// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace
namespace Friflo.ImGui.Headless;

public static class HeadlessExtensions
{
    public static ImTexture ToImTexture(this HeadlessTexture texture)
    {
        return new ImTexture(texture, 0);
    }
}
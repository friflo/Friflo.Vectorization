// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

public interface IGuiResources
{
    TmFont CreateDefaultFont  (TmGuiBackend backend);
    TmFont CreateMonocraftFont(TmGuiBackend backend, float fontSize, int width, int height, int firstChar, int charCount, string name);
}


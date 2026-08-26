// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Friflo.GPU;
using Friflo.WGPU;
using StbImageSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public static class ImDeviceExtensions
{
    extension (GpuDevice device)
    {
        public GuiModule? GetGuiModule()
        {
            if (device.TryGetModule(out DrawModule module)) {
                return module.guiModule;
            }
            return null;
        }
        
        public GpuTexture LoadTexture(Stream stream, string? label = null, TextureUsage usage = TextureUsage.TextureBinding | TextureUsage.CopyDst)
        {
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            var texture = device.CreateTexture(new GpuTextureDescriptor {
                label  = label,
                size   = [image.Width, image.Height],
                format = TextureFormat.RGBA8Unorm,
                usage  = usage
            });
            texture.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);
            return texture;
        }
    }

	// TODO IM_TEX  move methods to ImGuiBackend
    extension (ImGuiBackend backend)
    {
        internal Font CreateDefaultFont()
        {
            using var fontAtlas = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin_0.png");
            using var fntFile   = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin.fnt");
            using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
            var fntContent      = reader.ReadToEnd();
            
            return Font.CreateBMFont(backend, fntContent, fontAtlas!, "Default Font", false);
        }
        
        /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
        public Font CreateMonocraftFont(float fontSize, int width, int height, int firstChar, int charCount, string name)
        {
            using var ttfFont = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.Monocraft.ttf")!;
            
            return Font.CreateTtfFont(backend, ttfFont, fontSize, width, height, firstChar, charCount, name, true);
        }
        
        public Font CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
        {
            return Font.CreateBMFont(backend, fntContent, fontAtlas, name, true);
        }
        
        public Font CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
        {
            return Font.CreateTtfFont(backend, ttfStream, fontSize, width, height, firstChar, charCount, name, true);
        }
    }
}
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
namespace Friflo.WGPU.ImDraw;


public static class ImDeviceExtensions
{
    extension (GpuDevice device)
    {
        public Batch2D CreateBatch2D(TextureFormat targetFormat, int maxVertices = 60_000) {
            return new Batch2D(device, targetFormat, maxVertices);
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
        
        
        internal Font CreateDefaultFont()
        {
            using var fontAtlas = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin_0.png");
            using var fntFile   = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin.fnt");
            using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
            var fntContent      = reader.ReadToEnd();
            
            return Font.CreateBMFont(device, fntContent, fontAtlas!, "Default Font", false);
        }
        
        /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
        public Font CreateMonocraftFont(float fontSize, int width, int height, int firstChar, int charCount, string name)
        {
            using var ttfFont = typeof(DrawModule).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.Monocraft.ttf")!;
            
            return Font.CreateTtfFont(device, ttfFont, fontSize, width, height, firstChar, charCount, name, true);
        }
        
        public Font CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
        {
            return Font.CreateBMFont(device, fntContent, fontAtlas, name, true);
        }
        
        public Font CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
        {
            return Font.CreateTtfFont(device, ttfStream, fontSize, width, height, firstChar, charCount, name, true);
        }
    }
}
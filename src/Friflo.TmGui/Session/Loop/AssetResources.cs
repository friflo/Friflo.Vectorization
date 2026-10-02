// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


public readonly struct AssetResources
{
    private  readonly   TmGuiBackend                        rootBackend;
    private  readonly   Dictionary<object, int>             texture2Id      = new();
    public   readonly   Dictionary<string, ImageResource>   stringToImage   = new();
    public   readonly   List<ImageResource>                 images          = [null!];
    
    private int AddTexture(TmTexture texture)
    {
        var textureId = images.Count;
        texture2Id.Add(texture.native!, textureId);
        var asset = rootBackend.GetTextureImage(texture);
        var image = new ImageResource { textureId = textureId, asset = asset, texture = texture, assets = rootBackend.Assets };
        images.Add(image);
        if (asset.name != null) {
            stringToImage.TryAdd(asset.name, image);   // TODO fix me!!!
        }
        return textureId;
    }
    
    public AssetResources(TmGuiBackend rootBackend)
    {
        this.rootBackend = rootBackend;
    }

    public int GetTexture(TmTexture texture)
    {
        if (texture2Id.TryGetValue(texture.native!, out int id)) {
            return id;
        }
        return AddTexture(texture);
    }
}

public class ImageResource
{
    public  required    int             textureId;
    public  required    TmImageAsset    asset;
    public  required    TmTexture       texture;
    public  required    IGuiAssets      assets;
    private             byte[]?         pngArray;
    public              string          Etag { get; private set; } = "";

    public override string ToString() => $"{asset.name} - {texture}";
    
    public byte[] GetAsPng()
    {
        if (pngArray != null) {
            return pngArray;
        }

        using var stream = assets.CreatePng(asset);
        var buffer = new byte[stream.Length];

        stream.ReadExactly(buffer, 0, buffer.Length);
        
        Etag        = HashUtils.XxHash3(asset.data).ToString();
        pngArray    = buffer;

        return pngArray;
    }
}

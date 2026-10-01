// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


public readonly struct AssetResources
{
    private  readonly   TmGuiBackend                        rootBackend;
    private  readonly   Dictionary<object, int>             texture2Id      = new();
    public   readonly   Dictionary<string, ImageResource>   stringToImage   = new();
    public   readonly   List<ImageResource>                 images          = [default];
    
    private int AddTexture(TmTexture texture)
    {
        var textureId = images.Count;
        texture2Id.Add(texture.native!, textureId);
        var asset = rootBackend.GetTextureImage(texture);
        var image = new ImageResource { textureId = textureId, asset = asset, texture = texture };
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

public struct ImageResource
{
    public  int             textureId;
    public  TmImageAsset    asset;
    public  TmTexture       texture;

    public override string ToString() => $"{asset.name} - {texture}";
}

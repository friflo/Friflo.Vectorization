// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.SmGui.Headless;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


public readonly struct AssetResources
{
    private  readonly   CpuBackend                          cpuBackend;
    private  readonly   Dictionary<object, int>             texture2Id      = new();
    public   readonly   Dictionary<string, ImageResource>   stringToImage   = new();
    public   readonly   List<ImageResource?>                images          = [null];
    
    private int AddTexture(SmTexture texture)
    {
        var textureId = images.Count;
        texture2Id.Add(texture.native!, textureId);
        var asset       = cpuBackend.GetTextureImage(texture);
        var name        = asset.name;
        var sessionGuid = default(SessionGuid);
        if (name.StartsWith(WsBackend.Sid)) {
            var len     = WsBackend.Sid.Length;
            var end     = name.IndexOf('/', len);
            sessionGuid = SessionGuid.FromSpan(name.AsSpan(len, end - len));
        }
        var image = new ImageResource(sessionGuid) { textureId = textureId, asset = asset, texture = texture, assets = cpuBackend.Assets };
        images.Add(image);
        stringToImage.Add(name, image);
        return textureId;
    }
    
    internal AssetResources(CpuBackend cpuBackend)
    {
        this.cpuBackend = cpuBackend;
    }

    internal int GetTexture(SmTexture texture)
    {
        var native = texture.native;
        if (native == null) {
            return 0;
        }
        if (texture2Id.TryGetValue(native, out int id)) {
            return id;
        }
        return AddTexture(texture);
    }

    internal void RemoveSessionResources(SessionGuid sessionGuid)
    {
        for (int n = 1; n < images.Count; n++) {
            var image = images[n];
            if (image != null && image.sessionGuid.guid == sessionGuid.guid) {
                images[n] = null;
                stringToImage.Remove(image.asset.name);
                texture2Id.Remove(image.texture.native!);
            }
        }
    }
}

public sealed class ImageResource
{
    public   required   int             textureId;
    public   required   SmImageAsset    asset;
    public   required   SmTexture       texture;
    public   required   IGuiAssets      assets;
    internal readonly   SessionGuid     sessionGuid;
    private             byte[]?         pngArray;
    public              string          Etag { get; private set; } = "";

    public override string ToString() => $"{asset.name} - {texture}";
    
    internal ImageResource(SessionGuid  sessionGuid)
    {
        this.sessionGuid = sessionGuid;
    }
    
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

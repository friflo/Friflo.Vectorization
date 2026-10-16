// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref struct TmAction
{
    private readonly    TmBatch batch;  //  8 bytes 
    
    internal TmAction(TmBatch batch)
    {
        this.batch = batch;
    }
    
    public void OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) => batch.OpenUrl(url, target);
    
    public void SetWindowTitle(ReadOnlySpan<char> title) => batch.SetWindowTitle(title);
}


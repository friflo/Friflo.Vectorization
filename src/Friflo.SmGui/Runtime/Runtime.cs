// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable EmptyConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Runtime;


public class SmRuntime
{
    public SmRuntime() { }
    
    public virtual SmTimer CreateTimer()
    {
        return new ThreadTimer(16);
    }
}

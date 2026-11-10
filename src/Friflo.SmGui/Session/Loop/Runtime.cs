// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public class SmRuntime
{
    public virtual SmTimer CreateTimer()
    {
        return new SyncTimer(60);
    }
}


public abstract class SmEventReady
{
    protected static SmSessionLoop? SessionLoop => Loop;
    internal  static SmSessionLoop? Loop;
    
    public abstract void Set();
} 
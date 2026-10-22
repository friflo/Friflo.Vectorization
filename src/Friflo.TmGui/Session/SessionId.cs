// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.TmGui.Session;
// ReSharper disable ConvertToPrimaryConstructor

// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.TmGui;


// todo   move SessionId here





internal readonly struct SessionGuid : IEquatable<SessionGuid>
{
    public   readonly   Guid    guid;
    internal readonly   string? str;

    public   override   string  ToString() => str ?? "none";
    
    internal SessionGuid(Guid guid) {
        this.guid   = guid;
        str         = guid.ToString();
    }
    
    internal SessionGuid(int id)
    {
        guid    = Guid.Parse($"{id:D32}");
        str     = id.ToString();
    }
    
    internal static SessionGuid FromSpan(ReadOnlySpan<char> sidSpan)
    {
        if (int.TryParse(sidSpan, out var id)) {
            return new SessionGuid(id);
        }
        if (Guid.TryParse(sidSpan, out Guid guid)) {
            return new SessionGuid(guid);
        }
        return default;
    }
    
    public override bool Equals(object? obj)        => obj is SessionId other && Equals(other);
    public override int  GetHashCode()              => guid.GetHashCode();
    public          bool Equals(SessionGuid other)  => guid == other.guid;
    
    public static   bool operator ==(SessionGuid left, SessionGuid right)   => left.guid == right.guid;
    public static   bool operator !=(SessionGuid left, SessionGuid right)   => left.guid != right.guid;
}


internal readonly struct SessionKeys
{
    internal readonly   SessionId   id;
    internal readonly   SessionGuid guid;

    public SessionKeys(SessionId id, SessionGuid guid)
    {
        this.id     = id;
        this.guid   = guid;
    }
}

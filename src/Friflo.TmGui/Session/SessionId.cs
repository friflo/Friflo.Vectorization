// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertToAutoProperty
// ReSharper disable CheckNamespace
namespace Friflo.TmGui;


/**
 * <summary>
 * A session id identifies a specific client connection. It never leaves the server / host.
 * </summary>
 * <remarks>
 * Architectural overview of identifier roles and library boundaries:
 * <code>
 * | Identifier       | Scope / Purpose                                  | Code      | Used By       |
 * |------------------|--------------------------------------------------|-----------|---------------|
 * | SessionId (int)  | Server-side client / session id in TmSessionLoop | TmGui     | Server        |
 * | SessionGuid      | Secure public token for asset URLs and Reconnect | TmGui     | Client/Server |
 * | User / Domain ID | Application business logic, accounts, grouping   | User Code | Client/Server |
 * </code>
 * </remarks>
 */
public readonly struct SessionId : IEquatable<SessionId>
{
    public   readonly   int     id;

    public   override   string  ToString() => id.ToString();
    
    public SessionId(int id) {
        this.id = id;
    }
   
    public override bool Equals(object? obj)        => obj is SessionId other && Equals(other);
    public override int  GetHashCode()              => id;
    public          bool Equals(SessionId other)    => id == other.id;
    
    public static   bool operator ==(SessionId left, SessionId right)   => left.id == right.id;
    public static   bool operator !=(SessionId left, SessionId right)   => left.id != right.id;
}


/// <summary>
/// Secure public token for asset URLs and Reconnect's
/// </summary>
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
    
    public override bool Equals(object? obj)        => obj is SessionGuid other && Equals(other);
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

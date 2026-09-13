using System;
using System.IO.Hashing;
using Friflo.TmGui;
using NUnit.Framework;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToConstant.Local
namespace Tests.TmGui;

// ReSharper disable InconsistentNaming
public static class Tests_TmGui_Utils
{
    [Test]
    public static void Tests_TmGui_Utils_XxHash3()
    {
        var array = CreateByteArray(1000);
        var hash = HashUtils.XxHash3(array);
        Assert.That(hash, Is.EqualTo(14228893083880859301));
        
        ulong last = 0;
        
        for (int n = 1; n < array.Length; n++)
        {
            var span = array.AsSpan(0, n);
            hash = HashUtils.XxHash3(span);
            
            if (hash == last) Assert.Fail("same hash");
            last = hash;
        }
    }
    
    [Test]
    public static void Tests_TmGui_Utils_XxHash3_perf()
    {
        var array = CreateByteArray(3200);
        
        int repeat = 10;   // 100_000_000 - 3.1 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= HashUtils.XxHash3(array);
        }
        Console.WriteLine(accu);
    }
    
    [Test]
    public static void Tests_TmGui_Utils_System_IO_Hashing_XxHash3_perf()
    {
        var array = CreateByteArray(3200);
       
        int repeat = 10;   // 100_000_000 - 9.6 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= XxHash3.HashToUInt64(array);
        }
        Console.WriteLine(accu);
    }
    
    [Test]
    public static void Tests_TmGui_Utils_HashFNV_1a_perf()
    {
        var array = CreateByteArray(3200);
       
        int repeat = 10;   // 1_000_000 - 2.5 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= HashFNV_1a(array);
        }
        Console.WriteLine(accu);
    }
    
    
    private static ulong HashFNV_1a(ReadOnlySpan<byte> data)
    {
        ulong h = (ulong)data.Length * 0x9e3779b97f4a7c15UL;
        foreach (byte b in data)
        {
            h = (h ^ b) * 0xbf58476d1ce4e5b9UL;
        }
        return h;
    }
    
    private static byte[] CreateByteArray(int length)
    {
        var array = new byte[length];
        for (int n = 0; n < array.Length; n++) {
            array[n] = (byte)n;
        }
        return array;
    }
}
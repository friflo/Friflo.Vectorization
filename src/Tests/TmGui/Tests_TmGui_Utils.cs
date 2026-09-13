using System;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
       
        int repeat = 10;   // 10_000_000 - 12.2 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= HashFNV_1a_scalar(array);
        }
        Console.WriteLine(accu);
    }
    
    private static ulong HashFNV_1a_scalar(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        ulong h = (ulong)length * 0x9e3779b97f4a7c15UL;
        
        var length_div_8 = length / 8;
        int n;
        for (n = 0; n < length_div_8; n += 8) {
            var l = Unsafe.As<byte, ulong>(ref MemoryMarshal.GetReference(data.Slice(n, 8)));
            h = (h ^ l) * 0xbf58476d1ce4e5b9UL;
        }
        // remaining
        ulong rest = 0;
        for (; n < length; n++) {
            rest = (rest << 8) | data[n];
        }
        h = (h ^ rest) * 0xbf58476d1ce4e5b9UL;
        return h;
    }
    
    [Test]
    public static void Tests_TmGui_Utils_HashFNV_1a()
    {
        var array = CreateByteArray(10);

        Assert.That(HashFNV_1a_scalar(array.AsSpan(0, 0)),  Is.EqualTo(0));
        Assert.That(HashFNV_1a_scalar(array.AsSpan(0, 1)),  Is.EqualTo(15452995756747027501));
        Assert.That(HashFNV_1a_scalar(array.AsSpan(0, 8)),  Is.EqualTo(466183600257064232));
        Assert.That(HashFNV_1a_scalar(array.AsSpan(0, 10)), Is.EqualTo(13844614674567504819));
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
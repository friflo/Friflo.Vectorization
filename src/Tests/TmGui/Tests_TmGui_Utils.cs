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
        var array = CreateByteArray(3200);
        var hash = HashUtils.XxHash3(array);
        Assert.That(hash, Is.EqualTo(12775576852890513353));
        
        int repeat = 10;   // 100_000_000 - 3.1 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= HashUtils.XxHash3(array);
        }
        Console.WriteLine(accu);
    }
    
    [Test]
    public static void Tests_TmGui_Utils_System_IO_Hashing_XxHash3()
    {
        var array = CreateByteArray(3200);
       
        int repeat = 10;   // 100_000_000 - 9.6 sec  length: 3200
        ulong accu = 0;
        
        for (int n = 0; n < repeat; n++) {
            accu ^= XxHash3.HashToUInt64(array);
        }
        Console.WriteLine(accu);
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
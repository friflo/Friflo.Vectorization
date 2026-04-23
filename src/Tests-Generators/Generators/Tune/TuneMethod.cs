using System;
using System.Reflection;

namespace Tune;

public static class TuneMethod
{
    public static void Execute(string tuneMethod)
    {
        Console.WriteLine($"Tests-Generators-Code: TUNE_METHOD = {tuneMethod}");

        var methodIndex = tuneMethod.LastIndexOf('.');
        if (methodIndex == -1) {
            Console.WriteLine("Expect full qualified method name. E.g TUNE_METHOD = Tune.Tune_Vector2.MyMethod");
            return;
        }
        var className   = tuneMethod.Substring(0, methodIndex);
        var methodName  = tuneMethod.Substring(methodIndex + 1);
        var classType   = Type.GetType(className);
        if (classType == null) {
            Console.WriteLine($"Could not find Type: {className}");
            return;
        }
        var methodInfo = classType.GetMethod(methodName);
        if (methodInfo == null) {
            Console.WriteLine($"Could not find Method: {methodName}");
            return;
        }
        var setupInfo = FindSetupMethod(classType, out object instance);
        if (setupInfo != null) {
            var setupAction = (Action)Delegate.CreateDelegate(typeof(Action), instance, setupInfo);
            setupAction();
        }
        var action = (Action)Delegate.CreateDelegate(typeof(Action), instance, methodInfo);
        Console.WriteLine($"Run {methodName}() in while(true){{ }} loop ...");
        while (true) {
            action();
        }
    }
    
    private static MethodInfo FindSetupMethod(Type classType, out object instance)
    {
        var methods = classType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                           BindingFlags.Instance | BindingFlags.Static);
        MethodInfo setupMethod = null;
        instance = null;
        foreach (var method in methods) {
            foreach (var attribute in method.CustomAttributes) {
                switch (attribute.AttributeType.FullName) {
                    case "NUnit.Framework.SetUpAttribute":
                    case "BenchmarkDotNet.Attributes.GlobalSetupAttribute":
                        setupMethod = method;
                        break;
                }
            }
        }
        if (setupMethod == null) {
            return null;
        }
        if (IsStatic(classType)) {
            return setupMethod;
        }
        instance = Activator.CreateInstance(classType);
        return setupMethod;
    }
    
    private static bool IsStatic(Type type) => type.IsAbstract && type.IsSealed;
}
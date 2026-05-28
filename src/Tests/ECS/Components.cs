using System.Numerics;
using Friflo.Engine.ECS;

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value


// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable RedundantTypeDeclarationBody
namespace Tests.ECS {

// ------------------------------------------------ components



[ComponentKey("my1")]
[ComponentSymbol("M1")]
public struct MyComponent1 : IComponent {
    public          int     a;
    public override string  ToString() => a.ToString();
}

internal class CycleClass  { internal CycleClass    cycle;  }

// two classes with indirect type cycle
internal class CycleClass1 { internal CycleClass2   cycle2; }
internal class CycleClass2 { internal CycleClass1   cycle1; }

[ComponentKey("my2")]
[ComponentSymbol("M2 too long")]
public struct MyComponent2 : IComponent { public int b; }

[ComponentKey("my3")]
[ComponentSymbol(" M3", "invalid")]
public struct MyComponent3 : IComponent { public int b; }

[ComponentKey("my4")]
[ComponentSymbol("", "invalid1,invalid2,invalid3")]
public struct MyComponent4 : IComponent { public int b; }

[ComponentKey("my5")]
public struct MyComponent5 : IComponent { public int b; }

[ComponentKey("my6")]
public struct MyComponent6 : IComponent { public int b; }


public struct FloatComponent : IComponent { public float value; }
public struct FloatComponent2 : IComponent { public float value; }
public struct Position4 : IComponent { public Vector4 value; }
public struct Velocity4 : IComponent { public Vector4 value; }

public struct Position2 : IComponent { public Vector2 value; }
public struct Velocity2 : IComponent { public Vector2 value; }

public struct Position1 : IComponent { public float value; }
public struct Velocity1 : IComponent { public float value; }

public struct Velocity : IComponent { public Vector3 value; }


[AoSoA] public struct Pos2SoA : IComponent { public Vector2 value; }
[AoSoA] public struct Pos3SoA : IComponent { public Vector3 value; }
[AoSoA] public struct Pos4SoA : IComponent { public Vector4 value; }

[AoSoA] public struct Vel2SoA : IComponent { public Vector2 value; }
[AoSoA] public struct Vel3SoA : IComponent { public Vector3 value; }
[AoSoA] public struct Vel4SoA : IComponent { public Vector4 value; }

// ------------------------------------------------ tags
[TagName("test-tag")]
public struct TestTag  : ITag { }

[TagName("test-tag2")]
public struct TestTag2 : ITag { }

// Intentionally without [Tag("test-tag3")] attribute for testing
public struct TestTag3 : ITag { }

public struct TestTag4 : ITag { }

public struct TestTag5 : ITag { }

public struct TestTag6 : ITag { }

}





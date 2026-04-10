
# friflo Vectorization

friflo Vectorization is a C# source generator used to vectorize idiomatic **floating point** math
for **x86-64** processors from **Intel** and **AMD** using **AVX**.  
It is basically the counter part for *auto vectorization* used in languages like C/C++ and Rust.  
It enables similar optimizations like Unity Burst on the **.NET** platform.  
By applying vectorization the performance of math operations can be improved by magnitudes.

**Example**  
Given a typical code snippet that can be optimized with vectorization.  
Languages like C/C++ or Rust can vectorize the method by executing 8 operations in one CPU cycle instead of 1.  
C# has no auto vectorization and executes 1 operation per CPU cycle.
```cs
static void MovePositionVector(float[] position, float[] velocity, float deltaTime) {
    for (int n = 0; n < position.Length; n++) {
        position[n] += velocity[n] * deltaTime;
    }
}
```
<br/>

friflo Vectorization applies the same optimization by generation code similar to C/C++ or Rust compilers.  
To enable this a similar method without a loop has to be annotated with `[Vectorize]`.
```cs
[Vectorize]
static void MovePosition([Span] ref position, [Span] float velocity, float deltaTime) {
    position += velocity * deltaTime;
}
```
The source generator now creates a vectorized method suffixed `Vector` with the same signature as in the initial example.  
The generated shadow method can be called with:
```cs
    MovePositionVector(positions, velocities, deltaTime);
```


### Supported operations
- All common `float` operators: `+`, `-`, `*`, `/`.
- The common methods from `Vector2`, `Vector3` and `Vector4`.


### Vector specific optimization

Some combinations of math operations have specific AVX command to speedup execution.  
The source generator detect these patterns and use these specific commands.  
Currently implemented:
- `(a * b) + c`
- `1 / Sqrt(a)`

The instructions sets in AVX used for vectorization are designed to operate on *Struct of Arrays* - **SoA**.  
An application typically uses arrays of `Vector3` or `Vector4`. Their memory layout is *Array of Structs* - **AoS**.  
**SoA** requires typically less AVX instructions for execution.  
In case of **AoS** vector data need to converted to **SoA** which requires additional instructions slowing down execution.

Many math operation like  `+`, `-`, `*`, `/` can be executed without this conversion.  
Some `Vector3` methods like `Dot()`, `Length()`, `Normalize()` and `Cross()` require **SoA**.  
The code generator detect these cases and apply **SoA** conversion only if needed.  


### Notes

friflo Vectorization is implemented as an incremental C# generator.  
In IDE's like Rider or Visual Studio methods are only updated if they are edited.  
If a method cannot be vectorized a compiler message is generated. E.g. when using a `Console.WriteLine()`.

The generate code has no dependencies on other libraries. It only uses methods from the .NET BCL.

<br/>


## friflo ECS

This project started as a sub project of [friflo ECS](https://github.com/friflo/Friflo.Engine.ECS).  
In this context it is used to vectorize `[Query]` methods.
See [friflo ECS - Query Generator](https://friflo.gitbook.io/friflo.engine.ecs/documentation/query-optimization#query-generator).  
In this case the generated code requires `Friflo.Engine.ECS` as a dependency.

<br/>

*Support this project?*  
Leave a ⭐ at  [friflo Vectorization](https://github.com/friflo/Friflo.Vectorization)

<br/>

**License**

This project is licensed under MIT.  

Friflo.Engine.ECS  
Copyright © 2026   Ullrich Praetz - https://github.com/friflo
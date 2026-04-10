[![CI](https://img.shields.io/github/actions/workflow/status/friflo/Friflo.Vectorization/.github%2Fworkflows%2Fgenerators-ci.yml?logo=github-actions&logoColor=white&label=CI)](https://github.com/friflo/Friflo.Vectorization/actions/workflows/generators-ci.yml)
[![Discord](https://img.shields.io/discord/1209072279904976896?label=Discord&logo=discord&logoColor=white&labelColor=5E6AE9&color=4149A1)](https://discord.gg/nFfrhgQkb8)

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
static void MovePositionVector(Vector3[] position, Vector3[] velocity, float deltaTime) {
    for (int n = 0; n < position.Length; n++) {
        position[n] += velocity[n] * deltaTime;
    }
}
```
<br/>

friflo Vectorization applies the same optimization by generation C# code similar to C/C++ or Rust compilers.  
To enable this a similar method without a loop has to be annotated with `[Vectorize]`.
```cs
[Vectorize]
static void MovePosition([Span] ref Vector3 position, [Span] Vector3 velocity, float deltaTime) {
    position += velocity * deltaTime;
}
```
The source generator now creates a vectorized method suffixed `Vector`. It has the same parameters as the example on the top.
The generated shadow method can now be called with:
```cs
    MovePositionVector(positions, velocities, deltaTime);
```


### Supported operations
- All common operators for `float`, `Vector2`, `Vector3` and `Vector4`. These are: `+`, `-`, `*`, `/`.
- The common methods from `float`, `Vector2`, `Vector3` and `Vector4`.


### Vector specific optimization

Some combinations of math operations have specific AVX commands to speedup execution.  
The source generator detect these patterns and use these specific commands.  
Currently implemented:
- `(a * b) + c`
- `1 / Sqrt(a)`

The AVX instruction set used for vectorization is designed to operate on *Struct of Arrays* - **SoA**.  
An application typically uses arrays of `Vector3` or `Vector4`. Their memory layout is *Array of Structs* - **AoS**.  
**SoA** requires typically less AVX instructions for execution.  
In case **AoS** vector data need conversion to **SoA** additional instructions are added.

Many math operation like  `+`, `-`, `*`, `/` can be executed without this conversion.  
Some `Vector3` methods like `Dot()`, `Length()`, `Normalize()` and `Cross()` require **SoA**.  
The code generator detect these cases and apply **SoA** conversion only if needed.  


### Notes

friflo Vectorization is implemented as an incremental C# generator.  
In IDE's like Rider or Visual Studio methods are only updated if they are edited.  
If a method cannot be vectorized a compiler message is generated. E.g. when using a `Console.WriteLine()`.

The generated C# code has no dependencies on other libraries. It only uses **.NET** BCL API.  
To get an impression of the generated C# code check the files in the
[`Generated`](./src/Tests-Generators/Generated/Friflo.Vectorization.Generators/Friflo.Vectorization.Generators.AttributeQueryGenerator/) folder.

<br/>


## friflo ECS

This project started as a sub project of [friflo ECS](https://github.com/friflo/Friflo.Engine.ECS).  
In this context it is used to vectorize `[Query]` methods.
See [friflo ECS - Query Generator](https://friflo.gitbook.io/friflo.engine.ecs/documentation/query-optimization#query-generator).  
In this case the generated C# code requires `Friflo.Engine.ECS` as a dependency.

<br/>


## Feedback

Currently the most common used math operations are implemented. Will add missing methods over time.  
For feedback or support join [Discord](https://discord.gg/nFfrhgQkb8) or create an issue.

<br/>

*Support this project?*  
Leave a ⭐ at  [friflo Vectorization](https://github.com/friflo/Friflo.Vectorization)

**License**

This project is licensed under MIT.  

friflo Vectorization  
Copyright © 2026   Ullrich Praetz - https://github.com/friflo
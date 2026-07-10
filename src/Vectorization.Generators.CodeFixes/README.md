# Friflo.Vectorization.Generators


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
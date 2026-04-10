# Friflo.Vectorization.Attributes


Contains the attributes use by the C# Source generator [friflo Vectorization
](https://github.com/friflo/Friflo.Vectorization)
to vectorize methods containing floating point operations.


| Attribute         | Description
| ----------------- | -----------------------------------------------------------------------
| `[Vectorize]`     | Creates a shadow method suffixed with `Vector` for the annotated method.
| `[Span]`          | Annotated method parameters are converted to `Span<>` parameters.
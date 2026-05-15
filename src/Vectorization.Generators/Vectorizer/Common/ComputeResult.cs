// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.Generators;

public enum DataShape { 
    None,     // Initial state
    Vector,  // [X, Y, Z, W] - Interleaved AoS
    Scalar,  // [S, S, S, S] - Broadcasted result (from Dot, Length, etc.)
}

public readonly struct ComputeResult 
{
    private readonly    DataShape   shape;
    private readonly    bool        isValid;

    public override string ToString() => isValid ? shape.ToString() : "Invalid";

    private ComputeResult(DataShape shape, bool valid) 
    {
        this.shape = shape;
        isValid = valid;
    }

    public static ComputeResult Invalid => new ComputeResult(DataShape.None, false);
    public static ComputeResult Vector  => new ComputeResult(DataShape.Vector, true);
    public static ComputeResult Scalar  => new ComputeResult(DataShape.Scalar, true);
    
    public static implicit operator ComputeResult(DataShape shape) => new (shape, shape != DataShape.None);

    // This enables: if (!result) { return ...; }
    public static bool operator !(ComputeResult x) => !x.isValid;

    // DO NOT define 'operator true', 'operator false', or 'implicit operator bool'.
}

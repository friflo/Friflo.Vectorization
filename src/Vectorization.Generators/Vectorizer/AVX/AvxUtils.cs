// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public static class AvxUtils
{
    public static  bool InterleaveVector3(StringBuilder sb, string nm, Query query)
    {
        switch (query.vectorDimension) {
            case 1:
                sb.AppendLine($"            var {nm}_scalar = Vector256.Create({nm});");
                return true;
            case 2:
                if (query.useDeinterleave) {
                    sb.AppendLine($"            var {nm}_0 = Vector256.Create({nm}.X);");
                    sb.AppendLine($"            var {nm}_1 = Vector256.Create({nm}.Y);");
                } else {
                    sb.AppendLine($"            Vector128<float> {nm}_half = Vector128.Create({nm}.X, {nm}.Y, {nm}.X, {nm}.Y);");
                    sb.AppendLine($"            var {nm}_scalar = Avx.InsertVector128({nm}_half.ToVector256(), {nm}_half, 1);");
                }
                return true;
            case 3:
                sb.AppendLine($"            var {nm}_0 = Vector256.Create({nm}.X, {nm}.Y, {nm}.Z, {nm}.X, {nm}.Y, {nm}.Z, {nm}.X, {nm}.Y);");
                sb.AppendLine($"            var {nm}_1 = Vector256.Create({nm}.Z, {nm}.X, {nm}.Y, {nm}.Z, {nm}.X, {nm}.Y, {nm}.Z, {nm}.X);");
                sb.AppendLine($"            var {nm}_2 = Vector256.Create({nm}.Y, {nm}.Z, {nm}.X, {nm}.Y, {nm}.Z, {nm}.X, {nm}.Y, {nm}.Z);");
                return false;
            case 4:
                sb.AppendLine($"            Vector128<float> {nm}_half = Vector128.Create({nm}.X, {nm}.Y, {nm}.Z, {nm}.W);");
                sb.AppendLine($"            var {nm}_scalar = Avx.InsertVector128({nm}_half.ToVector256(), {nm}_half, 1);");
                // sb.AppendLine($"            var {nm}_1 = {nm}_0;");
                // sb.AppendLine($"            var {nm}_2 = {nm}_0;");
                // sb.AppendLine($"            var {nm}_3 = {nm}_0;");
                return true;
        }
        return true;
    }
    
    public static  void ScalarMask(StringBuilder sb, string name, int vectorDimension)
    {
        switch (vectorDimension) {
            case 1:
                // sb.AppendLine($"            Vector256<int> {name}_mask_0 = Vector256.Create( 0, 1, 2, 3, 4, 5, 6, 7);");
                break;
            case 2:
                sb.AppendLine($"            Vector256<int> {name}_mask_lo = Vector256.Create( 0, 0, 1, 1, 2, 2, 3, 3);");
                sb.AppendLine($"            Vector256<int> {name}_mask_hi = Vector256.Create( 4, 4, 5, 5, 6, 6, 7, 7);");
                sb.AppendLine();
                break;
            case 3:
                sb.AppendLine($"            Vector256<int> {name}_mask_0 = Vector256.Create(0, 0, 0, 1, 1, 1, 2, 2);");
                sb.AppendLine($"            Vector256<int> {name}_mask_1 = Vector256.Create(2, 3, 3, 3, 4, 4, 4, 5);");
                sb.AppendLine($"            Vector256<int> {name}_mask_2 = Vector256.Create(5, 5, 6, 6, 6, 7, 7, 7);");
                sb.AppendLine();
                break;
            case 4:
                sb.AppendLine($"            Vector256<int> {name}_mask_0 = Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1);");
                sb.AppendLine($"            Vector256<int> {name}_mask_1 = Vector256.Create(2, 2, 2, 2, 3, 3, 3, 3);");
                sb.AppendLine($"            Vector256<int> {name}_mask_2 = Vector256.Create(4, 4, 4, 4, 5, 5, 5, 5);");
                sb.AppendLine($"            Vector256<int> {name}_mask_3 = Vector256.Create(6, 6, 6, 6, 7, 7, 7, 7);");
                sb.AppendLine();
                break;
        }
    }

    public static void LoadMatrix(StringBuilder sb, string m, int queryVectorDimension)
    {
        switch (queryVectorDimension)
        {
            case 2:
                sb.AppendLine(
$"""
            // We use BroadcastScalarToVector128 to grab the first TWO floats of each row 
            // and repeat them across the 256-bit register.
            Vector128<float> {m}_row1 = Vector128.Create({m}.M11, {m}.M12, {m}.M11, {m}.M12);
            Vector128<float> {m}_row2 = Vector128.Create({m}.M21, {m}.M22, {m}.M21, {m}.M22);
            Vector128<float> {m}_row4 = Vector128.Create({m}.M41, {m}.M42, {m}.M41, {m}.M42);

            Vector256<float> matrix_0 = Avx.BroadcastVector128ToVector256((float*)&{m}_row1);
            Vector256<float> matrix_1 = Avx.BroadcastVector128ToVector256((float*)&{m}_row2);
            Vector256<float> matrix_3 = Avx.BroadcastVector128ToVector256((float*)&{m}_row4);                    
""");
                break;
            case 4:
                sb.AppendLine($"            // Load Matrix columns into 256-bit registers (each column duplicated)");
                sb.AppendLine($"            float* {m}_ptr = (float*)&{m};");
                sb.AppendLine($"            Vector256<float> {m}_0 = Avx.BroadcastVector128ToVector256({m}_ptr + 0);");
                sb.AppendLine($"            Vector256<float> {m}_1 = Avx.BroadcastVector128ToVector256({m}_ptr + 4);");
                sb.AppendLine($"            Vector256<float> {m}_2 = Avx.BroadcastVector128ToVector256({m}_ptr + 8);");
                sb.AppendLine($"            Vector256<float> {m}_3 = Avx.BroadcastVector128ToVector256({m}_ptr + 12);");
                break;
        }
/*
    Vector256<float> col0 = Vector256.Create(matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M11, matrix.M12, matrix.M13, matrix.M14);
    Vector256<float> col1 = Vector256.Create(matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M21, matrix.M22, matrix.M23, matrix.M24);
    Vector256<float> col2 = Vector256.Create(matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
    Vector256<float> col3 = Vector256.Create(matrix.M41, matrix.M42, matrix.M43, matrix.M44, matrix.M41, matrix.M42, matrix.M43, matrix.M44);
 */
    }

    public static void TrimEnd(StringBuilder stringBuilder)
    {
        var len = stringBuilder.Length - 1;
        while (stringBuilder[len] == '\n' ||
               stringBuilder[len] == '\r')
        {
            stringBuilder.Length = len--;
        }
    }
}
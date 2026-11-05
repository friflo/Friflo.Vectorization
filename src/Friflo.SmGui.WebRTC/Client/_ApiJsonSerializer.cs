// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;


// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable RedundantLambdaParameterType
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


[JsonSerializable(typeof(ApiRequest))]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(SignalMessage))]
internal sealed partial class ApiJsonSerializer : JsonSerializerContext
{
}
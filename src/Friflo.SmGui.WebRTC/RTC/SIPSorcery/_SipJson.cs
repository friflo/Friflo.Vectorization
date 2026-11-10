// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if !RTC_BROWSER

using System.Text.Json.Serialization;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.RTC.SIP;

[JsonSerializable(typeof(RTCIceCandidateInit))]
[JsonSerializable(typeof(RTCIceCandidate))]
internal sealed partial class SipJson : JsonSerializerContext { }

#endif

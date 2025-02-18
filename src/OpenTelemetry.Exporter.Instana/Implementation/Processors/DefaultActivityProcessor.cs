﻿// <copyright file="DefaultActivityProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors
{
    internal class DefaultActivityProcessor : ActivityProcessorBase, IActivityProcessor
    {
        public override async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
        {
            this.PreProcess(activity, instanaSpan);

            instanaSpan.N = InstanaExporterConstants.OTEL_SPAN_TYPE;

            string traceId = activity.TraceId.ToHexString();
            if (traceId.Length == 32)
            {
                instanaSpan.T = traceId.Substring(16);
                instanaSpan.Lt = traceId;
            }
            else
            {
                instanaSpan.T = traceId;
            }

            bool hasParent = false;
            string parentSpanId = activity.ParentSpanId.ToHexString();
            if (!string.IsNullOrEmpty(parentSpanId) && GetLongFromHex(parentSpanId) != 0)
            {
                hasParent = true;
                instanaSpan.P = parentSpanId;
            }

            instanaSpan.S = activity.SpanId.ToHexString();
            instanaSpan.K = GetSpanKind(activity.Kind);

            instanaSpan.Ts = activity.StartTimeUtc.Ticks;
            instanaSpan.D = activity.Duration.Ticks;

            SetKind(activity, instanaSpan);

            if (hasParent && instanaSpan.TransformInfo.IsEntrySpan)
            {
                // If an OTel entry span continues an ongoing trace (which is equivalent to the original span having a parent), it
                // always uses the IDs from the traceparent header, thus we mark the span with span.tp accordingly.
                instanaSpan.Tp = true;
            }

            await base.ProcessAsync(activity, instanaSpan);
        }

        private static SpanKind GetSpanKind(ActivityKind activityKind)
        {
            switch (activityKind)
            {
                case ActivityKind.Consumer:
                case ActivityKind.Server:
                    return SpanKind.ENTRY;
                case ActivityKind.Client:
                case ActivityKind.Producer:
                    return SpanKind.EXIT;
                case ActivityKind.Internal:
                    return SpanKind.INTERMEDIATE;
                default:
                    return SpanKind.NOT_SET;
            }
        }

        private static long GetLongFromHex(string hexValue)
        {
            if (!string.IsNullOrEmpty(hexValue))
            {
                try
                {
                    string[] ids = hexValue.Split(',');
                    return Convert.ToInt64(ids[ids.Length - 1].Trim(), 16);
                }
                catch (Exception)
                {
                }
            }

            return 0;
        }

        private static void SetKind(Activity activity, InstanaSpan instanaSpan)
        {
            bool isEntrySpan = false;
            switch (activity.Kind)
            {
                case ActivityKind.Server:
                    isEntrySpan = true;
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.SERVER_KIND;
                    break;
                case ActivityKind.Client:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.CLIENT_KIND;
                    break;
                case ActivityKind.Producer:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.PRODUCER_KIND;
                    break;
                case ActivityKind.Consumer:
                    isEntrySpan = true;
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.CONSUMER_KIND;
                    break;
                case ActivityKind.Internal:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.INTERNAL_KIND;
                    break;
                default:
                    break;
            }

            instanaSpan.TransformInfo.IsEntrySpan = isEntrySpan;
        }
    }
}

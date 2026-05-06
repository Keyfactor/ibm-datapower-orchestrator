// Copyright 2024 Keyfactor
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

using System;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    /// <summary>
    /// Shared plumbing for all IBM DataPower orchestrator job extensions. Provides PAM
    /// resolution with warn-on-empty fallback, JobResult helpers that append the
    /// <see cref="FlowLogger"/> summary, and exception unwrapping that surfaces
    /// <see cref="DataPowerApiException"/> details (HTTP status + response body).
    /// </summary>
    public abstract class JobBase
    {
        protected ILogger Logger;
        protected readonly IPAMSecretResolver Resolver;

        protected JobBase(IPAMSecretResolver resolver)
        {
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Resolves a PAM-eligible field. Returns the value as-is when it is null/empty
        /// (with a warning), otherwise hands it to the PAM resolver. This avoids passing
        /// empty strings into PAM providers which often misinterpret them as keys.
        /// </summary>
        protected string ResolvePamField(string name, string value)
        {
            Logger.LogTrace("Attempting to resolve PAM eligible field {FieldName}", name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Logger.LogWarning("PAM field {FieldName} has a null/empty value, returning as-is.", name);
                return value;
            }
            return Resolver.Resolve(value);
        }

        protected static JobResult SuccessResult(long jobHistoryId, string message = "")
        {
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = jobHistoryId,
                FailureMessage = message ?? ""
            };
        }

        protected static JobResult WarningResult(long jobHistoryId, string message)
        {
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Warning,
                JobHistoryId = jobHistoryId,
                FailureMessage = message ?? ""
            };
        }

        protected static JobResult FailureResult(long jobHistoryId, string message, FlowLogger flow = null)
        {
            var combined = message ?? "Unknown error";
            if (flow != null)
            {
                combined = $"{combined}\n\n{flow.GetSummary()}";
            }
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = jobHistoryId,
                FailureMessage = combined
            };
        }

        /// <summary>
        /// Unwraps an exception chain and produces a human-readable description. When a
        /// <see cref="DataPowerApiException"/> is anywhere in the chain (including inside
        /// an <see cref="AggregateException"/>), prefer its HTTP status + response body
        /// over the generic .Message - operators need to see what the appliance returned.
        /// </summary>
        protected static string DescribeException(Exception ex)
        {
            if (ex == null) return "Unknown error";

            var apiEx = DataPowerApiException.Find(ex);
            if (apiEx != null)
            {
                var body = string.IsNullOrWhiteSpace(apiEx.ResponseBody)
                    ? string.Empty
                    : $" - body: {Trim(apiEx.ResponseBody, 500)}";
                return $"DataPower API error during {apiEx.Operation}: HTTP {(int)apiEx.StatusCode} {apiEx.StatusCode}{body}";
            }

            if (ex is AggregateException agg && agg.InnerExceptions.Count > 0)
            {
                return agg.InnerExceptions[0].Message;
            }

            return ex.InnerException?.Message ?? ex.Message;
        }

        private static string Trim(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "...";
        }
    }
}

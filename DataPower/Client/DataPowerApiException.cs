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
using System.Net;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Client
{
    /// <summary>
    /// Thrown by <see cref="DataPowerClient"/> when the DataPower REST Management Interface
    /// returns a non-success status. Carries the HTTP status code and trimmed response body
    /// so callers can branch on specific conditions and operators can see what the appliance
    /// actually said.
    /// </summary>
    public class DataPowerApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Operation { get; }
        public string ResponseBody { get; }

        public DataPowerApiException(string message, HttpStatusCode statusCode, string operation, string responseBody)
            : base(message)
        {
            StatusCode = statusCode;
            Operation = operation;
            ResponseBody = responseBody;
        }

        public DataPowerApiException(string message, HttpStatusCode statusCode, string operation, string responseBody, Exception inner)
            : base(message, inner)
        {
            StatusCode = statusCode;
            Operation = operation;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// Walks an exception chain (including <see cref="AggregateException"/>) and returns the
        /// first <see cref="DataPowerApiException"/> found, or <c>null</c> if none is present.
        /// </summary>
        public static DataPowerApiException Find(Exception ex)
        {
            while (ex != null)
            {
                if (ex is DataPowerApiException api)
                    return api;
                if (ex is AggregateException agg)
                {
                    foreach (var inner in agg.InnerExceptions)
                    {
                        var found = Find(inner);
                        if (found != null) return found;
                    }
                    return null;
                }
                ex = ex.InnerException;
            }
            return null;
        }
    }
}

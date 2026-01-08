// Copyright 2023 Keyfactor
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    /// <summary>
    /// JSON converter that handles both old and new DataPower API response formats.
    /// Old format: {"value": "actual_value"}
    /// New format: "actual_value"
    /// </summary>
    public class CertDetailValueConverter : JsonConverter<CertDetailValue>
    {
        public override CertDetailValue ReadJson(JsonReader reader, Type objectType, CertDetailValue existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                // New format: direct string value
                return new CertDetailValue { Value = token.Value<string>() };
            }

            if (token.Type == JTokenType.Integer)
            {
                // Handle integer values (e.g., Version, SubjectPublicKeyBitLength)
                return new CertDetailValue { Value = token.Value<long>().ToString() };
            }

            if (token.Type == JTokenType.Object)
            {
                // Old format: {"value": "..."}
                var valueToken = token["value"];
                if (valueToken != null)
                {
                    string value;
                    if (valueToken.Type == JTokenType.String)
                    {
                        value = valueToken.Value<string>();
                    }
                    else if (valueToken.Type == JTokenType.Integer)
                    {
                        value = valueToken.Value<long>().ToString();
                    }
                    else
                    {
                        value = valueToken.ToString();
                    }
                    return new CertDetailValue { Value = value };
                }
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, CertDetailValue value, JsonSerializer serializer)
        {
            // When writing, use the old format for compatibility
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                writer.WriteValue(value.Value);
                writer.WriteEndObject();
            }
        }
    }
}

// Copyright 2022 Keyfactor
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

using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class CertificateDetailsObject
    {
        [JsonProperty("SerialNumber")] public CertDetailValue SerialNumber { get; set; }

        [JsonProperty("SignatureAlgorithm")] public CertDetailValue SignatureAlgorithm { get; set; }

        [JsonProperty("Issuer")] public CertDetailValue Issuer { get; set; }

        [JsonProperty("NotBefore")] public CertDetailValue NotBefore { get; set; }

        [JsonProperty("NotAfter")] public CertDetailValue NotAfter { get; set; }

        [JsonProperty("Subject")] public CertDetailValue Subject { get; set; }

        [JsonProperty("SubjectPublicKeyAlgorithm")]
        public CertDetailValue SubjectPublicKeyAlgorithm { get; set; }

        [JsonProperty("SubjectPublicKeyBitLength")]
        public CertDetailValue SubjectPublicKeyBitLength { get; set; }

        [JsonProperty("Base64")] public CertDetailValue EncodedCert { get; set; }
    }
}
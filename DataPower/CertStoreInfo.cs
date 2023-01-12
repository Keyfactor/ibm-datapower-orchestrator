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

using System.ComponentModel;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower
{
    public class CertStoreInfo
    {
        [DefaultValue("default")] public string Domain { get; set; }

        public string CertificateStore { get; set; }

        [JsonProperty("InventoryPageSize")]
        [DefaultValue(100)]
        public int InventoryPageSize { get; set; }

        [JsonProperty("PublicCertStoreName")]
        [DefaultValue("pubcert")]
        public string PublicCertStoreName { get; set; }

        [JsonProperty("InventoryBlackList")]
        [DefaultValue("")]
        public string InventoryBlackList { get; set; }

        [JsonProperty("Protocol")]
        [DefaultValue("https")]
        public string Protocol { get; set; }

        public string CryptoCertObjectPrefix { get; set; }
        public string CryptoKeyObjectPrefix { get; set; }
        public string CertFilePrefix { get; set; }
        public string KeyFilePrefix { get; set; }
    }
}
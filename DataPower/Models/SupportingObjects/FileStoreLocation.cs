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

using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    // One entry in the GET /mgmt/filestore/{domain} response. DataPower returns
    // these under filestore.location[] with names like "cert:" / "pubcert:" / "sharedcert:".
    public class FileStoreLocation
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("href")] public string Href { get; set; }
    }
}

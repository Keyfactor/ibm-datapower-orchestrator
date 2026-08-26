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

using System.Collections.Generic;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Client
{
    // Extracted so RequestManager and the Job classes can be unit tested against a
    // mock instead of the real DataPower REST Management Interface. DataPowerClient
    // remains the only implementation shipped to operators; this interface exists
    // purely as a test seam.
    public interface IDataPowerClient
    {
        string BaseUrl { get; set; }
        string Domain { get; set; }
        string Username { get; set; }
        string Password { get; set; }

        List<DomainInfo> ListDomains();
        List<string> ListFileStoreDirectories(string domain);
        bool SaveConfig();
        bool AddCertificateFile(CertificateAddRequest certAddRequest);
        bool AddCryptoCertificate(CryptoCertificateAddRequest cryptoCertAddRequest);
        bool UpdateCryptoCertificate(CryptoCertificateUpdateRequest cryptoCertUpdateRequest);
        bool AddCryptoKey(CryptoKeyAddRequest cryptoKeyAddRequest);
        bool UpdateCryptoKey(CryptoKeyUpdateRequest cryptoKeyUpdateRequest);
        ViewCryptoCertificatesResponse ViewCertificates(ViewCryptoCertificatesRequest viewCertificates);
        ViewCertificateDetailResponse ViewCryptoCertificate(ViewCertificateDetailRequest viewCertificate);
        ViewPublicCertificatesResponse ViewPublicCertificates(ViewPublicCertificatesRequest viewPubCertificates);
        ViewPubCertificateDetailResponse ViewPublicCertificate(ViewPubCertificateDetailRequest viewPubCertificate);
        void DeleteCryptoKey(DeleteCryptoKeyRequest request);
        void DeleteCryptoCertificate(DeleteCryptoCertificateRequest request);
        void DeleteCertificate(DeleteCertificateRequest request);
        ViewCryptoKeysResponse ViewCryptoKeys(ViewCryptoKeysRequest request);
        ViewCryptoCertificateSingleResponse ViewCryptoCertificate(ViewCryptoCertificatesRequest request);
    }
}

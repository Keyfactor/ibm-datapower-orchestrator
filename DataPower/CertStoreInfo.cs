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
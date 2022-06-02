using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewCryptoKeysResponse
    {
        public ViewCryptoKeysResponse()
        {
            CryptoKeys = new CryptoKey[0];
        }


        [JsonProperty("CryptoKey")] public CryptoKey[] CryptoKeys { get; set; }
    }
}
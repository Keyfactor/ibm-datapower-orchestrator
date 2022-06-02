using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    #region JSON Request and Response Classes

    public abstract class Request
    {
        [JsonIgnore] public string Method { get; set; }

        [JsonIgnore] public string Domain { get; set; }

        [JsonIgnore] public string Filename { get; set; }

        public string GetResource()
        {
            return "";
        }
    }

    #endregion
}
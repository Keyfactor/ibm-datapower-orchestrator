using System;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        private readonly ILogger<Inventory> _logger;

        private readonly RequestManager _reqManager;
        private string _protocol;

        public Inventory(ILogger<Inventory> logger)
        {
            _logger = logger;
            _reqManager = new RequestManager();
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                return PerformInventory(jobConfiguration, submitInventoryUpdate);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In Inventory.ProcessJob: {LogHandler.FlattenException(e)}");
                return new JobResult
                {
                    FailureMessage = $"Unknown Exception Occured In ProcessJob: {LogHandler.FlattenException(e)}",
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    Result = OrchestratorJobStatusJobResult.Failure
                };
            }
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                _logger.LogTrace("Parse: Certificate Inventory: " + config.CertificateStoreDetails.StorePath);
                var ci = Utility.ParseCertificateConfig(config);
                _protocol = ci.Protocol;
                _logger.LogTrace(
                    $"Certificate Config Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");
                _logger.LogTrace($"Any Job Config {JsonConvert.SerializeObject(config)}");
                _logger.LogTrace("Entering IBM DataPower: Certificate Inventory");
                _logger.LogTrace(
                    $"Entering processJob for Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");

                var apiClient = new DataPowerClient(config.ServerUsername, config.ServerPassword,
                    $"{_protocol}://" + config.CertificateStoreDetails.ClientMachine.Trim(), ci.Domain);

                var publicCertStoreName = ci.PublicCertStoreName;
                _logger.LogTrace($"$Public Store name is {publicCertStoreName}");

            var storePath = config.CertificateStoreDetails.StorePath; 
            
            var inventoryResult = storePath.Contains(publicCertStoreName)
                ? _reqManager.GetPublicCerts(config,apiClient,submitInventory,ci)
                : _reqManager.GetCerts(config,apiClient,submitInventory,ci);

                return inventoryResult;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In Inventory.PerformInventory: {LogHandler.FlattenException(e)}");
                throw;
            }
        }
    }
}
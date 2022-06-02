using System;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    public class Management : IManagementJobExtension
    {
        private readonly RequestManager _certManager;
        private readonly ILogger<Management> _logger;

        public Management(ILogger<Management> logger)
        {
            _logger = logger;
            _certManager = new RequestManager();
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Any Job Config {JsonConvert.SerializeObject(config)}");

                var ci = Utility.ParseCertificateConfig(config);
                var np = Utility.ParseStoreProperties(config);

                _logger.LogTrace($"ci {JsonConvert.SerializeObject(ci)}");
                _logger.LogTrace($"np {JsonConvert.SerializeObject(np)}");

                JobResult result;

                _logger.LogTrace("Entering IBM DataPower: Inventory Management for DOMAIN: " + ci.Domain);
                switch (config.OperationType.ToString())
                {
                    case "Add":
                        _logger.LogTrace("Entering Add Job..");
                        result = _certManager.Add(config, ci, np);
                        _logger.LogTrace("Finished Add Job..");
                        _logger.LogTrace($"result {JsonConvert.SerializeObject(result)}");
                        break;
                    case "Remove":
                        _logger.LogTrace("Entering Remove Job..");
                        result = _certManager.Remove(config, ci, np);
                        _logger.LogTrace("Finished Remove Job..");
                        _logger.LogTrace($"result {JsonConvert.SerializeObject(result)}");
                        break;
                    default:
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = "Unrecognized Operation"
                        };
                }

                _logger.MethodExit(LogLevel.Debug);

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In ProcessJob.ProcessJob: {LogHandler.FlattenException(e)}");
                return new JobResult
                {
                    FailureMessage = $"Unknown Exception Occured In ProcessJob: {LogHandler.FlattenException(e)}",
                    JobHistoryId = config.JobHistoryId,
                    Result = OrchestratorJobStatusJobResult.Failure
                };
            }
        }
    }
}
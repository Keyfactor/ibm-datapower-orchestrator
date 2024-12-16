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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    public class Management : IManagementJobExtension
    {
        private RequestManager _certManager;
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _resolver;


        public Management(IPAMSecretResolver resolver)
        {
            _certManager=new RequestManager(resolver);
            _logger = LogHandler.GetClassLogger<Management>();
            _resolver = resolver;
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);

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
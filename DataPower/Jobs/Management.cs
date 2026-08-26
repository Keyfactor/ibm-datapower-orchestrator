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
    public class Management : JobBase, IManagementJobExtension
    {
        // Exposed as protected internal (rather than a private field) purely as a
        // unit-test seam - a test subclass can reach in and swap CertManager.ClientFactory
        // for a mock IDataPowerClient factory. Production code never touches this.
        protected internal RequestManager CertManager { get; }

        public Management(IPAMSecretResolver resolver) : base(resolver)
        {
            Logger = LogHandler.GetClassLogger<Management>();
            CertManager = new RequestManager(resolver);
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (config == null)
            {
                Logger.LogError("ProcessJob called with null config.");
                return FailureResult(0, "ManagementJobConfiguration is null.");
            }

            using (var flow = new FlowLogger(Logger, "Management-ProcessJob"))
            {
                try
                {
                    Logger.MethodEntry(LogLevel.Debug);

                    flow.Step("ValidateConfig", () =>
                    {
                        if (config.CertificateStoreDetails == null)
                            throw new ArgumentNullException(nameof(config), "CertificateStoreDetails is null.");
                        if (string.IsNullOrWhiteSpace(config.CertificateStoreDetails.ClientMachine))
                            throw new ArgumentException("ClientMachine is null or empty.");
                        if (string.IsNullOrWhiteSpace(config.CertificateStoreDetails.StorePath))
                            throw new ArgumentException("StorePath is null or empty.");
                    });

                    CertStoreInfo ci = null;
                    flow.Step("ParseCertificateConfig", () =>
                    {
                        ci = Utility.ParseCertificateConfig(config);
                        if (ci == null)
                            throw new InvalidOperationException("Failed to parse certificate store configuration.");
                    }, $"storePath={config.CertificateStoreDetails.StorePath}");

                    Models.SupportingObjects.NamePrefix np = null;
                    flow.Step("ParseStoreProperties", () =>
                    {
                        np = Utility.ParseStoreProperties(config);
                    });

                    Logger.LogTrace($"ci {JsonConvert.SerializeObject(ci)}");
                    Logger.LogTrace($"np {JsonConvert.SerializeObject(np)}");
                    Logger.LogTrace("Entering IBM DataPower: Inventory Management for DOMAIN: " + ci.Domain);

                    JobResult result;
                    var operation = config.OperationType.ToString();

                    flow.Branch(operation);
                    try
                    {
                        switch (operation)
                        {
                            case "Add":
                                result = flow.Step<JobResult>("Add", () => CertManager.Add(config, ci, np));
                                break;
                            case "Remove":
                                result = flow.Step<JobResult>("Remove", () => CertManager.Remove(config, ci, np));
                                break;
                            default:
                                flow.Fail("OperationType", $"Unrecognized operation '{operation}'");
                                return FailureResult(config.JobHistoryId,
                                    $"Unrecognized Operation: {operation}", flow);
                        }
                    }
                    finally
                    {
                        flow.EndBranch();
                    }

                    Logger.LogTrace($"result {JsonConvert.SerializeObject(result)}");
                    Logger.MethodExit(LogLevel.Debug);

                    if (result?.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        flow.Step("Result", "SUCCESS");
                        return SuccessResult(config.JobHistoryId, flow.GetSummary());
                    }

                    if (result != null)
                    {
                        flow.Fail("Result", result.FailureMessage ?? "Operation reported non-success.");
                        result.FailureMessage = $"{result.FailureMessage}\n\n{flow.GetSummary()}";
                        return result;
                    }

                    return FailureResult(config.JobHistoryId, "Operation returned a null result.", flow);
                }
                catch (Exception e)
                {
                    var msg = DescribeException(e);
                    flow.Fail("ProcessJob", msg);
                    Logger.LogError(e, "Error In Management.ProcessJob: {ErrorMessage}", LogHandler.FlattenException(e));
                    return FailureResult(config.JobHistoryId,
                        $"Unknown Exception Occured In ProcessJob: {msg}", flow);
                }
            }
        }
    }
}

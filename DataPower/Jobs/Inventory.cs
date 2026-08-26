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
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    public class Inventory : JobBase, IInventoryJobExtension
    {
        // protected internal purely as a unit-test seam - see Management.CertManager.
        protected internal RequestManager ReqManager { get; }
        private string _protocol;

        public Inventory(IPAMSecretResolver resolver) : base(resolver)
        {
            Logger = LogHandler.GetClassLogger<Inventory>();
            ReqManager = new RequestManager(resolver);
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            if (jobConfiguration == null)
            {
                Logger.LogError("ProcessJob called with null jobConfiguration.");
                return FailureResult(0, "InventoryJobConfiguration is null.");
            }

            if (submitInventoryUpdate == null)
            {
                Logger.LogError("ProcessJob called with null submitInventoryUpdate.");
                return FailureResult(jobConfiguration.JobHistoryId, "SubmitInventoryUpdate delegate is null.");
            }

            using (var flow = new FlowLogger(Logger, "Inventory-ProcessJob"))
            {
                try
                {
                    Logger.MethodEntry(LogLevel.Debug);

                    flow.Step("ValidateConfig", () =>
                    {
                        if (jobConfiguration.CertificateStoreDetails == null)
                            throw new ArgumentNullException(nameof(jobConfiguration),
                                "CertificateStoreDetails is null.");
                        if (string.IsNullOrWhiteSpace(jobConfiguration.CertificateStoreDetails.ClientMachine))
                            throw new ArgumentException("ClientMachine is null or empty.");
                        if (string.IsNullOrWhiteSpace(jobConfiguration.CertificateStoreDetails.StorePath))
                            throw new ArgumentException("StorePath is null or empty.");
                    });

                    return PerformInventory(jobConfiguration, submitInventoryUpdate, flow);
                }
                catch (Exception e)
                {
                    var msg = DescribeException(e);
                    flow.Fail("ProcessJob", msg);
                    Logger.LogError(e, "Error In Inventory.ProcessJob: {ErrorMessage}", LogHandler.FlattenException(e));
                    return FailureResult(jobConfiguration.JobHistoryId,
                        $"Unknown Exception Occured In ProcessJob: {msg}", flow);
                }
            }
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory, FlowLogger flow)
        {
            try
            {
                CertStoreInfo ci = null;
                flow.Step("ParseCertificateConfig", () =>
                {
                    Logger.LogTrace("Parse: Certificate Inventory: " + config.CertificateStoreDetails.StorePath);
                    ci = Utility.ParseCertificateConfig(config);
                    if (ci == null)
                        throw new InvalidOperationException("Failed to parse certificate store configuration.");
                    _protocol = ci.Protocol;
                }, $"storePath={config.CertificateStoreDetails.StorePath}");

                Logger.LogTrace($"Certificate Config Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");

                IDataPowerClient apiClient = null;
                flow.Step("CreateApiClient", () =>
                {
                    apiClient = CreateApiClient(
                        ResolvePamField("ServerUserName", config.ServerUsername),
                        ResolvePamField("ServerPassword", config.ServerPassword),
                        $"{_protocol}://" + config.CertificateStoreDetails.ClientMachine.Trim(),
                        ci.Domain);
                }, $"domain={ci.Domain}, host={config.CertificateStoreDetails.ClientMachine}");

                var publicCertStoreName = ci.PublicCertStoreName;
                Logger.LogTrace($"$Public Store name is {publicCertStoreName}");

                var storePath = config.CertificateStoreDetails.StorePath;
                var inventoryResult = flow.Step<JobResult>(
                    storePath.Contains(publicCertStoreName) ? "GetPublicCerts" : "GetCerts",
                    () => storePath.Contains(publicCertStoreName)
                        ? ReqManager.GetPublicCerts(config, apiClient, submitInventory, ci, flow)
                        : ReqManager.GetCerts(config, apiClient, submitInventory, ci, flow));

                flow.Step("Result", $"{inventoryResult.Result}");

                // Append flow summary to result message so operators see the breadcrumb
                if (inventoryResult.Result == OrchestratorJobStatusJobResult.Success)
                {
                    return SuccessResult(config.JobHistoryId, flow.GetSummary());
                }

                inventoryResult.FailureMessage = $"{inventoryResult.FailureMessage}\n\n{flow.GetSummary()}";
                return inventoryResult;
            }
            catch (Exception e)
            {
                var msg = DescribeException(e);
                flow.Fail("PerformInventory", msg);
                Logger.LogError(e, "Error In Inventory.PerformInventory: {ErrorMessage}", LogHandler.FlattenException(e));
                return FailureResult(config.JobHistoryId, $"Inventory failed: {msg}", flow);
            }
        }
    }
}

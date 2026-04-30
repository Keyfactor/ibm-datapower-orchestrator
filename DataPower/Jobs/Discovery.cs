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
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Jobs
{
    public class Discovery : JobBase, IDiscoveryJobExtension
    {
        // Certificate-relevant filestore directories on DataPower
        private static readonly HashSet<string> CertStoreDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cert",
            "pubcert",
            "sharedcert"
        };

        public Discovery(IPAMSecretResolver resolver) : base(resolver)
        {
            Logger = LogHandler.GetClassLogger<Discovery>();
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(DiscoveryJobConfiguration jobConfiguration,
            SubmitDiscoveryUpdate submitDiscoveryUpdate)
        {
            if (jobConfiguration == null)
            {
                Logger.LogError("ProcessJob called with null jobConfiguration.");
                return FailureResult(0, "DiscoveryJobConfiguration is null.");
            }

            if (submitDiscoveryUpdate == null)
            {
                Logger.LogError("ProcessJob called with null submitDiscoveryUpdate.");
                return FailureResult(jobConfiguration.JobHistoryId, "SubmitDiscoveryUpdate delegate is null.");
            }

            using (var flow = new FlowLogger(Logger, "Discovery-ProcessJob"))
            {
                try
                {
                    Logger.MethodEntry(LogLevel.Debug);

                    flow.Step("ValidateConfig", () =>
                    {
                        if (string.IsNullOrWhiteSpace(jobConfiguration.ClientMachine))
                            throw new ArgumentException("ClientMachine is null or empty.");
                    });

                    return PerformDiscovery(jobConfiguration, submitDiscoveryUpdate, flow);
                }
                catch (Exception e)
                {
                    var msg = DescribeException(e);
                    flow.Fail("ProcessJob", msg);
                    Logger.LogError(e, "Error In Discovery.ProcessJob: {ErrorMessage}", LogHandler.FlattenException(e));
                    return FailureResult(jobConfiguration.JobHistoryId,
                        $"Unknown Exception Occured In ProcessJob: {msg}", flow);
                }
            }
        }

        private JobResult PerformDiscovery(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery, FlowLogger flow)
        {
            try
            {
                var protocol = "https";
                flow.Step("ParseProtocol", () =>
                {
                    if (config.JobProperties != null && config.JobProperties.ContainsKey("Protocol"))
                    {
                        protocol = config.JobProperties["Protocol"]?.ToString() ?? "https";
                    }
                }, $"protocol={protocol}");

                var baseUrl = $"{protocol}://" + config.ClientMachine.Trim();
                Logger.LogTrace($"Entering IBM DataPower: Discovery for appliance {config.ClientMachine}");

                DataPowerClient apiClient = null;
                flow.Step("CreateApiClient", () =>
                {
                    apiClient = new DataPowerClient(
                        ResolvePamField("ServerUserName", config.ServerUsername),
                        ResolvePamField("ServerPassword", config.ServerPassword),
                        baseUrl,
                        "default");
                }, $"host={config.ClientMachine}");

                List<Models.SupportingObjects.DomainInfo> domains = null;
                flow.Step("ListDomains", () =>
                {
                    domains = apiClient.ListDomains();
                }, $"will populate domains");

                var domainCount = domains?.Count ?? 0;
                Logger.LogTrace($"Found {domainCount} domain(s)");

                var discoveredLocations = new List<string>();

                if (domainCount == 0)
                {
                    flow.Skip("DiscoverDirectories", "no domains returned");
                }
                else
                {
                    flow.Branch($"PerDomain (count={domainCount})");
                    try
                    {
                        foreach (var domain in domains)
                        {
                            if (string.IsNullOrWhiteSpace(domain?.Name))
                            {
                                flow.Skip("Domain", "empty domain name");
                                continue;
                            }

                            try
                            {
                                List<string> directories = null;
                                flow.Step($"ListFileStore-{domain.Name}", () =>
                                {
                                    directories = apiClient.ListFileStoreDirectories(domain.Name);
                                });

                                var certDirectories = directories
                                    .Where(d => CertStoreDirectories.Contains(d))
                                    .ToList();

                                foreach (var dir in certDirectories)
                                {
                                    var storePath = $"{domain.Name}\\{dir}";
                                    discoveredLocations.Add(storePath);
                                    flow.Step($"Discovered-{storePath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Resilient by design: one inaccessible domain should not abort discovery
                                var inner = DescribeException(ex);
                                flow.Skip($"Domain-{domain.Name}", $"unable to list directories: {inner}");
                                Logger.LogWarning(ex, "Unable to list filestore directories for domain {DomainName}: {ErrorMessage}",
                                    domain.Name, inner);
                            }
                        }
                    }
                    finally
                    {
                        flow.EndBranch();
                    }
                }

                flow.Step("SubmitDiscovery", () => submitDiscovery.Invoke(discoveredLocations),
                    $"locationCount={discoveredLocations.Count}");

                Logger.MethodExit(LogLevel.Debug);

                flow.Step("Result", $"SUCCESS - {discoveredLocations.Count} locations discovered");
                return SuccessResult(config.JobHistoryId, flow.GetSummary());
            }
            catch (Exception e)
            {
                var msg = DescribeException(e);
                flow.Fail("PerformDiscovery", msg);
                Logger.LogError(e, "Error In Discovery.PerformDiscovery: {ErrorMessage}", LogHandler.FlattenException(e));
                return FailureResult(config.JobHistoryId, $"Discovery failed: {msg}", flow);
            }
        }
    }
}

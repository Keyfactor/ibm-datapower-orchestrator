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
    public class Discovery : IDiscoveryJobExtension
    {
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _resolver;

        // Certificate-relevant filestore directories on DataPower
        private static readonly HashSet<string> CertStoreDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cert",
            "pubcert",
            "sharedcert"
        };

        public Discovery(IPAMSecretResolver resolver)
        {
            _logger = LogHandler.GetClassLogger<Discovery>();
            _resolver = resolver;
        }

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        public string ExtensionName => "DataPower";

        public JobResult ProcessJob(DiscoveryJobConfiguration jobConfiguration,
            SubmitDiscoveryUpdate submitDiscoveryUpdate)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                return PerformDiscovery(jobConfiguration, submitDiscoveryUpdate);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In Discovery.ProcessJob: {LogHandler.FlattenException(e)}");
                return new JobResult
                {
                    FailureMessage = $"Unknown Exception Occured In ProcessJob: {LogHandler.FlattenException(e)}",
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    Result = OrchestratorJobStatusJobResult.Failure
                };
            }
        }

        private JobResult PerformDiscovery(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery)
        {
            try
            {
                var protocol = "https";
                if (config.JobProperties != null && config.JobProperties.ContainsKey("Protocol"))
                {
                    protocol = config.JobProperties["Protocol"]?.ToString() ?? "https";
                }

                var baseUrl = $"{protocol}://" + config.ClientMachine.Trim();

                _logger.LogTrace($"Entering IBM DataPower: Discovery for appliance {config.ClientMachine}");

                var apiClient = new DataPowerClient(
                    ResolvePamField("ServerUserName", config.ServerUsername),
                    ResolvePamField("ServerPassword", config.ServerPassword),
                    baseUrl,
                    "default");

                // Step 1: List all domains on the appliance
                _logger.LogTrace("Discovering domains on DataPower appliance...");
                var domains = apiClient.ListDomains();
                _logger.LogTrace($"Found {domains.Count} domain(s)");

                var discoveredLocations = new List<string>();

                // Step 2: For each domain, discover certificate store directories
                foreach (var domain in domains)
                {
                    _logger.LogTrace($"Discovering filestore directories for domain: {domain.Name}");
                    try
                    {
                        var directories = apiClient.ListFileStoreDirectories(domain.Name);
                        _logger.LogTrace($"Found {directories.Count} directory(ies) in domain {domain.Name}");

                        var certDirectories = directories
                            .Where(d => CertStoreDirectories.Contains(d))
                            .ToList();

                        foreach (var dir in certDirectories)
                        {
                            var storePath = $"{domain.Name}\\{dir}";
                            _logger.LogTrace($"Discovered certificate store: {storePath}");
                            discoveredLocations.Add(storePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Unable to list filestore directories for domain {domain.Name}: {LogHandler.FlattenException(ex)}");
                    }
                }

                _logger.LogTrace($"Discovery complete. Found {discoveredLocations.Count} certificate store location(s).");

                submitDiscovery.Invoke(discoveredLocations);

                _logger.MethodExit(LogLevel.Debug);

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In Discovery.PerformDiscovery: {LogHandler.FlattenException(e)}");
                throw;
            }
        }
    }
}

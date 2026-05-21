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
using System.Text.RegularExpressions;
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
        // Default cert-relevant filestore directories on DataPower. Used when the
        // operator leaves the Discovery job's "Directories to search" field empty.
        private static readonly HashSet<string> DefaultCertStoreDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cert",
            "pubcert",
            "sharedcert"
        };

        // pubcert and sharedcert are appliance-wide on DataPower (owned by the
        // default domain) - other domains can read them but writes must go through
        // default. Discovery emits these only under "default" so operators don't
        // get N copies of the same physical store, one per domain, all aliasing
        // each other.
        private static readonly HashSet<string> ApplianceWideDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pubcert",
            "sharedcert"
        };

        private const string DefaultDomainName = "default";

        // Keyfactor Command's Discovery form posts the comma-separated "Directories
        // to search" value into JobProperties. Try the common key names since the
        // exact casing has shifted across Command versions.
        private static readonly string[] DirsToSearchKeys = { "dirs", "Dirs", "directories", "Directories", "DirsToSearch" };

        // Extracts a stable group key for an exception thrown by a per-domain
        // ListFileStoreDirectories call. Strips domain-specific bits (the /_links/self/href
        // URL changes per domain) so identical RBAC failures across hundreds of domains
        // collapse into a single error group.
        private static readonly Regex ErrorMessageRegex = new Regex(
            "\"error\"\\s*:\\s*\\[\\s*\"([^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static string ErrorSignatureOf(Exception ex)
        {
            var apiEx = DataPowerApiException.Find(ex);
            if (apiEx != null)
            {
                var m = ErrorMessageRegex.Match(apiEx.ResponseBody ?? string.Empty);
                if (m.Success)
                    return $"HTTP {(int)apiEx.StatusCode} {apiEx.StatusCode}: {m.Groups[1].Value}";
                return $"HTTP {(int)apiEx.StatusCode} {apiEx.StatusCode}";
            }

            var msg = ex?.Message ?? "(no message)";
            return msg.Length > 80 ? msg.Substring(0, 80) + "..." : msg;
        }

        private static (HashSet<string> Dirs, string Source) ResolveDirsToSearch(DiscoveryJobConfiguration config)
        {
            if (config?.JobProperties != null)
            {
                foreach (var key in DirsToSearchKeys)
                {
                    if (!config.JobProperties.TryGetValue(key, out var raw)) continue;
                    var s = raw?.ToString();
                    if (string.IsNullOrWhiteSpace(s)) continue;

                    var dirs = new HashSet<string>(
                        s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => d.Trim().TrimEnd(':'))
                            .Where(d => d.Length > 0),
                        StringComparer.OrdinalIgnoreCase);

                    if (dirs.Count > 0)
                        return (dirs, $"user (key={key})");
                }
            }

            return (DefaultCertStoreDirectories, "default");
        }

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

                var resolvedDirs = ResolveDirsToSearch(config);
                var certStoreDirectories = resolvedDirs.Dirs;
                flow.Step("ResolveDirsToSearch",
                    $"source={resolvedDirs.Source}, dirs=[{string.Join(",", certStoreDirectories)}]");

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
                        var listedOk = 0;
                        var emptyNameSkipped = 0;

                        // Group per-domain failures by error signature instead of emitting a
                        // FAIL+SKIP line per failed domain. On appliances with 200+ domains and
                        // a non-trivial RBAC story, that easily produces a 50 KB summary which
                        // overflows Command's AgentJobStatus.Message column. One aggregated SKIP
                        // line per error class scales fine and is far more readable.
                        var errorGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                        foreach (var domain in domains)
                        {
                            if (string.IsNullOrWhiteSpace(domain?.Name))
                            {
                                emptyNameSkipped++;
                                continue;
                            }

                            List<string> directories;
                            try
                            {
                                directories = apiClient.ListFileStoreDirectories(domain.Name);
                            }
                            catch (Exception ex)
                            {
                                // Resilient by design: one inaccessible domain should not abort discovery
                                var signature = ErrorSignatureOf(ex);
                                if (!errorGroups.TryGetValue(signature, out var list))
                                {
                                    list = new List<string>();
                                    errorGroups[signature] = list;
                                }
                                list.Add(domain.Name);
                                Logger.LogWarning(ex,
                                    "Unable to list filestore directories for domain {DomainName}: {ErrorMessage}",
                                    domain.Name, DescribeException(ex));
                                continue;
                            }

                            listedOk++;

                            // DataPower's filestore location names carry a trailing colon
                            // (e.g. "cert:" / "pubcert:" / "sharedcert:"). Strip it before
                            // matching and before composing the store path.
                            var certDirectories = directories
                                .Select(d => d?.TrimEnd(':'))
                                .Where(d => !string.IsNullOrEmpty(d) && certStoreDirectories.Contains(d))
                                .ToList();

                            var isDefault = string.Equals(domain.Name, DefaultDomainName, StringComparison.OrdinalIgnoreCase);
                            foreach (var dir in certDirectories)
                            {
                                if (ApplianceWideDirectories.Contains(dir) && !isDefault)
                                    continue; // appliance-wide; emitted only under default

                                var storePath = $"{domain.Name}\\{dir}";
                                discoveredLocations.Add(storePath);
                                flow.Step($"Discovered-{storePath}");
                            }
                        }

                        var listedDetail = $"listed={listedOk}/{domainCount}";
                        if (emptyNameSkipped > 0)
                            listedDetail += $", emptyName={emptyNameSkipped}";
                        if (errorGroups.Count > 0)
                            listedDetail += $", failed={errorGroups.Values.Sum(v => v.Count)}";
                        flow.Step("ListFileStore", listedDetail);

                        foreach (var kvp in errorGroups.OrderByDescending(g => g.Value.Count))
                        {
                            var sample = kvp.Value.Take(5).ToList();
                            var more = kvp.Value.Count - sample.Count;
                            var sampleStr = string.Join(", ", sample) + (more > 0 ? $" (+{more} more)" : "");
                            flow.Skip($"DomainsFailed[{kvp.Key}]", $"{kvp.Value.Count} domain(s): {sampleStr}");
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

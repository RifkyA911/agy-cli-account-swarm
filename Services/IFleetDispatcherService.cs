using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IFleetDispatcherService
{
    Task<ResourceCheckResult> CheckPreflightResourcesAsync(string workspacePath);
    string SynthesizePrompt(string baseObjective, string role, DispatchMode mode, int workerIndex, int totalWorkers);
    Task<List<DispatchedWorkerTask>> DispatchFleetAsync(
        FleetDispatchConfig config, 
        IEnumerable<AccountProfile> selectedWorkers, 
        TerminalType terminal);
    Task<int> AbortFleetAsync(IEnumerable<DispatchedWorkerTask> activeTasks);
}

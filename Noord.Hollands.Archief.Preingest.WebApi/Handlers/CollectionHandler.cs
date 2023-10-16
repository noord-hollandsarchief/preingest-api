using Newtonsoft.Json;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Model;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Microsoft.Extensions.Options;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler to collect and bundle all collection instances information.
    /// </summary>
    public class CollectionHandler
    {
        private readonly AppSettings _settings = null;
        public CollectionHandler(IOptions<AppSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <summary>
        /// Gets all the collections.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public dynamic GetCollections()
        {
            var directory = new DirectoryInfo(_settings.DataFolderName);

            if (!directory.Exists)
                throw new DirectoryNotFoundException(String.Format("Data folder '{0}' not found!", _settings.DataFolderName));

            dynamic dataResults = null;

            var tarArchives = directory.GetFiles("*.*").Where(s => s.Extension.EndsWith(".tar", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase));

            tarArchives.ToList().ForEach(item =>
            {
                var workingDir = Path.Combine(directory.FullName, ChecksumHelper.GeneratePreingestGuid(item.Name).ToString());
                if (!Directory.Exists(workingDir))
                    directory.CreateSubdirectory(ChecksumHelper.GeneratePreingestGuid(item.Name).ToString());
            });

            List<JoinedQueryResult> currentActions = new List<JoinedQueryResult>();
            List<ExecutionPlan> executionPlan = new List<ExecutionPlan>();

            using (var context = new PreIngestStatusContext())
            {
                var query = context.PreingestActionCollection.Where(item => tarArchives.Select(tar
                    => ChecksumHelper.GeneratePreingestGuid(tar.Name)).ToList().Contains(item.FolderSessionId)).Join(context.ActionStateCollection,
                    states => states.ProcessId,
                    actions => actions.ProcessId,
                    (actions, states)
                    => new JoinedQueryResult { Actions = actions, States = states }).ToList();

                if (query != null && query.Count > 0)
                    currentActions.AddRange(query);

                var plans = context.ExecutionPlanCollection.Where(item => tarArchives.Select(tar => ChecksumHelper.GeneratePreingestGuid(tar.Name)).ToList().Contains(item.SessionId)).OrderBy(item => item.Sequence).ToArray();

                if (plans != null && plans.Length > 0)
                    executionPlan.AddRange(plans);
            }

            if (currentActions != null || currentActions.Count > 0)
            {
                var joinedActions = currentActions.Select(actions => actions.Actions).Distinct().Select(item => new QueryResultAction
                {
                    ActionStatus = String.IsNullOrEmpty(item.ActionStatus) ? "Executing" : item.ActionStatus,
                    Creation = item.Creation,
                    Description = item.Description,
                    FolderSessionId = item.FolderSessionId,
                    Name = item.Name,
                    ProcessId = item.ProcessId,
                    ResultFiles = String.IsNullOrEmpty(item.ResultFiles) ? new string[] { } : item.ResultFiles.Split(";").ToArray(),
                    Summary = String.IsNullOrEmpty(item.StatisticsSummary) ? null : JsonConvert.DeserializeObject<PreingestStatisticsSummary>(item.StatisticsSummary),
                    States = currentActions.Select(state
                        => state.States).Where(state
                            => state.ProcessId == item.ProcessId).Select(state
                                => new QueryResultState
                                {
                                    StatusId = state.StatusId,
                                    Name = state.Name,
                                    Creation = state.Creation
                                }).ToArray()
                });

                dataResults = tarArchives.OrderByDescending(item
                => item.CreationTime).Select(item
                    => new
                    {
                        Name = item.Name,
                        SessionId = ChecksumHelper.GeneratePreingestGuid(item.Name),
                        CreationTime = item.CreationTime,
                        LastWriteTime = item.LastWriteTime,
                        LastAccessTime = item.LastAccessTime,
                        Size = item.Length,
                        Settings = new SettingsReader(item.DirectoryName, ChecksumHelper.GeneratePreingestGuid(item.Name)).GetSettings(),
                        ScheduledPlan = new ScheduledPlanStatusHandler(executionPlan.Where(ep
                        => ep.SessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToList(), joinedActions.Where(preingestActions
                        => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetExecutionPlan(),
                        OverallStatus = new ContainerOverallStatusHandler(new ScheduledPlanStatusHandler(executionPlan.Where(ep
                        => ep.SessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToList(), joinedActions.Where(preingestActions
                        => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetExecutionPlan(), joinedActions.Where(preingestActions
                            => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetContainerStatus(),
                        Preingest = joinedActions.Where(preingestActions
                            => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToArray()
                    }).ToArray();
            }
            else
            {
                dataResults = tarArchives.OrderByDescending(item
                => item.CreationTime).Select(item
                    => new
                    {
                        Name = item.Name,
                        SessionId = ChecksumHelper.GeneratePreingestGuid(item.Name),
                        CreationTime = item.CreationTime,
                        LastWriteTime = item.LastWriteTime,
                        LastAccessTime = item.LastAccessTime,
                        Size = item.Length,
                        Settings = new SettingsReader(item.DirectoryName, ChecksumHelper.GeneratePreingestGuid(item.Name)).GetSettings(),
                        ScheduledPlan = new ScheduledPlanStatusHandler(executionPlan.Where(ep => ep.SessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToList()).GetExecutionPlan(),
                        OverallStatus = ContainerStatus.New,
                        Preingest = new object[] { }
                    }).ToArray();
            }

            return dataResults;
        }

        /// <summary>
        /// Get a collection with specific GUID.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        public dynamic GetCollection(Guid guid)
        {
            dynamic dataResults = null;

            var directory = new DirectoryInfo(_settings.DataFolderName);

            if (!directory.Exists)
                throw new DirectoryNotFoundException(String.Format("Data folder '{0}' not found!", _settings.DataFolderName));            

            var tarArchives = directory.GetFiles("*.*").Where(s => s.Extension.EndsWith(".tar", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase));
            bool exists = tarArchives.Select(tar => ChecksumHelper.GeneratePreingestGuid(tar.Name)).Contains(guid);
            if (!exists)
                throw new FileNotFoundException(String.Format("No tar container file found with GUID {0}!", guid));

            var tar = tarArchives.FirstOrDefault(tar => ChecksumHelper.GeneratePreingestGuid(tar.Name) == guid);
            if (tar == null)            
                throw new FileNotFoundException(String.Format("Tar with guid {0} returns null!", guid));            

            var workingDir = Path.Combine(directory.FullName, ChecksumHelper.GeneratePreingestGuid(tar.Name).ToString());
            if (!Directory.Exists(workingDir))
                directory.CreateSubdirectory(ChecksumHelper.GeneratePreingestGuid(tar.Name).ToString());              

            List<JoinedQueryResult> currentActions = new List<JoinedQueryResult>();
            List<ExecutionPlan> executionPlan = new List<ExecutionPlan>();

            using (var context = new PreIngestStatusContext())
            {
                //compare the tar list with db context if a guid exists in both collection AND then filter Guid from IN parameter
                var query = context.PreingestActionCollection.Where(item => item.FolderSessionId == guid).Join(context.ActionStateCollection,
                    states => states.ProcessId,
                    actions => actions.ProcessId,
                    (actions, states)
                    => new JoinedQueryResult { Actions = actions, States = states }).ToList();

                if (query != null && query.Count > 0)
                    currentActions.AddRange(query);

                var plans = context.ExecutionPlanCollection.Where(item => item.SessionId == guid).OrderBy(item => item.Sequence).ToArray();

                if (plans != null && plans.Length > 0)
                    executionPlan.AddRange(plans);
            }

            if (currentActions != null || currentActions.Count > 0)
            {
                var joinedActions = currentActions.Select(actions => actions.Actions).Distinct().Select(item => new QueryResultAction
                {
                    ActionStatus = String.IsNullOrEmpty(item.ActionStatus) ? "Executing" : item.ActionStatus,
                    Creation = item.Creation,
                    Description = item.Description,
                    FolderSessionId = item.FolderSessionId,
                    Name = item.Name,
                    ProcessId = item.ProcessId,
                    ResultFiles = String.IsNullOrEmpty(item.ResultFiles) ? new string[] { } : item.ResultFiles.Split(";").ToArray(),
                    Summary = String.IsNullOrEmpty(item.StatisticsSummary) ? null : JsonConvert.DeserializeObject<PreingestStatisticsSummary>(item.StatisticsSummary),
                    States = currentActions.Select(state
                        => state.States).Where(state
                            => state.ProcessId == item.ProcessId).Select(state
                                => new QueryResultState
                                {
                                    StatusId = state.StatusId,
                                    Name = state.Name,
                                    Creation = state.Creation
                                }).ToArray()
                });

                dataResults = tarArchives.OrderByDescending(item
                => item.CreationTime).Select(item
                    => new
                    {
                        Name = item.Name,
                        SessionId = ChecksumHelper.GeneratePreingestGuid(item.Name),
                        CreationTime = item.CreationTime,
                        LastWriteTime = item.LastWriteTime,
                        LastAccessTime = item.LastAccessTime,
                        Size = item.Length,
                        Settings = new SettingsReader(item.DirectoryName, ChecksumHelper.GeneratePreingestGuid(item.Name)).GetSettings(),
                        ScheduledPlan = new ScheduledPlanStatusHandler(executionPlan, joinedActions.Where(preingestActions => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetExecutionPlan(),
                        OverallStatus = new ContainerOverallStatusHandler(new ScheduledPlanStatusHandler(executionPlan, joinedActions.Where(preingestActions => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetExecutionPlan(), joinedActions.Where(preingestActions => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name))).GetContainerStatus(),
                        Preingest = joinedActions.Where(preingestActions => preingestActions.FolderSessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToArray()
                    }).FirstOrDefault(item => item.SessionId == guid);
            }
            else
            {
                dataResults = tarArchives.OrderByDescending(item
                => item.CreationTime).Select(item
                    => new
                    {
                        Name = item.Name,
                        SessionId = ChecksumHelper.GeneratePreingestGuid(item.Name),
                        CreationTime = item.CreationTime,
                        LastWriteTime = item.LastWriteTime,
                        LastAccessTime = item.LastAccessTime,
                        Size = item.Length,
                        Settings = new SettingsReader(item.DirectoryName, ChecksumHelper.GeneratePreingestGuid(item.Name)).GetSettings(),
                        ScheduledPlan = new ScheduledPlanStatusHandler(executionPlan.Where(ep => ep.SessionId == ChecksumHelper.GeneratePreingestGuid(item.Name)).ToList()).GetExecutionPlan(),
                        OverallStatus = ContainerStatus.New,
                        Preingest = new object[] { }
                    }).FirstOrDefault(item => item.SessionId == guid);
            }

            return dataResults;
        }

    }

}

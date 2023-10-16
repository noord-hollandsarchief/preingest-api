using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using System;
using System.Linq;
using System.Collections.Generic;

using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ContainerStatus
    {
        Success = 4,
        Failed = 3,
        Error = 2,
        Running = 1,
        New = 0,
        None = -1
    }

    public class ContainerOverallStatusHandler
    {
        private readonly ContainerStatus _status = ContainerStatus.None;
        public ContainerOverallStatusHandler(ExecutionPlanState[] plans, IEnumerable<QueryResultAction> actions)
        {
            //calculation with no scheduled plan or scheduled plan is fully done.

            if (_status == ContainerStatus.None &&
                    actions.ToList().Count == 0 &&
                        (plans.Count() == 0 || plans.Count(item => item.Status == ExecutionStatus.Pending) == plans.Count()))
            {
                _status = ContainerStatus.New;
            }
            else
            {
                var currentAvailableStatus = actions.Select(item
                    => (Enum.Parse(typeof(PreingestActionResults), item.ActionStatus, true) == null)
                    ? PreingestActionResults.None
                    : (PreingestActionResults)Enum.Parse(typeof(PreingestActionResults), item.ActionStatus, true)
                    ).Distinct().OrderBy(item => item);

                PreingestActionResults result = currentAvailableStatus.FirstOrDefault();
                switch (result)
                {
                    case PreingestActionResults.Executing:
                    case PreingestActionResults.None:
                        _status = ContainerStatus.Running;
                        break;
                    case PreingestActionResults.Failed:
                        _status = ContainerStatus.Failed;
                        break;
                    case PreingestActionResults.Error:
                        _status = ContainerStatus.Error;
                        break;
                    case PreingestActionResults.Success:
                        _status = ContainerStatus.Success;
                        break;
                    default:
                        _status = ContainerStatus.None;
                        break;
                }

                var isPending = plans.FirstOrDefault(item => item.Status == ExecutionStatus.Pending);
                if (_status != ContainerStatus.Failed && (isPending != null && isPending.StartOnError))
                    _status = ContainerStatus.Running;
            }
        }

        public ContainerStatus GetContainerStatus()
        {
            return this._status;
        }
    }
   
}

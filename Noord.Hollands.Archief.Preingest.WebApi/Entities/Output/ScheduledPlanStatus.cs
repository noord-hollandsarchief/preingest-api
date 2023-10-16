using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ExecutionStatus
    {
        Done = 0,
        Pending = 1,
        Executing = 2
    }

    public class ExecutionPlanState 
    {
        public ExecutionStatus Status { get; set; }
        public String ActionName { get; set; }
        public bool ContinueOnError { get; set; }
        public bool ContinueOnFailed { get; set; }
        public bool StartOnError { get; set; }
    }

    public class ScheduledPlanStatusHandler
    {
        private List<ExecutionPlanState> _executionPlan = null;

        public ScheduledPlanStatusHandler(List<ExecutionPlan> plan)
        {
            _executionPlan = new List<ExecutionPlanState>();
            if (plan.Count > 0)
            {
                var calculation = plan.Select(item => new ExecutionPlanState
                {
                    ActionName = item.ActionName,
                    ContinueOnError = item.ContinueOnError,
                    ContinueOnFailed = item.ContinueOnFailed,
                    StartOnError = item.StartOnError,
                    Status = ExecutionStatus.Pending
                }).ToArray();

                _executionPlan.AddRange(calculation);
            }
        }

        public ScheduledPlanStatusHandler(List<ExecutionPlan> plan, IEnumerable<QueryResultAction> actions)
        {    
            //left join
            _executionPlan = new List<ExecutionPlanState>();
            if(plan.Count > 0)
            {
                var calculation = plan.GroupJoin(actions, ep =>
                ep.ActionName,
                    a => a.Name,
                    (ep, a) => new { Left = ep, Right = a }).SelectMany(item => item.Right.DefaultIfEmpty(),
                    (ep, a) => new ExecutionPlanState
                    {
                        ActionName = ep.Left.ActionName,
                        ContinueOnError = ep.Left.ContinueOnError,
                        ContinueOnFailed = ep.Left.ContinueOnFailed,
                        StartOnError = ep.Left.StartOnError,
                        Status = (a == null) ? ExecutionStatus.Pending : (a.States.Count() == 0) ? ExecutionStatus.Pending : (a.States.Count() == 2) ? ExecutionStatus.Done : ExecutionStatus.Executing
                    }).ToList();             
   
                if (calculation != null && calculation.Count > 0)
                    _executionPlan.AddRange(calculation);
            }
        }

        public ExecutionPlanState[] GetExecutionPlan()
        {
            return _executionPlan.ToArray();
        }
    }
}

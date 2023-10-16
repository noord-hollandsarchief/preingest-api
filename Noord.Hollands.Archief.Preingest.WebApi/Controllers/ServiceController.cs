using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Noord.Hollands.Archief.Preingest.WebApi.Model;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Service;

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;

namespace Noord.Hollands.Archief.Preingest.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly ILogger<ServiceController> _logger;
        private readonly AppSettings _settings = null;
        private readonly IHubContext<PreingestEventHub> _eventHub;
        private readonly CollectionHandler _preingestCollection = null;

        public ServiceController(ILogger<ServiceController> logger, IOptions<AppSettings> settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _logger = logger;
            _settings = settings.Value;
            _eventHub = eventHub;
            _preingestCollection = preingestCollection;
        }

        [HttpPost("startplan/{guid}", Name = "StartPreingestPlan", Order = 1)]
        public IActionResult StartPlan(Guid guid, [FromBody] BodyExecutionPlan workflow)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            if(workflow == null || workflow.Workflow.Length == 0)
                return Problem("No plan to run.");

            _logger.LogInformation("Enter StartPlan.");      
            try
            {                        
                var existingPlan = new List<BodyPlan>();
                var currentArchive = _preingestCollection.GetCollection(guid);

                existingPlan = (currentArchive.ScheduledPlan as ExecutionPlanState[]) == null ? existingPlan : (currentArchive.ScheduledPlan as ExecutionPlanState[]).Select(item => new BodyPlan
                {
                    ActionName = ((ValidationActionType)Enum.Parse(typeof(ValidationActionType), item.ActionName)),
                    ContinueOnError = item.ContinueOnError,
                    ContinueOnFailed = item.ContinueOnFailed,
                    StartOnError = item.StartOnError
                }).ToList();

                // the same action then remove old onces first
                // var exceptCollection = workflow.Workflow.Except<BodyPlan>(existingPlan).ToList();
                
                QueryResultAction[] actions = currentArchive.Preingest as QueryResultAction[];
                List<QueryResultAction> alreadyProcessedActions = new List<QueryResultAction>();
                if (existingPlan.Count > 0)
                {
                    var intersectCollection = workflow.Workflow.Intersect<BodyPlan>(existingPlan).ToList();
                    var tempResult = actions.Where(action
                                        => intersectCollection.Exists(item
                                            => item.ActionName == ((ValidationActionType)Enum.Parse(typeof(ValidationActionType), action.Name)))).ToList();

                    alreadyProcessedActions.AddRange(tempResult);
                }
                else
                {
                    var tempResult = actions.Where(action
                                       => workflow.Workflow.ToList().Exists(item
                                           => item.ActionName == ((ValidationActionType)Enum.Parse(typeof(ValidationActionType), action.Name)))).ToList();

                    alreadyProcessedActions.AddRange(tempResult);
                }
                //clean the old plan
                //remove previous states if selected in new plan
                //save the new plan
                using (var context = new PreIngestStatusContext())
                {
                    try
                    {
                        //get old plan, remove old plan, save
                        var oldPlan = context.ExecutionPlanCollection.Where(item => item.SessionId == guid).ToArray();
                        if (oldPlan.Length > 0)
                        {
                            context.ExecutionPlanCollection.RemoveRange(oldPlan);
                            context.SaveChanges();
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException("Failed to remove old plan! " + e.Message);
                    }
                    finally
                    {
                        try
                        {
                            //remove the old action + results + messages if found
                            if (alreadyProcessedActions.Count > 0)
                            {
                                var allStatusIdFromAlreadyProcessedActions = alreadyProcessedActions.SelectMany(item => item.States).Select(item => item.StatusId).ToArray();
                                var allActionIdFromAlreadyProcessedActions = alreadyProcessedActions.Select(item => item.ProcessId).ToArray();
                                //remove any error message 
                                var messages = context.ActionStateMessageCollection.Where(item => allStatusIdFromAlreadyProcessedActions.Contains(item.StatusId)).ToArray();
                                context.ActionStateMessageCollection.RemoveRange(messages);
                                //remove any states
                                var states = context.ActionStateCollection.Where(item => allStatusIdFromAlreadyProcessedActions.Contains(item.StatusId)).ToArray();
                                context.ActionStateCollection.RemoveRange(states);
                                //remove any actions
                                var oldActions = context.PreingestActionCollection.Where(item => allActionIdFromAlreadyProcessedActions.Contains(item.ProcessId)).ToArray();
                                context.PreingestActionCollection.RemoveRange(oldActions);

                                context.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new ApplicationException("Failed to remove old action(s) + result(s) + message(s)! " + e.Message);
                        }
                        finally
                        {
                            try
                            {
                                //save the new plan
                                var newPlan = workflow.Workflow.Select((item, index) => new Entities.Context.ExecutionPlan { ActionName = item.ActionName.ToString(), SessionId = guid, ContinueOnError = item.ContinueOnError, ContinueOnFailed = item.ContinueOnFailed, StartOnError = item.StartOnError, Sequence = index });
                                if (newPlan.Count() > 0)
                                {
                                    context.ExecutionPlanCollection.AddRange(newPlan);
                                    context.SaveChanges();
                                }
                            }
                            catch (Exception e)
                            {
                                throw new ApplicationException("Failed to save the new plan! " + e.Message);
                            }
                        }
                    }
                }

                var settings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() }, Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                
                //reload
                currentArchive = _preingestCollection.GetCollection(guid);

                string collectionData = JsonConvert.SerializeObject(currentArchive, settings);
                _eventHub.Clients.All.SendAsync(nameof(IEventHub.SendNoticeToWorkerService), guid, collectionData).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _logger.LogInformation("Exit StartPlan.");
                return ValidationProblem(e.Message);
            }

            _logger.LogInformation("Exit StartPlan.");
            return Ok();
        }

        [HttpDelete("cancelplan/{guid}", Name = "CancelPreingestPlan", Order = 2)]
        public IActionResult CancelPlan(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter CancelPlan.");
            try
            {
                using (var context = new PreIngestStatusContext())
                {
                    var items = context.ExecutionPlanCollection.Where(item => item.SessionId == guid).ToArray();

                    if (items != null && items.Length > 0)
                        context.ExecutionPlanCollection.RemoveRange(items);

                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        return Problem("Failed to remove the plan! " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation("Exit CancelPlan.");
                return ValidationProblem(e.Message);
            }

            _logger.LogInformation("Exit CancelPlan.");
            return Ok();
        }
    }
}

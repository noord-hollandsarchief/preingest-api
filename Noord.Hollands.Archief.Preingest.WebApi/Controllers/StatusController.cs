using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Model;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Status;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.EventHub;

namespace Noord.Hollands.Archief.Preingest.WebApi.Controllers
{
    /// <summary>
    /// API controller for handling preingest actions (handlers) .
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.ControllerBase" />
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private AppSettings _settings = null;
        private readonly IHubContext<PreingestEventHub> _eventHub;
        private readonly CollectionHandler _preingestCollection = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public StatusController(ILogger<StatusController> logger, IOptions<AppSettings> settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _logger = logger;
            _settings = settings.Value;
            _eventHub = eventHub;
            _preingestCollection = preingestCollection;
        }

        /// <summary>
        /// Gets the action.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <returns></returns>
        [HttpGet("action/{actionGuid}", Name = "RetrieveActionRecordByGuid", Order = 1)]
        public IActionResult GetAction(Guid actionGuid)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter GetActions.");

            PreingestAction action = null;            
            try
            {
                using (var context = new PreIngestStatusContext())
                {
                    action = context.PreingestActionCollection.Find(actionGuid);
                    if (action != null && action.StatisticsSummary != null)
                    {
                        action.Status = context.ActionStateCollection.Where(item => item.ProcessId == action.ProcessId).ToList();
                        action.Status.ToList().ForEach(item => item.Messages = context.ActionStateMessageCollection.Where(m => m.StatusId == item.StatusId).ToList());
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
            }
            finally { }

            _logger.LogInformation("Exit GetActions.");

            if (action == null)
                return NotFound(String.Format("Action not found with ID '{0}'", actionGuid));

            return new JsonResult(new
            {
                action.Creation,
                action.Description,
                SessionId = action.FolderSessionId,
                action.Name,
                ActionId = action.ProcessId,
                ResultFiles = String.IsNullOrEmpty(action.ResultFiles) ? new string[] { } : action.ResultFiles.Split(";").ToArray(),
                action.ActionStatus,
                Summary = String.IsNullOrEmpty(action.StatisticsSummary) ? new object { } : JsonConvert.DeserializeObject<PreingestStatisticsSummary>(action.StatisticsSummary),
                States = action.Status.Count == 0 ? new object[] { } : action.Status.Select(item => new
                {
                    item.Name,
                    item.StatusId,
                    item.Creation,
                    Messages = (item.Messages.Count == 0) ? new object[] { } : item.Messages.Select(m => new { m.MessageId, m.Creation, m.Description }).ToArray()
                }).ToArray()
            });
        }

        /// <summary>
        /// Get a specific action.
        /// </summary>
        /// <param name="folderSessionGuid">The folder session unique identifier.</param>
        /// <returns></returns>
        [HttpGet("actions/{folderSessionGuid}", Name = "RetrieveAllActionsFromCollection", Order = 2)]
        public IActionResult GetActions(Guid folderSessionGuid)
        {
            if (folderSessionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter GetActions.");

            List<PreingestAction> actions = new List<PreingestAction>();
            try
            {
                using (var context = new PreIngestStatusContext())
                {
                    var result = context.PreingestActionCollection.Where(item => item.FolderSessionId == folderSessionGuid).ToList();
                    result.ForEach(action =>
                    {
                        action = context.PreingestActionCollection.Find(action.ProcessId);
                        action.Status = context.ActionStateCollection.Where(item => item.ProcessId == action.ProcessId).ToList();
                        action.Status.ToList().ForEach(item => item.Messages = context.ActionStateMessageCollection.Where(m => m.StatusId == item.StatusId).ToList());
                    });
                    actions.AddRange(result);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown : {0}, '{1}'.",  e.Message, e.StackTrace);
                return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
            }
            finally { }

            _logger.LogInformation("Exit GetActions.");

            return new JsonResult(actions.Select(action => new
            {
                action.Creation,
                action.Description,
                SessionId = action.FolderSessionId,
                action.Name,
                ActionId = action.ProcessId,
                ResultFiles = String.IsNullOrEmpty(action.ResultFiles) ? new string[] { } : action.ResultFiles.Split(";").ToArray(),
                action.ActionStatus,
                Summary = String.IsNullOrEmpty(action.StatisticsSummary) ? new object { } : JsonConvert.DeserializeObject<PreingestStatisticsSummary>(action.StatisticsSummary),
                States = action.Status.Count == 0 ? new object[] { } : action.Status.Select(item => new
                {
                    item.Name,
                    item.StatusId,
                    item.Creation,
                    Messages = (item.Messages.Count == 0) ? new object[] { } : item.Messages.Select(m => new { m.MessageId, m.Creation, m.Description }).ToArray()
                }).ToArray()
            }).ToArray());           
        }

        /// <summary>
        /// Adds the process action.
        /// </summary>
        /// <param name="folderSessionGuid">The folder session unique identifier.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        [HttpPost("new/{folderSessionGuid}", Name = "AddActionForCollection", Order = 3)]
        public IActionResult AddProcessAction(Guid folderSessionGuid, [FromBody] BodyNewAction data)
        {
            if (folderSessionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");
            if(data == null)
                return Problem("Input data is required");
            if (String.IsNullOrEmpty(data.Name))
                return Problem("Name is required");
            if (String.IsNullOrEmpty(data.Description))
                return Problem("Description is required");

            //if(String.IsNullOrEmpty(data.Result))
                //return Problem("Result filename is required.");
            
            _logger.LogInformation("Enter AddProcessAction.");

            var processId = Guid.NewGuid();
            var session = new PreingestAction
            {
                ProcessId = processId,
                FolderSessionId = folderSessionGuid,
                Description = data.Description,
                Name = data.Name,
                Creation = DateTimeOffset.Now,
                ResultFiles = data.Result
            };

            using (var context = new PreIngestStatusContext())
            {                
                try
                {
                    context.Add<PreingestAction>(session);
                    context.SaveChanges();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                    return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
                }
                finally 
                {
                    _logger.LogInformation("Exit AddProcessAction.");
                }
            } 
            
            return new JsonResult(session);
        }

        /// <summary>
        /// Updates the process action.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        [HttpPut("update/{actionGuid}", Name = "UpdateActionByGuid", Order = 4)]
        public IActionResult UpdateProcessAction(Guid actionGuid, [FromBody] BodyUpdate data)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");
            if (data == null)
                return Problem("Input data is required");
            if (String.IsNullOrEmpty(data.Result))
                return Problem("Result of the action (success/error/failed) is required");
            if (String.IsNullOrEmpty(data.Summary))
                return Problem("Summary (accepted/rejected/processed) is required");

            _logger.LogInformation("Enter UpdateProcessAction.");

            PreingestAction currentAction = null;
            using (var context = new PreIngestStatusContext())
            {
                try
                {
                    currentAction = context.Find<PreingestAction>(actionGuid);
                    if (currentAction != null)
                    {
                        currentAction.ActionStatus = data.Result;
                        currentAction.StatisticsSummary = data.Summary;
                    }
                    context.SaveChanges();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                    return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
                }
                finally
                {
                    _logger.LogInformation("Exit UpdateProcessAction.");
                }
            }

            if (currentAction == null)
                return NotFound();

            return new JsonResult(currentAction);
        }

        /// <summary>
        /// Adds the start state.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <returns></returns>
        [HttpPost("start/{actionGuid}", Name = "AddStartStatusByGuid", Order = 5)]
        public IActionResult AddStartState(Guid actionGuid)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter AddStartState.");

            var result = AddState(actionGuid, "Started");

            _logger.LogInformation("Exit AddStartState.");

            return result;
        }

        /// <summary>
        /// Adds the state of the completed.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <returns></returns>
        [HttpPost("completed/{actionGuid}", Name = "AddCompletedStatusByGuid", Order = 6)]
        public IActionResult AddCompletedState(Guid actionGuid)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter AddCompletedState.");

            var result = AddState(actionGuid, "Completed");

            _logger.LogInformation("Exit AddCompletedState.");

            return result;
        }

        /// <summary>
        /// Adds the state of the failed.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <param name="failMessage">The fail message.</param>
        /// <returns></returns>
        [HttpPost("failed/{actionGuid}", Name = "AddFailedStatusByGuid", Order = 7)]
        public IActionResult AddFailedState(Guid actionGuid, [FromBody] BodyMessage failMessage)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter AddFailedState.");

            String message = String.Empty;
            if (failMessage != null)
                message = failMessage.Message;

            var result = AddState(actionGuid, "Failed", message);
                        
            _logger.LogInformation("Exit AddFailedState.");

            return result;
        }

        /// <summary>
        /// Resets the session.
        /// </summary>
        /// <param name="folderSessionGuid">The folder session unique identifier.</param>
        /// <returns></returns>
        [HttpDelete("reset/{folderSessionGuid}", Name = "ClearHistoryDataCollection", Order = 8)]
        public IActionResult ResetSession(Guid folderSessionGuid)
        {
            return DeleteSession(folderSessionGuid);
        }

        /// <summary>
        /// Removes the session.
        /// </summary>
        /// <param name="folderSessionGuid">The folder session unique identifier.</param>
        /// <returns></returns>
        [HttpDelete("remove/{folderSessionGuid}", Name = "RemoveHistoryDataForCollection", Order = 9)]
        public IActionResult RemoveSession(Guid folderSessionGuid)
        {
            return DeleteSession(folderSessionGuid, true);
        }

        /// <summary>
        /// Sends the notification.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        [HttpPost("notify", Name = "NotifyClientOfAnEvent", Order = 10)]
        public IActionResult SendNotification([FromBody] BodyEventMessageBody message)
        {
            if (message == null)
                return Problem("POST body JSON object is null!");

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()

                },
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            object state = null;
            bool parse = Enum.TryParse(typeof(PreingestActionStates), message.State, out state);
            if (!parse)
                return Problem("Parsing state failed!");

            //if GUID folder doesn't exists, just return immediate and ignore state;
            try
            {
                _preingestCollection.GetCollection(message.SessionId);
            }
            catch { return Ok(); }                

            //trigger full events
            _eventHub.Clients.All.SendAsync(nameof(IEventHub.SendNoticeEventToClient),
                JsonConvert.SerializeObject(new EventHubMessage
                {
                    EventDateTime = message.EventDateTime,
                    SessionId = message.SessionId,
                    Name = message.Name,
                    State = (PreingestActionStates)state,
                    Message = message.Message,
                    Summary = message.HasSummary ? new PreingestStatisticsSummary { Accepted = message.Accepted, Processed = message.Processed, Rejected = message.Rejected, Start = message.Start.Value, End = message.End.Value } : null
                }, settings)).GetAwaiter().GetResult();            

            if ((PreingestActionStates)state == PreingestActionStates.Started || (PreingestActionStates)state == PreingestActionStates.Failed || (PreingestActionStates)state == PreingestActionStates.Completed)
            {
                //notify client update collections status
                string collectionsData = JsonConvert.SerializeObject(_preingestCollection.GetCollections(), settings);
                _eventHub.Clients.All.SendAsync(nameof(IEventHub.CollectionsStatus), collectionsData).GetAwaiter().GetResult();
                //notify client collection /{ guid} status
                string collectionData = JsonConvert.SerializeObject(_preingestCollection.GetCollection(message.SessionId), settings);
                _eventHub.Clients.All.SendAsync(nameof(IEventHub.CollectionStatus), message.SessionId, collectionData).GetAwaiter().GetResult();

                if ((PreingestActionStates)state == PreingestActionStates.Failed || (PreingestActionStates)state == PreingestActionStates.Completed)
                    _eventHub.Clients.All.SendAsync(nameof(IEventHub.SendNoticeToWorkerService), message.SessionId, collectionData).GetAwaiter().GetResult();
            }
            return Ok();
        }

        /// <summary>
        /// Adds the state.
        /// </summary>
        /// <param name="actionGuid">The action unique identifier.</param>
        /// <param name="statusValue">The status value.</param>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        private IActionResult AddState(Guid actionGuid, String statusValue, String message = null)
        {
            if (actionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            if(String.IsNullOrEmpty(statusValue))
                return Problem("Status value is required.");

            _logger.LogInformation("Enter AddState.");

            JsonResult result = null;
            using (var context = new PreIngestStatusContext())
            {
                var currentSession = context.Find<PreingestAction>(actionGuid);
                if (currentSession != null)
                {
                    var item = new ActionStates { StatusId = Guid.NewGuid(), Creation = DateTimeOffset.Now, Name = statusValue, ProcessId = currentSession.ProcessId, Session = currentSession };
                    context.Add<ActionStates>(item);
                    StateMessage stateMessage = null;
                    try
                    {
                        context.SaveChanges();

                        if (!String.IsNullOrEmpty(message))
                        {
                            stateMessage = new StateMessage
                            {
                                Creation = DateTimeOffset.Now,
                                Description = message,
                                MessageId = Guid.NewGuid(),
                                Status = item,
                                StatusId = item.StatusId
                            };
                            context.Add<StateMessage>(stateMessage);
                            try
                            {
                                context.SaveChanges();
                            }
                            catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                            finally { }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                        return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
                    }
                    finally { }

                    if (stateMessage != null)
                        result = new JsonResult(new { item.StatusId, item.ProcessId, item.Creation, item.Name, Message = new { stateMessage.Creation, stateMessage.Description, stateMessage.MessageId, stateMessage.StatusId } });
                    else
                        result = new JsonResult(new { item.StatusId, item.ProcessId, item.Creation, item.Name });              
                }
            }

            _logger.LogInformation("Exit AddState.");

            if (result == null)
                return NoContent();

            return result;
        }
        /// <summary>
        /// Deletes the session.
        /// </summary>
        /// <param name="folderSessionGuid">The folder session unique identifier.</param>
        /// <param name="fullDelete">if set to <c>true</c> [full delete].</param>
        /// <returns></returns>
        private IActionResult DeleteSession(Guid folderSessionGuid, bool fullDelete = false)
        {
            if (folderSessionGuid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter DeleteSession.");

            String containerLocation = String.Empty;
            if (fullDelete)
            {
                var tarArchive = Directory.GetFiles(_settings.DataFolderName, "*.*").Select(i => new FileInfo(i)).Where(s
                    => s.Extension.EndsWith(".tar", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)).Select(item
                    => new
                    {
                        CollectionName = item.Name,
                        SessionFolderId = ChecksumHelper.GeneratePreingestGuid(item.Name),
                        CollectionFullName = item.FullName
                    }).Where(item => item.SessionFolderId == folderSessionGuid).FirstOrDefault();

                if (tarArchive == null)
                    return Problem(String.Format("Container not found with GUID {0}", folderSessionGuid));

                containerLocation = tarArchive.CollectionFullName;
            }

            try
            {
                using (var context = new PreIngestStatusContext())
                {
                    var sessions = context.PreingestActionCollection.Where(item => item.FolderSessionId == folderSessionGuid).ToList();
                    var statusesIds = sessions.Select(item => item.ProcessId).ToArray();

                    var statusus = context.ActionStateCollection.Where(item => statusesIds.Contains(item.ProcessId)).ToList();
                    var messagesIds = statusus.Select(item => item.StatusId).ToArray();

                    var messages = context.ActionStateMessageCollection.Where(item => messagesIds.Contains(item.StatusId)).ToList();
                    
                    var scheduledPlan = context.ExecutionPlanCollection.Where(item => item.SessionId == folderSessionGuid).ToArray();

                    //remove any exeception messages
                    context.ActionStateMessageCollection.RemoveRange(messages);
                    //remove (actions) statusus
                    context.ActionStateCollection.RemoveRange(statusus);
                    //remove (folder session) actions
                    context.PreingestActionCollection.RemoveRange(sessions);
                    //remove plan                    
                    context.ExecutionPlanCollection.RemoveRange(scheduledPlan);

                    context.SavedChanges += (object sender, Microsoft.EntityFrameworkCore.SavedChangesEventArgs e) =>
                    {
                        try
                        {
                            DirectoryInfo di = new DirectoryInfo(Path.Combine(_settings.DataFolderName, folderSessionGuid.ToString()));
                            if (di.Exists)
                                di.Delete(true);
                        }
                        catch { }
                        finally { }

                        if (fullDelete)
                        {
                            try
                            {
                                if (System.IO.File.Exists(containerLocation))
                                    System.IO.File.Delete(containerLocation);
                            }
                            catch { }
                            finally { }
                        }
                    };
                    context.SaveChanges();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
            }
            finally { }            

            _logger.LogInformation("Exit DeleteSession.");

            return Ok();
        }
    }
}

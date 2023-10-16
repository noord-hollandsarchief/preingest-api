using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using Newtonsoft.Json; 
using Newtonsoft.Json.Serialization;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Model;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.EventHub;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Abstract class for all handlers.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.IPreingest" />
    public abstract class AbstractPreingestHandler : IPreingest
    {
        private readonly AppSettings _settings = null;
        protected Guid _guidSessionFolder = Guid.Empty;
        private ILogger _logger = null;
        private bool _isToPX = false, _isMDTO = false;

        private readonly object triggerLock = new object();

        private readonly IHubContext<PreingestEventHub> _eventHub;
        private readonly CollectionHandler _preingestCollection = null;

        public event EventHandler<PreingestEventArgs> PreingestEvents;
        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractPreingestHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public AbstractPreingestHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _settings = settings;
            _eventHub = eventHub;
            _preingestCollection = preingestCollection;
        }

        /// <summary>
        /// Gets the application settings.
        /// </summary>
        /// <value>
        /// The application settings.
        /// </value>
        public AppSettings ApplicationSettings
        {
            get { return this._settings; }
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.IO.FileNotFoundException">Collection not found!</exception>
        /// <exception cref="System.ApplicationException">
        /// Metadata files not found!
        /// </exception>
        public virtual void Execute()
        {
            if (!File.Exists(TargetCollection))
                throw new FileNotFoundException("Collection not found!", TargetCollection);

            DirectoryInfo directoryInfoSessionFolder = new DirectoryInfo(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString()));
            int countToPXFiles = directoryInfoSessionFolder.GetFiles("*.metadata", SearchOption.AllDirectories).Count();
            int countMDTOFiles = directoryInfoSessionFolder.GetFiles("*.xml", SearchOption.AllDirectories).Where(item => item.Name.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).Count();

            if (countToPXFiles > 0 && countMDTOFiles > 0)
                throw new ApplicationException(String.Format("Found ToPX ({0}) and/or MDTO ({1}) files in collection with GUID {2}. Cannot handle both types in one collection.", countToPXFiles, countMDTOFiles, SessionGuid));

            if (countMDTOFiles == 0 && countToPXFiles == 0)
                throw new ApplicationException("Metadata files not found!");

            this._isToPX = (countToPXFiles > 0);
            this._isMDTO = (countMDTOFiles > 0);
        }

        /// <summary>
        /// Gets the session unique identifier.
        /// </summary>
        /// <value>
        /// The session unique identifier.
        /// </value>
        public Guid SessionGuid
        {
            get
            {
                return this._guidSessionFolder;
            }
        }
        /// <summary>
        /// Gets or sets the action process identifier.
        /// </summary>
        /// <value>
        /// The action process identifier.
        /// </value>
        public Guid ActionProcessId { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        public ILogger Logger { get => _logger; set => _logger = value; }
        /// <summary>
        /// Sets the session unique identifier.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        public void SetSessionGuid(Guid guid)
        {
            this._guidSessionFolder = guid;
            ValidateAction();
        }

        /// <summary>
        /// Gets or sets the tar filename.
        /// </summary>
        /// <value>
        /// The tar filename.
        /// </value>
        public String TarFilename { get; set; }
        /// <summary>
        /// Raises the <see cref="E:Trigger" /> event.
        /// </summary>
        /// <param name="e">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        protected virtual void OnTrigger(PreingestEventArgs e)
        {
            EventHandler<PreingestEventArgs> handler = PreingestEvents;
            if (handler != null)
            {
                if (e.ActionType == PreingestActionStates.Started)
                    e.PreingestAction.Summary.Start = e.Initiate;

                if (e.ActionType == PreingestActionStates.Completed || e.ActionType == PreingestActionStates.Failed)
                    e.PreingestAction.Summary.End = e.Initiate;

                handler(this, e);                   
            }
        }
        /// <summary>
        /// Triggers the specified sender.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        public void Trigger(object sender, PreingestEventArgs e)
        {
            lock (triggerLock)
            {
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    },
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                //send notifications events to client
                _eventHub.Clients.All.SendAsync(nameof(IEventHub.SendNoticeEventToClient),
                    JsonConvert.SerializeObject(new EventHubMessage
                    {
                        EventDateTime = e.Initiate,
                        SessionId = e.PreingestAction.Properties.SessionId,
                        Name = e.PreingestAction.Properties.ActionName,
                        State = e.ActionType,
                        Message = e.Description,
                        Summary = e.PreingestAction.Summary
                    }, settings)).GetAwaiter().GetResult();

                if (this.ActionProcessId == Guid.Empty) 
                    return;
                if (e.ActionType == PreingestActionStates.Started)
                    this.AddStartState(this.ActionProcessId);
                if (e.ActionType == PreingestActionStates.Completed)
                    this.AddCompleteState(this.ActionProcessId);
                if (e.ActionType == PreingestActionStates.Failed)
                {
                    string message = e.PreingestAction.Properties == null ? String.Empty : e.PreingestAction.Properties.Messages == null ? String.Empty : String.Concat(e.PreingestAction.Properties.Messages);
                    this.AddFailedState(this.ActionProcessId, message);
                }
                if (e.ActionType == PreingestActionStates.Failed || e.ActionType == PreingestActionStates.Completed)
                {
                    string result = (e.PreingestAction.ActionResult != null) ? e.PreingestAction.ActionResult.ResultValue.ToString() : PreingestActionResults.None.ToString();
                    string summary = (e.PreingestAction.Summary != null) ? JsonConvert.SerializeObject(e.PreingestAction.Summary, settings) : String.Empty;
                    this.UpdateProcessAction(this.ActionProcessId, result, summary);                   
                }

                if (e.ActionType == PreingestActionStates.Completed || e.ActionType == PreingestActionStates.Failed)
                {
                    SaveJson(new DirectoryInfo(TargetFolder), e.PreingestAction.Properties.ActionName, e.PreingestAction);
                }

                if (e.ActionType == PreingestActionStates.Started || e.ActionType == PreingestActionStates.Completed || e.ActionType == PreingestActionStates.Failed)
                {
                    //notify client update collections status
                    string collectionsData = JsonConvert.SerializeObject(_preingestCollection.GetCollections(), settings);
                    _eventHub.Clients.All.SendAsync(nameof(IEventHub.CollectionsStatus), collectionsData).GetAwaiter().GetResult();
                    //notify client collection /{ guid} status
                    string collectionData = JsonConvert.SerializeObject(_preingestCollection.GetCollection(e.PreingestAction.Properties.SessionId), settings);
                    _eventHub.Clients.All.SendAsync(nameof(IEventHub.CollectionStatus), e.PreingestAction.Properties.SessionId, collectionData).GetAwaiter().GetResult();

                    if (e.ActionType == PreingestActionStates.Completed || e.ActionType == PreingestActionStates.Failed)                    
                        _eventHub.Clients.All.SendAsync(nameof(IEventHub.SendNoticeToWorkerService), e.PreingestAction.Properties.SessionId, collectionData).GetAwaiter().GetResult();                    
                }
            }
        }
        /// <summary>
        /// Gets the target collection.
        /// </summary>
        /// <value>
        /// The target collection.
        /// </value>
        public String TargetCollection { get => Path.Combine(ApplicationSettings.DataFolderName, TarFilename); }
        /// <summary>
        /// Gets the target folder.
        /// </summary>
        /// <value>
        /// The target folder.
        /// </value>
        public String TargetFolder { get => Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString()); }
        /// <summary>
        /// Gets a value indicating whether this instance is to px.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is to px; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsToPX { get => this._isToPX; }
        /// <summary>
        /// Gets a value indicating whether this instance is mdto.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is mdto; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsMDTO { get => this._isMDTO; }
        /// <summary>
        /// Currents the action properties.
        /// </summary>
        /// <param name="collectionName">Name of the collection.</param>
        /// <param name="actionName">Name of the action.</param>
        /// <param name="actionResult">The action result.</param>
        /// <returns></returns>
        protected PreingestActionModel CurrentActionProperties(String collectionName, String actionName, PreingestActionResults actionResult = PreingestActionResults.None)
        {
            var eventModel = new PreingestActionModel();
            eventModel.Properties = new PreingestProperties
            {
                SessionId = SessionGuid,
                CollectionItem = collectionName,
                ActionName = actionName,
                CreationTimestamp = DateTimeOffset.Now
            };
            eventModel.ActionResult = new PreingestResult() { ResultValue = actionResult };
            eventModel.Summary = new PreingestStatisticsSummary();

            return eventModel;
        }
        /// <summary>
        /// Saves the handler execution result in JSON format.
        /// </summary>
        /// <param name="outputFolder">The output folder.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="data">The data.</param>
        /// <param name="useTimestamp">if set to <c>true</c> [use timestamp].</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Re-throwing exception! See inner exception for more information</exception>
        protected String SaveJson(DirectoryInfo outputFolder, String typeName, object data, bool useTimestamp = false)
        {
            string fileName = new FileInfo(Path.GetTempFileName()).Name;
            if (!String.IsNullOrEmpty(typeName))
                fileName = typeName.Trim();

            string outputFile = useTimestamp ? Path.Combine(outputFolder.FullName, String.Concat(fileName, "_", DateTime.Now.ToFileTime().ToString(), ".json")) : Path.Combine(outputFolder.FullName, String.Concat(fileName, ".json"));

            // Without this try-catch any exception is silently swallowed somewhere. Feedback is important when using
            // network shares for the data folder, which may fail in the (automatic) Dispose at the end of "using",
            // which calls FlushWriteBuffer yielding "Access to the path '...' is denied", and leaving an empty JSON
            // file on disk.
            try {
                using (StreamWriter file = File.CreateText(outputFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.Serialize(file, data);
                }
            }
            catch (Exception e) {
                _logger.LogError(e, "An exception was thrown while saving JSON file, {0}: '{1}'.", e.Message, e.StackTrace);
                // See comment above: whatever is catching this should have logged the error and return an error to the
                // caller of the API, but it does not
                throw new Exception("Re-throwing exception! See inner exception for more information", e);
            }

            return outputFile;
        }
        /// <summary>
        /// Adds the process action.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        public Guid AddProcessAction(Guid processId, String name, String description, String result)
        {
            if (processId == Guid.Empty)
                processId = Guid.NewGuid();

            if (String.IsNullOrEmpty(description) || String.IsNullOrEmpty(result))
                return Guid.Empty;
            
            using (var context = new PreIngestStatusContext())
            {
                var session = new PreingestAction
                {
                    ProcessId = processId,
                    FolderSessionId = SessionGuid,
                    Description = description,
                    Name = name,
                    Creation = DateTimeOffset.Now,
                    ResultFiles = result
                };

                context.Add<PreingestAction>(session);
                try
                {
                    context.SaveChanges();
                }
                catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                finally { }
            }

            ActionProcessId = processId;
            return processId;
        }
        /// <summary>
        /// Updates the process action.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <param name="result">The result.</param>
        /// <param name="summary">The summary.</param>
        public void UpdateProcessAction(Guid actionId, String result, String summary)
        {
            using (var context = new PreIngestStatusContext())
            {
                var currentAction = context.Find<PreingestAction>(actionId);
                if (currentAction != null)
                {
                    if (!String.IsNullOrEmpty(result))
                        currentAction.ActionStatus = result;

                    if (!String.IsNullOrEmpty(summary))
                        currentAction.StatisticsSummary = summary;

                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                    finally { }
                }
            }
        }
        /// <summary>
        /// Adds the start state.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        public void AddStartState(Guid processId)
        {
            using (var context = new PreIngestStatusContext())
            {
                var currentSession = context.Find<PreingestAction>(processId);
                if (currentSession != null)
                {
                    var item = new ActionStates { StatusId = Guid.NewGuid(), Creation = DateTimeOffset.Now, Name = "Started", ProcessId = currentSession.ProcessId, Session = currentSession };
                    context.Add<ActionStates>(item);
                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                    finally { }
                }
            }
        }
        /// <summary>
        /// Adds the state of the complete.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        public void AddCompleteState(Guid processId)
        {
            using (var context = new PreIngestStatusContext())
            {
                var currentSession = context.Find<PreingestAction>(processId);
                if (currentSession != null)
                {
                    var item = new ActionStates { StatusId = Guid.NewGuid(), Creation = DateTimeOffset.Now, Name = "Completed", ProcessId = currentSession.ProcessId, Session = currentSession };
                    context.Add<ActionStates>(item);

                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                    finally { }
                }
            }
        }
        /// <summary>
        /// Adds the state of the failed.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        /// <param name="message">The message.</param>
        public void AddFailedState(Guid processId, string message)
        {
            using (var context = new PreIngestStatusContext())
            {
                var currentSession = context.Find<PreingestAction>(processId);
                if (currentSession != null)
                {
                    var item = new ActionStates { StatusId = Guid.NewGuid(), Creation = DateTimeOffset.Now, Name = "Failed", ProcessId = currentSession.ProcessId, Session = currentSession };
                    context.Add<ActionStates>(item);

                    try
                    {
                        context.SaveChanges();

                        if (!String.IsNullOrEmpty(message))
                        {
                            var stateMessage = new StateMessage
                            {
                                Creation = DateTimeOffset.Now,
                                Description = message,
                                MessageId = Guid.NewGuid(),
                                Status = item,
                                StatusId = item.StatusId

                            };
                            context.Add<StateMessage>(stateMessage);
                        }

                        context.SaveChanges();
                    }
                    catch (Exception e) { _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", e.Message, e.StackTrace); }
                    finally { }
                }
            }
        }
        /// <summary>
        /// Validates the action.
        /// </summary>
        /// <exception cref="System.ApplicationException">
        /// SessionId is empty!
        /// or
        /// </exception>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public virtual void ValidateAction()
        {
            if (SessionGuid == Guid.Empty)
                throw new ApplicationException("SessionId is empty!");

            var directory = new DirectoryInfo(ApplicationSettings.DataFolderName);
            if (!directory.Exists)
                throw new DirectoryNotFoundException(String.Format("Data folder '{0}' not found!", ApplicationSettings.DataFolderName));

            var tarArchives = directory.GetFiles("*.*").Where(s => s.Extension.EndsWith(".tar", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase) || s.Extension.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)).Select(item
                        => new { Tar = item.Name, SessionId = ChecksumHelper.GeneratePreingestGuid(item.Name) }).ToList();

            TarFilename = tarArchives.First(item => item.SessionId == SessionGuid).Tar;

            if (String.IsNullOrEmpty(TarFilename))
                throw new ApplicationException(String.Format("Tar file not found for GUID '{0}'!", SessionGuid));

            bool exists = System.IO.Directory.Exists(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString()));
            if (!exists)
                throw new DirectoryNotFoundException(String.Format("Session {0} not found.", SessionGuid));
        }
    }
}

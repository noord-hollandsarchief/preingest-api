using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Service;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler for creating Excel report
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class ExcelCreatorHandler : AbstractPreingestHandler, IDisposable
    {
        private CollectionHandler _preingestCollection = null;
        /// <summary>
        /// Initializes a new instance of the <see cref="ExcelCreatorHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public ExcelCreatorHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
            this._preingestCollection = preingestCollection;
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.ApplicationException">No action result(s) found! Nothing to create an Excel output.</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            eventModel.Summary.Processed = 1;

            OnTrigger(new PreingestEventArgs { Description = "Start generate Excel report", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            bool isSuccess = false;
            var anyMessages = new List<String>();
            try
            {
                var filePath = Path.Combine(TargetFolder, String.Format("{0}.xlsx", this.GetType().Name));
                if (File.Exists(filePath))
                    File.Delete(filePath);

                dynamic collection = _preingestCollection.GetCollection(SessionGuid);

                QueryResultAction[] actionArray = ((QueryResultAction[])collection.Preingest);
                if (!(actionArray != null && actionArray.Length > 0))
                    throw new ApplicationException("No action result(s) found! Nothing to create an Excel output.");

                ExportExcelFullReportHandler.BuildExcel(new DirectoryInfo(TargetFolder), actionArray);

                eventModel.Summary.Processed = 1;
                eventModel.ActionData = new string[] { filePath };

                if (eventModel.Summary.Rejected > 0)
                {
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                    eventModel.Summary.Accepted = 0;
                }
                else
                {
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;
                    eventModel.Summary.Accepted = 1;
                    eventModel.Summary.Rejected = 0;
                }

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;

                Logger.LogError(e, "An exception occured in retrieving Excel file!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured in retrieving Excel file!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = 0;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in retrieving Excel file!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Generate Excel file is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }


    }
}

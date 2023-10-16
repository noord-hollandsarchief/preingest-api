using nClam;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    //Check 1.0
    /// <summary>
    /// Handler for scanning virus or malicious files in a collection. It uses microservice ClamAV to do the actual scanning.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class ScanVirusValidationHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScanVirusValidationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public ScanVirusValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description=String.Format("Start scanning for virus in '{0}'.", TargetFolder), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel }); 

            var anyMessages = new List<String>();
            var scanResults = new List<VirusScanItem>();
            bool isSucces = false;

            try
            {                           
                string[] files = Directory.GetFiles(TargetFolder, "*.*", SearchOption.AllDirectories);
                eventModel.Summary.Processed = files.Count();

                int port = 3310;
                Int32.TryParse(ApplicationSettings.ClamServerPort, out port);
                string server = ApplicationSettings.ClamServerNameOrIp;

                var clam = new ClamClient(server, port);

                foreach (var fullFilename in files)
                {
                    this.Logger.LogInformation("Scanning file '{0}'", fullFilename);

                    var scanResult = clam.ScanFileOnServerAsync(fullFilename).Result;

                    string message = string.Empty;
                    switch (scanResult.Result)
                    {
                        case ClamScanResults.Clean:
                            message = String.Format("Bestand '{0}' is schoon.", fullFilename);
                            break;
                        case ClamScanResults.VirusDetected:
                            message = String.Format("Een virus ({0}) is gevonden in bestand {1}.", scanResult.InfectedFiles.First().VirusName, fullFilename);
                            break;
                        case ClamScanResults.Error:
                            message = String.Format("Er is een fout opgetreden in '{0}'. Fout : {1}", fullFilename, scanResult.RawResult);
                            break;
                        case ClamScanResults.Unknown:
                        default:
                            message = String.Format("Er is een onbekende fout opgetreden in '{0}'. {1}", fullFilename, scanResult.RawResult);
                            break;
                    }

                    scanResults.Add(new VirusScanItem { IsClean = (scanResult.Result == ClamScanResults.Clean), Description = message, Filename = fullFilename });
                    OnTrigger(new PreingestEventArgs {Description = String.Format("Scan file '{0}'.", fullFilename), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                    this.Logger.LogInformation("Scan virus result: '{0}'", message);
                }

                eventModel.Summary.Accepted = scanResults.Where(item => item.IsClean).Count();
                eventModel.Summary.Rejected = scanResults.Where(item => !item.IsClean).Count();

                eventModel.ActionData = scanResults.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSucces = true;
            }
            catch(Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "An exception occured in scan virus!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured in scan virus!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = -1;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in scan virus!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Scanning in folder for virus is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }
    }
}

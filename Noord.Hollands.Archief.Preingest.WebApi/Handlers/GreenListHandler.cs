using CsvHelper;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using System.Net.Http;
using System.Threading;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler to compare DROID classifcation CSV output with NHA preference list.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class GreenListHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GreenListHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public GreenListHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }

        /// <summary>
        /// Get the DROIDS CSV output file location.
        /// </summary>
        /// <returns></returns>
        public String DroidCsvOutputLocation()
        {
            var directory = new DirectoryInfo(TargetFolder);
            var files = directory.GetFiles("*.csv");

            if (files.Count() > 0)
            {
                FileInfo droidCsvFile = files.OrderByDescending(item => item.CreationTime).First();
                if (droidCsvFile == null)
                    return null;
                else
                    return droidCsvFile.FullName;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.IO.FileNotFoundException">
        /// CSV file not found! Run DROID first.
        /// or
        /// Greenlist JSON file not found!
        /// </exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start compare extensions with greenlist.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                base.Execute();

                string droidCsvFile = DroidCsvOutputLocation();
                if (String.IsNullOrEmpty(droidCsvFile))
                    throw new FileNotFoundException("CSV file not found! Run DROID first.", droidCsvFile);

                List<GreenListItem> extensionData = new List<GreenListItem>();
                string url = String.Format("http://{0}:{1}/voorkeursformatenlijst", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    var result = JsonConvert.DeserializeObject<GreenListItem[]>(response.Content.ReadAsStringAsync().Result).ToList();
                    extensionData.AddRange(result);
                }               

                OnTrigger(new PreingestEventArgs { Description = "Read CSV file.",  Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                var actionDataList = new List<DataItem>();

                using (var reader = new StreamReader(droidCsvFile))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>().ToList();

                        var filesByDroid = this.IsToPX ? records.Where(item
                            => item.TYPE == "File" && item.EXT != "metadata").Select(item => new DataItem
                            {
                                Location = item.FILE_PATH,
                                Name = item.NAME,
                                Extension = item.EXT,
                                FormatName = item.FORMAT_NAME,
                                FormatVersion = item.FORMAT_VERSION,
                                Puid = item.PUID,
                                IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                            }).ToList() : records.Where(item
                            => item.TYPE == "File" && !item.NAME.EndsWith(".mdto.xml")).Select(item => new DataItem
                            {
                                Location = item.FILE_PATH,
                                Name = item.NAME,
                                Extension = item.EXT,
                                FormatName = item.FORMAT_NAME,
                                FormatVersion = item.FORMAT_VERSION,
                                Puid = item.PUID,
                                IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                            }).ToList();
                        
                        filesByDroid.ForEach(file =>
                        {
                            OnTrigger(new PreingestEventArgs { Description = string.Format("Processing {0}", file.Location), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                            //2 - no puid found
                            if (string.IsNullOrEmpty(file.Puid))
                            {
                                actionDataList.Add(new DataItem
                                {
                                    Puid = file.Puid,
                                    Name = file.Name,
                                    Location = file.Location,
                                    FormatVersion = file.FormatVersion,
                                    FormatName = file.FormatName,
                                    Extension = file.Extension,
                                    IsSuccess = false,
                                    Message = "Geen Pronom ID gevonden.",
                                    IsExtensionMismatch = file.IsExtensionMismatch
                                });
                                eventModel.Summary.Rejected = eventModel.Summary.Rejected + 1;
                                return;
                            }

                            //3 - extension mismatch                                                       
                            if (file.IsExtensionMismatch)
                            {
                                actionDataList.Add(new DataItem
                                {
                                    Puid = file.Puid,
                                    Name = file.Name,
                                    Location = file.Location,
                                    FormatVersion = file.FormatVersion,
                                    FormatName = file.FormatName,
                                    Extension = file.Extension,
                                    IsSuccess = false,
                                    Message = "Verkeerde extensie combinatie gevonden.",
                                    IsExtensionMismatch = file.IsExtensionMismatch
                                });
                                eventModel.Summary.Rejected = eventModel.Summary.Rejected + 1;
                                return;
                            }

                            //4 - pronom in nha list
                            bool existsPuidInNhaList = extensionData.Exists(item => item.Puid.Equals(file.Puid, StringComparison.InvariantCultureIgnoreCase));
                            if (existsPuidInNhaList)
                            {
                                file.IsSuccess = true;
                                file.Message = "Pronom ID gevonden in NHA voorkeurslijst.";
                                actionDataList.Add(file);
                                eventModel.Summary.Accepted = eventModel.Summary.Accepted + 1;
                                return;
                            }

                            //5 - extension in nha list 
                            bool existsExtInNhaList = extensionData.Exists(item => item.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase) && String.IsNullOrEmpty(item.Puid));
                            if (existsExtInNhaList)
                            {
                                file.IsSuccess = true;
                                file.Message = "Extensie gevonden in NHA voorkeurslijst";
                                actionDataList.Add(file);
                                eventModel.Summary.Accepted = eventModel.Summary.Accepted + 1;
                                return;
                            }
                            //6 - fault
                            actionDataList.Add(new DataItem
                            {
                                Puid = file.Puid,
                                Name = file.Name,
                                Location = file.Location,
                                FormatVersion = file.FormatVersion,
                                FormatName = file.FormatName,
                                Extension = file.Extension,
                                IsSuccess = false,
                                Message = "Voldoet niet aan NHA voorkeurslijst.",
                                IsExtensionMismatch = file.IsExtensionMismatch
                            });
                            eventModel.Summary.Rejected = eventModel.Summary.Rejected + 1;
                        });

                        eventModel.ActionData = actionDataList.ToArray();

                        OnTrigger(new PreingestEventArgs { Description = "Done comparing both lists.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                    }
                }

                eventModel.Summary.Processed = actionDataList.Count;
                eventModel.Summary.Accepted = actionDataList.Count(item => item.IsSuccess);
                eventModel.Summary.Rejected = actionDataList.Count(item => !item.IsSuccess);

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                Logger.LogError(e, "Comparing greenlist with CSV failed!");

                anyMessages.Add(String.Format("Comparing greenlist with CSV failed!"));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                //eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;               

                OnTrigger(new PreingestEventArgs { Description="An exception occured while comparing greenlist with CSV!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description="Comparing greenlist using CSV from DROID is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });                
            }
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }

        /// <summary>
        /// Entity for a CSV record 
        /// </summary>
        internal class DataItem
        {
            public string Location { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public string FormatName { get; set; }
            public string FormatVersion { get; set; }
            public string Puid { get; set; }
            public bool IsExtensionMismatch { get; set; }
            public string Message { get; set; }
            public bool IsSuccess { get; set; }
        }
    }
}
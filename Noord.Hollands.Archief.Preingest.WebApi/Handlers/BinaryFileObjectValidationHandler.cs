using CsvHelper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler for checking extension and type mismatch of a file and zero byte length
    /// Validaties voor elementen <startDatumLooptijd>, <termijnLooptijd> en <termijnEinddatum> (voor: MDTO)
    /// Identificeren van een mogelijke mismatch tussen formaatextensie en PUID, incl. rapportage
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class BinaryFileObjectValidationHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryFileObjectValidationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public BinaryFileObjectValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : 
            base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }

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

        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start validation.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
            bool isSucces = false;
            try
            {
                base.Execute();
                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                if (collection == null)
                    throw new DirectoryNotFoundException(String.Format("Folder '{0}' not found!", TargetFolder));

                string droidCsvFile = DroidCsvOutputLocation();
                if (String.IsNullOrEmpty(droidCsvFile))
                    throw new FileNotFoundException("CSV file not found! Run DROID first.", droidCsvFile);

                PreingestEventArgs execEventArgs = new PreingestEventArgs { Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel };

                var start = DateTime.Now;
                Logger.LogInformation("Start validation in '{0}'.", TargetFolder);
                Logger.LogInformation("Start time {0}", start);

                List<ActionDataItem> actionDataList = ValidateFileLengthAndIdentifyMismatchPronom(droidCsvFile);
 
                eventModel.Summary.Processed = actionDataList.Count();
                eventModel.Summary.Accepted = actionDataList.Count(item => item.IsSuccess);
                eventModel.Summary.Rejected = actionDataList.Count(item => !item.IsSuccess);

                eventModel.ActionData = actionDataList.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                var end = DateTime.Now;
                Logger.LogInformation("End of the validation.");
                Logger.LogInformation("End time {0}", end);
                TimeSpan processTime = (TimeSpan)(end - start);
                Logger.LogInformation(String.Format("Processed in {0} ms.", processTime));

                isSucces = true;
            }
            catch (Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "Exception occured in validation!");

                var anyMessages = new List<String>();
                anyMessages.Clear();
                anyMessages.Add("Exception occured in validation!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = -1;
                eventModel.Summary.Rejected = -1;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in validation!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Validation is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        private List<ActionDataItem> ValidateFileLengthAndIdentifyMismatchPronom(String droidCsvFile)
        {
            List<ActionDataItem> actionDataList = new List<ActionDataItem>();

            using (var reader = new StreamReader(droidCsvFile))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    var filesByDroid = this.IsToPX ?
                        records.Where(item => item.TYPE == "File" && item.EXT != "metadata").Select(item => new DataItem
                        {
                            Location = item.FILE_PATH,
                            Name = item.NAME,
                            Extension = item.EXT,
                            FormatName = item.FORMAT_NAME,
                            FormatVersion = item.FORMAT_VERSION,
                            Puid = item.PUID,
                            IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                        }).ToList() :
                        records.Where(item => item.TYPE == "File" && !item.NAME.EndsWith(".mdto.xml")).Select(item => new DataItem
                        {
                            Location = item.FILE_PATH,
                            Name = item.NAME,
                            Extension = item.EXT,
                            FormatName = item.FORMAT_NAME,
                            FormatVersion = item.FORMAT_VERSION,
                            Puid = item.PUID,
                            IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                        }).ToList();

                    filesByDroid.ForEach(droidFileItem =>
                    {
                        FileInfo fileInfo = new FileInfo(droidFileItem.Location);
                        var dataItem = new ActionDataItem
                        {
                            FileName = fileInfo.Name,
                            Location = fileInfo.DirectoryName,
                            IsSuccess = true,
                            FileSize = fileInfo.Length,
                            Message = ""
                        };
                        StringBuilder outputMessage = new StringBuilder();

                        if (!fileInfo.Exists)
                        {
                            outputMessage.Append(String.Format("Bestand niet gevonden: {0}", droidFileItem.Location));
                            dataItem.IsSuccess = false;
                        }
                        else
                        {
                            if (fileInfo.Length == 0)
                            {
                                outputMessage.Append("Een zero-byte-file gedetecteerd. Bestand heeft een grootte van 0 bytes");
                                dataItem.IsSuccess = false;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(droidFileItem.Puid))
                                {
                                    if (droidFileItem.IsExtensionMismatch)
                                    {
                                        outputMessage.Append(String.Format("PRONOM PUID is {0}, bestandsextensie is {1} maar volgens DROID een verkeerde combinatie", droidFileItem.Puid, droidFileItem.Extension));
                                        dataItem.IsSuccess = false;
                                    }
                                    else
                                    {
                                        outputMessage.Append(String.Format("PRONOM PUID is {0}, bestandsextensie is {1}. Combinatie is correct", droidFileItem.Puid, droidFileItem.Extension));
                                        dataItem.IsSuccess = true;
                                    }
                                }
                                else
                                {
                                    outputMessage.Append(String.Format("DROID kan het bestand {0} niet classificeren. PUID waarde uit PRONOM register via DROID is leeg", fileInfo.FullName));
                                    dataItem.IsSuccess = false;
                                }
                            }
                            dataItem.Message = outputMessage.ToString();
                        }
                        actionDataList.Add(dataItem);
                    });
                }
            }

            return actionDataList;
        }

        internal class ActionDataItem
        {
            public bool IsSuccess { get; set; }
            public String FileName { get; set; }
            public String Location { get; set; }
            public long FileSize { get; set; }
            public String Message { get; set; }
        }
        internal class DataItem
        {
            public string Location { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public string FormatName { get; set; }
            public string FormatVersion { get; set; }
            public string Puid { get; set; }
            public bool IsExtensionMismatch { get; set; }
        }
    }
}

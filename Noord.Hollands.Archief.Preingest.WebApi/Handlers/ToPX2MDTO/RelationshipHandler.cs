using CsvHelper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using MDTO = Noord.Hollands.Archief.Preingest.WebApi.Entities.MDTO.v1_0;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.ToPX2MDTO
{
    /// <summary>
    /// Convert all ToPX files to MDTO according to mapping sheet https://www.nationaalarchief.nl/archiveren/mdto/mapping-van-tmlo-tmr-naar-mdto
    /// Tip: use XSLT transformation afterwards for correction.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class RelationshipHandler : AbstractPreingestHandler, IDisposable
    {
        internal class ResultItem
        {
            public string MetadataFileName { get; set; }
            public string[] Messages { get; set; }

            public bool IsSuccess { get; set; }

        }
        private const String EXTENSION_MDTO = ".mdto.xml";
        /// <summary>
        /// Initializes a new instance of the <see cref="ToPX2MDTOHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public RelationshipHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            PreingestEvents += Trigger;
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            
        }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start updating MDTO metadata files (relationship references between MDTO files).", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
                       
            bool isSucces = false; 
            List<ResultItem> actionData = new List<ResultItem>();
            IDictionary<String, MDTO.mdtoType> currentCache = new Dictionary<String, MDTO.mdtoType>();

            try
            {
                base.Execute();
      
                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                FileInfo[] files = collection.GetFiles("*.*", SearchOption.AllDirectories);
                var listOfBinary = files.Where(item => !item.FullName.EndsWith(EXTENSION_MDTO)).ToList();
                var listOfMetadata = files.Where(item => item.FullName.EndsWith(EXTENSION_MDTO)).ToList();

                string collectionArchiveName = string.Empty;
                if (collection.GetFiles(String.Concat("*", EXTENSION_MDTO), SearchOption.TopDirectoryOnly).Count() == 1)  
                    collectionArchiveName = collection.Name;//current folder is archive collection name
                else
                {
                    if (collection.GetDirectories().Count() == 0)
                        throw new DirectoryNotFoundException(String.Format("No collection folder found in '{0}'!", collection.FullName));
                    //first folder is archive collection name
                    collectionArchiveName = collection.GetDirectories().FirstOrDefault().Name;
                }
                var scanResult = LoadAggregateEachEndNode(listOfBinary, listOfMetadata, collectionArchiveName);
               
                foreach (KeyValuePair<string, List<KeyValuePair<string, XDocument>>> kvpItem in scanResult)
                {
                    String endNodeLastMetadataFile = kvpItem.Key;
                    List<KeyValuePair<String, XDocument>> listOfNodes = kvpItem.Value;
                    listOfNodes.Reverse();
                    //listOfNodes: start altijd eerst met bestand niveau.
                    MDTO.mdtoType nextMDTO = null;
                    MDTO.mdtoType prevMDTO = null;
                    
                    Queue<KeyValuePair<String, XDocument>> queue = new Queue<KeyValuePair<String, XDocument>>(kvpItem.Value);
                    while (queue.Count > 0)
                    {                        
                        var resultItem = new ResultItem { Messages = new string[] { } };
                        var anyMessages = new List<String>();

                        try
                        {
                            var queueItem = queue.Dequeue();
                            string metadataFileName = queueItem.Key;
                            resultItem.MetadataFileName = metadataFileName;
                            var currentXmlMDTO = queueItem.Value;
                            var prevXmlMDTO = currentXmlMDTO;
                            var nextXmlMDTO = (queue.Count == 0) ? null : queue.Peek().Value;

                            MDTO.mdtoType currentMDTO = currentCache.ContainsKey(metadataFileName) ? currentCache[metadataFileName] : DeserializerHelper.DeSerializeObject<MDTO.mdtoType>(currentXmlMDTO.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces));
                            nextMDTO = nextXmlMDTO == null ? null : currentCache.ContainsKey(queue.Peek().Key) ? currentCache[queue.Peek().Key] : DeserializerHelper.DeSerializeObject<MDTO.mdtoType>(nextXmlMDTO.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces));
                                                      
                            var upReferenceList = (prevMDTO == null) ? null : (prevMDTO.Item as MDTO.informatieobjectType).identificatie.Select(item 
                                => new MDTO.verwijzingGegevens { verwijzingIdentificatie = item, verwijzingNaam = (prevMDTO.Item as MDTO.informatieobjectType).naam }).ToArray();
                            var downReferenceList = (nextMDTO == null) ? null : nextMDTO.IsBestand ? (nextMDTO.Item as MDTO.bestandType).identificatie.Select(item 
                                => new MDTO.verwijzingGegevens { verwijzingIdentificatie = item, verwijzingNaam = (nextMDTO.Item as MDTO.bestandType).naam }).ToArray() 
                                : (nextMDTO.Item as MDTO.informatieobjectType).identificatie.Select(item 
                                => new MDTO.verwijzingGegevens { verwijzingIdentificatie = item, verwijzingNaam = (nextMDTO.Item as MDTO.informatieobjectType).naam }).ToArray();

                            if (currentMDTO.IsBestand)
                            {
                                anyMessages.Add( "Referentie bijgewerkt voor bestandsType.");
                                currentMDTO.UpdateRelationshipReference(upReferenceList.First());
                            }
                            else
                            {
                                anyMessages.Add("Referentie bijgewerkt voor informatieobjectType.");
                                currentMDTO.UpdateRelationshipReference(upReferenceList, downReferenceList);
                            }

                            if (!currentCache.ContainsKey(metadataFileName))
                                currentCache.Add(metadataFileName, currentMDTO);
                            else
                                currentCache[metadataFileName] = currentMDTO;

                            prevMDTO = currentMDTO;//reset if reaches the end
                            resultItem.IsSuccess = true;

                            OnTrigger(new PreingestEventArgs
                            {
                                Description = String.Format("Update relationship references {0}", metadataFileName),
                                Initiate = DateTimeOffset.Now,
                                ActionType = PreingestActionStates.Executing,
                                PreingestAction = eventModel
                            });
                        }
                        catch (Exception e)
                        {
                            anyMessages.Add(e.Message);
                            anyMessages.Add(e.StackTrace);
                            resultItem.IsSuccess = false;
                        }
                        finally
                        {                            
                            resultItem.Messages = anyMessages.ToArray();
                            actionData.Add(resultItem);
                        }
                    }                    
                }

                if (currentCache.Count > 0) 
                    OnTrigger(new PreingestEventArgs { Description = String.Format("Saving all MDTO files. Count: {0}.", currentCache.Count), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                
                foreach (KeyValuePair<String, MDTO.mdtoType> item in currentCache)
                {
                    try
                    {
                        if (File.Exists(item.Key))
                            File.Delete(item.Key);
                        SerializerHelper.SerializeObjectToXmlFile<MDTO.mdtoType>(item.Value, item.Key);
                    }
                    catch (Exception e)
                    {
                        var resultItem = actionData.FirstOrDefault(action => action.MetadataFileName == item.Key);
                        if (resultItem != null)
                        {
                            var messages = new List<String>(resultItem.Messages);
                            messages.Add(e.Message);
                            messages.Add(e.StackTrace);
                            resultItem.Messages = messages.ToArray();
                            resultItem.IsSuccess = false;
                        }
                    }
                }

                eventModel.Summary.Processed = actionData.Count();
                eventModel.Summary.Accepted = actionData.Count(item => item.IsSuccess);
                eventModel.Summary.Rejected = actionData.Count(item => !item.IsSuccess);
                eventModel.ActionData = actionData.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSucces = true;                
            }
            catch (Exception e)
            {
                isSucces = false;
                var anyMessages = new List<String>();
                Logger.LogError(e, String.Format("An exception occured while converting ToPX to MDTO for collection {0}!", this.SessionGuid));
                anyMessages.Clear();
                anyMessages.Add(String.Format("An exception occured while converting ToPX to MDTO for collection {0}!", this.SessionGuid));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = String.Format("An exception occured while converting ToPX to MDTO for collection {0}!", this.SessionGuid), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Updating MDTO files is done!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        private List<ToPX2MDTOConverter.DataItem> LoadDroidResult(string droidCsvFile)
        {
            List<ToPX2MDTOConverter.DataItem> droidResults = new List<ToPX2MDTOConverter.DataItem>();
            using (var reader = new StreamReader(droidCsvFile))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    var filesByDroid = records.Where(item
                        => item.TYPE == "File" && item.EXT != "metadata").Select(item => new ToPX2MDTOConverter.DataItem
                        {
                            Location = item.FILE_PATH,
                            Name = item.NAME,
                            Extension = item.EXT,
                            FormatName = item.FORMAT_NAME,
                            FormatVersion = item.FORMAT_VERSION,
                            Puid = item.PUID,
                            IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                        }).ToList();
                    droidResults.AddRange(filesByDroid);
                }
            }
            return droidResults;
        }

        private IDictionary<String, List<KeyValuePair<String, XDocument>>> LoadAggregateEachEndNode(List<FileInfo> files, List<FileInfo> metadataList, String collectionName)
        {
            IDictionary<String, List<KeyValuePair<String, XDocument>>> resultDictionary = new Dictionary<String, List<KeyValuePair<String, XDocument>>>();

            foreach (FileInfo fileItem in files)
            {
                var metadataFile = String.Concat(fileItem.FullName, ".bestand", EXTENSION_MDTO);

                bool exists = metadataList.Exists((item) =>
                {
                    return item.FullName.Equals(metadataFile, StringComparison.InvariantCultureIgnoreCase);
                });

                if (!exists || !File.Exists(metadataFile))
                    throw new FileNotFoundException(String.Format("File '{0}' not found!", metadataFile));

                //Windows split, in Linux is it different.
                var folders = fileItem.Directory.FullName.Split('/').ToArray();

                int startingPoint = folders.ToList().IndexOf(collectionName);
                int endingPoint = folders.Count();

                var storagePoint = String.Join('/', folders.Skip(0).Take(startingPoint));

                var fullStructurePath = folders.Skip(startingPoint).Take(endingPoint).ToList();
                int counter = 0;

                var aggregationList = new List<KeyValuePair<String, XDocument>>();
                //add bestand self 
                XDocument bestand = XDocument.Load(metadataFile);
                aggregationList.Add(new KeyValuePair<String, XDocument>(metadataFile, bestand));

                foreach (var item in fullStructurePath)
                {
                    //shift the part folder names
                    var tempList = new List<String>(fullStructurePath);
                    tempList.RemoveRange((fullStructurePath.Count) - counter, counter);

                    string folderPath = Path.Combine(tempList.ToArray());

                    var currentProcessingDirectory = new DirectoryInfo(String.Concat(storagePoint, "/", folderPath));

                    string mdtoOrToPXmetadataFilename = String.Concat(currentProcessingDirectory.Name, EXTENSION_MDTO);
                    string mdtoOrToPXmetadataFullname = Path.Combine(currentProcessingDirectory.FullName, mdtoOrToPXmetadataFilename);

                    if (!File.Exists(mdtoOrToPXmetadataFullname))
                    {
                        string message = this.IsToPX ? String.Format("Adding parent into bestand failed! Parent not found in '{0}'.", mdtoOrToPXmetadataFullname) : String.Format("Adding parent into bestand failed! Parent not found in '{0}'.", mdtoOrToPXmetadataFullname);
                        throw new FileNotFoundException(message);
                    }

                    XDocument next = XDocument.Load(mdtoOrToPXmetadataFullname);
                    aggregationList.Add(new KeyValuePair<String, XDocument>(mdtoOrToPXmetadataFullname, next));

                    counter++;
                }
                resultDictionary.Add(metadataFile, aggregationList);
            }

            return resultDictionary;
        }

    }
}

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView;

using System;
using System.Collections.Generic;
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
    public class ToPX2MDTOHandler : AbstractPreingestHandler, IDisposable
    {
        internal class ResultItem
        {
            public string ToPXFilename { get; set; }
            public string[] Messages { get; set; }
            public string MDTOFilename { get; set; }
            public bool IsSuccess { get; set; }

        }
        private const String EXTENSION_TOPX = ".metadata";
        /// <summary>
        /// Initializes a new instance of the <see cref="ToPX2MDTOHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public ToPX2MDTOHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
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
            OnTrigger(new PreingestEventArgs { Description = "Start converting .metadata files.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
                       
            bool isSucces = false; 
            List<ResultItem> actionData = new List<ResultItem>();
            try
            {
                base.Execute();
                
                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                FileInfo[] files = collection.GetFiles("*.*", SearchOption.AllDirectories);
                var listOfBinary = files.Where(item => !item.FullName.EndsWith(EXTENSION_TOPX)).ToList();
                var listOfMetadata = files.Where(item => item.FullName.EndsWith(EXTENSION_TOPX)).ToList();

                string collectionArchiveName = string.Empty;
                if (collection.GetFiles(String.Concat("*", EXTENSION_TOPX), SearchOption.TopDirectoryOnly).Count() == 1)  
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
                    
                    //listOfNodes: start altijd eerst met bestand niveau.
                    foreach (KeyValuePair<String, XDocument> nodeItem in listOfNodes)
                    {
                        string metadataFileName = nodeItem.Key;
                        var resultItem = new ResultItem { ToPXFilename = metadataFileName, Messages = new string[] { } };
                        var anyMessages = new List<String>();

                        string xmlContent = String.Empty;
                        try
                        {
                            using (Utilities.ToPX2MDTOConverter convertor = new Utilities.ToPX2MDTOConverter(metadataFileName))
                            {
                                resultItem.MDTOFilename = metadataFileName.Replace(".metadata", convertor.IsBestand ? ".bestand.mdto.xml" : ".mdto.xml");
                                OnTrigger(new PreingestEventArgs
                                {
                                    Description = String.Format("Converting: {0}", metadataFileName),
                                    Initiate = DateTimeOffset.Now,
                                    ActionType = PreingestActionStates.Executing,
                                    PreingestAction = eventModel
                                });

                                var mdto = convertor.Convert();
                                xmlContent = SerializerHelper.SerializeObjectToString(mdto);
                                //remove if exists
                                if (File.Exists(resultItem.MDTOFilename))
                                    File.Delete(resultItem.MDTOFilename);
                            }
                        }
                        catch (Exception e)
                        {
                            anyMessages.Add(e.Message);
                            anyMessages.Add(e.StackTrace);
                        }
                        finally
                        {
                            if (!String.IsNullOrEmpty(xmlContent))
                            {
                                XDocument xmlDocument = XDocument.Parse(xmlContent);
                                xmlDocument.Save(resultItem.MDTOFilename);
                                anyMessages.Add(String.Concat("MDTO output resultaat is opgeslagen als bestand >> ", resultItem.MDTOFilename));
                                resultItem.IsSuccess = true;
                            }
                            else
                            {
                                resultItem.IsSuccess = false;
                                anyMessages.Add("MDTO output resultaat opslaan is niet gelukt!");
                            }
                        }
                        
                        resultItem.Messages = anyMessages.ToArray();
                        actionData.Add(resultItem);
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
                //clean up
                //remove temp file;
                //try
                //{
                //    File.Delete(xsdTmpFilename);
                //}
                //catch { }
                //clean up
                //remove old topx files
                actionData.ForEach(item => { try { File.Delete(item.ToPXFilename); } catch { } });
                SaveStructure(TargetFolder);

                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Conversion is done!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// Saves the structure.
        /// </summary>
        /// <param name="sessionFolder">The session folder.</param>
        private void SaveStructure(string sessionFolder)
        {
            try
            {
                var folder = new DirectoryInfo(sessionFolder);
                folder.Refresh();
                DataTreeHandler handler = new DataTreeHandler(SessionGuid, folder);
                handler.ClearCachedJson();
                handler.Load();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Aggregate from each end node/files from bottom to the top level.
        /// </summary>
        /// <param name="files">The files.</param>
        /// <param name="metadataList">The metadata list.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        private IDictionary<String, List<KeyValuePair<String, XDocument>>> LoadAggregateEachEndNode(List<FileInfo> files, List<FileInfo> metadataList, String collectionName)
        {
            IDictionary<String, List<KeyValuePair<String, XDocument>>> resultDictionary = new Dictionary<String, List<KeyValuePair<String, XDocument>>>();

            foreach (FileInfo fileItem in files)
            {
                var metadataFile = String.Concat(fileItem.FullName, EXTENSION_TOPX);

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

                    string mdtoOrToPXmetadataFilename = String.Concat(currentProcessingDirectory.Name, EXTENSION_TOPX);
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

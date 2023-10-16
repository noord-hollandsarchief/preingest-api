using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using MDTO = Noord.Hollands.Archief.Preingest.WebApi.Entities.MDTO.v1_0;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.ToPX2MDTO
{
    /// <summary>
    /// Convert all ToPX files to MDTO according to mapping sheet https://www.nationaalarchief.nl/archiveren/mdto/mapping-van-tmlo-tmr-naar-mdto
    /// Tip: use XSLT transformation afterwards for correction.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class FixityPropsHandler : AbstractPreingestHandler, IDisposable
    {
        internal class ResultItem
        {
            public string MDTOFilename { get; set; }
            public string BinaryFilename { get; set; }
            public string[] Messages { get; set; }

            public bool IsSuccess { get; set; }

        }
        private const String EXTENSION_MDTO = ".mdto.xml";
        /// <summary>
        /// Initializes a new instance of the <see cref="FixityPropsHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public FixityPropsHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
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
            OnTrigger(new PreingestEventArgs { Description = "Start updating MDTO metadata files (checksum algorithm + values, SHA-256).", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
                       
            bool isSucces = false; 
            List<ResultItem> actionData = new List<ResultItem>();

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
                    
                    //listOfNodes: start altijd eerst met bestand niveau.
                    foreach (KeyValuePair<String, XDocument> nodeItem in listOfNodes)
                    {
                        string metadataFileName = nodeItem.Key;                        
                        var anyMessages = new List<String>();
                        bool isBestand = (nodeItem.Value.Root.FirstNode as XElement).Name.LocalName.Equals("bestand", StringComparison.InvariantCultureIgnoreCase);

                        //logic
                        if (isBestand)
                        {
                            var resultItem = new ResultItem { MDTOFilename = metadataFileName, BinaryFilename = nodeItem.Key.Replace(".bestand.mdto.xml", string.Empty), Messages = new string[] { } };
                            try
                            {
                                MDTO.mdtoType mdto = DeserializerHelper.DeSerializeObject<MDTO.mdtoType>(nodeItem.Value.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces));
                                var currentChecksums = (mdto.Item as MDTO.bestandType).checksum == null ? new List<MDTO.checksumGegevens>() : new List<MDTO.checksumGegevens>((mdto.Item as MDTO.bestandType).checksum);
                                currentChecksums.Clear();//optional???
                                if (!File.Exists(resultItem.BinaryFilename))
                                    throw new FileNotFoundException(String.Format("Binaire bestand '{0}' niet gevonden. Kan geen fixity waarde berekenen.", resultItem.BinaryFilename));

                                string waarde = ChecksumHelper.CreateSHA256Checksum(new FileInfo(resultItem.BinaryFilename));
                                currentChecksums.Add(new MDTO.checksumGegevens
                                {
                                    checksumAlgoritme = new MDTO.begripGegevens
                                    {
                                        begripLabel = "SHA-256",
                                        begripCode = null,
                                        begripBegrippenlijst = new MDTO.verwijzingGegevens
                                        {
                                            verwijzingIdentificatie = null,//new MDTO.identificatieGegevens { identificatieBron = "", identificatieKenmerk = "" },
                                            verwijzingNaam = "MDTO begrippenlijsten versie 1.0"
                                        }
                                    },
                                    checksumDatum = DateTime.Now,
                                    checksumWaarde = waarde
                                });

                                (mdto.Item as MDTO.bestandType).checksum = currentChecksums.ToArray();

                                anyMessages.Add(String.Format("Bijgewerkt: {0} >> SHA-256, {1}", metadataFileName, waarde));
                                if (File.Exists(metadataFileName))
                                    File.Delete(metadataFileName);

                                SerializerHelper.SerializeObjectToXmlFile<MDTO.mdtoType>(mdto, metadataFileName);
                                resultItem.IsSuccess = true;

                                OnTrigger(new PreingestEventArgs
                                {
                                    Description = String.Format("Update fixity {0}", metadataFileName),
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
                Logger.LogError(e, String.Format("An exception occured while updating ToPX to MDTO for collection {0}!", this.SessionGuid));
                anyMessages.Clear();
                anyMessages.Add(String.Format("An exception occured while updating ToPX to MDTO for collection {0}!", this.SessionGuid));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = String.Format("An exception occured while updating ToPX to MDTO for collection {0}!", this.SessionGuid), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Updating MDTO files is done!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
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

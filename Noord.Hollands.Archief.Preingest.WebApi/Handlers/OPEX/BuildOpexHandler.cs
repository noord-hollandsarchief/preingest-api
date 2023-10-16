using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Schema;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{
    /// <summary>
    /// Handler for building ToPX or MDTO into OPEX.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class BuildOpexHandler : AbstractPreingestHandler, IDisposable
    {
        private const string OPEX_TOPLEVEL_NAME = "opex-{0}-{1}";

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildOpexHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public BuildOpexHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) 
            : base(settings, eventHub, preingestCollection)
        {
            PreingestEvents += Trigger;
        }
        public void Dispose()
        {
            PreingestEvents -= Trigger;
        }

        /// <summary>
        /// Gets or sets the inheritance setting.
        /// </summary>
        /// <value>
        /// The inheritance setting.
        /// </value>
        public Inheritance InheritanceSetting { get; set; }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.IO.DirectoryNotFoundException">Expanded collection folder not found!</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start building Opex for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                base.Execute();

                string sessionFolder = Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString());
                BodySettings settings = new SettingsReader(this.ApplicationSettings.DataFolderName, SessionGuid).GetSettings();
                if (settings != null && !String.IsNullOrEmpty(settings.MergeRecordAndFile))
                {
                    bool doMerge = settings.MergeRecordAndFile.Equals("Ja", StringComparison.InvariantCultureIgnoreCase);

                    if(doMerge && InheritanceSetting.MethodResult == InheritanceMethod.None)
                    {
                        //true && true
                        this.InheritanceSetting.MethodResult = InheritanceMethod.Combine;
                    }
                }

                string addNameSpaces = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.ADD_NAMESPACE);
                string stripNameSpaces = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.STRIP_NAMESPACE);
                string opexFolders = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.OPEX_FOLDERS);
                string opexFolderFiles = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.OPEX_FOLDER_FILES);
                string opexFiles = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.OPEX_FILES);
                string opexFinalize = Path.Combine(ApplicationSettings.PreWashFolder, OpexItem.OPEX_FINALIZE);

                var fileChkList = new List<bool>();
                fileChkList.Add(File.Exists(addNameSpaces));
                fileChkList.Add(File.Exists(stripNameSpaces));
                fileChkList.Add(File.Exists(opexFolders));
                fileChkList.Add(File.Exists(opexFolderFiles));
                fileChkList.Add(File.Exists(opexFiles));
                fileChkList.Add(File.Exists(opexFinalize));

                if (fileChkList.Contains(false))
                {
                    var items =  Stylesheet.StylesheetHandler.GetStylesheetList();

                    foreach(KeyValuePair<String, String> item in items)
                    {
                        if (item.Key.Contains("-opex-"))
                        {
                            XDocument xsl = XDocument.Parse(item.Value);
                            string newFile = Path.Combine(ApplicationSettings.PreWashFolder, item.Key.Replace("Noord.Hollands.Archief.Preingest.WebApi.Stylesheet.", string.Empty));
                            xsl.Save(newFile);
                        }
                    }
                }

                List<StylesheetItem> stylesheetList = new List<StylesheetItem>();

                OnTrigger(new PreingestEventArgs { Description = String.Format("Load settings for building Opex.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                var stylesheets = Directory.GetFiles(ApplicationSettings.PreWashFolder, "*-opex-*.xsl").Select(item => new StylesheetItem { KeyLocation = item, XmlContent = XDocument.Load(item).ToString() }).ToArray();
                stylesheetList.AddRange(stylesheets);
 
                List<OpexItem> potentionalOpexList = new List<OpexItem>();

                OnTrigger(new PreingestEventArgs { Description = String.Format("Reading collection to build Opex for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                //trigger event read collections
                if (this.IsToPX)
                {
                    var listOfMetadata = new DirectoryInfo(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString())).GetFiles("*.metadata", SearchOption.AllDirectories).Select(item
                        => new OpexItem(this.TargetFolder, item.FullName,
                        XDocument.Load(item.FullName).ToString(),
                        item.Directory.GetFiles("*.metadata", SearchOption.TopDirectoryOnly).Count())).ToArray();
                    potentionalOpexList.AddRange(listOfMetadata);
                    OnTrigger(new PreingestEventArgs { Description = String.Format("Found '{0}' metadata ({1}) files.", potentionalOpexList.Count, this.IsToPX ? "ToPX" : "MDTO"), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                }

                if (this.IsMDTO)
                {
                    var listOfMetadata = new DirectoryInfo(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString())).GetFiles("*.xml", SearchOption.AllDirectories).Where(item => item.Name.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).Select(item
                        => new OpexItem(this.TargetFolder, item.FullName,
                        XDocument.Load(item.FullName).ToString(),
                        item.Directory.GetFiles("*.xml", SearchOption.TopDirectoryOnly).Where(item
                            => item.Name.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).Count())).ToArray();
                    potentionalOpexList.AddRange(listOfMetadata);
                    OnTrigger(new PreingestEventArgs { Description = String.Format("Found '{0}' metadata ({1}) files.", potentionalOpexList.Count, this.IsToPX ? "ToPX" : "MDTO"), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                }

                OnTrigger(new PreingestEventArgs { Description = String.Format("Creating Opex files."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                //trigger event process opex
                potentionalOpexList.ForEach(item =>
                {
                    item.InitializeOpex(stylesheetList);
                });

                var jsonData = new List<String>();

                OnTrigger(new PreingestEventArgs { Description = String.Format("Removing original metadata files."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                //remove the old metadata files topx or mdto
                potentionalOpexList.ForEach(item =>
                {
                    try
                    {
                        File.Delete(item.KeyLocation);
                    }
                    catch (Exception delete)
                    {
                        anyMessages.Add(String.Format("Deleting file '{0}' failed! {1} {2}", item.KeyLocation, delete.Message, delete.StackTrace));
                    }
                });

                OnTrigger(new PreingestEventArgs { Description = String.Format("Saving Opex files (only file level)."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                var sortedPotentionalOpexList = new List<OpexItem>();

                if (InheritanceSetting.MethodResult == InheritanceMethod.Combine)
                {
                    var sorted = potentionalOpexList.Where(item => !item.IsFile).GroupBy(item => item.Level, (key, opex) => new { Level = key, Item = opex }).OrderByDescending(item
                       => item.Level).FirstOrDefault();
                    sortedPotentionalOpexList.AddRange(sorted.Item.OfType<OpexItem>());
                }

                //save bestand level first
                potentionalOpexList.Where(item => item.IsFile).ToList().ForEach(item =>
                {
                    try
                    {
                        var metadataList = new List<System.Xml.XmlElement>();

                        if (InheritanceSetting.MethodResult == InheritanceMethod.Combine)
                        {
                            var parts = item.KeyLocation.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
                            var startsWith = parts.Skip(0).Take(parts.Count() - 1).ToArray();
                            var currentItemPath = "/" + Path.Combine(startsWith);

                            var profile = sortedPotentionalOpexList.FirstOrDefault(sorted => sorted.KeyLocation.StartsWith(currentItemPath));

                            if (profile == null)
                                throw new ApplicationException(String.Format("Adding profile to DescriptiveMetadata failed! Cannot find profile record in '{0}'", currentItemPath));

                            var xmlProfileDoc = new System.Xml.XmlDocument();
                            xmlProfileDoc.LoadXml(profile.OriginalMetadata);
                            metadataList.Add(xmlProfileDoc.DocumentElement);
                        }

                        if (InheritanceSetting.MethodResult == InheritanceMethod.None)
                            metadataList.Clear();

                        item.UpdateOpexFileLevel(ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, metadataList, true);
                        var xml = XDocument.Parse(item.FinalUpdatedOpexMetadata);
                        xml.Save(item.KeyOpexLocation);
                    }
                    catch (Exception save)
                    {
                        anyMessages.Add(String.Format("Saving Opex file '{0}' failed! {1} {2}", item.KeyOpexLocation, save.Message, save.StackTrace));
                    }
                });

                OnTrigger(new PreingestEventArgs { Description = String.Format("Saving Opex files (folder levels)."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                //update mappen levels
                //save the rest of the folder levels
                potentionalOpexList.Where(item => !item.IsFile).ToList().ForEach(item =>
                {
                    try
                    {
                        item.UpdateOpexFolderLevel();
                        var xml = XDocument.Parse(item.FinalUpdatedOpexMetadata);
                        xml.Save(item.KeyOpexLocation);                        
                    }
                    catch (Exception save)
                    {
                        anyMessages.Add(String.Format("Saving Opex file '{0}' failed! {1} {2}", item.KeyOpexLocation, save.Message, save.StackTrace));
                    }
                });

                OnTrigger(new PreingestEventArgs { Description = String.Format("Rounding up Opex."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                //create container opex file,
                //move the result to a folder named 'opex';
                //this folder will be used by the bucket container for upload to S3
                var currentCollectionDI = new DirectoryInfo(TargetFolder);
                var currentCollectionFoldername = currentCollectionDI.GetDirectories().OrderBy(item => item.CreationTime).FirstOrDefault();
                if (currentCollectionFoldername == null)
                    throw new DirectoryNotFoundException("Expanded collection folder not found!");

                var opexContainerFilename = CreateContainerOpexMetadata(currentCollectionDI, currentCollectionFoldername.Name);
                var opexUploadFolder = Directory.CreateDirectory(Path.Combine(TargetFolder, "opex"));

                opexContainerFilename.MoveTo(Path.Combine(opexUploadFolder.FullName, opexContainerFilename.Name), true);
                currentCollectionFoldername.MoveTo(Path.Combine(opexUploadFolder.FullName, currentCollectionFoldername.Name));

                OnTrigger(new PreingestEventArgs { Description = String.Format("Reading Opex files for XSD schema validation."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                var newOpexFiles = opexUploadFolder.GetFiles("*.opex", SearchOption.AllDirectories).ToList();
                var schemaList = SchemaHandler.GetSchemaList();

                var strXsd = schemaList["Noord.Hollands.Archief.Preingest.WebApi.Schema.OPEX-Metadata.xsd"];
                var xsdTmpFilename = Path.GetTempFileName();
                var xsd = XDocument.Parse(strXsd);
                xsd.Save(xsdTmpFilename);

                OnTrigger(new PreingestEventArgs { Description = String.Format("Validate Opex files with XSD schema."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                newOpexFiles.ForEach(opex =>
                {
                    try
                    {
                        if (String.IsNullOrEmpty(opex.FullName) || !File.Exists(opex.FullName))
                            throw new FileNotFoundException(String.Format("Opex file with location '{0}' is empty or not found!", opex.FullName));

                        XDocument xml = XDocument.Load(opex.FullName);

                        SchemaValidationHandler.Validate(xml.ToString(), xsdTmpFilename);                        
                    }
                    catch (Exception validate)
                    {
                        anyMessages.Add(String.Format("Schema validation error for Opex file '{0}'! {1} {2}", opex.FullName, validate.Message, validate.StackTrace));
                    }
                });

                try { File.Delete(xsdTmpFilename); } catch { }

                var result = potentionalOpexList.Select(item => String.Format("{0} >> {1}", item.KeyLocation, item.KeyOpexLocation)).ToArray();
                jsonData.AddRange(result);
                opexContainerFilename.Refresh();
                jsonData.Add("All moved to folder 'opex'.");
                jsonData.Add(opexContainerFilename.FullName);

                eventModel.ActionData = jsonData.ToArray();
                eventModel.Summary.Processed = potentionalOpexList.Count;
                eventModel.Summary.Accepted = potentionalOpexList.Count;
                eventModel.Summary.Rejected = anyMessages.Count();
                eventModel.Properties.Messages = anyMessages.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                anyMessages.Clear();
                anyMessages.Add(String.Format("Build Opex with collection: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Build Opex with collection: '{0}' failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while building opex!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                SaveStructure();

                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Build Opex with a collection is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// Saves the structure.
        /// </summary>
        /// <param name="sessionFolder">The session folder.</param>
        private void SaveStructure()
        {
            try
            {
                var folder = new DirectoryInfo(TargetFolder);
                folder.Refresh();
                DataTreeHandler handler = new DataTreeHandler(SessionGuid, folder);
                handler.ClearCachedJson();
                handler.Load();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Creates the container opex metadata.
        /// </summary>
        /// <param name="currentOutputResultFolder">The current output result folder.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <param name="description">The description.</param>
        /// <param name="securityTag">The security tag.</param>
        /// <returns></returns>
        private FileInfo CreateContainerOpexMetadata(DirectoryInfo currentOutputResultFolder, String collectionName, String description = "Preservica Opex Container - Noord-Hollandsarchief", String securityTag = "closed")
        {
            string formattedDate = DateTime.Now.ToString("yyyy-MM-dd");
            string formattedTime = DateTime.Now.ToString("HH-mm-ss");
            string topLevelOpexFileName = String.Format(OPEX_TOPLEVEL_NAME, formattedDate, formattedTime);

            string opexMetadataFilename = Path.Combine(currentOutputResultFolder.FullName, String.Concat(topLevelOpexFileName, ".opex"));

            opexMetadata opex = new opexMetadata();

            opex.Transfer = new transfer();
            opex.Transfer.SourceID = Guid.NewGuid().ToString();
            opex.Transfer.Manifest = new manifest();
            opex.Transfer.Manifest.Folders = new string[] { collectionName };

            opex.Properties = new Properties();
            opex.Properties.Title = currentOutputResultFolder.Name;
            opex.Properties.Description = description;
            opex.Properties.SecurityDescriptor = securityTag;

            opex.DescriptiveMetadata = new DescriptiveMetadata();

            Preingest.WebApi.Utilities.SerializerHelper.SerializeObjectToXmlFile<opexMetadata>(opex, opexMetadataFilename);

            return new FileInfo(opexMetadataFilename);
        }
                
   }
}

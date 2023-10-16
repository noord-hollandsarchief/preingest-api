using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Microsoft.JSInterop.Implementation;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{   
    public class BuildNonMetadataOpexHandler : AbstractPreingestHandler, IDisposable
    {
        private const string OPEX_TOPLEVEL_NAME = "opex-{0}-{1}";
        public BuildNonMetadataOpexHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
            : base(settings, eventHub, preingestCollection)
        {
            PreingestEvents += Trigger;
        }
        public void Dispose()
        {
            PreingestEvents -= Trigger;
        }

        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start building Opex for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            var jsonData = new List<String>();

            try
            {
                if (IsMDTO || IsToPX)
                    throw new ApplicationException("Files found with extension of .mdto.xml or .metadata. Please use different call to build Opex with metadata.");

                if (Directory.Exists(Path.Combine(TargetFolder, "opex")))
                    Directory.Delete(Path.Combine(TargetFolder, "opex"), true);

                if (Directory.GetDirectories(TargetFolder).Length == 0)
                    throw new DirectoryNotFoundException("No folder found to process. Please expand the archive file first.");

                var folderColl = Directory.EnumerateDirectories(Directory.GetDirectories(TargetFolder).Select(item => new DirectoryInfo(item)).OrderBy(item 
                    => item.CreationTime).First().FullName, "*", SearchOption.AllDirectories).Select(item => new OpexNonMetadataItem(TargetFolder, item, false));
                var filesColl = Directory.EnumerateFiles(Directory.GetDirectories(TargetFolder).Select(item => new DirectoryInfo(item)).OrderBy(item
                    => item.CreationTime).First().FullName, "*", SearchOption.AllDirectories).Select(item => new OpexNonMetadataItem(TargetFolder, item, true));

                var totalResult = new List<OpexNonMetadataItem>();
                totalResult.AddRange(filesColl);
                totalResult.AddRange(folderColl);

                var unpackedFolder = Directory.GetDirectories(TargetFolder).FirstOrDefault();
                OnTrigger(new PreingestEventArgs { Description = String.Format("Start creating Opex in folder '{0}'...", unpackedFolder), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                //get XSD from resources
                var schemaList = Schema.SchemaHandler.GetSchemaList();
                var strXsd = schemaList["Noord.Hollands.Archief.Preingest.WebApi.Schema.OPEX-Metadata.xsd"];
                var xsdTmpFilename = Path.GetTempFileName();
                var xsd = XDocument.Parse(strXsd);
                xsd.Save(xsdTmpFilename);

                totalResult.ForEach(item =>
                {
                    try
                    {
                        OnTrigger(new PreingestEventArgs { Description = String.Format("Processing '{0}'.", item.KeyOpexLocation), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                        if (item.IsFile) item.UpdateOpexFileLevel(); else item.UpdateOpexFolderLevel();
                        var xml = XDocument.Parse(item.OpexXml);
                        xml.Save(item.KeyOpexLocation);
                        SchemaValidationHandler.Validate(xml.ToString(), xsdTmpFilename);
                        jsonData.Add(String.Format("{0}", item.KeyOpexLocation));
                    }
                    catch (Exception foreachExc)
                    {
                        anyMessages.Add(String.Format("Saving Opex '{0}' failed! {1} {2}", item.KeyLocation, foreachExc.Message, foreachExc.StackTrace));
                    }
                    finally { }
                });

                //delete xsd temp file
                try { File.Delete(xsdTmpFilename); } catch { }

                CreateContainerOpex();

                eventModel.ActionData = jsonData.ToArray();
                eventModel.Summary.Processed = jsonData.ToArray().Length;
                eventModel.Summary.Accepted = totalResult.Count - anyMessages.Count;
                eventModel.Summary.Rejected = anyMessages.Count;
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

                OnTrigger(new PreingestEventArgs
                {
                    Description = "An exception occured while building opex!",
                    Initiate = DateTimeOffset.Now,
                    ActionType = PreingestActionStates.Failed,
                    PreingestAction = eventModel
                });
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
                var handler = new DataTreeHandler(SessionGuid, folder);
                handler.ClearCachedJson();
                handler.Load();
            }
            catch (Exception) { }
        }

        private void CreateContainerOpex()
        {
            var currentCollectionFoldername = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
            if (currentCollectionFoldername == null)
                throw new DirectoryNotFoundException("Expanded collection folder not found!");

            var opexContainerFilename = CreateContainerOpexMetadata(currentCollectionFoldername, currentCollectionFoldername.Name);
            var opexUploadFolder = Directory.CreateDirectory(Path.Combine(TargetFolder, "opex"));

            //opexContainerFilename.MoveTo(Path.Combine(opexUploadFolder.FullName, opexContainerFilename.Name), true);
            currentCollectionFoldername.MoveTo(Path.Combine(opexUploadFolder.FullName, currentCollectionFoldername.Name));
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

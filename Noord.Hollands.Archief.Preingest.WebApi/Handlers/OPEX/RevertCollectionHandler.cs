using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Noord.Hollands.Archief.Preingest.WebApi.Schema;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{
    public class RevertCollectionHandler : AbstractPreingestHandler, IDisposable
    {
        public RevertCollectionHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
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
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start reverting DIP '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            var jsonData = new List<String>();

            try
            {
                if (!TargetCollection.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    throw new ApplicationException(String.Format("Een DIP bestand met een .zip extensie verwacht. Collectie heeft een {0} extensie.", new FileInfo(TargetCollection).Extension));               

                OnTrigger(new PreingestEventArgs { Description = String.Format("Validate zip contents."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                var validationOutput = ValidateZipContent();
                jsonData.AddRange(validationOutput.Select(item => item.Message));

                //check if contains pax/xip >> opex
                OnTrigger(new PreingestEventArgs { Description = String.Format("Extract zip contents."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                var extractionResultOutput = DoExtractionAndConversion();
                jsonData.AddRange(extractionResultOutput.Where(item => item.IsSuccess == true).Select(item => item.Message));
                anyMessages.AddRange(extractionResultOutput.Where(item => item.IsSuccess == false).Select(item => item.Message));

                OnTrigger(new PreingestEventArgs { Description = String.Format("Wrap up revert action."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                eventModel.ActionData = jsonData.ToArray();
                eventModel.Summary.Processed = jsonData.Count + anyMessages.Count;
                eventModel.Summary.Accepted = jsonData.Count;
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
                anyMessages.Add(String.Format("Reverting DIP: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Reverting DIP: '{0}' failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs
                {
                    Description = "An exception occured while reverting DIP!",
                    Initiate = DateTimeOffset.Now,
                    ActionType = PreingestActionStates.Failed,
                    PreingestAction = eventModel
                });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Reverting DIP is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        private ResultItem[] ValidateZipContent()
        {            
            List<ResultItem> results = new List<ResultItem>();

            using (ZipArchive zip = (ZipArchive)ZipFile.OpenRead(TargetCollection))
            {
                var countZipOpex = zip.Entries.Where(item => item.FullName.EndsWith(".zip.opex")).Count();
                if( countZipOpex <= 0)                
                    results.Add(new ResultItem { IsSuccess = false, Message ="Inhoud zip bestand geen item(s) gevonden met .zip.opex extensie." });
                
                var countPaxZip = zip.Entries.Where(item => item.FullName.EndsWith(".pax.zip")).Count();
                if( countPaxZip <= 0)
                    results.Add(new ResultItem { IsSuccess = false, Message = "Inhoud zip bestand geen item(s) gevonden met .pax.zip extensie." });

                var countXip = zip.Entries.Where(item => item.FullName.EndsWith(".xip")).Count();
                if (countPaxZip <= 0)
                    results.Add(new ResultItem { IsSuccess = false, Message = "Inhoud zip bestand geen item(s) gevonden met .xip extensie." });

                var schemaList = SchemaHandler.GetSchemaList();

                var strOpexXsd = schemaList["Noord.Hollands.Archief.Preingest.WebApi.Schema.OPEX-Metadata.xsd"];
                var xsdOpexTmpFilename = Path.GetTempFileName();
                var xsdOpex = XDocument.Parse(strOpexXsd);
                xsdOpex.Save(xsdOpexTmpFilename);

                var strXipXsd = schemaList["Noord.Hollands.Archief.Preingest.WebApi.Schema.XIP-V6.xsd"];
                var xsdXipTmpFilename = Path.GetTempFileName();
                var xsdXip = XDocument.Parse(strXipXsd);
                xsdXip.Save(xsdXipTmpFilename);

                zip.Entries.ToList().ForEach(entry =>
                {
                    if (entry.FullName.EndsWith(".zip.opex"))
                    {
                        try
                        {
                            byte[] opexByteData = new byte[0];
                            using (MemoryStream opexMemory = new MemoryStream())
                            {
                                using (Stream opexStream = entry.Open())
                                    opexStream.CopyTo(opexMemory);
                                opexByteData = opexMemory.ToArray();
                            }
                            string xml = Encoding.UTF8.GetString(opexByteData);
                            SchemaValidationHandler.Validate(xml, xsdOpexTmpFilename);
                        }
                        catch (Exception validate)
                        {
                            results.Add(new ResultItem
                            {
                                Message = String.Format("Schema validation error for Opex item '{0}'! {1} {2}", entry.FullName, validate.Message, validate.StackTrace),
                                IsSuccess = false
                            });
                        }
                    }
                    if (entry.FullName.EndsWith(".xip"))
                    {
                        try
                        {
                            byte[] xipByteData = new byte[0];
                            using (MemoryStream opexMemory = new MemoryStream())
                            {
                                using (Stream opexStream = entry.Open())
                                    opexStream.CopyTo(opexMemory);
                                xipByteData = opexMemory.ToArray();
                            }

                            string xml = Encoding.UTF8.GetString(xipByteData);
                            SchemaValidationHandler.Validate(xml, xsdXipTmpFilename);
                        }
                        catch (Exception validate)
                        {
                            results.Add(new ResultItem
                            {
                                Message = String.Format("Schema validation error for Opex item '{0}'! {1} {2}", entry.FullName, validate.Message, validate.StackTrace),
                                IsSuccess = false
                            });
                        }
                    }
                });

                try
                {
                    File.Delete(xsdOpexTmpFilename);
                    File.Delete(xsdXipTmpFilename);
                }
                catch { }

                return results.ToArray();
            }
        }

        private ResultItem[] DoExtractionAndConversion(string targetFolderName = "meta")
        {
            List<ResultItem> results = new List<ResultItem>();

            using (BinaryMetadataCollectionHandler innerHandler = new BinaryMetadataCollectionHandler())
            {
                List<string> paxZipQueueForDeletionList = new List<string>();

                using (ZipArchive zip = (ZipArchive)ZipFile.OpenRead(TargetCollection))
                {
                    var totalCollection = zip.Entries.ToList();

                    foreach (var zipItem in totalCollection)
                    {
                        try
                        {
                            var targetPathParts = Path.Combine(TargetFolder, targetFolderName).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                            var pathParts = zipItem.FullName.Split("/", StringSplitOptions.RemoveEmptyEntries);
                            var total = new string[targetPathParts.Length + pathParts.Length];
                            targetPathParts.CopyTo(total, 0);
                            pathParts.CopyTo(total, targetPathParts.Length);

                            string extractionOutput = String.Empty;

                            var parentFolder = total.Skip(0).Take(total.Length - 1).ToArray();
                            string extractionParentFolder = Path.DirectorySeparatorChar + Path.Combine(parentFolder);

                            //create folder structure first
                            Directory.CreateDirectory(extractionParentFolder);

                            if (zipItem.FullName.EndsWith(".zip.opex"))
                            {
                                byte[] opexByteData = new byte[0];
                                using (MemoryStream opexMemory = new MemoryStream())
                                {
                                    using (Stream opexStream = zipItem.Open())
                                        opexStream.CopyTo(opexMemory);
                                    opexByteData = opexMemory.ToArray();
                                }

                                string xml = Encoding.UTF8.GetString(opexByteData);
                                opexMetadata opexObject = Utilities.DeserializerHelper.DeSerializeObject<opexMetadata>(xml);
                                //add to handler
                                innerHandler.PutItem(zipItem.FullName, opexObject);
                            }
                            if (!zipItem.FullName.EndsWith(".zip.opex") && zipItem.FullName.EndsWith(".opex"))
                            {
                                //extractionOutput = Path.Combine(total);
                                //zipItem.ExtractToFile(extractionOutput, true);
                                byte[] opexByteData = new byte[0];
                                using (MemoryStream opexMemory = new MemoryStream())
                                {
                                    using (Stream opexStream = zipItem.Open())
                                        opexStream.CopyTo(opexMemory);
                                    opexByteData = opexMemory.ToArray();
                                }

                                string xml = Encoding.UTF8.GetString(opexByteData);
                                opexMetadata opexObject = Utilities.DeserializerHelper.DeSerializeObject<opexMetadata>(xml);
                                //add to handler
                                innerHandler.PutItem(zipItem.FullName, opexObject);
                                //add to handler
                                //innerHandler.PutItem(zipItem.FullName, opexObject);
                            }
                            if (zipItem.FullName.EndsWith(".pax.zip"))
                            {
                                //extract the opex metadata
                                extractionOutput = Path.DirectorySeparatorChar + Path.Combine(total);
                                zipItem.ExtractToFile(extractionOutput, true);

                                using (ZipArchive paxZip = (ZipArchive)ZipFile.OpenRead(extractionOutput))
                                {
                                    var xip = paxZip.Entries.FirstOrDefault(xip => xip.FullName.EndsWith(".xip"));
                                    if (xip != null)
                                    {
                                        byte[] xipByteData = new byte[0];
                                        using (MemoryStream mem = new MemoryStream())
                                        {
                                            using (Stream stream = xip.Open())
                                                stream.CopyTo(mem);

                                            xipByteData = mem.ToArray();
                                        }

                                        string xml = Encoding.UTF8.GetString(xipByteData);
                                        XIPType xipObject = Utilities.DeserializerHelper.DeSerializeObject<XIPType>(xml);
                                        innerHandler.PutItem(xipObject);

                                        var bitStream = xipObject.Bitstream.FirstOrDefault();
                                        if (bitStream == null)
                                            throw new ApplicationException(String.Format("No bit stream object found in the XIP: '{0}'", xip.FullName));

                                        var binaryFilename = bitStream.Filename;
                                        var binaryLocation = bitStream.PhysicalLocation;
                                        var binaryEntryKey = String.Concat(binaryLocation, "/", binaryFilename);

                                        var binaryFile = paxZip.Entries.FirstOrDefault(binary => binary.FullName == binaryEntryKey);
                                        if (binaryFile != null)
                                        {
                                            var extractBinFileOutput = Path.Combine(extractionParentFolder, binaryFilename);
                                            binaryFile.ExtractToFile(extractBinFileOutput, true);
                                            //add to handler
                                            paxZipQueueForDeletionList.Add(extractionOutput);
                                        }
                                    }
                                }
                            }

                        }
                        catch(Exception exc)
                        {
                            results.Add(new ResultItem
                            {
                                IsSuccess = false,
                                Message = String.Format("Extractie mislukt voor item '{0}' uit zip bestand. {1} {2}", zipItem.FullName, exc.Message, exc.StackTrace)
                            });
                        }
                    }
                }
                paxZipQueueForDeletionList.ForEach(item => File.Delete(item));

                var sorted = innerHandler.GetInformationMapItems().OrderByDescending(item => item.IsCompletely).ToList();
                sorted.ForEach(opex =>
                {
                    //opex.SaveOpex(new DirectoryInfo(targetFolder));
                    string resultOutput = opex.SaveDescriptiveMetadata(new DirectoryInfo(Path.Combine(TargetFolder, targetFolderName)));
                    results.Add(new ResultItem { IsSuccess = true, Message = String.Format("Metadata beschrijving opgeslagen in: '{0}'.", resultOutput) });
                });
            }

            return results.ToArray();
        }

        internal class ResultItem
        {
            public String Message { get; set; }
            public bool IsSuccess { get; set; }
        }

        internal class BinaryMetadataCollectionHandler : IDisposable
        {
            List<MetadataContainerItem> _internal;
            public BinaryMetadataCollectionHandler()
            {
                _internal = new List<MetadataContainerItem>();
            }

            public void Dispose()
            {
                this._internal.Clear();
                this._internal = null;
            }

            public IEnumerable<MetadataContainerItem> GetInformationMapItems()
            {
                return _internal.AsEnumerable();
            }

            public void PutItem(XIPType xip)
            {
                MetadataContainerItem item = new MetadataContainerItem(xip);

                bool isTrue = _internal.Exists(result =>
                {
                    bool retval = false;
                    retval = result.Reference == item.Reference;
                    return retval;
                });

                if (isTrue)
                {
                    var map = _internal.First(map => map.Reference == item.Reference);
                    map.XIP = xip;
                }
                else
                {
                    _internal.Add(item);
                }
            }

            public void PutItem(String key, opexMetadata opex)
            {
                MetadataContainerItem item = new MetadataContainerItem(key, opex);
                bool isTrue = _internal.Exists(result =>
                {
                    bool retval = false;
                    retval = result.Reference == item.Reference;
                    return retval;
                });

                if (isTrue)
                {
                    var map = _internal.First(map => map.Reference == item.Reference);
                    map.OPEX = opex;
                    map.Key = key;
                }
                else
                {
                    _internal.Add(item);
                }
            }
        }
    }
}


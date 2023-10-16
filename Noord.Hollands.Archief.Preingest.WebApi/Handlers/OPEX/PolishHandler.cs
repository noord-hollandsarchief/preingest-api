using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{
    /// <summary>
    /// Handler for post-transform OPEX files with XSL(T). It's uses XSLT1.0 transformation. Future enhancement: connect with microservice XSLWeb for XSLT2.0/XSLT3.0
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class PolishHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PolishHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public PolishHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            PreingestEvents += Trigger;
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            PreingestEvents -= Trigger;
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.ApplicationException">
        /// Settings are not saved!
        /// or
        /// Polish setting is empty!
        /// or
        /// Zero OPEX files found!
        /// </exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start polishing Opex for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                string sessionFolder = Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString());
                BodySettings settings = new SettingsReader(this.ApplicationSettings.DataFolderName, SessionGuid).GetSettings();

                if (settings == null)
                    throw new ApplicationException("Settings are not saved!");

                if (String.IsNullOrEmpty(settings.Polish))
                    throw new ApplicationException("Polish setting is empty!");

                DirectoryInfo directoryInfoSessionFolder = new DirectoryInfo(sessionFolder);
                List<FileInfo> opexFiles = directoryInfoSessionFolder.GetFiles("*.opex", SearchOption.AllDirectories).ToList();

                if (opexFiles.Count == 0)
                    throw new ApplicationException("Zero OPEX files found!");

                string stylesheetFileLocation = Path.Combine(ApplicationSettings.PreWashFolder, settings.Polish);

                var jsonData = new List<string>();

                int countTotal = opexFiles.Count;

                OnTrigger(new PreingestEventArgs
                {
                    Description = String.Format("Transforming: {0} files", countTotal),
                    Initiate = DateTimeOffset.Now,
                    ActionType = PreingestActionStates.Executing,
                    PreingestAction = eventModel
                });

                opexFiles.ForEach(file =>
                {
                    string result = string.Empty;

                    try
                    {
                        var opex = XDocument.Load(file.FullName).ToString();
                        var xsl = XDocument.Load(stylesheetFileLocation).ToString();
                        result = Transform(xsl, opex);
                    }
                    catch (Exception e)
                    {
                        anyMessages.Add(String.Format("Transformation with Opex file '{0}' failed! {1} {2}", file.FullName, e.Message, e.StackTrace));
                    }
                    finally
                    {
                        if (!String.IsNullOrEmpty(result))
                        {
                            try
                            {
                                XDocument doc = XDocument.Parse(result);
                                doc.Save(file.FullName);
                            }
                            catch (Exception ie)
                            {
                                anyMessages.Add(String.Format("Saving Opex file '{0}' failed! {1} {2}", file.FullName, ie.Message, ie.StackTrace));
                            }
                            finally
                            {
                                jsonData.Add(String.Format("Transformation processed: {0}", file.FullName));
                            }
                        }
                    }
                });

                //update opex
                var profiles = opexFiles.Select(item => new { Xml = XDocument.Load(item.FullName).ToString(), FullName = item.FullName }).Where(item => item.Xml.Contains("<Files>")).ToList();
                countTotal = profiles.Count;

                OnTrigger(new PreingestEventArgs
                {
                    Description = String.Format("Updating profile (Opex files): Total {0} files.", countTotal),
                    Initiate = DateTimeOffset.Now,
                    ActionType = PreingestActionStates.Executing,
                    PreingestAction = eventModel
                });

                profiles.ForEach(item =>
                {
                    FileInfo opex = new FileInfo(item.FullName);
                    FileInfo[] currentContent = opex.Directory.GetFiles().Where(content => content.FullName != item.FullName).ToArray();

                    var currentOpexMetadataFile = Preingest.WebApi.Utilities.DeserializerHelper.DeSerializeObjectFromXmlFile<opexMetadata>(item.Xml);
                    //overwrite
                    if (currentContent.Count() > 0)
                    {
                        if (currentOpexMetadataFile.Transfer.Manifest == null)
                            currentOpexMetadataFile.Transfer.Manifest = new manifest();

                        currentOpexMetadataFile.Transfer.Manifest.Files = currentContent.Select(item => new fileItem
                        {
                            size = item.Length,
                            typeSpecified = true,
                            type = item.Extension.Equals(".opex", StringComparison.InvariantCultureIgnoreCase) ? fileType.metadata : fileType.content,
                            sizeSpecified = true,
                            Value = item.Name
                        }).ToArray();

                        string output = Preingest.WebApi.Utilities.SerializerHelper.SerializeObjectToString(currentOpexMetadataFile);
                        var newDoc = XDocument.Parse(output);
                        newDoc.Save(item.FullName);
                    }
                });

                OnTrigger(new PreingestEventArgs { Description = String.Format("Reading Opex files for XSD schema validation."), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                var newOpexFiles = directoryInfoSessionFolder.GetFiles("*.opex").ToList();
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

                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.ActionData = jsonData.ToArray();

                eventModel.ActionResult.ResultValue = PreingestActionResults.Success;
                eventModel.Summary.Processed = opexFiles.Count;
                eventModel.Summary.Accepted = (opexFiles.Count - anyMessages.Count);
                eventModel.Summary.Rejected = anyMessages.Count;

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
                anyMessages.Add(String.Format("Run polish with collection: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Run polish with collection: '{0}' failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while running checksum!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)                
                    OnTrigger(new PreingestEventArgs { Description = "Polish run with a collection is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
                
            }
        }
        /// <summary>
        /// Transforms the specified XSL.
        /// </summary>
        /// <param name="xsl">The XSL.</param>
        /// <param name="xml">The XML.</param>
        /// <returns></returns>
        private String Transform(string xsl, string xml)
        {
            string output = String.Empty;

            using (StringReader srt = new StringReader(xsl)) // xslInput is a string that contains xsl
            using (StringReader sri = new StringReader(xml)) // xmlInput is a string that contains xml
            {
                using (XmlReader xrt = XmlReader.Create(srt))
                using (XmlReader xri = XmlReader.Create(sri))
                {
                    XslCompiledTransform xslt = new XslCompiledTransform();
                    xslt.Load(xrt);
                    using (UTF8StringWriter sw = new UTF8StringWriter())
                    using (XmlWriter xwo = XmlWriter.Create(sw, xslt.OutputSettings)) // use OutputSettings of xsl, so it can be output as HTML
                    {
                        xslt.Transform(xri, xwo);
                        output = sw.ToString();
                    }
                }
            }

            return output;
        }
        /// <summary>
        /// Specific StringWriter UTF8 encoding
        /// </summary>
        /// <seealso cref="System.IO.StringWriter" />
        internal class UTF8StringWriter : StringWriter
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="UTF8StringWriter"/> class.
            /// </summary>
            public UTF8StringWriter() { }
            /// <summary>
            /// Initializes a new instance of the <see cref="UTF8StringWriter"/> class.
            /// </summary>
            /// <param name="formatProvider">An <see cref="T:System.IFormatProvider" /> object that controls formatting.</param>
            public UTF8StringWriter(IFormatProvider formatProvider) : base(formatProvider) { }
            /// <summary>
            /// Initializes a new instance of the <see cref="UTF8StringWriter"/> class.
            /// </summary>
            /// <param name="sb">The <see cref="T:System.Text.StringBuilder" /> object to write to.</param>
            public UTF8StringWriter(StringBuilder sb) : base(sb) { }
            /// <summary>
            /// Initializes a new instance of the <see cref="UTF8StringWriter"/> class.
            /// </summary>
            /// <param name="sb">The <see cref="T:System.Text.StringBuilder" /> object to write to.</param>
            /// <param name="formatProvider">An <see cref="T:System.IFormatProvider" /> object that controls formatting.</param>
            public UTF8StringWriter(StringBuilder sb, IFormatProvider formatProvider) : base(sb, formatProvider) { }

            /// <summary>
            /// Gets the <see cref="T:System.Text.Encoding" /> in which the output is written.
            /// </summary>
            public override Encoding Encoding
            {
                get
                {
                    return Encoding.UTF8;
                }
            }
        }
    }
}

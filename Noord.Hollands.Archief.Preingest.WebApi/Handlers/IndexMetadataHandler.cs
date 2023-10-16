using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Xsl;
using System.Text;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using System.Web;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler to flatten all metadata files in a collection into an Excel report.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class IndexMetadataHandler : AbstractPreingestHandler, IDisposable
    {
        private List<ProcessResultItem> _validationResultList = null;
        /// <summary>
        /// Initializes a new instance of the <see cref="IndexMetadataHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public IndexMetadataHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
            _validationResultList = new List<ProcessResultItem>();

        }

        /// <summary>
        /// Gets or sets the root names.
        /// </summary>
        /// <value>
        /// The root names.
        /// </value>
        public String[] RootNames { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [ignore error validation errors].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [ignore error validation errors]; otherwise, <c>false</c>.
        /// </value>
        public bool IgnoreErrorValidationErrors { get; set; }
        /// <summary>
        /// Gets or sets the name of the schema.
        /// </summary>
        /// <value>
        /// The name of the schema.
        /// </value>
        public String SchemaName { get; set; }
        /// <summary>
        /// Gets or sets the name of the style.
        /// </summary>
        /// <value>
        /// The name of the style.
        /// </value>
        public String StyleName { get; set; }
        /// <summary>
        /// Gets or sets the count metadata.
        /// </summary>
        /// <value>
        /// The count metadata.
        /// </value>
        private Int32 CountMetadata { get; set; }
        /// <summary>
        /// Gets or sets the current XSD file location.
        /// </summary>
        /// <value>
        /// The current XSD file location.
        /// </value>
        private String CurrentXsdFileLocation { get; set; }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.ApplicationException">
        /// Settings are not saved!
        /// or
        /// Cannot determine ToPX either MDTO!
        /// or
        /// Error(s) found looking for extra XML files!
        /// </exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name); 
            eventModel.Summary.Processed = 1;

            OnTrigger(new PreingestEventArgs { Description = "Start indexing metadata", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            bool isSuccess = false;
            var anyMessages = new List<String>();

            try
            {
                base.Execute();

                BodySettings settings = new SettingsReader(this.ApplicationSettings.DataFolderName, SessionGuid).GetSettings();

                if (settings == null)
                    throw new ApplicationException("Settings are not saved!");
                StyleName = "Noord.Hollands.Archief.Preingest.WebApi.Stylesheet.flatten.xsl";

                SchemaName = String.IsNullOrEmpty(settings.SchemaToValidate) ? 
                    String.Format("Noord.Hollands.Archief.Preingest.WebApi.Schema.{0}", IsToPX ? "ToPX-2.3_2.xsd" : IsMDTO ? "MDTO-XML 1.0.xsd" : throw new ApplicationException("Cannot determine ToPX either MDTO!")) :
                     String.Format("Noord.Hollands.Archief.Preingest.WebApi.Schema.{0}", settings.SchemaToValidate);
                                
                IgnoreErrorValidationErrors = settings.IgnoreValidation.Equals("Ja", StringComparison.InvariantCultureIgnoreCase);

                //if argument from call is set, settings is empty: use values from argument
                //if argument from call is set, settings is set, but different sequence or content, use values from settings
                if (!String.IsNullOrEmpty(settings.RootNamesExtraXml))
                {
                    var inputRootNamesExtraXml = settings.RootNamesExtraXml.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    bool isSequenceEqual = Enumerable.SequenceEqual<String>(RootNames, inputRootNamesExtraXml);
                    if (!isSequenceEqual)
                        RootNames = inputRootNamesExtraXml;
                }

                List<ResultPair> totalResult = new List<ResultPair>();
                var result = ContinueDefaultCollectionItems(TargetFolder);

                eventModel.Summary.Processed = result.TotalContentData.Count;

                if (result.TotalContentData != null && result.TotalContentData.Count > 0)
                {
                    totalResult.Add(result);
                    var emptyElementsResult = ContinueDefaultCollectionItems(TargetFolder, false, true);
                    totalResult.Add(emptyElementsResult);
                }
                else
                {
                    _validationResultList.Add(new ProcessResultItem
                    {
                        Error = new ApplicationException(String.Format("No metadata files found.")),
                        Metadata = null,
                        Type = ProcessType.SchemaValidation
                    });
                }

                if (RootNames != null && RootNames.Length > 0)
                {
                    foreach (string rootName in RootNames)
                    {
                        var extraResult = ContinueExtraCollectionItems(TargetFolder, rootName);
                        eventModel.Summary.Processed = eventModel.Summary.Processed + extraResult.TotalContentData.Count;

                        if (extraResult.TotalContentData.Count == 0)
                        {
                            _validationResultList.Add(new ProcessResultItem
                            {
                                Error = new ApplicationException(String.Format("No extra XML files found with root element '{0}'.", rootName)),
                                Metadata = null,
                                Type = ProcessType.XmlOthers
                            });
                        }
                        else
                        {
                            totalResult.Add(extraResult);
                        }
                    }

                    if (_validationResultList.Count > 0 && !IgnoreErrorValidationErrors)
                        throw new ApplicationException("Error(s) found looking for extra XML files!");                    
                }

                CreateExcel(totalResult);

                eventModel.Summary.Accepted = (eventModel.Summary.Processed - _validationResultList.Count);
                eventModel.Summary.Rejected = 0;

                eventModel.ActionData = new string[] {  };

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;

                Logger.LogError(e, "An exception occured while indexing the metadata files!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured while indexing the metadata files!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);
                anyMessages.AddRange(_validationResultList.Select(item => item.ToString()));
                
                eventModel.Summary.Accepted = (eventModel.Summary.Processed - _validationResultList.Count);
                eventModel.Summary.Rejected = 1;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while indexing the metadata files!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Indexing metadata files is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// Continues the default collection items.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns></returns>
        /// <exception cref="System.ApplicationException">
        /// Validation error(s) found using XSD schema.
        /// or
        /// Stylesheet flatten.xsl not found in embedded resource.
        /// or
        /// Error(s) found while transforming metadata files with stylesheet!
        /// </exception>
        private ResultPair ContinueDefaultCollectionItems(string folder, bool validateSchema = true, bool flattenOnlyEmptyElements = false)
        {
            DirectoryInfo targetDirectory = new DirectoryInfo(folder);

            string filter = string.Empty;

            if (this.IsToPX)
                filter = "*.metadata";
            if (this.IsMDTO)
                filter = "*.mdto.xml";

            var filesResultList = targetDirectory.GetFiles(filter, SearchOption.AllDirectories).ToList();

            CountMetadata = filesResultList.Count;

            //Set schema from embedded resource to output file in temp folder of the container
            var schemaList = Schema.SchemaHandler.GetSchemaList();
            var xsdTempPath = Path.GetTempFileName();            
            var xsdSchemaContent = schemaList[SchemaName];
            XDocument.Parse(xsdSchemaContent).Save(xsdTempPath);
            CurrentXsdFileLocation = xsdTempPath;

            if (CountMetadata == 0)
            {
                string message = IsMDTO ? "No *.mdto.xml files found." : "No *.metadata files found.";
                _validationResultList.Add(new ProcessResultItem
                {
                    Error = new ApplicationException(message),
                    Metadata = null,
                    Type = ProcessType.SchemaValidation
                });

                return new ResultPair() { Name = "Leeg", TotalContentData = new Dictionary<string, Dictionary<string, string>>(), TotalContentHeader = new HashSet<string>() };
            }

            if (validateSchema)
            {
                foreach (var fileInfo in filesResultList)
                {
                    try
                    {
                        var doc = XDocument.Load(fileInfo.FullName);
                        string metadataContent = doc.ToString();
                        Utilities.SchemaValidationHandler.Validate(metadataContent, CurrentXsdFileLocation);
                    }
                    catch (Exception e)
                    {
                        this._validationResultList.Add(new ProcessResultItem { Error = e, Metadata = fileInfo, Type = ProcessType.SchemaValidation });
                    }
                    finally { }
                }

                if (_validationResultList.Count > 0 && !IgnoreErrorValidationErrors)
                    throw new ApplicationException("Validation error(s) found using XSD schema.");
            }

            var styleList = Stylesheet.StylesheetHandler.GetStylesheetList();
            string stylesheet = styleList[StyleName];

            if (String.IsNullOrEmpty(stylesheet))
                throw new ApplicationException("Stylesheet flatten.xsl not found in embedded resource.");
            XDocument flattenXsl = XDocument.Parse(stylesheet);

            var totalContentData = new Dictionary<String, Dictionary<String, String>>();
            var totalContentHeader = new HashSet<String>();
            totalContentHeader.Add("Bestandslocatie");

            foreach (var fileInfo in filesResultList)
            {
                try
                { 
                    XDocument metadata = XDocument.Load(fileInfo.FullName);

                    string result = Transform(flattenXsl.ToString(), metadata.ToString(), flattenOnlyEmptyElements);
                    string[] content = result.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    var singleContentData = new Dictionary<String, String>();

                    foreach (String line in content)
                    {
                        var splitCount = line.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitCount.Count() == 2)
                        {
                            var key = splitCount[0];
                            var val = String.IsNullOrEmpty(splitCount[1]) ? splitCount[1] : HttpUtility.HtmlDecode(splitCount[1]);

                            totalContentHeader.Add(key);

                            if (singleContentData.ContainsKey(key))
                            {
                                var newVal = String.Concat(singleContentData[key], Environment.NewLine, val);
                                singleContentData[key] = newVal;
                            }
                            else
                            {
                                singleContentData.Add(key, val);
                            }
                        }
                    }
                    totalContentData.Add(fileInfo.FullName, singleContentData);
                }
                catch (Exception e)
                {
                    this._validationResultList.Add(new ProcessResultItem { Error = e, Metadata = fileInfo, Type = ProcessType.XslXmlTransformation });
                    
                }
            }

            if (_validationResultList.Count > 0 && !IgnoreErrorValidationErrors) 
                throw new ApplicationException("Error(s) found while transforming metadata files with stylesheet!");            

            return new ResultPair { Name = String.Concat(IsToPX ? "topx" : IsMDTO ? "mdto" : "onbekend", flattenOnlyEmptyElements ? " - NULL" : ""), TotalContentData = totalContentData, TotalContentHeader = totalContentHeader };
        }

        /// <summary>
        /// Creates the excel.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <exception cref="System.Exception">Re-throw exception in CreateExcel.</exception>
        private void CreateExcel(List<ResultPair> data)
        {
            try
            {
                var excelOutput = ExportExcelIndexMetadataHandler.ExportToExcel(data, _validationResultList);
                FileInfo excelFile = new FileInfo(excelOutput);
                excelFile.MoveTo(Path.Combine(TargetFolder, "IndexMetadataHandler.xlsx"), true);
            }
            catch (Exception e)
            {
                throw new Exception("Re-throw exception in CreateExcel.", e);
            }
            finally {  }
        }

        /// <summary>
        /// Continues the extra collection items.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="rootName">Name of the root.</param>
        /// <returns></returns>
        /// <exception cref="System.ApplicationException">
        /// Stylesheet flatten.xsl not found in embedded resource.
        /// or
        /// Error(s) found while transforming extra xml files with styelsheet!
        /// </exception>
        private ResultPair ContinueExtraCollectionItems(string folder, string rootName)
        {
            DirectoryInfo targetDirectory = new DirectoryInfo(folder);
            string filter = "*.xml";
            var filesResultList = targetDirectory.GetFiles(filter, SearchOption.AllDirectories);

            CountMetadata = filesResultList.Where(item => !item.Name.EndsWith(".mdto.xml")).Count();

            if (CountMetadata == 0)
                return new ResultPair();

            var styleList = Stylesheet.StylesheetHandler.GetStylesheetList();
            string stylesheet = styleList[StyleName];

            if (String.IsNullOrEmpty(stylesheet))
                throw new ApplicationException("Stylesheet flatten.xsl not found in embedded resource.");
            XDocument flattenXsl = XDocument.Parse(stylesheet);

            var totalContentData = new Dictionary<String, Dictionary<String, String>>();
            var totalContentHeader = new HashSet<String>();
            totalContentHeader.Add("Bestandslocatie");

            foreach (var fileInfo in filesResultList)
            {
                try
                {
                    var xml = XDocument.Load(fileInfo.FullName);

                    if (xml.Root.Name.LocalName != rootName)
                        continue;

                    string result = Transform(flattenXsl.ToString(), xml.ToString());
                    string[] content = result.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    var singleContentData = new Dictionary<String, String>();

                    foreach (String line in content)
                    {
                        var splitCount = line.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitCount.Count() == 2)
                        {
                            var key = splitCount[0];
                            var val = String.IsNullOrEmpty(splitCount[1]) ? splitCount[1] : HttpUtility.HtmlDecode(splitCount[1]);

                            totalContentHeader.Add(key);

                            if (singleContentData.ContainsKey(key))
                            {
                                var newVal = String.Concat(singleContentData[key], Environment.NewLine, val);
                                singleContentData[key] = newVal;
                            }
                            else
                            {
                                singleContentData.Add(key, val);
                            }
                        }
                    }
                    totalContentData.Add(fileInfo.FullName, singleContentData);
                }
                catch (Exception e)
                {
                    this._validationResultList.Add(new ProcessResultItem { Error = e, Metadata = fileInfo, Type = ProcessType.XslXmlTransformation });                    
                }
                finally { }
            }

            if (_validationResultList.Count > 0 && !IgnoreErrorValidationErrors)
                throw new ApplicationException("Error(s) found while transforming extra xml files with styelsheet!");

            return new ResultPair { Name = rootName, TotalContentHeader = totalContentHeader, TotalContentData = totalContentData };
        }

        /// <summary>
        /// Transforms the specified XSL.
        /// </summary>
        /// <param name="xsl">The XSL.</param>
        /// <param name="xml">The XML.</param>
        /// <returns></returns>
        private String Transform(string xsl, string xml, bool flattenEmptyElements = false)
        {
            string output = String.Empty;

            using (StringReader srt = new StringReader(xsl)) // xslInput is a string that contains xsl
            using (StringReader sri = new StringReader(xml)) // xmlInput is a string that contains xml
            {
                using (XmlReader xrt = XmlReader.Create(srt))
                using (XmlReader xri = XmlReader.Create(sri))
                {
                    XsltArgumentList argsList = new XsltArgumentList();
                    argsList.AddParam("paramOutputType", "", flattenEmptyElements ? "empty" : "nonempty");

                    XslCompiledTransform xslt = new XslCompiledTransform();
                    xslt.Load(xrt);
                    using (UTF8StringWriter sw = new UTF8StringWriter())
                    using (XmlWriter xwo = XmlWriter.Create(sw, xslt.OutputSettings)) // use OutputSettings of xsl, so it can be output as HTML
                    {
                        xslt.Transform(xri, argsList, xwo);
                        output = sw.ToString();
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;

            try
            {
                if (File.Exists(CurrentXsdFileLocation))
                    File.Delete(CurrentXsdFileLocation);

                _validationResultList.Clear();
            }
            catch { }
        }

        /// <summary>
        /// Specific UTF-8 StringWriter
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

        /// <summary>
        /// Process options
        /// </summary>
        public enum ProcessType
        {
            SchemaValidation,
            XslXmlTransformation,
            XmlOthers
        }

        /// <summary>
        /// Entity for holding process data
        /// </summary>
        public class ProcessResultItem
        {
            /// <summary>
            /// Gets or sets the creation.
            /// </summary>
            /// <value>
            /// The creation.
            /// </value>
            private DateTime Creation { get; set; }
            /// <summary>
            /// Initializes a new instance of the <see cref="ProcessResultItem"/> class.
            /// </summary>
            public ProcessResultItem()
            {
                Creation = DateTime.Now;
            }
            /// <summary>
            /// Gets or sets the error.
            /// </summary>
            /// <value>
            /// The error.
            /// </value>
            public Exception Error { get; set; }
            /// <summary>
            /// Gets or sets the metadata.
            /// </summary>
            /// <value>
            /// The metadata.
            /// </value>
            public FileInfo Metadata { get; set; }
            /// <summary>
            /// Gets or sets the type.
            /// </summary>
            /// <value>
            /// The type.
            /// </value>
            public ProcessType Type { get; set; }

            public override string ToString()
            {
                return String.Concat(Creation.ToString("yyyy-MM-ddTHH:mm:ss"), "\t", Type, "\t", Metadata == null ? "Geen bestand(en) gevonden." : Metadata.FullName, Environment.NewLine, "\t\t\t\t\t", Error.Message);
            }
        }

        /// <summary>
        /// Holds data, ready for conversion to Excel data
        /// </summary>
        public class ResultPair
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>
            /// The name.
            /// </value>
            public String Name { get; set; }
            /// <summary>
            /// Gets or sets the total content data.
            /// </summary>
            /// <value>
            /// The total content data.
            /// </value>
            public Dictionary<string, Dictionary<string, string>> TotalContentData { get; set; }
            /// <summary>
            /// Gets or sets the total content header.
            /// </summary>
            /// <value>
            /// The total content header.
            /// </value>
            public HashSet<string> TotalContentHeader { get; set; }
        }
    }
}

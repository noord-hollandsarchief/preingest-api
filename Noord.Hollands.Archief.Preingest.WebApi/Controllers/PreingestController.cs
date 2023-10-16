using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;

namespace Noord.Hollands.Archief.Preingest.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PreingestController : ControllerBase
    {
        private readonly ILogger<PreingestController> _logger;
        private AppSettings _settings = null;
        private readonly IHubContext<PreingestEventHub> _eventHub;
        private readonly CollectionHandler _preingestCollection = null;

        public PreingestController(ILogger<PreingestController> logger, IOptions<AppSettings> settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _logger = logger;
            _settings = settings.Value;
            _eventHub = eventHub;
            _preingestCollection = preingestCollection;            
        }

        [HttpGet("check", Name = "ServiceHealthCheck", Order = 0)]
        public IActionResult Check()
        {
            JsonResult result = null;
            using (HealthCheckHandler handler = new HealthCheckHandler(_settings, _eventHub, _preingestCollection))
            {
                handler.Execute();

                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                DateTime buildDate =  LinkerHelper.GetLinkerTimestampUtc(assembly);

                result = new JsonResult(new
                {
                    ProductName = fvi.ProductName,
                    ProductVersion = fvi.ProductVersion,
                    BuildDateTime = DateTimeOffset.FromFileTime (buildDate.ToFileTime()),
                    Title = "Underlying services health check.",
                    CreationTimestamp = DateTimeOffset.Now,
                    ActionName = typeof(HealthCheckHandler).Name,
                    Messages = new string[] {
                        "preingest: available",
                        String.Format("clamav: {0}", handler.IsAliveClamAv ? "available" : "not available"),
                        String.Format("xslweb: {0}", handler.IsAliveXslWeb ? "available" : "not available"),
                        String.Format("droid: {0}", handler.IsAliveDroid ? "available" : "not available"),
                        String.Format("database: {0}", handler.IsAliveDatabase ? "available" : "not available")
                    }
                });
            }
            return result;
        }       

        [HttpPost("calculate/{guid}", Name = "CalculateCollectionChecksum", Order = 1)]
        public IActionResult CollectionChecksumCalculation(Guid guid, [FromBody] BodyChecksum checksum)
        {
            _logger.LogInformation("Enter CollectionChecksumCalculation.");
            if(checksum == null)
                return Problem("Post Json body is null!");

            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            if (String.IsNullOrEmpty(checksum.ChecksumType))
                return BadRequest("Missing checksum type.");          
            
            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                //data map id                   
                Task.Run(() =>
                {
                    using (ContainerChecksumHandler handler = new ContainerChecksumHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.SetSessionGuid(guid);
                        handler.Logger = _logger;
                        handler.Checksum = checksum.ChecksumType;
                        handler.DeliveredChecksumValue = checksum.InputChecksumValue;
                        processId = handler.AddProcessAction(processId, typeof(ContainerChecksumHandler).Name, String.Format("Container file {0}", handler.TarFilename), String.Concat(typeof(ContainerChecksumHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ContainerChecksumHandler).Name, handler.SessionGuid);
                        handler.Execute();                       
                    }
                });                 
            }
            catch (Exception e )
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ContainerChecksumHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ContainerChecksumHandler).Name);
            }

            _logger.LogInformation("Exit CollectionChecksumCalculation.");
            return new JsonResult(new { Message = "Container checksum calculation started.", SessionId = guid, ActionId = processId });
        }

        //Voorbereiding  
        [HttpPost("unpack/{guid}", Name = "ExpandCollectionArchive", Order = 2)]
        public IActionResult Unpack(Guid guid)
        {
            _logger.LogInformation("Enter Unpack.");

            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");
              
            //database action id
            Guid processId = Guid.NewGuid();
            try
            {   
                Task.Run(() =>
                {
                    using (UnpackTarHandler handler = new UnpackTarHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.SetSessionGuid(guid);
                        handler.Logger = _logger;

                        processId = handler.AddProcessAction(processId, typeof(UnpackTarHandler).Name, String.Format("Container file {0}", handler.TarFilename), String.Concat(typeof(UnpackTarHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(UnpackTarHandler).Name, handler.SessionGuid);  
                        handler.Execute();
                    }
                });
            }
            catch(Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(UnpackTarHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(UnpackTarHandler).Name);
            }

            _logger.LogInformation("Exit Unpack.");
            return new JsonResult(new { Message = "Expand archive started", SessionId = guid, ActionId = processId });
        }

        [HttpPost("virusscan/{guid}", Name = "RunVirusMalwareScan", Order = 3)]
        public IActionResult VirusScan(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter VirusScan.");  

            //database process id
            Guid processId = Guid.NewGuid();

            try
            {
                //data map id
                Task.Run(() =>
                {
                    using (ScanVirusValidationHandler handler = new ScanVirusValidationHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.SetSessionGuid(guid);
                        handler.Logger = _logger;  
                        processId = handler.AddProcessAction(processId, typeof(ScanVirusValidationHandler).Name, String.Format("Scan for virus on folder {0}", guid), String.Concat(typeof(ScanVirusValidationHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ScanVirusValidationHandler).Name, guid.ToString());
                        handler.Execute();
                    }       
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ScanVirusValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ScanVirusValidationHandler).Name);
            }

            _logger.LogInformation("Exit VirusScan.");
            return new JsonResult(new { Message = String.Format("Virusscan started."), SessionId = guid, ActionId = processId });
        }
                
        [HttpPost("naming/{guid}", Name = "NamingConventionValidation", Order = 4)]
        public IActionResult Naming(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Naming."); 

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {                                     
                Task.Run(() =>
                {
                    using (NamingValidationHandler handler = new NamingValidationHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.SetSessionGuid(guid);
                        handler.Logger = _logger;
                        processId = handler.AddProcessAction(processId, typeof(NamingValidationHandler).Name, String.Format("Name check on folders, sub-folders and files : folder {0}", guid), String.Concat(typeof(NamingValidationHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(NamingValidationHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(NamingValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(NamingValidationHandler).Name);
            }

            _logger.LogInformation("Exit Naming.");
            return new JsonResult(new { Message = String.Format("Folder(s) and file(s) naming check started."), SessionId = guid, ActionId = processId });
        }
       
        [HttpPost("sidecar/{guid}", Name = "SidecarValidation", Order = 5)]
        public IActionResult Sidecar(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Sidecar.");          

            //database process id
            Guid processId = Guid.NewGuid();
            try
            { 
                Task.Run(() =>
                {
                    using (SidecarValidationHandler handler = new SidecarValidationHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(SidecarValidationHandler).Name, String.Format("Sidecar structure check for aggregation and metadata : folder {0}", guid), String.Concat(typeof(SidecarValidationHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(SidecarValidationHandler).Name, guid.ToString());
                        
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(SidecarValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(SidecarValidationHandler).Name);
            }
            _logger.LogInformation("Exit Sidecar.");
            return new JsonResult(new { Message = String.Format("Structure sidecar check started."), SessionId = guid, ActionId = processId });
        }              
 
        [HttpPost("profiling/{guid}", Name = "CreateDroidProfile", Order = 6)]
        public IActionResult Profiling(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Profiling.");
                        
            string actionId = string.Empty;
            try
            {
                using (DroidValidationHandler handler = new DroidValidationHandler(_settings, _eventHub, _preingestCollection))
                {
                    handler.Logger = _logger;
                    handler.SetSessionGuid(guid);
                    _logger.LogInformation("Execute handler ({0}).", typeof(DroidValidationHandler).Name);
                    var result = handler.GetProfiles().Result;
                    _logger.LogInformation("Profiling is completed.");
                    actionId = (result != null) ? result.ActionId : string.Empty;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(DroidValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(DroidValidationHandler).Name);
            }

            _logger.LogInformation("Exit Profiling.");

            return new JsonResult(new { Message = String.Format("Droid profiling is started."), SessionId = guid, ActionId = actionId });
        }

        [HttpPost("exporting/{guid}", Name = "ExportingDroidResultCSV", Order = 7)]
        public IActionResult Exporting(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Exporting.");

            string actionId = string.Empty;
            try
            {
                using (DroidValidationHandler handler = new DroidValidationHandler(_settings, _eventHub, _preingestCollection))
                {
                    handler.Logger = _logger;
                    handler.SetSessionGuid(guid);
                    _logger.LogInformation("Execute handler ({0}).", typeof(DroidValidationHandler).Name);
                    var result = handler.GetExporting().Result;
                    _logger.LogInformation("Exporting is completed.");
                    actionId = (result != null) ? result.ActionId : string.Empty;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(DroidValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(DroidValidationHandler).Name);
            }

            _logger.LogInformation("Exit Exporting.");

            return new JsonResult(new { Message = String.Format("Droid exporting (CSV) is started."), SessionId = guid, ActionId = actionId });
        }       

        [HttpPost("reporting/{type}/{guid}", Name = "DroidReportingPDFDroidXMLPlanetsXML", Order = 8)]
        public IActionResult Reporting(Guid guid, String type)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Reporting.");

            DroidValidationHandler.ReportingStyle style = DroidValidationHandler.ReportingStyle.Pdf;
            switch (type)
            {
                case "pdf":
                    style = DroidValidationHandler.ReportingStyle.Pdf;
                    break;
                case "droid":
                    style = DroidValidationHandler.ReportingStyle.Droid;
                    break;
                case "planets":
                    style = DroidValidationHandler.ReportingStyle.Planets;
                    break;
                default:
                    return NotFound(String.Format ("Unknown report type {0}", type));
            }
   
            string actionId = string.Empty;
            try
            {
                using (DroidValidationHandler handler = new DroidValidationHandler(_settings, _eventHub, _preingestCollection))
                {
                    handler.Logger = _logger;
                    handler.SetSessionGuid(guid);
                    _logger.LogInformation("Execute handler ({0}).", typeof(DroidValidationHandler).Name);
                    var result = handler.GetReporting(style).Result;
                    _logger.LogInformation("Reporting is completed.");
                    actionId = (result != null) ? result.ActionId : string.Empty;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(DroidValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(DroidValidationHandler).Name);
            }

            _logger.LogInformation("Exit Reporting.");

            return new JsonResult(new { Message = String.Format("Droid reporting ({0}) is started.", style), SessionId = guid, ActionId = actionId });
        }

        [HttpPut("signature/update", Name = "DroidSignatureUpdate", Order = 9)]
        public IActionResult SignatureUpdate()
        {
            _logger.LogInformation("Enter SignatureUpdate.");
                  
            try
            {
                using (DroidValidationHandler handler = new DroidValidationHandler(_settings, _eventHub, _preingestCollection))
                {
                    handler.Logger = _logger;  

                    _logger.LogInformation("Execute handler ({0}).", typeof(DroidValidationHandler).Name);
                    var result = handler.SetSignatureUpdate().Result; 
                    _logger.LogInformation("SignatureUpdate is completed.");
                }
                _logger.LogInformation("Execute handler ({0}).", typeof(DroidValidationHandler).Name);
                         
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(DroidValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(DroidValidationHandler).Name);
            }

            _logger.LogInformation("Exit SignatureUpdate.");

            return Ok();
        }

        [HttpPost("greenlist/{guid}", Name = "PreferenceGreenListComparisonCheck", Order = 10)]
        public IActionResult Greenlist(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter GreenListCheck.");                     
     
            //database process id
            Guid processId = Guid.NewGuid();
            try
            {              
                Task.Run(() =>
                {
                    using (GreenListHandler handler = new GreenListHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(GreenListHandler).Name, String.Format("Compare CSV result with greenlist"), String.Concat(typeof(GreenListHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(GreenListHandler).Name, guid.ToString());
                        handler.Execute();
                    }                       
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(GreenListHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(GreenListHandler).Name);
            }
            _logger.LogInformation("Exit GreenListCheck.");
            return new JsonResult(new { Message = String.Format("Greenlist check is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("encoding/{guid}", Name = "EncodingMetadataFilesCheck", Order = 11)]
        public IActionResult Encoding(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter EncodingCheck.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                //data map id               
                Task.Run(() =>
                {
                    using (EncodingHandler handler = new EncodingHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(EncodingHandler).Name, String.Format("Retrieve the encoding for all metadata files : folder {0}", guid), String.Concat(typeof(EncodingHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(EncodingHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(EncodingHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(EncodingHandler).Name);
            }
            _logger.LogInformation("Exit EncodingCheck.");
            return new JsonResult(new { Message = String.Format("Encoding UTF-8 .metadata files check is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("validate/{guid}", Name = "ValidateMetadataContent", Order = 12)]
        public IActionResult ValidateMetadata(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter ValidateMetadata.");           

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {                               
                Task.Run(() =>
                {
                    using (MetadataValidationHandler handler = new MetadataValidationHandler(_settings, _eventHub, _preingestCollection))
                {
                    handler.Logger = _logger;
                    handler.SetSessionGuid(guid);
                    
                    processId = handler.AddProcessAction(processId, typeof(MetadataValidationHandler).Name, String.Format("Validate all metadata files with XSD schema and schema+ : folder {0}", guid), String.Concat(typeof(MetadataValidationHandler).Name, ".json"));
                    _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(MetadataValidationHandler).Name, guid.ToString());
                    handler.Execute();
                } 
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(MetadataValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(MetadataValidationHandler).Name);
            }
            _logger.LogInformation("Exit ValidateMetadata.");
            return new JsonResult(new { Message = String.Format("Validate metadata files is started."), SessionId = guid, ActionId = processId });
        } 

        [HttpPost("excelcreator/{guid}", Name = "GenerateExcelReport", Order = 13)]
        public IActionResult CreateExcel(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter CreateExcel.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (ExcelCreatorHandler handler = new ExcelCreatorHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(ExcelCreatorHandler).Name, String.Format("Create Excel from folder {0}", guid), String.Concat(String.Concat(typeof(ExcelCreatorHandler).Name, ".xlsx"), ";", String.Concat(typeof(ExcelCreatorHandler).Name, ".json")));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ExcelCreatorHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ExcelCreatorHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ExcelCreatorHandler).Name);
            }

            _logger.LogInformation("Exit CreateExcel.");
            return new JsonResult(new { Message = String.Format("Excel creator is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPut("settings/{guid}", Name = "SavePreingestSettings", Order = 14)]
        public IActionResult PutSettings(Guid guid, [FromBody] BodySettings settings)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter PutSettings.");
               
            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (SettingsHandler handler = new SettingsHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        handler.CurrentSettings = settings;
                        processId = handler.AddProcessAction(processId, typeof(SettingsHandler).Name, String.Format("Save user input setting(s) for folder {0}", guid), String.Concat(typeof(SettingsHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(SettingsHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(SettingsHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(SettingsHandler).Name);
            }

            _logger.LogInformation("Exit PutSettings.");
            return new JsonResult(new { Message = String.Format("Settings are stored."), SessionId = guid, ActionId = processId });
        }
 
        [HttpPost("prewash/{guid}", Name = "PrewashMetadataFiles", Order = 15)]
        public IActionResult PreWashMetadata(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter PreWashMetadata.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (PrewashHandler handler = new PrewashHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(PrewashHandler).Name, String.Format("Prewash metadata files: folder {0}", guid), String.Concat(typeof(PrewashHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(PrewashHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(PrewashHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(PrewashHandler).Name);
            }
            _logger.LogInformation("Exit PreWashMetadata.");
            return new JsonResult(new { Message = String.Format("Prewash started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("indexing/{guid}", Name = "IndexMetadataFilesToExcel", Order = 16)]
        public IActionResult IndexMetadataFiles(Guid guid, [FromBody] String[] extraXmlRootNodeNames)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter IndexMetadataFiles.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (IndexMetadataHandler handler = new IndexMetadataHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        handler.RootNames = extraXmlRootNodeNames;
                        processId = handler.AddProcessAction(processId, typeof(IndexMetadataHandler).Name, String.Format("Indexing metadata files in folder {0}", guid), String.Concat(String.Concat(typeof(IndexMetadataHandler).Name, ".xlsx"), ";", String.Concat(typeof(IndexMetadataHandler).Name, ".json")));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(IndexMetadataHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(IndexMetadataHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(IndexMetadataHandler).Name);
            }

            _logger.LogInformation("Exit IndexMetadataFiles.");
            return new JsonResult(new { Message = String.Format("Indexing metadata files is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("detection/{guid}", Name = "DetectFilesWithPasswordProtection", Order = 17)]
        public IActionResult DetectPasswordProtection(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter DetectPasswordProtection.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (PasswordDetectionHandler handler = new PasswordDetectionHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(PasswordDetectionHandler).Name, String.Format("Detect files with password protection in folder {0}", guid), String.Concat(typeof(PasswordDetectionHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(PasswordDetectionHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(PasswordDetectionHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(PasswordDetectionHandler).Name);
            }

            _logger.LogInformation("Exit DetectPasswordProtection.");
            return new JsonResult(new { Message = String.Format("Detect files with password protection is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("pronom/{guid}", Name = "UpdateMetadataFilesWithPRONOM", Order = 18)]
        public IActionResult UpdateWithPronom(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter UpdateWithPronom.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (BinaryFileMetadataMutationHandler handler = new BinaryFileMetadataMutationHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(BinaryFileMetadataMutationHandler).Name, String.Format("Update metadata (bestand) files with PRONOM values in folder {0}", guid), String.Concat(typeof(BinaryFileMetadataMutationHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(BinaryFileMetadataMutationHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(BinaryFileMetadataMutationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(BinaryFileMetadataMutationHandler).Name);
            }

            _logger.LogInformation("Exit UpdateWithPronom.");
            return new JsonResult(new { Message = String.Format("Update metadata (bestand) files with PRONOM values is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("binaries/{guid}", Name = "LookingForZBFPlusPRONOMMismatch", Order = 19)]
        public IActionResult ValidateBinaries(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter ValidateBinaries.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (BinaryFileObjectValidationHandler handler = new BinaryFileObjectValidationHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(BinaryFileObjectValidationHandler).Name, String.Format("Validation for non-metadata files looking for ZBF and mismatch (PRONOM) in folder {0}", guid), String.Concat(typeof(BinaryFileObjectValidationHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(BinaryFileObjectValidationHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(BinaryFileObjectValidationHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(BinaryFileObjectValidationHandler).Name);
            }

            _logger.LogInformation("Exit ValidateBinaries.");
            return new JsonResult(new { Message = String.Format("Validation for non-metadata files looking for ZBF and mismatch (PRONOM) is started."), SessionId = guid, ActionId = processId });
        }

    }
}

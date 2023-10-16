using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView;

namespace Noord.Hollands.Archief.Preingest.WebApi.Controllers
{
    /// <summary>
    /// API controller for retrieving preingest information
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.ControllerBase" />
    [Route("api/[controller]")]
    [ApiController]
    public class OutputController : ControllerBase
    {
        private readonly ILogger<OutputController> _logger;
        private readonly AppSettings _settings = null;
        private readonly CollectionHandler _preingestHandler = null;
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="preingestHandler">The preingest handler.</param>
        public OutputController(ILogger<OutputController> logger, IOptions<AppSettings> settings, CollectionHandler preingestHandler)
        {
            _logger = logger;
            _settings = settings.Value;
            _preingestHandler = preingestHandler;
        }

        /// <summary>
        /// Gets the collections.
        /// </summary>
        /// <returns></returns>
        [HttpGet("collections", Name = "GetListCollections", Order = 0)]
        public IActionResult GetCollections()
        {
            dynamic dataResults = null;            
            try
            {
                dataResults = _preingestHandler.GetCollections();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
            }
            finally
            {
                _logger.LogInformation("Exit UpdateProcessAction.");
            }
            
            if (dataResults == null)
                return NotFound("Not collections data found!");
            
            return new JsonResult(dataResults);  
        }

        /// <summary>
        /// Gets the collection.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        [HttpGet("collection/{guid}", Name = "GetSingleCollection", Order = 1)]
        public IActionResult GetCollection(Guid guid)
        {
            var directory = new DirectoryInfo(_settings.DataFolderName);

            if (!directory.Exists)
                return Problem(String.Format("Data folder '{0}' not found!", _settings.DataFolderName));

            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            dynamic dataResults = null;

            try
            {
                dataResults = _preingestHandler.GetCollection(guid);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace);
                return ValidationProblem(String.Format("An exception was thrown : {0}, '{1}'.", e.Message, e.StackTrace));
            }
            finally
            {
                _logger.LogInformation("Exit UpdateProcessAction.");
            }

            if (dataResults == null)
                return NotFound(String.Format("Not data found for collection '{0}'!", guid));

            return new JsonResult(dataResults);
        }

        /// <summary>
        /// Gets the json.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <param name="json">The json.</param>
        /// <returns></returns>
        [HttpGet("json/{guid}/{json}", Name = "GetJsonResults", Order = 2)]
        public IActionResult GetJson(Guid guid, string json)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            if (String.IsNullOrEmpty(json))
                return Problem("Json file name is empty.");

            var directory = new DirectoryInfo(Path.Combine(_settings.DataFolderName, guid.ToString()));

            if (!directory.Exists)
                return Problem(String.Format("Data folder with session guid '{0}' not found!", directory.FullName));

            var fileinfo = directory.GetFiles(json);
            if (fileinfo.Length == 0)
                return Problem(String.Format("File in session guid '{0}' not found!", json));

            string content = System.IO.File.ReadAllText(fileinfo.First().FullName);
            
            var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")                
            };
            return new RandomJsonResponseMessageResult(result);           
        }

        /// <summary>
        /// Gets the report.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        [HttpGet("report/{guid}/{file}", Name = "GetReportByFilename", Order = 3)]
        public IActionResult GetReport(Guid guid, string file)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            if (string.IsNullOrEmpty(file))
                return Problem("File name is empty.");

            var directory = new DirectoryInfo(Path.Combine(_settings.DataFolderName, guid.ToString()));
            if (!directory.Exists)
                return Problem(String.Format("Data folder with session guid '{0}' not found!", directory.FullName));

            var fileList = directory.GetFiles(file);
            if (fileList.Length == 0)
                return Problem(String.Format("File in session guid '{0}' not found!", file));

            var fileinfo = fileList.First();

            string contentType = String.Empty;

            switch (fileinfo.Extension)
            {
                case ".pdf":
                    contentType = "application/pdf";
                    break;
                case ".xml":
                    contentType = "text/xml";
                    break;
                case ".csv":
                    contentType = "text/csv";
                    break;
                case ".json":
                    contentType = "application/json";
                    break;
                default:
                    contentType = "application/octet-stream";
                    break;
            }

            return new PhysicalFileResult(fileinfo.FullName, contentType);
        }

        /// <summary>
        /// Gets the stylesheet list.
        /// </summary>
        /// <returns></returns>
        [HttpGet("stylesheets", Name = "GetListOfFilesFromPrewash", Order = 4)]
        public IActionResult GetStylesheetList()
        {
            if (String.IsNullOrEmpty(_settings.PreWashFolder))
                return Problem("Prewash folder is not set in appsettings.");

            if (!Directory.Exists(_settings.PreWashFolder))
                return Problem(String.Format("Prewash folder not found (folder '{0}')!", _settings.PreWashFolder));

            var prewashFiles = new DirectoryInfo(_settings.PreWashFolder).GetFiles("*.xsl*").Select(item => new
            {
                Filename = item.Name,
                Name = item.Name.Remove(item.Name.Length - item.Extension.Length, item.Extension.Length)
            }).ToArray();

            return new JsonResult(prewashFiles);
        }

        /// <summary>
        /// Gets the schema list.
        /// </summary>
        /// <returns></returns>
        [HttpGet("schemas", Name = "GetListOfSchemas", Order = 5)]
        public IActionResult GetSchemaList()
        {
            var dictionary = Schema.SchemaHandler.GetSchemaList();
            return new JsonResult(dictionary.Keys.Select(item => new
            {
                Filename = item.Remove(0, "Noord.Hollands.Archief.Preingest.WebApi.Schema.".Length),
                Name = item.Remove(0, "Noord.Hollands.Archief.Preingest.WebApi.Schema.".Length).Replace(".xsd", string.Empty)
            }).OrderBy(item => item.Name).ToArray());
        }

        [HttpGet("view_structure/{guid}", Name = "GetCollectionStructure", Order = 6)]
        public async System.Threading.Tasks.Task<IActionResult> GetCollectionStructure(Guid guid)
        {
            var directory = new DirectoryInfo(Path.Combine(_settings.DataFolderName, guid.ToString()));

            if (!directory.Exists)
                return Problem(String.Format("Data folder with session guid '{0}' not found!", directory.FullName));

            DataTreeHandler.TreeRoot rootModel = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                using (DataTreeHandler handler = new DataTreeHandler(guid, directory))
                {
                    handler.Load();
                    rootModel = handler.DataRoot;
                }
            });

            return new JsonResult(rootModel);
        }

        [HttpGet("item_content/{base64EncodedValue}", Name = "GetCollectionItemContent", Order = 7)]
        public async System.Threading.Tasks.Task<IActionResult> GetCollectionItem(String base64EncodedValue)
        {
            if (String.IsNullOrEmpty(base64EncodedValue))
                return Problem(String.Format("Cannot process empty value! Expected base64 encoded value."));

            string pathItem = DataTreeHandler.Base64Decode(base64EncodedValue);

            if (!System.IO.File.Exists(pathItem))
                return Problem(String.Format("Cannot find metadata file object! Expected filename '{0}'.", pathItem));

            DataTreeHandler.ItemContent rootModel = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                rootModel = DataTreeHandler.GetItemContent(pathItem);
            });
            return new JsonResult(rootModel);
        }

        [HttpGet("item_properties/{base64EncodedValue}", Name = "GetSidecarItemProperties", Order = 6)]
        public async System.Threading.Tasks.Task<IActionResult> GetCollectionItemProps(String base64EncodedValue)
        {
            if (String.IsNullOrEmpty(base64EncodedValue))
                return Problem(String.Format("Cannot process empty value! Expected base64 encoded value."));

            string pathItem = DataTreeHandler.Base64Decode(base64EncodedValue);

            if (!System.IO.File.Exists(pathItem))
                return Problem(String.Format("Cannot find metadata file object! Expected filename '{0}'.", pathItem));            

            DataTreeHandler.ItemContent rootModel = null;
            await System.Threading.Tasks.Task.Run(() =>
                        {                            
                            rootModel = DataTreeHandler.GetItemProperties(pathItem, _settings.DataFolderName);
                        });
            return new JsonResult(rootModel);
        }
    }
}

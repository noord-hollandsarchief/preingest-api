using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX;

using System;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Controllers
{
    /// <summary>
    /// API Controller class for OPEX
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.ControllerBase" />
    [Route("api/[controller]")]
    [ApiController]
    public class OpexController : ControllerBase
    {
        private readonly ILogger<OpexController> _logger;
        private readonly AppSettings _settings = null;
        private readonly CollectionHandler _preingestCollection = null;
        private readonly IHubContext<PreingestEventHub> _eventHub;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpexController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public OpexController(ILogger<OpexController> logger, IOptions<AppSettings> settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _logger = logger;
            _settings = settings.Value;
            _preingestCollection = preingestCollection;
            _eventHub = eventHub;
        }

        /// <summary>
        /// Builds the OPEX structure.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <param name="setting">The setting.</param>
        /// <returns></returns>
        [HttpPut("buildopex/{guid}", Name = "BuildOpexForIngest", Order = 1)]
        public IActionResult BuildOpex(Guid guid, [FromBody] Inheritance setting)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter BuildOpex.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (BuildOpexHandler handler = new BuildOpexHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        handler.InheritanceSetting = setting;
                        processId = handler.AddProcessAction(processId, typeof(BuildOpexHandler).Name, String.Format("Build Opex with collection ID: {0}", guid), String.Concat(typeof(BuildOpexHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(BuildOpexHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(BuildOpexHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(BuildOpexHandler).Name);
            }
            _logger.LogInformation("Exit BuildOpex.");
            return new JsonResult(new { Message = String.Format("Build Opex started."), SessionId = guid, ActionId = processId });
        }

        /// <summary>
        /// Shows the bucket.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        [HttpGet("showbucket/{guid}", Name = "ShowBucketContent", Order = 2)]
        public IActionResult ShowBucket(Guid guid)
        {
            _logger.LogInformation("Enter ShowBucket.");

            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (ShowBucketHandler handler = new ShowBucketHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(ShowBucketHandler).Name, String.Format("Show the bucket.", guid),
                            String.Concat(typeof(ShowBucketHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ShowBucketHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ClearBucketHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ClearBucketHandler).Name);
            }

            _logger.LogInformation("Exit ShowBucket.");
            return new JsonResult(new { Message = String.Format("Showing the bucket is initiated."), SessionId = guid, ActionId = processId });
        }

        /// <summary>
        /// Clears the bucket.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        [HttpDelete("clearbucket/{guid}", Name = "ClearBucket", Order = 3)]
        public IActionResult ClearBucket(Guid guid)
        {
            _logger.LogInformation("Enter ClearBucket.");

            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (ClearBucketHandler handler = new ClearBucketHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(ClearBucketHandler).Name, String.Format("Clear the bucket.", guid),
                            String.Concat(typeof(ClearBucketHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ClearBucketHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ClearBucketHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ClearBucketHandler).Name);
            }

            _logger.LogInformation("Exit ClearBucket.");
            return new JsonResult(new { Message = String.Format("Clearing the bucket is initiated."), SessionId = guid, ActionId = processId });
        }

        /// <summary>
        /// Upload to the bucket.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        [HttpPost("upload2bucket/{guid}", Name = "Upload2Bucket", Order = 4)]
        public IActionResult Upload2Bucket(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Upload2Bucket.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (UploadBucketHandler handler = new UploadBucketHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(UploadBucketHandler).Name, String.Format("Upload to the bucket.", guid), String.Concat(typeof(UploadBucketHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(UploadBucketHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(UploadBucketHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(UploadBucketHandler).Name);
            }

            _logger.LogInformation("Exit Upload2Bucket.");
            return new JsonResult(new { Message = String.Format("Upload is initiated."), SessionId = guid, ActionId = processId });
        }

        /// <summary>
        /// Runs the fixity checks.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <param name="hashType">Type of the hash.</param>
        /// <returns></returns>
        [HttpPost("checksum/{guid}", Name = "RunChecksumWithEveryFiles", Order = 5)]
        public IActionResult RunChecksum(Guid guid, [FromBody] Algorithm hashType)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter RunChecksum.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (FilesChecksumHandler handler = new FilesChecksumHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        handler.HashType = hashType;
                        processId = handler.AddProcessAction(processId, typeof(FilesChecksumHandler).Name, String.Format("Polish Opex with collection ID: {0}", guid), String.Concat(typeof(FilesChecksumHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(FilesChecksumHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(FilesChecksumHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(FilesChecksumHandler).Name);
            }
            _logger.LogInformation("Exit RunChecksum.");

            return new JsonResult(new { Message = String.Format("Checksum run started."), SessionId = guid, ActionId = processId });
        }

        /// <summary>
        /// Polishes the OPEX files in a OPEX folder structure.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns></returns>
        [HttpPost("polish/{guid}", Name = "PolishOpexFiles", Order = 6)]
        public IActionResult Polish(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Polish.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (PolishHandler handler = new PolishHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(PolishHandler).Name, String.Format("Polish Opex with collection ID: {0}", guid), String.Concat(typeof(PolishHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(PolishHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(FilesChecksumHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(FilesChecksumHandler).Name);
            }
            _logger.LogInformation("Exit Polish.");

            return new JsonResult(new { Message = String.Format("Polish run started."), SessionId = guid, ActionId = processId });
        }

        [HttpPut("buildnonmetadataopex/{guid}", Name = "BuildOpexNonMetadata", Order = 7)]
        public IActionResult BuildOpexNonMetadata(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter BuildOpex.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (BuildNonMetadataOpexHandler handler = new BuildNonMetadataOpexHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(BuildNonMetadataOpexHandler).Name, String.Format("Build Opex with collection ID: {0}", guid), String.Concat(typeof(BuildNonMetadataOpexHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(BuildNonMetadataOpexHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(BuildOpexHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(BuildOpexHandler).Name);
            }
            _logger.LogInformation("Exit BuildOpexNonMetadata.");
            return new JsonResult(new { Message = String.Format("Build Opex started."), SessionId = guid, ActionId = processId });
        }

        [HttpPut("revert/{guid}", Name = "RevertCollection", Order = 8)]
        public IActionResult Revert(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter Revert.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (RevertCollectionHandler handler = new RevertCollectionHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(RevertCollectionHandler).Name, String.Format("Reverting collection ID: {0}", guid), String.Concat(typeof(RevertCollectionHandler).Name, ".json"));
                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(RevertCollectionHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(RevertCollectionHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(RevertCollectionHandler).Name);
            }
            _logger.LogInformation("Exit Revert.");
            return new JsonResult(new { Message = String.Format("Reverting collection started."), SessionId = guid, ActionId = processId });
        }
    }
}

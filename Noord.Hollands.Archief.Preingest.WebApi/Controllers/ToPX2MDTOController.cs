using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.ToPX2MDTO;
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
    public class ToPX2MDTOController : ControllerBase
    {
        private readonly ILogger<OpexController> _logger;
        private readonly AppSettings _settings = null;
        private readonly CollectionHandler _preingestCollection = null;
        private readonly IHubContext<PreingestEventHub> _eventHub;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToPX2MDTOController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public ToPX2MDTOController(ILogger<OpexController> logger, IOptions<AppSettings> settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
        {
            _logger = logger;
            _settings = settings.Value;
            _preingestCollection = preingestCollection;
            _eventHub = eventHub;
        }

        [HttpPost("start_conversion/{guid}", Name = "StartConvertingToPX2MDTO", Order = 1)]
        public IActionResult Convert(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter ToPXToMDTO.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (ToPX2MDTOHandler handler = new ToPX2MDTOHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(ToPX2MDTOHandler).Name, String.Format("Converting ToPX to MDTO in folder {0}", guid), String.Concat(typeof(ToPX2MDTOHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(ToPX2MDTOHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(ToPX2MDTOHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(ToPX2MDTOHandler).Name);
            }

            _logger.LogInformation("Exit ToPXToMDTO.");
            return new JsonResult(new { Message = String.Format("Converting ToPX to MDTO is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("update_fileformat/{guid}", Name = "UpdateFileFormatUsingPRONOMForMDTO", Order = 2)]
        public IActionResult UpdatePronom(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter UpdatePronom.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (PronomPropsHandler handler = new PronomPropsHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(PronomPropsHandler).Name, String.Format("Update PRONOM in MDTO files (bestandType) with folder {0}", guid), String.Concat(typeof(PronomPropsHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(PronomPropsHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(PronomPropsHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(PronomPropsHandler).Name);
            }

            _logger.LogInformation("Exit UpdatePronom.");
            return new JsonResult(new { Message = String.Format("Update PRONOM in MDTO files (bestandType) is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("update_fixity/{guid}", Name = "UpdateFileFixityForMDTO", Order = 3)]
        public IActionResult UpdateFixity(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter UpdateFixity.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (FixityPropsHandler handler = new FixityPropsHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(FixityPropsHandler).Name, String.Format("Update fixity in MDTO (bestandsType, SHA-256) with folder {0}", guid), String.Concat(typeof(FixityPropsHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(FixityPropsHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(FixityPropsHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(FixityPropsHandler).Name);
            }

            _logger.LogInformation("Exit UpdateFixity.");
            return new JsonResult(new { Message = String.Format("Update fixity in MDTO (bestandsType, SHA-256) is started."), SessionId = guid, ActionId = processId });
        }

        [HttpPost("update_relationship/{guid}", Name = "UpdateRelationshipReferencesForMDTO", Order = 4)]
        public IActionResult UpdateRelationshipReferences(Guid guid)
        {
            if (guid == Guid.Empty)
                return Problem("Empty GUID is invalid.");

            _logger.LogInformation("Enter UpdateRelationshipReferences.");

            //database process id
            Guid processId = Guid.NewGuid();
            try
            {
                Task.Run(() =>
                {
                    using (RelationshipHandler handler = new RelationshipHandler(_settings, _eventHub, _preingestCollection))
                    {
                        handler.Logger = _logger;
                        handler.SetSessionGuid(guid);
                        processId = handler.AddProcessAction(processId, typeof(RelationshipHandler).Name, String.Format("Update the references in MDTO with folder {0}", guid), String.Concat(typeof(RelationshipHandler).Name, ".json"));

                        _logger.LogInformation("Execute handler ({0}) with GUID {1}.", typeof(RelationshipHandler).Name, guid.ToString());
                        handler.Execute();
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An exception was thrown in {0}: '{1}'.", typeof(RelationshipHandler).Name, e.Message);
                return ValidationProblem(e.Message, typeof(RelationshipHandler).Name);
            }

            _logger.LogInformation("Exit UpdateRelationshipReferences.");
            return new JsonResult(new { Message = String.Format("Update the references in MDTO is started."), SessionId = guid, ActionId = processId });
        }

    }
}

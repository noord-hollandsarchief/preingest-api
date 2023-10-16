using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler to check the encoding of every metadata files
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class EncodingHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncodingHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public EncodingHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override void Execute()
        {
            var anyMessages = new List<String>();
            bool isSucces = false;
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);

            OnTrigger(new PreingestEventArgs { Description = "Start encoding check on all metadata files.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            try
            {
                base.Execute();

                var data = new List<EncodingItem>();

                string[] metadatas = this.IsToPX ? Directory.GetFiles(TargetFolder, "*.metadata", SearchOption.AllDirectories) : Directory.GetFiles(TargetFolder, "*.xml", SearchOption.AllDirectories).Where(item => item.EndsWith(".mdto.xml")).ToArray();
                eventModel.Summary.Processed = metadatas.Count();

                Encoding bom = null;
                Encoding stream = null;
                String xml = string.Empty;
                bool isUtf8Bom = false, isUtf8Stream = false, isUtf8Xml = false;

                foreach (string file in metadatas)
                {
                    EncodingItem encodingResultItem = new EncodingItem { MetadataFile = file };
                    Logger.LogInformation("Get encoding from file : '{0}'", file);
                    try
                    {
                        bom = EncodingHelper.GetEncodingByBom(file);
                        stream = EncodingHelper.GetEncodingByStream(file);
                        xml = EncodingHelper.GetXmlEncoding(File.ReadAllText(file));

                        isUtf8Bom = bom == null ? false : (bom.EncodingName.ToUpperInvariant().Contains("UTF-8") || bom.EncodingName.ToUpperInvariant().Contains("UTF8"));
                        isUtf8Stream = stream == null ? false : (stream.EncodingName.ToUpperInvariant().Contains("UTF-8") || stream.EncodingName.ToUpperInvariant().Contains("UTF8"));
                        isUtf8Xml = xml == null ? false : (xml.ToUpperInvariant().Equals("UTF-8") || xml.ToUpperInvariant().Equals("UTF8"));

                        encodingResultItem.IsUtf8 = (isUtf8Bom || isUtf8Stream || isUtf8Xml);   
                        encodingResultItem.Description = String.Format("Byte Order Mark : {0}, Stream : {1}, XML : {2}", (bom != null) ? bom.EncodingName : "Byte Order Mark niet gevonden", (stream != null) ? stream.EncodingName : "In stream niet gevonden", String.IsNullOrEmpty(xml) ? "In XML niet gevonden" : xml);
                        
                    }
                    catch (Exception innerException)
                    {
                        encodingResultItem.IsUtf8 = false;
                        encodingResultItem.Description = String.Format("Bestand veroorzaakt een fout: {0}.", innerException.Message);
                    }
                    finally
                    {
                        data.Add(encodingResultItem);
                        OnTrigger(new PreingestEventArgs { Description = String.Format("Running encoding check on '{0}'", file), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                    }
                }        

                eventModel.Summary.Accepted = data.Where(item => item.IsUtf8).Count();
                eventModel.Summary.Rejected = data.Where(item => !item.IsUtf8).Count();

                eventModel.ActionData = data.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSucces = true;
            }
            catch (Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "An exception occured in get encoding from file!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured in get encoding from file!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Rejected = eventModel.Summary.Processed;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Processed = 0;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in get encoding from file!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Retrieving encoding on metadata files is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }
    }
}

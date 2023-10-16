using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{
    /// <summary>
    /// Handler for uploading files to a bucket.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class UploadBucketHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UploadBucketHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public UploadBucketHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
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
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Upload to bucket for folder '{0}'.", SessionGuid), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
           
            bool isSuccess = false;
            var anyMessages = new List<String>();
           
            try
            {
                Root dataResult;
                string targetFolder = System.IO.Path.Combine(this.ApplicationSettings.DataFolderName, SessionGuid.ToString(), "opex");

                if (!System.IO.Directory.Exists(targetFolder))                
                    throw new System.IO.DirectoryNotFoundException(String.Format("Opex map niet gevonden: '{0}'!", targetFolder));                

                string encodedPath = Utilities.ChecksumHelper.Base64Encode(targetFolder);
                string url = String.Format("http://{0}:{1}/bucket/upload/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.PutAsync(url, null).Result;
                    string result = response.Content.ReadAsStringAsync().Result;
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {                        
                        dataResult = JsonConvert.DeserializeObject<Root>(result);
                    }
                    else
                    {
                        throw new ApplicationException(result);
                    }                  
                }

                eventModel.ActionData = dataResult.Result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 1;
                eventModel.Summary.Rejected = 0;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                anyMessages.Clear();
                anyMessages.Add(String.Format("Call upload to bucket failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Call upload to bucket failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while uploading to bucket!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                {
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;
                    OnTrigger(new PreingestEventArgs { Description = "Upload to bucket is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
                }
            }
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        internal class Root
        {
            [JsonProperty("result")]
            public string Result { get; set; }
        }

    }
}

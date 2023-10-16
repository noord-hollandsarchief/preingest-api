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
    /// Handler to call for clearing the S3 bucket. Use seperate microservice to do the job. 
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class ClearBucketHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClearBucketHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public ClearBucketHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
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
            OnTrigger(new PreingestEventArgs { Description = String.Format("Clear the bucket.", SessionGuid), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
           
            bool isSuccess = false;
            var anyMessages = new List<String>();            
            try
            {
                Root dataResult;
                string url = String.Format("http://{0}:{1}/bucket/clear", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.DeleteAsync(url).Result;

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
                anyMessages.Add(String.Format("Call to clear the bucket failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Call to clear the bucket failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while clearing the bucket!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                {
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;
                    OnTrigger(new PreingestEventArgs { Description = "Clear the bucket is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
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

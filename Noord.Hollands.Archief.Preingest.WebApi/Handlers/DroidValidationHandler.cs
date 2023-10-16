using Newtonsoft.Json;

using Microsoft.AspNetCore.SignalR;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    //Check 5
    /// <summary>
    /// Handler for executing DROID classification functionalities. Handler use a seperate microservices to handle the logic.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class DroidValidationHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Entity for holding status information
        /// </summary>
        public class StatusResult
        {
            public String Message { get; set; }
            public Boolean Result { get; set; }
            public String ActionId { get; set; }
        }
        /// <summary>
        /// Reporting output options: PDF, DROID, Planets.
        /// </summary>
        public enum ReportingStyle
        {            
            Pdf,
            Droid,
            Planets
        }

        AppSettings _settings = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="DroidValidationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public DroidValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this._settings = settings;
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Method is not supported in this object. Use instead GetProfiles/GetReporting/GetExporting.</exception>
        public override void Execute() => throw new NotSupportedException("Method is not supported in this object. Use instead GetProfiles/GetReporting/GetExporting.");

        /// <summary>
        /// Call DROID for update the signature files.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to request data!</exception>
        /// <exception cref="System.ApplicationException">Droid signature update request failed!</exception>
        public async Task<StatusResult> SetSignatureUpdate()
        {
            StatusResult result = null;
            using (HttpClient client = new HttpClient())
            {
                string url = String.Format("http://{0}:{1}/{2}", _settings.DroidServerName, _settings.DroidServerPort, "api/droid/v6.5/signature/update");
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Failed to request data!");

                result = JsonConvert.DeserializeObject<StatusResult>(await response.Content.ReadAsStringAsync());

                if (result == null || !result.Result)
                    throw new ApplicationException("Droid signature update request failed!");
            }
            return result;
        }

        /// <summary>
        /// Start a profile before generating any results.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to request data!</exception>
        /// <exception cref="System.ApplicationException">Droid profiles request failed!</exception>
        public async Task<StatusResult> GetProfiles()
        {
            StatusResult result = null;
            using (HttpClient client = new HttpClient())
            {
                string url = String.Format("http://{0}:{1}/{2}/{3}", _settings.DroidServerName, _settings.DroidServerPort, "api/droid/v6.5/profiles/", SessionGuid);
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Failed to request data!");

                result = JsonConvert.DeserializeObject<StatusResult>(await response.Content.ReadAsStringAsync());

                if (result == null || !result.Result)
                    throw new ApplicationException("Droid profiles request failed!");
            }

            return result;
        }

        /// <summary>
        /// Exporting the DROID classification results into CSV file.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to request data!</exception>
        /// <exception cref="System.ApplicationException">Droid exporting request failed!</exception>
        public async Task<StatusResult> GetExporting()
        {
            StatusResult result = null;
            using (HttpClient client = new HttpClient())
            {
                string url = String.Format("http://{0}:{1}/{2}/{3}", _settings.DroidServerName, _settings.DroidServerPort, "api/droid/v6.5/exporting/", SessionGuid);
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Failed to request data!");

                result = JsonConvert.DeserializeObject<StatusResult>(await response.Content.ReadAsStringAsync());

                if (result == null || !result.Result)
                    throw new ApplicationException("Droid exporting request failed!");
            }

            return result;
        }

        /// <summary>
        /// Generate a PDF, DROID or Planets report.
        /// </summary>
        /// <param name="style">The style.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to request data!</exception>
        /// <exception cref="System.ApplicationException">Droid reporting request failed!</exception>
        public async Task<StatusResult> GetReporting(ReportingStyle style)
        {
            string reportType = string.Empty;
            switch (style)
            {
                case ReportingStyle.Droid:
                    reportType = "droid";
                    break;
                case ReportingStyle.Planets:
                    reportType = "planets";
                    break;
                case ReportingStyle.Pdf:
                default:
                    reportType = "pdf";
                    break;
            }

            StatusResult result = null;
            using (HttpClient client = new HttpClient())
            {
                string url = String.Format("http://{0}:{1}/{2}/{4}/{3}", _settings.DroidServerName, _settings.DroidServerPort, "api/droid/v6.5/reporting/", SessionGuid, reportType);
                var httpResponse = await client.GetAsync(url);

                if (!httpResponse.IsSuccessStatusCode)
                    throw new Exception("Failed to request data!");

                result = JsonConvert.DeserializeObject<StatusResult>(await httpResponse.Content.ReadAsStringAsync());

                if (result == null || !result.Result)
                    throw new ApplicationException("Droid reporting request failed!");
            }

            return result;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            
        }
    }
}

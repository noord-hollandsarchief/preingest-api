using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using CsvHelper;
using System.Globalization;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading;
using System.Text.Json.Serialization;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler to check specific documents (MS Office and Adobe Acrobats) for password protection.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class PasswordDetectionHandler : AbstractPreingestHandler, IDisposable
    {
        private string[] _passwordProtectedDocs = new string[] { "doc", "xls", "ppt", "docx", "xlsx", "pptx", "pdf", "docm", "dotx", "dotm", "xlsm", "xltx", "xlsb", "xlam", "pptm", "ppsx", "ppsm" };
        public PasswordDetectionHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
            : base(settings, eventHub, preingestCollection)
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
        /// <exception cref="System.IO.FileNotFoundException">CSV file not found! Run DROID first.</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start detection for files with password protection for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                base.Execute();

                string droidCsvFile = DroidCsvOutputLocation();
                if (String.IsNullOrEmpty(droidCsvFile))
                    throw new FileNotFoundException("CSV file not found! Run DROID first.", droidCsvFile);

                string sessionFolder = Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString());

                var jsonData = new List<ResultItem>();

                eventModel.Summary.Rejected = 0;
                eventModel.Summary.Accepted = 0;

                using (var reader = new StreamReader(droidCsvFile))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>().ToList();

                        var filesByDroid = this.IsToPX ? records.Where(item
                            => item.TYPE == "File" && item.EXT != "metadata").Select(item => new DataItem
                            {
                                Location = item.FILE_PATH,
                                Name = item.NAME,
                                Extension = item.EXT,
                                FormatName = item.FORMAT_NAME,
                                FormatVersion = item.FORMAT_VERSION,
                                Puid = item.PUID,
                                IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                            }).ToList() : records.Where(item
                            => item.TYPE == "File" && !item.NAME.EndsWith(".mdto.xml")).Select(item => new DataItem
                            {
                                Location = item.FILE_PATH,
                                Name = item.NAME,
                                Extension = item.EXT,
                                FormatName = item.FORMAT_NAME,
                                FormatVersion = item.FORMAT_VERSION,
                                Puid = item.PUID,
                                IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                            }).ToList();

                        var filtered = filesByDroid.Where(record => _passwordProtectedDocs.Contains(record.Extension.ToLowerInvariant())).ToList();
                        var except = filesByDroid.Where(record => !_passwordProtectedDocs.Contains(record.Extension.ToLowerInvariant())).ToList();

                        eventModel.Summary.Processed = filtered.Count;

                        foreach (DataItem binary in filtered)
                        {
                            bool isProtected = false;
                            bool containsMacros = false;
                            bool containsEmbeddedFiles = false;
                            bool containsEmbeddedLinks = false;

                            if (binary.Extension.Equals("pdf", StringComparison.InvariantCultureIgnoreCase))
                            {
                                try
                                {
                                    isProtected = PdfHelper.IsPasswordProtected(binary.Location);
                                    string encodedLocation = ChecksumHelper.Base64Encode(binary.Location);
                                    string url = String.Format("http://{0}:{1}/utilities/find_embedded_files/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedLocation);
                                    RootPdf dataResult;
                                    using (HttpClient client = new HttpClient())
                                    {
                                        client.Timeout = Timeout.InfiniteTimeSpan;
                                        HttpResponseMessage response = client.PostAsync(url, null).Result;
                                        response.EnsureSuccessStatusCode();

                                        dataResult = JsonConvert.DeserializeObject<RootPdf>(response.Content.ReadAsStringAsync().Result);
                                        containsEmbeddedFiles = dataResult.Embedded.Count > 0;                                   
                                    }
                                }
                                catch (Exception ie)
                                {
                                    anyMessages.Add(String.Format("Er heeft een fout plaatsgevonden met bestand {0}: {1}, {2}", binary.Location, ie.Message, ie.StackTrace));
                                }
                            }
                            else
                            {                                
                                try
                                {
                                    isProtected = MsOfficeHelper.IsPasswordProtected(binary.Location);
                                    string encodedLocation = ChecksumHelper.Base64Encode(binary.Location);
                                    string url = String.Format("http://{0}:{1}/utilities/scan_for_macros/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedLocation);
                                    RootOffice dataResult;
                                    using (HttpClient client = new HttpClient())
                                    {
                                        client.Timeout = Timeout.InfiniteTimeSpan;
                                        HttpResponseMessage response = client.PostAsync(url, null).Result;
                                        response.EnsureSuccessStatusCode();
                                        
                                        dataResult = JsonConvert.DeserializeObject<RootOffice>(response.Content.ReadAsStringAsync().Result);
                                        containsMacros = dataResult.Result.VbaMacros == "true" || dataResult.Result.XmlMacros == "true";
                                        containsEmbeddedFiles = dataResult.Result.EmbeddedFiles == "true";
                                        containsEmbeddedLinks = dataResult.Result.EmbeddedLinks == "true";
                                    }
                                }
                                catch (Exception ie)
                                {
                                    anyMessages.Add(String.Format("Er heeft een fout plaatsgevonden met bestand {0}: {1}, {2}", binary.Location, ie.Message, ie.StackTrace));
                                }
                            }

                            if (isProtected)
                                eventModel.Summary.Rejected = eventModel.Summary.Rejected + 1;

                            jsonData.Add(new ResultItem
                            {
                                Bestand = binary.Location,
                                IsProtected = isProtected, // ? "Wachtwoord beveiliging gevonden" : "Geen wachtwoord beveiliging kunnen vinden",
                                HasMacros = containsMacros, // ? "Eén of meerdere macros gevonden" : "Geen macros kunnen vinden"
                                HasEmbeddedFiles = containsEmbeddedFiles,
                                HasEmbeddedLinks = containsEmbeddedLinks
                            });
                        }

                        if (eventModel.Summary.Rejected > 0)
                            anyMessages.Add("Er zijn bestanden met wachtwoord beveiliging gevonden.");

                        if (except.Count > 0)
                        {
                            anyMessages.Add("Deze bestanden worden niet gecontroleerd op wachtwoord beveiliging:");
                            except.ForEach(item => anyMessages.Add(item.Location));
                        }
                    }
                }

                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.ActionData = jsonData.ToArray();
                eventModel.Summary.Accepted = eventModel.Summary.Processed - eventModel.Summary.Rejected;

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else if(eventModel.Properties.Messages.Count() > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                anyMessages.Clear();
                anyMessages.Add(String.Format("Running detection for files with password protection with collection: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Running detection for files with password protection with collection: '{0}' failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while running detection for files with password protection!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Password protection detection run with a collection is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// The Droid CSV output location.
        /// </summary>
        /// <returns></returns>
        private String DroidCsvOutputLocation()
        {
            var directory = new DirectoryInfo(TargetFolder);
            var files = directory.GetFiles("*.csv");

            if (files.Count() > 0)
            {
                FileInfo droidCsvFile = files.OrderByDescending(item => item.CreationTime).First();
                if (droidCsvFile == null)
                    return null;
                else
                    return droidCsvFile.FullName;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Internal object for holding execution result data.
        /// </summary>
        internal class DataItem
        {
            public string Location { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public string FormatName { get; set; }
            public string FormatVersion { get; set; }
            public string Puid { get; set; }
            public bool IsExtensionMismatch { get; set; }
            public string Message { get; set; }
            public bool InGreenList { get; set; }
        }

        // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
        internal class Indicator
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }
        }

        internal class Result
        {
            [JsonPropertyName("embeddedLinks")]
            public string EmbeddedLinks { get; set; }

            [JsonPropertyName("vbaMacros")]
            public string VbaMacros { get; set; }

            [JsonPropertyName("xmlMacros")]
            public string XmlMacros { get; set; }

            [JsonPropertyName("vbaEncrypted")]
            public string VbaEncrypted { get; set; }

            [JsonPropertyName("embeddedFiles")]
            public string EmbeddedFiles { get; set; }

            [JsonPropertyName("indicators")]
            public List<Indicator> Indicators { get; set; }
        }

        internal class RootOffice
        {
            [JsonPropertyName("result")]
            public Result Result { get; set; }
        }

        internal class ResultItem
        {
            public String Bestand { get; set; }
            public bool IsProtected { get; set; }
            public bool HasMacros { get; set; }
            public bool HasEmbeddedFiles { get; set; }
            public bool HasEmbeddedLinks { get; set; }
        }

        // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
        internal class Embedded
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("files")]
            public List<File> Files { get; set; }
        }

        internal class File
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("length")]
            public int Length { get; set; }

            [JsonPropertyName("size")]
            public int Size { get; set; }
        }

        internal class RootPdf
        {
            [JsonPropertyName("embedded")]
            public Embedded Embedded { get; set; }
        }



    }
}

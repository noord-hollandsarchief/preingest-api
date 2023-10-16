using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using Mono.Unix;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView;

using Newtonsoft.Json;

using Org.BouncyCastle.Crypto.Prng;
using DocumentFormat.OpenXml.Vml;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler for expanding a TAR archive collection file.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class UnpackTarHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnpackTarHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public UnpackTarHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
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
        /// <exception cref="System.IO.FileNotFoundException">Collection not found!</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);  
            OnTrigger(new PreingestEventArgs { Description= String.Format("Start expanding container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            List<String> output = new List<string>();

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                string encodedArchive = Utilities.ChecksumHelper.Base64Encode(TargetCollection);
                string url = String.Format("http://{0}:{1}/archive/expand/{2}/{3}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, SessionGuid, encodedArchive);

                OnTrigger(new PreingestEventArgs { Description = "Container is expanding content.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                Root dataResult;
                if (TargetCollection.EndsWith(".tar", StringComparison.InvariantCultureIgnoreCase) || TargetCollection.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase))
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = Timeout.InfiniteTimeSpan;
                        HttpResponseMessage response = client.PostAsync(url, null).Result;
                        response.EnsureSuccessStatusCode();
                        dataResult = JsonConvert.DeserializeObject<Root>(response.Content.ReadAsStringAsync().Result);

                        if (dataResult != null && dataResult.Result != null)
                            output.AddRange(dataResult.Result);
                    }
                }
                if (TargetCollection.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<string> entriesDestList = new List<string>();
                    using (ZipArchive zipArchive = (ZipArchive)ZipFile.OpenRead(TargetCollection))
                    {
                        entriesDestList.AddRange(zipArchive.Entries.Select(item => item.FullName).ToArray());
                        zipArchive.ExtractToDirectory(TargetFolder);                        
                    }
                    dataResult = new Root { Result = entriesDestList };
                    output.AddRange(dataResult.Result);
                }
                
                var fileInformation = new FileInfo(TargetCollection);
                anyMessages.Add(String.Concat("Name : ", fileInformation.Name));
                anyMessages.Add(String.Concat("Extension : ", fileInformation.Extension));
                anyMessages.Add(String.Concat("Size : ", fileInformation.Length));
                anyMessages.Add(String.Concat("CreationTime : ", fileInformation.CreationTimeUtc));
                anyMessages.Add(String.Concat("LastAccessTime : ", fileInformation.LastAccessTimeUtc));
                anyMessages.Add(String.Concat("LastWriteTime : ", fileInformation.LastWriteTimeUtc));

                eventModel.Properties.Messages = anyMessages.ToArray();

                bool isWindows = RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                if (!isWindows)
                {
                    var unixDirInfo = new UnixDirectoryInfo(TargetFolder);
                    //trigger event executing
                    var passEventArgs = new PreingestEventArgs { Description = String.Format("Execute chmod 777 for container '{0}'", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel };
                    OnTrigger(passEventArgs);
                    ScanPath(unixDirInfo, passEventArgs);
                }

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                anyMessages.Clear();
                anyMessages.Add(String.Format("Unpack container file: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Unpack container file: '{0}' failed!", TargetCollection);
                
                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs {Description = "An exception occured while unpacking a container!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                {                    
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;                    
                    eventModel.Summary.Processed = output.Count;
                    eventModel.Summary.Accepted = output.Count;
                    eventModel.Summary.Rejected = 0;
                    eventModel.ActionData = output.ToArray();

                    SaveStructure();
                    OnTrigger(new PreingestEventArgs { Description = "Unpacking the container is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });                    
                }
            }
        }

        /// <summary>
        /// Saves the structure.
        /// </summary>
        /// <param name="sessionFolder">The session folder.</param>
        private void SaveStructure()
        {
            try
            {
                var folder = new DirectoryInfo(TargetFolder);
                folder.Refresh();
                DataTreeHandler handler = new DataTreeHandler(SessionGuid, folder);
                handler.ClearCachedJson();
                handler.Load();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Scans the path.
        /// </summary>
        /// <param name="dirinfo">The dirinfo.</param>
        /// <param name="passEventArgs">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        private void ScanPath(UnixDirectoryInfo dirinfo, PreingestEventArgs passEventArgs)
        {
            passEventArgs.Description = String.Format("Processing folder '{0}'.", dirinfo.FullName);
            OnTrigger(passEventArgs);
            dirinfo.FileAccessPermissions = FileAccessPermissions.AllPermissions;
            foreach (var fileinfo in dirinfo.GetFileSystemEntries())
            {                
                switch (fileinfo.FileType)
                {
                    case FileTypes.RegularFile:    
                        fileinfo.FileAccessPermissions = FileAccessPermissions.AllPermissions;                    
                        break;
                    case FileTypes.Directory:
                        ScanPath((UnixDirectoryInfo)fileinfo, passEventArgs);
                        break;
                    default:
                        /* Do nothing for symlinks or other weird things. */
                        break;
                }
            }
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        internal class Root
        {
            [JsonProperty("result")]
            public List<string> Result { get; set; }
        }
    }
}

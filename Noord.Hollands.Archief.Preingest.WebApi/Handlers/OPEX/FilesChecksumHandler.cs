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
using System.Text;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX
{
    /// <summary>
    /// Handler to the fixity of every binary file in a collection.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class FilesChecksumHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilesChecksumHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public FilesChecksumHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection)
            : base(settings, eventHub, preingestCollection)
        {
            PreingestEvents += Trigger;
        }
        public void Dispose()
        {
            PreingestEvents -= Trigger;
        }

        /// <summary>
        /// Gets or sets the type of the hash.
        /// </summary>
        /// <value>
        /// The type of the hash.
        /// </value>
        public Algorithm HashType { get; set; }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.ApplicationException">Algorithm is not set!</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = String.Format("Start running checksum for container '{0}'.", TargetCollection), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSuccess = false;
            try
            {
                base.Execute();

                if (HashType == null)
                    throw new ApplicationException("Algorithm is not set!");

                string sessionFolder = Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString());

                //list of output result json files
                var outputJson = new DirectoryInfo(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString())).GetFiles("*.*", SearchOption.TopDirectoryOnly).ToList();
                //list all files from guid folder
                var allFiles = new DirectoryInfo(Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString())).GetFiles("*.*", SearchOption.AllDirectories).ToList();
                //list of only files without json 
                var targetFiles = allFiles.Where(item => !outputJson.Exists((x) => { return x.FullName == item.FullName; })).ToList();
                                
                var jsonData = new List<ResultType>();

                if (HashType.ProcessingMode == ExecutionMode.CalculateAndCompare)
                {
                    var metadataFiles = this.IsToPX ? targetFiles.Where(item => item.Name.EndsWith(".metadata", StringComparison.InvariantCultureIgnoreCase)).ToList() : targetFiles.Where(item => item.Name.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    //compare modus: compare checksum algorithm/value in metadata with self check algorithm/value
                    if (IsToPX)
                    {
                        var listOfFiles = metadataFiles.Select(item => new
                            {
                                Fullname = item.FullName,
                                ToPX = TryLoadOrCatch<Entities.ToPX.v2_3_2.topxType>(item, (s) =>
                                {
                                    if (s.Length > 0)
                                        jsonData.Add(new ResultType { Type = ResultType.InfoType.Message, Data = s });                                    
                                })
                            }).Where(item => (item.ToPX != null) && (item.ToPX.Item is Entities.ToPX.v2_3_2.bestandType)).Select(item => new
                            {
                                Fullname = item.Fullname,
                                Bestand = item.ToPX.Item as Entities.ToPX.v2_3_2.bestandType
                            }).ToList();

                        listOfFiles.ForEach(topx =>
                        {
                            var fixityList = topx.Bestand.formaat.Select(fixity => fixity.fysiekeIntegriteit != null ?
                            new
                            {
                                FixitiyAlgorithm = fixity.fysiekeIntegriteit.algoritme != null ? fixity.fysiekeIntegriteit.algoritme.Value : String.Empty,
                                FixitiyValue = fixity.fysiekeIntegriteit.waarde != null ? fixity.fysiekeIntegriteit.waarde.Value : String.Empty
                            } :
                            new
                            {
                                FixitiyAlgorithm = string.Empty,
                                FixitiyValue = string.Empty
                            }).ToList();

                            fixityList.ForEach(fixity =>
                            {
                                bool isEmptyAlgorithm = String.IsNullOrEmpty(fixity.FixitiyAlgorithm);
                                bool isEmptyValue = String.IsNullOrEmpty(fixity.FixitiyValue);

                                if (isEmptyAlgorithm && isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Geen algoritme + waarde gevonden in '{0}'", topx.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { topx.Fullname, "Geen algoritme + waarde gevonden in metadata." }
                                    });
                                }

                                if (isEmptyAlgorithm && !isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Geen algoritme, maar wel een waarde gevonden in '{0}'", topx.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { topx.Fullname, "Geen algoritme maar wel een waarde gevonden in metadata." }
                                    });
                                }

                                if (!isEmptyAlgorithm && isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Algoritme gevonden, maar geen waarde in '{0}'", topx.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { topx.Fullname, "Algoritme gevonden, maar geen waarde gevonden in metadata." }
                                    });
                                }

                                if (!isEmptyAlgorithm && !isEmptyValue)
                                {
                                    AlgorithmTypes foundAlgorithm = Translate(fixity.FixitiyAlgorithm);
                                    var fixityCheckResult = RunFixityCheck(new FileInfo(topx.Fullname.Replace(".metadata", String.Empty, StringComparison.InvariantCultureIgnoreCase)), foundAlgorithm, fixity.FixitiyValue);

                                    anyMessages.AddRange(fixityCheckResult.Where(item => item.Type == ResultType.InfoType.Message).SelectMany(item => item.Data.ToArray()));
                                    jsonData.AddRange(fixityCheckResult.ToArray());
                                }
                            });
                        });
                    }

                    if (IsMDTO)
                    {
                        var listOfFiles = metadataFiles.Select(item
                            => new
                            {
                                Fullname = item.FullName,
                                MDTO = TryLoadOrCatch<Entities.MDTO.v1_0.mdtoType>(item, (s) =>
                                {
                                    if (s.Length > 0)
                                        jsonData.Add(new ResultType { Type = ResultType.InfoType.Message, Data = s });
                                })
                            }).Where(item => (item.MDTO != null) && (item.MDTO.Item is Entities.MDTO.v1_0.bestandType)).Select(item => new
                            {
                                Fullname = item.Fullname,
                                Bestand = item.MDTO.Item as Entities.MDTO.v1_0.bestandType
                            }).ToList();

                        listOfFiles.ForEach(mdto =>
                        {
                            var fixityList = mdto.Bestand.checksum.Select(fixity => new
                            {
                                FixitiyAlgorithm = fixity.checksumAlgoritme != null ? fixity.checksumAlgoritme.begripLabel : String.Empty,
                                FixitiyValue = fixity.checksumWaarde
                            }).ToList();

                            fixityList.ForEach(fixity =>
                            {
                                bool isEmptyAlgorithm = String.IsNullOrEmpty(fixity.FixitiyAlgorithm);
                                bool isEmptyValue = String.IsNullOrEmpty(fixity.FixitiyValue);

                                if (isEmptyAlgorithm && isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Geen algoritme + waarde gevonden in '{0}'", mdto.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { mdto.Fullname, "Geen algoritme + waarde gevonden in metadata." }
                                    });
                                }

                                if (isEmptyAlgorithm && !isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Geen algoritme, maar wel een waarde gevonden in '{0}'", mdto.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { mdto.Fullname, "Geen algoritme maar wel een waarde gevonden in metadata." }
                                    });
                                }

                                if (!isEmptyAlgorithm && isEmptyValue)
                                {
                                    anyMessages.Add(String.Format("Algoritme gevonden, maar geen waarde in '{0}'", mdto.Fullname));
                                    jsonData.Add(new ResultType
                                    {
                                        Type = ResultType.InfoType.Action,
                                        Data = new string[] { mdto.Fullname, "Algoritme gevonden, maar geen waarde gevonden in metadata." }
                                    });
                                }

                                if (!isEmptyAlgorithm && !isEmptyValue)
                                {
                                    AlgorithmTypes foundAlgorithm = Translate(fixity.FixitiyAlgorithm);
                                    var fixityCheckResult = RunFixityCheck(new FileInfo(mdto.Fullname.Replace(".bestand.mdto.xml", String.Empty, StringComparison.InvariantCultureIgnoreCase)), foundAlgorithm, fixity.FixitiyValue);

                                    anyMessages.AddRange(fixityCheckResult.Where(item => item.Type == ResultType.InfoType.Message).SelectMany(item => item.Data.ToArray()));
                                    jsonData.AddRange(fixityCheckResult.ToArray());
                                }
                            });
                        });
                    }                    
                    eventModel.Properties.Messages = anyMessages.ToArray();
                }

                if (HashType.ProcessingMode == ExecutionMode.OnlyCalculate)
                {
                    var binaryFiles = this.IsToPX ? targetFiles.Where(item => !item.Name.EndsWith(".metadata", StringComparison.InvariantCultureIgnoreCase)).ToList() : targetFiles.Where(item => !item.Name.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    eventModel.Summary.Processed = binaryFiles.Count();

                    foreach (FileInfo file in binaryFiles)
                    {
                        var fixityCheckResult = RunFixityCheck(file, HashType.ChecksumType);
                        anyMessages.AddRange(fixityCheckResult.Where(item => item.Type == ResultType.InfoType.Message).SelectMany(item => item.Data.ToArray()));
                        jsonData.AddRange(fixityCheckResult.ToArray());
                    }                    
                    eventModel.Properties.Messages = new string[] { HashType.ChecksumType.ToString() };                    
                }

                eventModel.ActionData = jsonData.ToArray();
                eventModel.Summary.Processed = jsonData.Count();
                eventModel.Summary.Accepted = jsonData.Count(item => item.Type == ResultType.InfoType.Action);
                eventModel.Summary.Rejected = anyMessages.Count() + jsonData.Count(item => item.Type == ResultType.InfoType.Message);                

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSuccess = true;
            }
            catch (Exception e)
            {
                isSuccess = false;
                anyMessages.Clear();
                anyMessages.Add(String.Format("Running checksum with collection: '{0}' failed!", TargetCollection));
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                Logger.LogError(e, "Running checksum with collection: '{0}' failed!", TargetCollection);

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();
                eventModel.Summary.Processed = 1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = 1;

                OnTrigger(new PreingestEventArgs { Description = "An exception occured while running checksum!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSuccess)
                    OnTrigger(new PreingestEventArgs { Description = "Checksum run with a collection is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        private T TryLoadOrCatch<T>(FileInfo item, Action<String[]> action)
        {
            T result = default(T);
            try
            {
                result = DeserializerHelper.DeSerializeObjectFromXmlFile<T>(item);
            }
            catch(Exception e)
            {
                List<String> sb = new List<String>();
                sb.Add(String.Format("Inlezen van XML bestand '{0}' in '{1}' is niet gelukt!", item.Name, item.DirectoryName));
                sb.Add(e.Message);
                sb.Add(e.StackTrace);
                if (e.InnerException != null)
                {
                    sb.Add(e.InnerException.Message);
                    sb.Add(e.InnerException.StackTrace);
                }
                action(sb.ToArray());
            }

            return result;
        }


        /// <summary>
        /// Internal entity for holding process information
        /// </summary>
        internal class ResultType
        {
            public enum InfoType
            {
                Message,
                Action,
            }

            public InfoType Type { get; set; }
            public String[] Data { get; set; }
            public bool IsSuccess { get => Type == InfoType.Action; }
        }
        /// <summary>
        /// Runs the fixity check.
        /// </summary>
        /// <param name="binaryFiles">The binary files.</param>
        /// <param name="useAlgorithm">The use algorithm.</param>
        /// <param name="valueToCompare">The value to compare.</param>
        /// <returns></returns>
        private List<ResultType> RunFixityCheck(FileInfo binaryFiles, AlgorithmTypes useAlgorithm, string valueToCompare = null)
        {
            List<ResultType> checkResult = new List<ResultType>();

            string currentCalculation = string.Empty, url = string.Empty, urlPart = string.Empty;

            try
            {
                switch (useAlgorithm)
                {
                    default:                    
                    case AlgorithmTypes.MD5:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/md5/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateMD5Checksum(binaryFiles, url);
                        }
                        break;
                    case AlgorithmTypes.SHA1:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/sha1/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateSHA1Checksum(binaryFiles, url);
                        }
                        break;
                    case AlgorithmTypes.SHA224:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/sha224/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateSHA224Checksum(binaryFiles, url);
                        }
                        break;
                    case AlgorithmTypes.SHA256:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/sha256/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateSHA256Checksum(binaryFiles, url);
                        }
                        break;
                    case AlgorithmTypes.SHA384:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/sha384/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateSHA384Checksum(binaryFiles, url);
                        }
                        break;
                    case AlgorithmTypes.SHA512:
                        {
                            string encodedPath = ChecksumHelper.Base64Encode(binaryFiles.FullName);
                            url = String.Format("http://{0}:{1}/fixity/sha512/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, encodedPath);
                            currentCalculation = ChecksumHelper.CreateSHA512Checksum(binaryFiles, url);
                        }
                        break;
                }               
            }
            catch(Exception e)
            {
                checkResult.Add(new ResultType
                {
                    Type = ResultType.InfoType.Message,
                    Data = new string[] { binaryFiles.FullName, "Checksum waarde berekenen niet gelukt!", e.Message, e.StackTrace }
                });
            }
            finally {

                if (!String.IsNullOrEmpty(currentCalculation) && HashType.ProcessingMode == ExecutionMode.OnlyCalculate)
                    checkResult.Add(new ResultType
                    {
                        Type = ResultType.InfoType.Action,
                        Data = new string[] { binaryFiles.FullName, currentCalculation }
                    });

                if (!String.IsNullOrEmpty(currentCalculation) && HashType.ProcessingMode == ExecutionMode.CalculateAndCompare)
                {
                    if (currentCalculation.Equals(valueToCompare, StringComparison.InvariantCultureIgnoreCase))
                    {
                        checkResult.Add(new ResultType
                        {
                            Type = ResultType.InfoType.Action,
                            Data = new string[] { binaryFiles.FullName,
                            useAlgorithm.ToString(),
                            string.Format("Berekende waarde: {0}", currentCalculation),
                            string.Format("Meegeleverde waarde : {0}", valueToCompare),
                        }
                        });
                    }
                    else
                    {
                        checkResult.Add(new ResultType
                        {
                            Type = ResultType.InfoType.Message,
                            Data = new string[] { String.Format("Checksum waarde van bestand '{0}' met algorithme '{1}' komen niet overeen!{2}Berekende waarde: {3}{2}Meegeleverde waarde:{4}", binaryFiles.FullName, useAlgorithm, Environment.NewLine, currentCalculation, valueToCompare) }                          
                        });
                    }
                }
            }

            return checkResult;
        }

        /// <summary>
        /// Translates the specified input to a algorithm type.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        private AlgorithmTypes Translate(String input)
        {
            AlgorithmTypes result = AlgorithmTypes.MD5;//default

            bool isSha1 = Regex.IsMatch(input, "^(SHA1|SHA-1|)$");
            bool isSha224 = Regex.IsMatch(input, "^(SHA224|SHA-224)$");
            bool isSha256 = Regex.IsMatch(input, "^(SHA256|SHA-256)$");
            bool isSha384 = Regex.IsMatch(input, "^(SHA384|SHA-384)$");
            bool isSha512 = Regex.IsMatch(input, "^(SHA512|SHA-512)$");
            bool isMd5 = Regex.IsMatch(input, "^(MD5|MD-5)$");

            if (isSha1) return AlgorithmTypes.SHA1;
            if (isSha224) return AlgorithmTypes.SHA224;
            if (isSha256) return AlgorithmTypes.SHA256;
            if (isSha384) return AlgorithmTypes.SHA384;
            if (isSha512) return AlgorithmTypes.SHA512;
            if (isMd5) return AlgorithmTypes.MD5;

            return result;
        }
    }
}

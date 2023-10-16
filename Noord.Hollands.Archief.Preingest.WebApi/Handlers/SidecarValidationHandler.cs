using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    //Check 5
    /// <summary>
    /// Step for executing some sidecar validations in ToPX and MDTO
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class SidecarValidationHandler : AbstractPreingestHandler, IDisposable
    {
        private const String EXTENSION_MDTO = ".mdto.xml";
        private const String EXTENSION_TOPX = ".metadata";

        public SidecarValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }
        private String CollectionTitlePath(String fullnameLocation)
        {
            return fullnameLocation.Remove(0, TargetFolder.Length);
        }
        /// <summary>
        /// Executes this step logic.
        /// </summary>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start sidecar structure validation.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
            bool isSucces = false;

            List<ISidecar> sidecarTreeNode = new List<ISidecar>();
            try
            {
                base.Execute();

                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                if (collection == null)
                    throw new DirectoryNotFoundException(String.Format("Folder '{0}' not found!", TargetFolder));

                PreingestEventArgs execEventArgs = new PreingestEventArgs { Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel };

                var start = DateTime.Now;
                Logger.LogInformation("Start validate sidecar structure in '{0}'.", TargetFolder);
                Logger.LogInformation("Start time {0}", start);

                SetItemsSummary(collection, execEventArgs);

                //Controle van de sidecarstructuur
                //Controles op de aanwezigheid van de sidecarbestanden, Informatieobjecten en Bestanden
                //1.
                //2.
                //3.
                List<MessageResult> total = new List<MessageResult>();
                var structureMessages = ValidateStructure(collection, execEventArgs);
                total.AddRange(structureMessages);
                if (total.Count(item => item.IsAcceptable == false) == 0)
                {
                    /**
                     Controle van de aggregatieniveaus van de Informatieobjecten
                        De controle van de aggregatieniveaus van de Informatieobjecten bestaat uit twee onderdelen:
                        1.	Controle van het element <aggregatieniveau> voor elk Informatieobject op een geldige waarde, incl. rapportage.
                        2.	Controle van de nesting van de aggregatieniveaus van de Informatieobjecten, incl. rapportage.
                     **/
                    var aggregationMessages = ValidateAggregation(collection, execEventArgs);
                    total.AddRange(aggregationMessages);
                    if (total.Count(item => item.IsAcceptable == false) == 0)
                    {
                        /****
                         * 
                         * Controle van het element <identificatie> op unieke waarden, incl. rapportage
                            Voor alle Informatieobjecten in de sidecarstructuur wordt gecontroleerd of er exact één 
                            informatieobject aanwezig is met dezelfde waarde of waarden voor <identificatie>. 
                         **/
                        var uniquenessMessages = ValidateIdUniqueness(collection, execEventArgs);
                        total.AddRange(uniquenessMessages);
                    }
                }

                eventModel.Summary.Processed = total.Count;
                eventModel.Summary.Accepted = total.Where(item => item.IsAcceptable).Count();//validationResult.Where(item => item.IsCorrect).Count();
                eventModel.Summary.Rejected = total.Where(item => !item.IsAcceptable).Count();//validationResult.Where(item => !item.IsCorrect).Count();                
                
                eventModel.ActionData = total.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                var end = DateTime.Now;
                Logger.LogInformation("End of the validation for the sidecar structure.");
                Logger.LogInformation("End time {0}", end);
                TimeSpan processTime = (TimeSpan)(end - start);
                Logger.LogInformation(String.Format("Processed in {0} ms.", processTime));

                isSucces = true;
            }
            catch (Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "Exception occured in sidecar structure validation!");

                var anyMessages = new List<String>();
                anyMessages.Clear();
                anyMessages.Add("Exception occured in sidecar structure validation!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = -1;
                eventModel.Summary.Rejected = -1;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in sidecar structure validation!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Sidecar structure validation is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel, SidecarStructure = sidecarTreeNode });
            }
        }
        /// <summary>
        /// Calculate items and generate a summary of objects.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="eventArgs">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        private void SetItemsSummary(DirectoryInfo target, PreingestEventArgs eventArgs)
        {
            eventArgs.Description = String.Format("Start counting objects.", TargetFolder);
            OnTrigger(eventArgs);

            FileInfo[] files = target.GetFiles("*.*", SearchOption.AllDirectories);
            String extension = this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX;
            //With MDTO , extra condition check with xml files

            var listOfMetadata = files.Where(item => item.FullName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToList();

            if (this.IsToPX)
            {
                //XNamespace ns = "http://www.nationaalarchief.nl/ToPX/v2.3";
                var xmlList = listOfMetadata.Select(xml => XDocument.Load(xml.FullName)).ToList();
                XNamespace ns = xmlList.Select(item => item.Root.GetDefaultNamespace()).Distinct().First();

                var aggregaties = xmlList.Where(xml => xml.Root.Elements(ns + "aggregatie").Count() > 0).ToList();
                var bestanden = xmlList.Where(xml => xml.Root.Elements(ns + "bestand").Count() > 0).ToList();

                String[] messages = new String[]
                {
                    String.Format("Archief : {0} item(s)", aggregaties.Count(item => item.Root.Element(ns + "aggregatie").Element(ns +"aggregatieniveau" ).Value.Equals("archief", StringComparison.InvariantCultureIgnoreCase) )),
                    String.Format("Series : {0} item(s)", aggregaties.Count(item => item.Root.Element(ns + "aggregatie").Element(ns +"aggregatieniveau" ).Value.Equals("serie", StringComparison.InvariantCultureIgnoreCase))),
                    String.Format("Record : {0} item(s)", aggregaties.Count(item => item.Root.Element(ns + "aggregatie").Element(ns +"aggregatieniveau" ).Value.Equals("record", StringComparison.InvariantCultureIgnoreCase))),
                    String.Format("Dossier : {0} item(s)", aggregaties.Count(item => item.Root.Element(ns + "aggregatie").Element(ns +"aggregatieniveau" ).Value.Equals("dossier", StringComparison.InvariantCultureIgnoreCase))),
                    String.Format("Bestand : {0} item(s)", bestanden.Count())
                };
                //save the summary
                eventArgs.PreingestAction.Properties.Messages = messages;
                eventArgs.PreingestAction.Summary.Processed = listOfMetadata.Count;
            }

            if (this.IsMDTO)
            {
                var xmlList = listOfMetadata.Select(xml => XDocument.Load(xml.FullName)).ToList();
                XNamespace ns =  xmlList.Select(item => item.Root.GetDefaultNamespace()).Distinct().First();

                var informatieobjecten = xmlList.Where(xml => xml.Root.Elements(ns + "informatieobject").Count() > 0).ToList();
                var bestanden = xmlList.Where(xml => xml.Root.Elements(ns + "bestand").Count() > 0).ToList();
                
                
                var summaryListSimplified = informatieobjecten.Select(item => new
                {
                    Xml = item,
                    Label = item.Root != null && item.Root.Element(ns + "informatieobject") != null && 
                            item.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau") != null && 
                            item.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau").Element(ns + "begripLabel") != null ? 
                            item.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau").Element(ns + "begripLabel").Value : null
                }).ToList();

                var summaryListGrouped = summaryListSimplified.GroupBy(item => item.Label).Select(group => new
                {
                    Label = group.Key,
                    Count = group.Count()
                }).OrderBy(x => x.Label);

                List<String> messages = summaryListGrouped.Select(item => String.Format("{0} : {1} item(s)", item.Label, item.Count)).ToList();

                messages.Add(String.Format("Bestand : {0} item(s)", bestanden.Count()));
                eventArgs.PreingestAction.Properties.Messages = messages.ToArray();
                eventArgs.PreingestAction.Summary.Processed = listOfMetadata.Count;
            }
        }
        /// <summary>
        /// Validates the sidecar structure.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="eventArgs">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Input parameter is null! Expect DirectoryInfo as input.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        private List<MessageResult> ValidateStructure(DirectoryInfo target, PreingestEventArgs eventArgs)
        {
            String extension = this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX;

            eventArgs.Description = String.Format("Start validate collection structure.", TargetFolder);
            OnTrigger(eventArgs);

            //first run validation check through files and folder objects
            //second run validation check through metadata files
            //eventually validation will be done both ways

            List<MessageResult> messages = new List<MessageResult>();

            if (target == null)
                throw new ArgumentNullException("Input parameter is null! Expect DirectoryInfo as input.");

            if (!target.Exists)
                throw new DirectoryNotFoundException(String.Format("Not found: '{0}'.", target.FullName));

            FileInfo[] files = target.GetFiles("*.*", SearchOption.AllDirectories);
            DirectoryInfo[] directories = Directory.GetDirectories(target.FullName, "*", SearchOption.AllDirectories).Select(item => new DirectoryInfo(item)).ToArray();

            int sum = (files.Count() + directories.Count() + 1);//plus 1 itself
            if ((sum % 2) != 0) //odd, not even. Expected even....
                messages.Add(
                   new MessageResult
                   {
                       IsAcceptable = false,
                       Message = String.Format("De som van het aantal objecten is niet een even getal! Totale objecten: {2}, bestanden: {0}, mappen: {1}",
                       files.Count(), (directories.Count() + 1), sum)
                   });

            //total list of objects (stripped without the .metadata);
            List<FileSystemInfo> strippedCombinedCollection = new List<FileSystemInfo>();
            var filesStrippedMetadataExtension = files.Where(item => !item.FullName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToArray();

            strippedCombinedCollection.AddRange(filesStrippedMetadataExtension);
            strippedCombinedCollection.AddRange(directories);

            var filesOnlyMetadataExtension = files.Where(item => item.FullName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToList();

            //check for each object if it's paired with a metadata file ToPX (.metadata) or MDTO (.mdto.xml);
            foreach (var collectionObject in strippedCombinedCollection)
            {
                string fullnameMetadata = String.Empty;
                if (collectionObject is FileInfo)
                    fullnameMetadata = String.Concat(collectionObject.FullName, this.IsMDTO ? String.Concat(".bestand", EXTENSION_MDTO) : EXTENSION_TOPX);
                else
                    fullnameMetadata = String.Concat(Path.Combine(collectionObject.FullName, collectionObject.Name), this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX);

                bool exists = filesOnlyMetadataExtension.Exists((informationObject) => { return File.Exists(fullnameMetadata); });
                if (!exists)
                {
                    string message = String.Format("Geen bijbehorende {0} metadata bestand gevonden voor object ({2}) '{1}'",
                        this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX, collectionObject.Name, collectionObject is FileInfo ? "bestand" : "map");

                    StringBuilder messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine(message);
                    messageBuilder.AppendLine(String.Format("Object type: {0}", (collectionObject is FileInfo) ? "bestand" : "map"));
                    messageBuilder.AppendLine(String.Format("Doel: {0}", collectionObject.FullName));
                    messageBuilder.AppendLine(String.Format("Verwacht: {0}", fullnameMetadata));

                    messages.Add(new MessageResult { IsAcceptable = false, Message = messageBuilder.ToString() });
                }
                else
                {
                    string message = String.Format("Metadata bestand ({0}) gevonden voor object ({2}) '{1}'",
                       this.IsMDTO ? String.Concat(collectionObject is FileInfo ? "bestand" : String.Empty, EXTENSION_MDTO) : EXTENSION_TOPX, collectionObject.Name, collectionObject is FileInfo ? "bestand" : "map");

                    messages.Add(new MessageResult { Message = message, IsAcceptable = true });
                }
            }

            
            //third run from metadata
            foreach (var metadata in filesOnlyMetadataExtension)
            {
                string ext = (this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX);
                var questionMarkIsFile = metadata.FullName.Remove(metadata.FullName.Length - ext.Length, ext.Length);

                questionMarkIsFile = (this.IsMDTO && questionMarkIsFile.EndsWith(".bestand")) ? questionMarkIsFile.Replace(".bestand", string.Empty) : questionMarkIsFile;

                bool filExists = File.Exists(questionMarkIsFile);
                bool dirExists = Directory.Exists(questionMarkIsFile);

                if (!dirExists && filExists)
                {
                    var parts = metadata.FullName.Split("/", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                    var bestandMetadata = parts.Last();
                    var bestandBinary = this.IsMDTO ? bestandMetadata.Remove(bestandMetadata.Length - (".bestand" + ext).Length, (".bestand" + ext).Length) : bestandMetadata.Remove(bestandMetadata.Length - ext.Length, ext.Length);
                    //remove metadata one
                    parts.Remove(bestandMetadata);
                    //add binary one
                    parts.Add(bestandBinary);
                    string fullLocation = "/" + Path.Join(parts.ToArray());
                    if (File.Exists(fullLocation))
                    {
                        messages.Add(new MessageResult { Message = String.Format("Bijbehorende object (bestand) '{0}' gevonden voor metadata bestand '{1}' in '{2}'", bestandBinary, bestandMetadata, metadata.DirectoryName), IsAcceptable = true });
                    }
                    else
                    {
                        messages.Add(new MessageResult { Message = String.Format("Bijbehorende object (bestand) '{0}' niet gevonden voor metadata bestand '{1}'. Verwacht object (bestand) '{0}' in '{2}'", bestandBinary, bestandMetadata, metadata.DirectoryName), IsAcceptable = false });
                    }
                }
                else
                {
                    var parts = metadata.FullName.Split("/", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    //take the last two and compare the names
                    var lastTwo = parts.Skip(parts.Count - 2).Take(2);
                    bool isSidecarFolderCorrect = (lastTwo.First() == lastTwo.Last().Remove(lastTwo.Last().Length - ext.Length, ext.Length));

                    if (isSidecarFolderCorrect)//found 
                        messages.Add(new MessageResult { Message = String.Format("Bijbehorende object (map) gevonden voor metadata bestand '{0}' in '{1}'", metadata.Name, metadata.DirectoryName), IsAcceptable = true });
                    else
                        messages.Add(new MessageResult { Message = String.Format("Bijbehorende object (map) niet gevonden voor metadata bestand '{0}'. Verwacht object '{0}' maar heb '{1}' in '{2}'", lastTwo.Last().Remove(lastTwo.Last().Length - ext.Length, ext.Length), lastTwo.First(), metadata.DirectoryName), IsAcceptable = false });
                }
            }

            //fourth run: check for empty folder(s) (no bin files, metadata files not count)
            List<DirectoryInfo> emptyFolderList = new List<DirectoryInfo>();
            IsEmptyLeaf(target.FullName, emptyFolderList);
            var emptyFolderMessages = emptyFolderList.Select(item => new MessageResult
            {
                IsAcceptable = false,
                Message = String.Format("In de map '{0}' is geen binaire bestand en/of bijbehorende metadata bestand hiervan gevonden. Op de laagste niveau van de agreggatie structuur wordt 'Bestand' verwacht.", item)
            });

            messages.AddRange(emptyFolderMessages);

            return messages;
        }
        /// <summary>
        /// Start validation for aggregation levels in ToPX or MDTO.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="eventArgs">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        private List<MessageResult> ValidateAggregation(DirectoryInfo target, PreingestEventArgs eventArgs)
        {
            eventArgs.Description = String.Format("Start validate aggregation combination.", TargetFolder);
            OnTrigger(eventArgs);

            List<MessageResult> messages = new List<MessageResult>();

            FileInfo[] files = target.GetFiles("*.*", SearchOption.AllDirectories);

            String extension = this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX;

            var listOfBinary = files.Where(item => !item.FullName.EndsWith(extension)).ToList();
            var listOfMetadata = files.Where(item => item.FullName.EndsWith(extension)).ToList();

            //assumption, need extra double check!!!
            //With MDTO , extra condition check with xml files
            string collectionArchiveName = string.Empty;           
            if (target.GetFiles((this.IsToPX) ? String.Concat("*", EXTENSION_TOPX) : String.Concat("*", EXTENSION_MDTO), SearchOption.TopDirectoryOnly).Count() == 1)
            {
                //current folder is archive collection name
                collectionArchiveName = target.Name;
            }
            else
            {
                if (target.GetDirectories().Count() == 0)
                    throw new DirectoryNotFoundException(String.Format("No collection folder found in '{0}'!", target.FullName));
                //first folder is archive collection name
                collectionArchiveName = target.GetDirectories().FirstOrDefault().Name;
            }

            if (this.IsToPX)
            {
                /** topx
                 * Archief
                 *  Serie 0..*
                 *  Dossier 0..*
                 *  
                 *  Serie
                 *      Serie 0..*
                 * 
                 * Dossier
                 *  Record 0..*
                 *  Bestand 1..*, 
                 *  Dossier 0..*
                 **/

                var scanResult = AggregationToPX(listOfBinary, listOfMetadata, collectionArchiveName);

                foreach (var keyValuePair in scanResult)
                {
                    string currentBestandMetadataFile = keyValuePair.Key;

                    var structureConstruction = keyValuePair.Value.Select(item => item.Root.Elements().First().Name.LocalName).Reverse();
                    XNamespace ns = keyValuePair.Value.Select(item => item.Root.GetDefaultNamespace()).Distinct().First();

                    var aggregationLevel = keyValuePair.Value.Select(item =>
                    (item.Root.Elements().First().Name.LocalName == "bestand") ? "Bestand" :
                        item.Root.Elements().First().Element(ns + "aggregatieniveau").Value).Reverse();

                    //aggregation construction test
                    var structureValue = String.Join('|', structureConstruction);
                    var levelValue = String.Join('|', aggregationLevel);

                    string pattern = @"^(aggregatie\|){1,}(bestand){1}$";
                    System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
                    bool isMatch = regex.IsMatch(structureValue);
                    if (!isMatch)
                        messages.Add(new MessageResult { Message = String.Format("Ongeldige ToPX element(en) nesting {0} (top/down) gevonden voor {1}", structureValue, currentBestandMetadataFile), IsAcceptable = false });
                    else
                        messages.Add(new MessageResult { Message = String.Format("Acceptabele ToPX element(en) nesting {0} (top/down) >> {1}", structureValue, currentBestandMetadataFile), IsAcceptable = true });
                                       
                    //aggregation levels acceptable route
                    pattern = @"^(Archief\|){1}(Serie\|){0,}(Dossier\|){1,}(Record\|){0,}(Bestand){1}$";
                    regex = new System.Text.RegularExpressions.Regex(pattern);                    
                    isMatch = regex.IsMatch(levelValue);
                    using (SidecarStructureRulesHandler rules = new SidecarStructureRulesHandler(keyValuePair))
                    {
                        if (!isMatch)
                        {
                            rules.Explane();
                            var explanations = rules.GetExplanations();
                            var explanationResults = explanations.Select(item => new MessageResult { Message = String.Format("Ongeldige aggregatie nesting {2} (top/down). {0} Zie: {1}", item.ExplantionText, String.Concat(item.TargetMetadata, "; ", currentBestandMetadataFile), levelValue), IsAcceptable = false });
                            messages.AddRange(explanationResults);
                        }
                        else
                        {
                            var paths = String.Join("; ", rules.GetLevelWithMetadataLocation().Keys.ToArray());
                            messages.Add(new MessageResult { Message = String.Format("Acceptabele aggregatie nesting {0} (top/down) >> {1}", levelValue, String.Concat(paths, "; ", currentBestandMetadataFile)), IsAcceptable = true });
                        }
                    }
                }
            }

            if (this.IsMDTO)
            {
                //mdto
                /***
                 * InformatieObject (Archief)
                 *      InformatieObject (Serie)
                 *          InformatieObject (Dossier)
                 *              InformatieObject (Archiefstuk)
                 *                  Bestand
                ***/
                var scanResult = AggregationMDTO(listOfBinary, listOfMetadata, collectionArchiveName);

                foreach (var keyValuePair in scanResult)
                {
                    string currentBestandMetadataFile = keyValuePair.Key;

                    XNamespace ns = keyValuePair.Value.Select(item => item.Root.GetDefaultNamespace()).Distinct().First();
                    var construction = keyValuePair.Value.Select(item
                        => item.Root.Element(ns + "informatieobject") == null ? item.Root.Element(ns + "bestand").Name.LocalName : item.Root.Element(ns + "informatieobject").Name.LocalName).Reverse();

                    var structureValue = String.Join('|', construction);

                    //aggregatie element test
                    string pattern = @"^(informatieobject\|){1,}(bestand){1}$";

                    System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
                    bool isMatch = regex.IsMatch(structureValue);

                    if (!isMatch)
                        messages.Add(new MessageResult { Message = String.Format("Ongeldige MDTO element(en) nesting {0} (top/down) gevonden voor {1}", structureValue, currentBestandMetadataFile), IsAcceptable = false });
                    else
                        messages.Add(new MessageResult { Message = String.Format("Acceptabele MDTO element(en) nesting {0} (top/down) >> {1}", structureValue, currentBestandMetadataFile), IsAcceptable = true });

                    //aggregation levels acceptable route
                    string pattern2 = @"^((Archief\|){1}(Serie\|){0,}(Dossier\|){1,1}(Zaak\|){0,1}(Archiefstuk\|){1,}(Bestand){1})|(^(Archief\|){1}(Zaak\|){1,1}(Archiefstuk\|){1,}(Bestand){1})|(^(Archief\|){1}(Serie\|){0,}(Archiefstuk\|){1,1}(Bestand){1})$";

                    regex = new System.Text.RegularExpressions.Regex(pattern2);

                    var aggregationLevel = keyValuePair.Value.Select(item =>
                    (item.Root.Elements().First().Name.LocalName == "bestand") ? "Bestand" :
                        item.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau").Element(ns + "begripLabel").Value).Reverse();

                    var levelValue = String.Join('|', aggregationLevel);
                    isMatch = regex.IsMatch(levelValue);
                    using (SidecarStructureRulesHandler rules = new SidecarStructureRulesHandler(keyValuePair))
                    {                        
                        if (!isMatch)
                        {
                            rules.Explane();
                            var explanations = rules.GetExplanations();
                            var explanationResults = explanations.Select(item => new MessageResult { Message = String.Format("Ongeldige aggregatie nesting {2} (top/down). {0} Zie: {1}", item.ExplantionText, String.Concat(item.TargetMetadata, "; ", currentBestandMetadataFile), levelValue), IsAcceptable = false });
                            messages.AddRange(explanationResults);
                        }
                        else
                        {
                            var paths = String.Join("; ", rules.GetLevelWithMetadataLocation().Keys.ToArray());
                            messages.Add(new MessageResult { Message = String.Format("Acceptabele aggregatie nesting {0} (top/down) >> {1}", levelValue, String.Concat(paths, "; ", currentBestandMetadataFile)), IsAcceptable = true });
                        }
                    }
                }
            }

            return messages;
        }
        /// <summary>
        /// Aggregate ToPX collection.
        /// </summary>
        /// <param name="files">The files.</param>
        /// <param name="metadataList">The metadata list.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        private IDictionary<String, List<XDocument>> AggregationToPX(List<FileInfo> files, List<FileInfo> metadataList, String collectionName)
        {
            IDictionary<String, List<XDocument>> resultDictionary = new Dictionary<String, List<XDocument>>();

            foreach (FileInfo fileItem in files)
            {
                var metadataFile = String.Concat(fileItem.FullName, EXTENSION_TOPX);

                bool exists = metadataList.Exists((item) =>
                {
                    return item.FullName.Equals(metadataFile, StringComparison.InvariantCultureIgnoreCase);
                });

                if (!exists || !File.Exists(metadataFile))
                    throw new FileNotFoundException(String.Format("File '{0}' not found!", metadataFile));

                //Windows split, in Linux is it different. TODO!!!!
                var folders = fileItem.Directory.FullName.Split('/').ToArray();

                int startingPoint = folders.ToList().IndexOf(collectionName);
                int endingPoint = folders.Count();

                var storagePoint = String.Join('/', folders.Skip(0).Take(startingPoint));

                var fullStructurePath = folders.Skip(startingPoint).Take(endingPoint).ToList();
                int counter = 0;

                var aggregationList = new List<XDocument>();
                //add bestand self 
                XDocument bestand = XDocument.Load(metadataFile);
                aggregationList.Add(bestand);

                foreach (var item in fullStructurePath)
                {
                    //shift the part folder names
                    var tempList = new List<String>(fullStructurePath);
                    tempList.RemoveRange((fullStructurePath.Count) - counter, counter);

                    string folderPath = Path.Combine(tempList.ToArray());

                    var currentProcessingDirectory = new DirectoryInfo(String.Concat(storagePoint, "/", folderPath));

                    string mdtoOrToPXmetadataFilename = String.Concat(currentProcessingDirectory.Name, EXTENSION_TOPX);
                    string mdtoOrToPXmetadataFullname = Path.Combine(currentProcessingDirectory.FullName, mdtoOrToPXmetadataFilename);

                    if (!File.Exists(mdtoOrToPXmetadataFullname))
                    {
                        string message = String.Format("Verwacht ToPX metadatabestand '{0}' in map '{1}'.", mdtoOrToPXmetadataFullname, currentProcessingDirectory.FullName);
                        throw new FileNotFoundException(message);
                    }

                    XDocument next = XDocument.Load(mdtoOrToPXmetadataFullname);
                    aggregationList.Add(next);

                    counter++;
                }

                resultDictionary.Add(metadataFile, aggregationList);

            }

            return resultDictionary;
        }
        /// <summary>
        /// Aggregate MDTO collection.
        /// </summary>
        /// <param name="filesList">The files list.</param>
        /// <param name="metadataList">The metadata list.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException"></exception>
        private IDictionary<String, List<XDocument>> AggregationMDTO(List<FileInfo> filesList, List<FileInfo> metadataList, String collectionName)
        {
            IDictionary<String, List<XDocument>> resultDictionary = new Dictionary<String, List<XDocument>>();

            foreach (FileInfo fileItem in filesList)
            {
                var metadataFile = String.Concat(fileItem.FullName, ".bestand", EXTENSION_MDTO);

                bool exists = metadataList.Exists((item) =>
                {
                    return item.FullName.Equals(metadataFile, StringComparison.InvariantCultureIgnoreCase);
                });

                if (!exists || !File.Exists(metadataFile))
                    throw new FileNotFoundException(String.Format("File '{0}' not found!", metadataFile));

                //Windows split, in Linux is it different. TODO!!!!
                var folders = fileItem.Directory.FullName.Split('/').ToArray();

                int startingPoint = folders.ToList().IndexOf(collectionName);
                int endingPoint = folders.Count();

                var storagePoint = String.Join('/', folders.Skip(0).Take(startingPoint));

                var fullStructurePath = folders.Skip(startingPoint).Take(endingPoint).ToList();
                int counter = 0;

                var aggregationList = new List<XDocument>();
                //add bestand self           
                XDocument bestand = XDocument.Load(metadataFile);
                aggregationList.Add(bestand);

                foreach (var item in fullStructurePath)
                {
                    //shift the part folder names
                    var tempList = new List<String>(fullStructurePath);
                    tempList.RemoveRange((fullStructurePath.Count) - counter, counter);

                    string folderPath = Path.Combine(tempList.ToArray());

                    var currentProcessingDirectory = new DirectoryInfo(String.Concat(storagePoint, "/", folderPath));

                    string mdtoOrToPXmetadataFilename = String.Concat(currentProcessingDirectory.Name, EXTENSION_MDTO);
                    string mdtoOrToPXmetadataFullname = Path.Combine(currentProcessingDirectory.FullName, mdtoOrToPXmetadataFilename);

                    if (!File.Exists(mdtoOrToPXmetadataFullname))
                    {
                        string message = String.Format("Verwacht MDTO metadatabestand '{0}' in map '{1}'.", mdtoOrToPXmetadataFullname, currentProcessingDirectory.FullName);
                        throw new FileNotFoundException(message);
                    }

                    XDocument next = XDocument.Load(mdtoOrToPXmetadataFullname);
                    aggregationList.Add(next);

                    counter++;
                }

                resultDictionary.Add(metadataFile, aggregationList);
            }
            return resultDictionary;
        }
        /// <summary>
        /// Validates the identifier uniqueness.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="eventArgs">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Input parameter is null! Expect DirectoryInfo as input.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        private List<MessageResult> ValidateIdUniqueness(DirectoryInfo target, PreingestEventArgs eventArgs)
        {
            String extension = this.IsMDTO ? EXTENSION_MDTO : EXTENSION_TOPX;

            eventArgs.Description = String.Format("Start validate collection structure.", TargetFolder);
            OnTrigger(eventArgs);

            List<MessageResult> messages = new List<MessageResult>();
            List<Uniqueness> listofUniqueItems = new List<Uniqueness>();

            if (target == null)
                throw new ArgumentNullException("Input parameter is null! Expect DirectoryInfo as input.");

            if (!target.Exists)
                throw new DirectoryNotFoundException(String.Format("Not found: '{0}'.", target.FullName));

            FileInfo[] files = target.GetFiles("*.*", SearchOption.AllDirectories);
            DirectoryInfo[] directories = Directory.GetDirectories(target.FullName, "*", SearchOption.AllDirectories).Select(item => new DirectoryInfo(item)).ToArray();
   
            //total list of objects (stripped without the .metadata);
            List<FileSystemInfo> strippedCombinedCollection = new List<FileSystemInfo>();
            var filesStrippedMetadataExtension = files.Where(item => !item.FullName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToArray();

            strippedCombinedCollection.AddRange(filesStrippedMetadataExtension);
            strippedCombinedCollection.AddRange(directories);

            var filesOnlyMetadataExtension = files.Where(item => item.FullName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var xmlList = filesOnlyMetadataExtension.Select(xml => new { FileInfoObject = xml,  Xml = XDocument.Load(xml.FullName) }).ToList();
            XNamespace ns = xmlList.Select(item => item.Xml.Root.GetDefaultNamespace()).Distinct().First();

            if (this.IsToPX)
            {
                var aggregaties = xmlList.Where(item => item.Xml.Root.Elements(ns + "aggregatie").Count() > 0).Select(item
                    => new Uniqueness
                    {
                        IdentificatieKenmerk = String.Concat(item.Xml.Root.Element(ns + "aggregatie").Descendants(ns + "identificatiekenmerk").Select(node => node.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces))),
                        Xml = item.Xml,
                        MetadataFilename = item.FileInfoObject
                    }).ToList();

                var bestanden = xmlList.Where(item => item.Xml.Root.Elements(ns + "bestand").Count() > 0).Select(item
                    => new Uniqueness
                    {
                        IdentificatieKenmerk = String.Concat(item.Xml.Root.Element(ns + "bestand").Descendants(ns + "identificatiekenmerk").Select(node => node.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces))),
                        Xml = item.Xml,
                        MetadataFilename = item.FileInfoObject
                    }).ToList();

                listofUniqueItems.AddRange(aggregaties);
                listofUniqueItems.AddRange(bestanden);
            }

            if (this.IsMDTO)
            {
                var informatieobjecten = xmlList.Where(item => item.Xml.Root.Elements(ns + "informatieobject").Count() > 0).Select(item
                    => new Uniqueness
                    {
                        IdentificatieKenmerk = String.Concat(item.Xml.Root.Element(ns + "informatieobject").Descendants(ns + "identificatie").Select(node => node.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces))),
                        Xml = item.Xml,
                        MetadataFilename = item.FileInfoObject
                    }).ToList();

                var bestanden = xmlList.Where(item => item.Xml.Root.Elements(ns + "bestand").Count() > 0).Select(item
                    => new Uniqueness
                    {
                        IdentificatieKenmerk = String.Concat(item.Xml.Root.Element(ns + "bestand").Descendants(ns + "identificatie").Select(node => node.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces))),
                        Xml = item.Xml,
                        MetadataFilename = item.FileInfoObject
                    }).ToList();

                listofUniqueItems.AddRange(informatieobjecten);
                listofUniqueItems.AddRange(bestanden);
            }

            var summaryListGrouped = listofUniqueItems.GroupBy(item => item.UniqueReferenceHash).Select(group => new
            {
                IdentificatieKenmerkHashcode = group.Key,
                Count = group.Count()
            }).Where(item => item.Count >= 2).OrderByDescending(x => x.Count);

            if( summaryListGrouped.Count() == 0)
            {
                messages.Add(new MessageResult { IsAcceptable = true, Message = "Geen identificatie-duplicaten gevonden in de collectie" });
            }
            else
            {
                messages.Add(new MessageResult { IsAcceptable = false, Message = String.Format("Er zijn identificatie-duplicaten gevonden in de collectie: {0}", summaryListGrouped.Count()) });

                summaryListGrouped.ToList().ForEach(summaryItem =>
                {
                    var hash = summaryItem.IdentificatieKenmerkHashcode;
                    var summaryMessages =  listofUniqueItems.Where(item => item.UniqueReferenceHash == hash).Select(item => new MessageResult
                    {
                        IsAcceptable = false,
                        Message = String.Format("Duplicaat identificatie {0} - {1}", item.MetadataFilename.FullName, item.IdentificatieKenmerk)
                    }).ToArray();

                    messages.AddRange(summaryMessages);
                });
            }

            return messages;
        }

        /// <summary>
        /// Determines whether the specified folder path is empty.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="emptyFolderList">The empty folder list.</param>
        /// <returns>
        ///   <c>true</c> if the specified folder path is empty; otherwise, <c>false</c>.
        /// </returns>
        private bool IsEmptyLeaf(string folderPath, List<DirectoryInfo> emptyFolderList)
        {
            bool allSubFoldersEmpty = true;
            foreach (var subFolder in Directory.EnumerateDirectories(folderPath))
            {
                bool isEmptyFiles = IsEmptyLeaf(subFolder, emptyFolderList);
                bool hasNoSubFolders = (Directory.GetDirectories(subFolder).Count() == 0);
                if (isEmptyFiles && hasNoSubFolders)
                {
                    emptyFolderList.Add(new DirectoryInfo(subFolder));
                }
                else
                {
                    allSubFoldersEmpty = false;
                }
            }

            if (allSubFoldersEmpty && !HasFiles(folderPath))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Determines whether the specified folder path has files.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <returns>
        ///   <c>true</c> if the specified folder path has files; otherwise, <c>false</c>.
        /// </returns>
        private bool HasFiles(string folderPath)
        {
            var files = Directory.EnumerateFiles(folderPath).Where(item => !item.EndsWith(IsToPX ? ".metadata" : ".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).Any();
            return files;
        }

        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }

        /// <summary>
        /// Simple message entity container for holding results
        /// </summary>
        internal class MessageResult
        {
            public String Message { get; set; }
            public Boolean IsAcceptable { get; set; }

            public override string ToString()
            {
                return Message;
            }
        }

        /// <summary>
        /// Entity container for holding ID and determine the uniqueness of XML element identificatie in ToPX and MDTO
        /// </summary>
        internal class Uniqueness
        {
            private Guid _id = Guid.Empty;
            public Uniqueness()
            {
                _id = Guid.NewGuid();
            }
            public Guid InternalId { get { return this._id; } }

            public string IdentificatieKenmerk { get; set; }
            public XDocument Xml { get; set; }
            public FileInfo MetadataFilename { get; set; }
            public string UniqueReferenceHash
            {
                get
                {
                    return CreateReferenceHash(IdentificatieKenmerk);
                }
            }
            
            public override int GetHashCode()
            {
                return _id.GetHashCode();
            }

            public override string ToString()
            {
                return String.Format("{1} {0}", IdentificatieKenmerk, UniqueReferenceHash);
            }
            
            private string CreateReferenceHash(string input)
            {
                // Use input string to calculate MD5 hash
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    // Convert the byte array to hexadecimal string
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        sb.Append(hashBytes[i].ToString("X2"));
                    }
                    return sb.ToString();
                }
            }
        }
    }
}

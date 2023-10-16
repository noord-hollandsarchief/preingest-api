using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX.v2_3_2;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    //Check 5
    /// <summary>
    /// Handler for validation all metadata files. It uses XSLWeb for validaiton with XSD + Schematron
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class MetadataValidationHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataValidationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public MetadataValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }

        /// <summary>
        /// Gets the processing URL.
        /// </summary>
        /// <param name="servername">The servername.</param>
        /// <param name="port">The port.</param>
        /// <param name="pad">The pad.</param>
        /// <returns></returns>
        private String GetProcessingUrl(string servername, string port, string pad)
        {
            string data = this.ApplicationSettings.DataFolderName.EndsWith("/") ? this.ApplicationSettings.DataFolderName : this.ApplicationSettings.DataFolderName + "/";
            string reluri = pad.Remove(0, data.Length);
            string newUri = String.Join("/", reluri.Split("/", StringSplitOptions.None).Select(item => Uri.EscapeDataString(item)));
            return IsToPX ? String.Format(@"http://{0}:{1}/topxvalidation/{2}", servername, port, newUri) : String.Format(@"http://{0}:{1}/mdtovalidation/{2}", servername, port, newUri);
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.Exception">Failed to request data! Status code not equals 200.</exception>
        /// <exception cref="System.ApplicationException">Metadata validation request failed!</exception>
        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start validate .metadata files.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

            var anyMessages = new List<String>();
            bool isSucces = false;
            var validation = new List<MetadataValidationItem>();
            try
            {
                base.Execute();

                string sessionFolder = Path.Combine(ApplicationSettings.DataFolderName, SessionGuid.ToString());
                string[] metadatas = IsToPX ? Directory.GetFiles(sessionFolder, "*.metadata", SearchOption.AllDirectories) : Directory.GetFiles(sessionFolder, "*.xml", SearchOption.AllDirectories).Where(item => item.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase)).ToArray();

                eventModel.Summary.Processed = metadatas.Count();

                foreach (string file in metadatas)
                {
                    Logger.LogInformation("Metadata validatie : {0}", file);
                    var schemaResult = ValidateSchema(file);
                    ValidateNonEmptyStringValues(file, schemaResult);

                    if (this.IsMDTO)
                    {
                        ValidateMetBeperkingenLijstAuteursWet1995(file, schemaResult);
                        ValidateBeperkingGebruikTermijn(file, schemaResult);
                        ValidateVoorwaardelijkeControleBeperkingGebruik(file, schemaResult);
                        //2
                        ValidateVoorwaardelijkeControleElementDekkingInTijd(file, schemaResult);
                    }
                    if (this.IsToPX)
                    {
                        ValidateOpenbaarheidRegels(file, schemaResult);
                        ValidateOpenbaarheidDatumOfPeriodeRegels(file, schemaResult);
                        //2
                        ValidateVoorwaardelijkeControleSubelementenBeginEindDekkingInTijd(file, schemaResult);
                    }
                    //3
                    ValidateOmvang(file, schemaResult);

                    validation.Add(schemaResult);

                    OnTrigger(new PreingestEventArgs { Description = String.Format("Processing file '{0}'", file), Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });
                }

                eventModel.Summary.Accepted = validation.Where(item => item.IsValidated && item.IsConfirmSchema).Count();
                eventModel.Summary.Rejected = validation.Where(item => !item.IsValidated || !item.IsConfirmSchema).Count();
                eventModel.ActionData = validation.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSucces = true;
            }
            catch (Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "An exception occured in metadata validation!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured in metadata validation!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                //eventModel.Summary.Processed = 0;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in metadata validation!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Validation is done!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        /// <summary>
        /// Validates the non empty string values. ToPX and MDTO metadata version
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateNonEmptyStringValues(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(file);

                var nsmgr = new XmlNamespaceManager(xml.NameTable);
                if (this.IsMDTO)
                    nsmgr.AddNamespace("m", "https://www.nationaalarchief.nl/mdto");
                if (this.IsToPX)
                    nsmgr.AddNamespace("t", "http://www.nationaalarchief.nl/ToPX/v2.3");

                //XmlNodeList xNodeList = this.IsToPX ? xml.SelectNodes("/t:ToPX//*/text()", nsmgr) : xml.SelectNodes("/m:MDTO//*/text()", nsmgr);
                XmlNodeList xNodeList = this.IsToPX ? xml.SelectNodes("/t:ToPX//*[not(.//text()[normalize-space()])]", nsmgr) : xml.SelectNodes("/m:MDTO//*[not(.//text()[normalize-space()])]", nsmgr);
                foreach (XmlNode xNode in xNodeList)
                {
                    string text = xNode.InnerText;
                    string name = xNode.Name;

                    if (String.IsNullOrEmpty(text))
                    {
                        var findings = schemaResult.ErrorMessages.ToList();
                        findings.Add(String.Format("Melding: Lege element gevonden, element: {0} | text: {1}", name, text));
                        schemaResult.ErrorMessages = findings.ToArray();
                    }
                    else
                    {
                        bool anyControlCharInText = text.Any(s => char.IsControl(s));
                        if (anyControlCharInText)
                        {
                            var findings = schemaResult.ErrorMessages.ToList();
                            findings.Add(String.Format("Melding: control karakter(s) in de tekst gevonden, element: {0} | text: {1}", name, text));
                            schemaResult.ErrorMessages = findings.ToArray();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateNonEmptyStringValues') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateNonEmptyStringValues') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        /// <summary>
        /// Validates the bepering gebruik termijn.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        /// <exception cref="System.ApplicationException"></exception>
        private void ValidateBeperkingGebruikTermijn(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                //parse to xml to see if is valid XML
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "informatieobject") == null)
                    return;//if bestandsType, return immediate

                Entities.MDTO.v1_0.mdtoType mdto = DeserializerHelper.DeSerializeObject<Entities.MDTO.v1_0.mdtoType>(File.ReadAllText(file));
                var informatieobject = mdto.Item as Entities.MDTO.v1_0.informatieobjectType;
                if (informatieobject == null)
                    throw new ApplicationException(String.Format("Omzetten naar informatieobject type niet gelukt. Valideren van beperkingGebruikTermijn is niet gelukt voor metadata '{0}'", file));

                if (informatieobject.beperkingGebruik == null)
                    return;

                informatieobject.beperkingGebruik.ToList().ForEach(item =>
                {
                    if (item.beperkingGebruikTermijn == null)
                        return;

                    DateTime? termijnStartdatumLooptijd = item.beperkingGebruikTermijn.termijnStartdatumLooptijd;
                    string termijnLooptijd = item.beperkingGebruikTermijn.termijnLooptijd;
                    string termijnEinddatum = item.beperkingGebruikTermijn.termijnEinddatum;

                    DateTime? parseOut = ParseTermijnDatum(termijnEinddatum);

                    DateTime? dtTermijnStartdatumLooptijd = (termijnStartdatumLooptijd.Value == DateTime.MinValue) ? null : termijnStartdatumLooptijd.Value;
                    TimeSpan? tsTermijnLooptijd = String.IsNullOrEmpty(termijnLooptijd) ? null : XmlConvert.ToTimeSpan(termijnLooptijd);
                    DateTime? dtTermijnEinddatum = String.IsNullOrEmpty(termijnEinddatum) ? null : parseOut;

                    if (dtTermijnStartdatumLooptijd.HasValue && tsTermijnLooptijd.HasValue && dtTermijnEinddatum.HasValue)
                    {
                        //calculeren
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        DateTime isdtTermijnStartdatumLooptijd = dtTermijnStartdatumLooptijd.Value.Add(tsTermijnLooptijd.Value);
                        int result = DateTime.Compare(dtTermijnEinddatum.Value, isdtTermijnStartdatumLooptijd);

                        if (result < 0)
                            currentErrorMessages.Add("Meldig: termijnEinddatum is eerder dan (termijnStartdatumLooptijd + termijnLooptijd)"); //relationship = "is eerder dan";
                        else if (result == 0)
                            currentErrorMessages.Add("Meldig: termijnEinddatum is gelijk (termijnStartdatumLooptijd + termijnLooptijd)");//relationship = "is gelijk aan";
                        else
                            currentErrorMessages.Add("Meldig: termijnEinddatum is later dan (termijnStartdatumLooptijd + termijnLooptijd)");  //relationship = "is later dan";
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    if (dtTermijnStartdatumLooptijd.HasValue && !tsTermijnLooptijd.HasValue && dtTermijnEinddatum.HasValue)
                    {
                        /**
                         * 
                         * Check:
                            termijnEinddatum => termijnStartdatum
                            Indien fout: termijnEinddatum is eerder dan termijnStartdatum

                            Melding die ook moet worden gegeven:
                            Er is geen waarde opgegeven voor het element <termijnLooptijd>,  er is wel een <termijnStartdatum>  en <termijnEinddatum>
                         */
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        int result = DateTime.Compare(dtTermijnStartdatumLooptijd.Value, dtTermijnEinddatum.Value);

                        if (result < 0)
                        {
                            //Uitgeschakeld bevinding #4 uit test 10-05-2022 op verzoek van Mark. 
                            //currentErrorMessages.Add("Melding: termijnStartdatumLooptijd is eerder dan termijnEinddatum"); //relationship = "is eerder dan";
                        }
                        else if (result == 0)
                        {
                            currentErrorMessages.Add("Melding: termijnStartdatumLooptijd is gelijk termijnEinddatum");//relationship = "is gelijk aan";
                        }
                        else
                        {
                            currentErrorMessages.Add("Melding: termijnStartdatumLooptijd is later dan termijnEinddatum");  //relationship = "is later dan";
                        }

                        currentErrorMessages.Add("Melding: er is geen waarde opgegeven voor het element 'termijnLooptijd',  er is wel een 'termijnStartdatumLooptijd' en 'termijnEinddatum'");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    if (!dtTermijnStartdatumLooptijd.HasValue && !tsTermijnLooptijd.HasValue && dtTermijnEinddatum.HasValue)
                    {
                        return;
                    }
                    if (!dtTermijnStartdatumLooptijd.HasValue && tsTermijnLooptijd.HasValue && !dtTermijnEinddatum.HasValue)
                    {
                        //Melding: de elementen 'termijnStartdatum en 'termijnEinddatum' ontbreken, er is wel een 'termijnLooptijd'
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add("Melding: de elementen 'termijnStartdatumLooptijd' en 'termijnEinddatum' ontbreken, er is wel een 'termijnLooptijd'");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    if (dtTermijnStartdatumLooptijd.HasValue && tsTermijnLooptijd.HasValue && !dtTermijnEinddatum.HasValue)
                    {
                        //Melding: 'termijnEinddatum' heeft geen waarde, maar 'termijnStartdatum' en 'termijnLooptijd' hebben geldige waarden.
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add("Melding: 'termijnEinddatum' heeft geen waarde, maar 'termijnStartdatumLooptijd' en 'termijnLooptijd' hebben geldige waarden");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    if (!dtTermijnStartdatumLooptijd.HasValue && !tsTermijnLooptijd.HasValue && dtTermijnEinddatum.HasValue)
                    {
                        return;
                    }
                    if (!dtTermijnStartdatumLooptijd.HasValue && !tsTermijnLooptijd.HasValue && !dtTermijnEinddatum.HasValue)
                    {
                        return;
                    }
                });

            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateBeperkingGebruikTermijn') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateBeperkingGebruikTermijn') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        private DateTime? ParseTermijnDatum(String termijnDatum)
        {
            if (String.IsNullOrEmpty(termijnDatum))
                return null;
            // xsd:gYear
            // xsd:gYearMonth
            // xsd:date
            DateTime parseOut = DateTime.MinValue;
            bool isSuccess = DateTime.TryParse(termijnDatum, out parseOut);
            if (isSuccess)
                return parseOut;

            string yearFormat = @"^\d{4}$";
            string yeahMonthFormat = @"^\d{4}-\d{2}$";

            bool isYear = Regex.IsMatch(termijnDatum, yearFormat);
            bool isYearMonth = Regex.IsMatch(termijnDatum, yeahMonthFormat);

            if (isYear)
            {
                int year = Int32.Parse(termijnDatum);
                int lastDay = DateTime.DaysInMonth(year, 12);
                DateTime yearOnly = new DateTime(year, 12, lastDay);//max it out in a year;
                return yearOnly;
            }

            if (isYearMonth)
            {
                var splitResultList = termijnDatum.Split("-", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                int year = Int32.Parse(splitResultList.First());
                int month = Int32.Parse(splitResultList.Last());
                int lastDay = DateTime.DaysInMonth(year, month);

                DateTime yearOnly = new DateTime(year, month, lastDay);//max it out in a year/month;
                return yearOnly;
            }

            return null;
        }

        /// <summary>
        /// Validates the openbaarheid rule. Only for ToPX.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateOpenbaarheidRegels(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument bestandXml = XDocument.Load(file);
                XNamespace bns = bestandXml.Root.GetDefaultNamespace();
                if (bestandXml.Root.Elements().First().Name.LocalName == "bestand")
                {
                    List<String> messages = new List<string>();
                    messages.AddRange(schemaResult.ErrorMessages);

                    FileInfo currentMetadataFile = new FileInfo(file);
                    DirectoryInfo currentMetadataFolder = currentMetadataFile.Directory;
                    String currentMetadataFolderName = currentMetadataFolder.Name;
                    var folderMetadataFile = currentMetadataFolder.GetFiles(String.Format("{0}.metadata", currentMetadataFolderName)).FirstOrDefault();
                    if (folderMetadataFile == null)
                    {
                        messages.AddRange(new string[] {
                                    String.Format("Kan het bovenliggende Dossier of Record metadata bestand met de naam '{0}.metadata' niet vinden in de map '{0}'", currentMetadataFolderName),
                                    String.Format("Controleren op openbaarheid is niet gelukt voor {0}.", file)});
                    }
                    else
                    {
                        //if upper parent metadata is Dossier, need to check openbaarheid in bestand metadata
                        //if pupper parent metadata is Record, just skip
                        XDocument recordOrDossierXml = XDocument.Load(folderMetadataFile.FullName);
                        XNamespace rdns = recordOrDossierXml.Root.GetDefaultNamespace();
                        if (recordOrDossierXml.Root.Element(rdns + "aggregatie").Element(rdns + "aggregatieniveau").Value.Equals("Dossier", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var openbaarheid = bestandXml.Root.Element(bns + "bestand").Element(bns + "openbaarheid");
                            if (openbaarheid == null)
                            {
                                messages.AddRange(new string[] { String.Format("Bovenliggende metadata bestand heeft een aggregatieniveau 'Dossier'. Op bestandsniveau wordt dan 'openbaarheid' element verwacht. Element is niet gevonden") });
                            }
                            else if (openbaarheid.Element(bns + "omschrijvingBeperkingen") == null)
                            {
                                messages.AddRange(new string[] { String.Format("Bovenliggende metadata bestand heeft een aggregatieniveau 'Dossier'. Op bestandsniveau wordt dan 'openbaarheid' element verwacht. Element 'openbaarheid' gevonden maar niet element 'omschrijvingBeperkingen'") });
                            }
                            else
                            {
                                string omschrijvingBeperkingen = openbaarheid.Element(bns + "omschrijvingBeperkingen").Value;
                                Match match = Regex.Match(omschrijvingBeperkingen, "^(Openbaar|Niet openbaar|Beperkt openbaar)$", RegexOptions.ECMAScript);
                                if (!match.Success)
                                {
                                    messages.AddRange(new string[] { String.Format("Onjuiste waarde voor element 'omschrijvingBeperkingen' gevonden. Gevonden waarde = '{1}', verwachte waarde = 'Openbaar' of 'Niet openbaar' of 'Beperkt openbaar' in {0}", file, omschrijvingBeperkingen) });
                                }
                            }
                        }
                    }

                    schemaResult.IsConfirmSchema = !(messages.Count() > 0);
                    schemaResult.ErrorMessages = messages.ToArray();
                }
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateOpenbaarheidRegels') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateOpenbaarheidRegels') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        /// <summary>
        /// Validates the openbaarheid rule with date. Only for ToPX.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateOpenbaarheidDatumOfPeriodeRegels(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument metadataXml = XDocument.Load(file);
                XNamespace ns = metadataXml.Root.GetDefaultNamespace();

                if (metadataXml.Root.Elements().First().Name.LocalName == "bestand")
                    return;

                var openbaarheid = metadataXml.Root.Element(ns + "aggregatie").Element(ns + "openbaarheid");

                if (openbaarheid == null)
                    return;

                List<String> messages = new List<string>();
                messages.AddRange(schemaResult.ErrorMessages);

                var datumOfPeriode = openbaarheid.Element(ns + "datumOfPeriode");

                if (datumOfPeriode == null)
                    messages.AddRange(new string[] { "Element datumOfPeriode niet gevonden! Element is verplicht! Verdere controle uitvoeren is niet mogelijk." });

                if (datumOfPeriode != null)
                {
                    var periode = datumOfPeriode.Element(ns + "periode");
                    if (periode == null)
                        return;

                    var begin = periode.Element(ns + "begin");
                    var eind = periode.Element(ns + "eind");

                    if (begin == null)
                        messages.AddRange(new string[] { "Het element 'openbaarheid/datumOfPeriode/periode' moet zowel het subelement 'begin' als 'eind' hebben." });
                    if (eind == null)
                        messages.AddRange(new string[] { "Het element 'openbaarheid/datumOfPeriode/periode' moet zowel het subelement 'begin' als 'eind' hebben." });

                    if (begin != null && eind != null)
                    {
                        var filterList = new string[] { "periode", "begin", "eind" };
                        var periodeTypeList = periode.Descendants().SelectMany(item => item.Descendants()).Select(item => item.Name.LocalName).ToList();
                        var exceptList = periodeTypeList.Except(filterList);

                        int countBeginEindType = exceptList.Distinct().Count();//moet 1 zijn, meer dan 1 zijn dus verschillende types en niet distinct
                        if (countBeginEindType > 1)
                            messages.Add(String.Format("De elementen 'begin' en 'eind' voor het element 'openbaarheid/datumOfPeriode/periode' zijn niet van hetzelfde datatype! Gevonden verschillen {0}.", String.Join(", ", exceptList.ToArray())));

                        //verschillende type zullen in onderstaande condities niet voldoen vanwege && in de conditie
                        var beginJaar = begin.Element(ns + "jaar");
                        var eindJaar = eind.Element(ns + "jaar");

                        if (beginJaar != null && eindJaar != null)
                        {
                            int eindJaarWaarde = 0;
                            int beginJaarWaarde = 0;

                            bool eindParse = Int32.TryParse(eindJaar.Value, out eindJaarWaarde);
                            bool beginParse = Int32.TryParse(beginJaar.Value, out beginJaarWaarde);

                            if (!beginParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'begin' is niet gelukt." });
                            if (!eindParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'eind' is niet gelukt." });
                            if (eindJaarWaarde < beginJaarWaarde)
                                messages.AddRange(new string[] { "De waarde voor het element 'eind' moet groter of gelijk zijn aan de waarde van het element 'begin' voor het element voor het element 'openbaarheid/datumOfPeriode'" });
                        }

                        var beginDatum = begin.Element(ns + "datum");
                        var eindDatum = eind.Element(ns + "datum");

                        if (beginDatum != null && eindDatum != null)
                        {
                            DateTime eindDatumWaarde = DateTime.MinValue;
                            DateTime beginDatumWaarde = DateTime.MinValue;

                            bool eindParse = DateTime.TryParse(eindDatum.Value, out eindDatumWaarde);
                            bool beginParse = DateTime.TryParse(beginDatum.Value, out beginDatumWaarde);

                            if (!beginParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'begin' is niet gelukt." });
                            if (!eindParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'eind' is niet gelukt." });

                            int result = DateTime.Compare(eindDatumWaarde, beginDatumWaarde);
                            if (result < 0)
                                messages.Add("De waarde voor het element 'eind' moet groter of gelijk zijn aan de waarde van het element 'begin' voor het element voor het element 'openbaarheid/datumOfPeriode'");
                            //else if (result == 0)
                            //    messages.Add("Waarde van einddatum is gelijk aan waarde van beginDatum.");
                            //else
                            //    messages.Add("Waarde van einddatum is later dan waarde van beginDatum.");
                        }

                        var beginDatumEnTijd = begin.Element(ns + "datumEnTijd");
                        var eindDatumEnTijd = eind.Element(ns + "datumEnTijd");

                        if (beginDatumEnTijd != null && eindDatumEnTijd != null)
                        {
                            DateTime eindDatumEnTijdWaarde = DateTime.MinValue;
                            DateTime beginDatumEnTijdWaarde = DateTime.MinValue;

                            bool eindParse = DateTime.TryParse(eindDatum.Value, out eindDatumEnTijdWaarde);
                            bool beginParse = DateTime.TryParse(beginDatum.Value, out beginDatumEnTijdWaarde);

                            if (!beginParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'begin' is niet gelukt." });
                            if (!eindParse)
                                messages.AddRange(new string[] { "Converteren van waarde in element 'eind' is niet gelukt." });

                            int result = DateTime.Compare(eindDatumEnTijdWaarde, beginDatumEnTijdWaarde);
                            if (result < 0)
                                messages.Add("De waarde voor het element 'eind' moet groter of gelijk zijn aan de waarde van het element 'begin' voor het element voor het element 'openbaarheid/datumOfPeriode'");
                            //else if (result == 0)
                            //    messages.Add("Waarde van einddatumEnTijd is gelijk aan waarde van beginDatumEnTijd.");
                            //else
                            //    messages.Add("Waarde van einddatumEnTijd is later dan waarde van beginDatumEnTijd.");
                        }
                    }
                }
                schemaResult.IsConfirmSchema = !(messages.Count() > 0);
                schemaResult.ErrorMessages = messages.ToArray();
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateOpenbaarheidDatumOfPeriodeRegels') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateOpenbaarheidDatumOfPeriodeRegels') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        /// <summary>
        /// Validates metadata with XSD schema. For ToPX and MDTO.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to request data! Status code not equals 200.</exception>
        /// <exception cref="System.ApplicationException">Metadata validation request failed!</exception>
        private MetadataValidationItem ValidateSchema(string file)
        {
            MetadataValidationItem validation = new MetadataValidationItem();

            string requestUri = GetProcessingUrl(ApplicationSettings.XslWebServerName, ApplicationSettings.XslWebServerPort, file);
            var errorMessages = new List<String>();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var httpResponse = client.GetAsync(requestUri).Result;

                    if (!httpResponse.IsSuccessStatusCode)
                        throw new Exception("Failed to request data! Status code not equals 200.");

                    var rootError = JsonConvert.DeserializeObject<Root>(httpResponse.Content.ReadAsStringAsync().Result);
                    if (rootError == null)
                        throw new ApplicationException("Metadata validation request failed!");

                    //schema+ validation
                    if (rootError.SchematronValidationReport != null && rootError.SchematronValidationReport.errors != null
                        && rootError.SchematronValidationReport.errors.Count > 0)
                    {
                        var messages = rootError.SchematronValidationReport.errors.Select(item => item.message).ToArray();
                        errorMessages.AddRange(messages);
                    }
                    //default schema validation
                    if (rootError.SchemaValidationReport != null && rootError.SchemaValidationReport.errors != null
                        && rootError.SchemaValidationReport.errors.Count > 0)
                    {
                        var messages = rootError.SchemaValidationReport.errors.Select(item => String.Concat(item.message, ", ", String.Format("Line: {0}, col: {1}", item.line, item.col))).ToArray();
                        errorMessages.AddRange(messages);
                    }

                    if (errorMessages.Count > 0)
                    {
                        //error
                        validation = new MetadataValidationItem
                        {
                            IsValidated = true,
                            IsConfirmSchema = false,
                            IsConfirmBegrippenLijst = null,
                            ErrorMessages = errorMessages.ToArray(),
                            MetadataFilename = file,
                            RequestUri = Uri.UnescapeDataString(requestUri)
                        };
                    }
                    else
                    {
                        //no error
                        validation = new MetadataValidationItem
                        {
                            IsValidated = true,
                            IsConfirmSchema = true,
                            IsConfirmBegrippenLijst = null,
                            ErrorMessages = new string[0],
                            MetadataFilename = file,
                            RequestUri = Uri.UnescapeDataString(requestUri)
                        };
                    }

                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, String.Format("Exception occured in metadata validation with request '{0}' for metadata file '{1}'!", Uri.UnescapeDataString(requestUri), file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation with request '{0}' for metadata file '{1}'!", Uri.UnescapeDataString(requestUri), file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                //error
                validation = new MetadataValidationItem
                {
                    IsValidated = false,
                    IsConfirmSchema = false,
                    IsConfirmBegrippenLijst = null,
                    ErrorMessages = errorMessages.ToArray(),
                    MetadataFilename = file,
                    RequestUri = requestUri
                };
            }
            return validation;
        }

        /// <summary>
        /// Validates the with beperkingen lijst auteurs wet1995. Only for MDTO.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateMetBeperkingenLijstAuteursWet1995(string file, MetadataValidationItem schemaResult)
        {
            BeperkingCategorie categorie = BeperkingCategorie.OPENBAARHEID_ARCHIEFWET_1995;
            BeperkingResult validation = new BeperkingResult() { IsSuccess = null, Results = new string[0] { } };
            var errorMessages = new List<String>();

            string url = String.Format("http://{0}:{1}/begrippenlijst/{2}", ApplicationSettings.UtilitiesServerName, ApplicationSettings.UtilitiesServerPort, categorie.ToString());
            try
            {
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "informatieobject") == null)
                {
                    return;//if bestandsType, return immediate
                }

                var countBeperkingGebruik = xmlDocument.Root.Element(ns + "informatieobject").Elements(ns + "beperkingGebruik").Select(item => new Beperking
                {
                    BegripCode = (item.Element(ns + "beperkingGebruikType") == null)
                    ? String.Empty : item.Element(ns + "beperkingGebruikType").Element(ns + "begripCode") == null
                    ? String.Empty : item.Element(ns + "beperkingGebruikType").Element(ns + "begripCode").Value,
                    BegripLabel = (item.Element(ns + "beperkingGebruikType") == null)
                    ? String.Empty : item.Element(ns + "beperkingGebruikType").Element(ns + "begripLabel") == null
                    ? String.Empty : item.Element(ns + "beperkingGebruikType").Element(ns + "begripLabel").Value,
                }).ToList();

                if (countBeperkingGebruik.Count == 0)
                {
                    return;//if zero return immediate
                }

                List<Beperking> beperkingList = new List<Beperking>();
                //load list form MS
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    var result = JsonConvert.DeserializeObject<Beperking[]>(response.Content.ReadAsStringAsync().Result);
                    beperkingList.AddRange(result);
                }

                //var resultList = countBeperkingGebruik.Select(item => new { Contains = beperkingList.Contains(item), Item = item, Results = item.GetEqualityMessageResults() }).ToList();
                var resultList = countBeperkingGebruik.Select(beperking => beperking.IsItemValid(beperkingList));

                validation = new BeperkingResult
                {
                    IsSuccess = (resultList.Count(item => item.IsSuccess == false) == 0), //no false result means item exists in beperkingLijst
                    Results = resultList.SelectMany(item => item.Results).ToArray(),
                };// String.Format("Controle op element 'beperkingGebruik' is {0}. {1}", item.IsSuccess.HasValue && item.IsSuccess.Value ? "succesvol" : "niet succesvol"
            }
            catch (Exception e)
            {
                Logger.LogError(e, String.Format("Exception occured in metadata validation with request '{0}' for metadata file '{1}'!", url, file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation with request '{0}' for metadata file '{1}'!", url, file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                //error
                validation = new BeperkingResult
                {
                    IsSuccess = false,
                    Results = errorMessages.ToArray(),
                };
            }
            finally
            {
                schemaResult.IsConfirmBegrippenLijst = validation.IsSuccess;
                List<String> messages = schemaResult.ErrorMessages.ToList();
                messages.AddRange(validation.Results);
                schemaResult.ErrorMessages = messages.ToArray();
            }
        }

        /// <summary>
        /// Validates beperkingGebruik/beperkingTermijn. Only for MDTO.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateVoorwaardelijkeControleBeperkingGebruik(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "informatieobject") == null)
                {
                    return;//if bestandsType, return immediate
                }

                Entities.MDTO.v1_0.mdtoType mdto = DeserializerHelper.DeSerializeObject<Entities.MDTO.v1_0.mdtoType>(File.ReadAllText(file));
                var informatieobject = mdto.Item as Entities.MDTO.v1_0.informatieobjectType;
                if (informatieobject == null)
                    throw new ApplicationException(String.Format("Omzetten naar informatieobject type niet gelukt. Valideren van beperkingGebruikTermijn is niet gelukt voor metadata '{0}'", file));

                if (informatieobject.beperkingGebruik == null)
                    return;

                informatieobject.beperkingGebruik.ToList().ForEach(item =>
                {
                    if (item.beperkingGebruikTermijn == null)
                        return;

                    bool openbaar = item.beperkingGebruikType.begripLabel.Contains("openbaar", StringComparison.InvariantCultureIgnoreCase);
                    bool beperkt = item.beperkingGebruikType.begripLabel.Contains("beperkt", StringComparison.InvariantCultureIgnoreCase);

                    //alleen openbaar
                    if (openbaar && !beperkt)
                    {
#pragma warning disable
                        //DateTime is nooit een NULL. Hoog waarschijnlijk al met schema validatie al gedetecteerd....
                        if (item.beperkingGebruikTermijn.termijnStartdatumLooptijd == null || item.beperkingGebruikTermijn.termijnStartdatumLooptijd == DateTime.MinValue)
#pragma warning enable
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add("Het subelement 'beperkingGebruik/beperkingGebruikTermijn/termijnStartdatumLooptijd' heeft geen waarde.");
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                    }
                    //beperkt openbaar
                    if (openbaar && beperkt)
                    {
#pragma warning disable

                        //DateTime is nooit een NULL. Hoog waarschijnlijk al met schema validatie al gedetecteerd....
                        bool startHasValue = item.beperkingGebruikTermijn.termijnStartdatumLooptijd != null || item.beperkingGebruikTermijn.termijnStartdatumLooptijd > DateTime.MinValue;
#pragma warning enable
                        //xsd:gYear xsd:gYearMonth xsd:date
                        bool eindHasValue = !String.IsNullOrEmpty(item.beperkingGebruikTermijn.termijnEinddatum);

                        //one of both is missing then....
                        if (!eindHasValue)
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add("Het subelement 'beperkingGebruik/beperkingGebruikTermijn/termijnEinddatum' moet een waarde hebben.");
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                        if (!startHasValue)
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add("Het subelement 'beperkingGebruik/beperkingGebruikTermijn/termijnStartdatumLooptijd' moet een waarde hebben.");
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                        if (startHasValue && eindHasValue)
                        {
                            DateTime parseOut = DateTime.MinValue;
                            string yearFormat = @"^\d{4}$";
                            string yeahMonthFormat = @"^\d{4}-\d{2}$";

                            bool isDate = DateTime.TryParse(item.beperkingGebruikTermijn.termijnEinddatum, out parseOut);
                            bool isYear = Regex.IsMatch(item.beperkingGebruikTermijn.termijnEinddatum, yearFormat);
                            bool isYearMonth = Regex.IsMatch(item.beperkingGebruikTermijn.termijnEinddatum, yeahMonthFormat);

                            if (!isDate)
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add("Het subelement 'beperkingGebruik/beperkingGebruikTermijn/termijnStartdatumLooptijd' als 'beperkingGebruik/beperkingGebruikTermijn/termijnEinddatum' zijn niet van hetzelfde datatype.");
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                            else
                            {
                                int result = DateTime.Compare(parseOut, item.beperkingGebruikTermijn.termijnStartdatumLooptijd);
                                if (result < 0)
                                {
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add("Het subelement 'beperkingGebruik/beperkingGebruikTermijn/termijnEinddatum' moet groter of gelijk zijn aan de waard van het element 'beperkingGebruik/beperkingGebruikTermijn/termijnStartdatumLooptijd'.");
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                }
                            }
                        }
                    }

                });
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleBeperkingGebruik') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleBeperkingGebruik') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        /// <summary>
        /// Validates the dekking/InTijd rule with dates. Only for ToPX.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateVoorwaardelijkeControleSubelementenBeginEindDekkingInTijd(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "aggregatie") == null)
                {
                    return;//if bestandsType, return immediate
                }

                Entities.ToPX.v2_3_2.topxType topx = DeserializerHelper.DeSerializeObject<Entities.ToPX.v2_3_2.topxType>(File.ReadAllText(file));
                var aggregatie = topx.Item as Entities.ToPX.v2_3_2.aggregatieType;
                if (aggregatie == null)
                    throw new ApplicationException(String.Format("Omzetten naar aggregatie type niet gelukt. Valideren van 'begin/eind datum' in' dekkingInTijd' is niet gelukt voor metadata '{0}'.", file));

                bool archiefOrDossier = aggregatie.aggregatieniveau.Value == Entities.ToPX.v2_3_2.aggregatieAggregatieniveauType.Archief || aggregatie.aggregatieniveau.Value == Entities.ToPX.v2_3_2.aggregatieAggregatieniveauType.Dossier;

                if (archiefOrDossier)
                {
                    if (aggregatie.dekking == null)
                    {
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add("Het element 'dekking' is niet aanwezig.");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    else
                    {
                        aggregatie.dekking.ToList().ForEach(dekkingInTijdItem =>
                        {
                            if (dekkingInTijdItem.inTijd == null)
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add("Het element 'dekking/InTijd' is niet aanwezig.");
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                            else
                            {
                                bool isBegin = (dekkingInTijdItem.inTijd.begin == null);
                                bool isEind = (dekkingInTijdItem.inTijd.eind == null);
                                if (!isBegin && isEind)
                                {
                                    //wel begin, geen eind
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add(String.Format("Het subelement 'eind' ontbreekt voor het element 'dekking/inTijd'."));
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();

                                }
                                if (isBegin && !isEind)
                                {
                                    //geen begin, wel eind
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add(String.Format("Het subelement 'begin' ontbreekt voor het element 'dekking/inTijd'."));
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                }
                                if (isBegin && isEind)
                                {
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add(String.Format("Het element 'dekking/InTijd/begin' en 'dekking/InTijd/eind' zijn niet aanwezig."));
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                }
                                else
                                {
                                    DateTime beginParseDate = DateTime.MinValue;
                                    DateTime eindParseDate = DateTime.MinValue;

                                    if (dekkingInTijdItem.inTijd.eind == null || dekkingInTijdItem.inTijd.begin == null)
                                    {
                                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                        currentErrorMessages.Add("Waarde ontleden voor het element 'begin' of 'eind' in het element 'dekking/inTijd' niet gelukt.");
                                        if (dekkingInTijdItem.inTijd.eind == null)
                                            currentErrorMessages.Add("Element 'eind' in het element 'dekking/inTijd' is niet aanwezig.");
                                        if (dekkingInTijdItem.inTijd.begin == null)
                                            currentErrorMessages.Add("Element 'begin' in het element 'dekking/inTijd' is niet aanwezig.");
                                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                        return;
                                    }

                                    String beginDateWaarde = string.Empty;
                                    if (dekkingInTijdItem.inTijd.begin.Item is datumOfJaarTypeDatum)
                                        beginDateWaarde = (dekkingInTijdItem.inTijd.begin.Item as datumOfJaarTypeDatum).Value.ToString();
                                    if (dekkingInTijdItem.inTijd.begin.Item is datumOfJaarTypeDatumEnTijd)
                                        beginDateWaarde = (dekkingInTijdItem.inTijd.begin.Item as datumOfJaarTypeDatumEnTijd).Value.ToString();
                                    if (dekkingInTijdItem.inTijd.begin.Item is datumOfJaarTypeJaar)
                                        beginDateWaarde = (dekkingInTijdItem.inTijd.begin.Item as datumOfJaarTypeJaar).Value.ToString();

                                    String eindDateWaarde = String.Empty;
                                    if (dekkingInTijdItem.inTijd.eind.Item is datumOfJaarTypeDatum)
                                        eindDateWaarde = (dekkingInTijdItem.inTijd.eind.Item as datumOfJaarTypeDatum).Value.ToString();
                                    if (dekkingInTijdItem.inTijd.eind.Item is datumOfJaarTypeDatumEnTijd)
                                        eindDateWaarde = (dekkingInTijdItem.inTijd.eind.Item as datumOfJaarTypeDatumEnTijd).Value.ToString();
                                    if (dekkingInTijdItem.inTijd.eind.Item is datumOfJaarTypeJaar)
                                        eindDateWaarde = (dekkingInTijdItem.inTijd.eind.Item as datumOfJaarTypeJaar).Value.ToString();

                                    string yearFormat = @"^\d{4}$";
                                    string yeahMonthFormat = @"^\d{4}-\d{2}$";

                                    bool isDateBegin = DateTime.TryParse(beginDateWaarde, out beginParseDate);
                                    bool isYearBegin = Regex.IsMatch(beginDateWaarde, yearFormat);
                                    bool isYearMonthBegin = Regex.IsMatch(beginDateWaarde, yeahMonthFormat);

                                    bool isDateEind = DateTime.TryParse(eindDateWaarde, out eindParseDate);
                                    bool isYearEind = Regex.IsMatch(eindDateWaarde, yearFormat);
                                    bool isYearMonthEind = Regex.IsMatch(eindDateWaarde, yeahMonthFormat);

                                    if ((isDateBegin && isDateEind) || (isYearBegin && isYearEind) || (isYearMonthBegin && isYearMonthEind))
                                    {
                                        DateTime? beginDateTime = ParseTermijnDatum(beginDateWaarde);
                                        DateTime? eindDateTime = ParseTermijnDatum(eindDateWaarde);

                                        if (!beginDateTime.HasValue || !eindDateTime.HasValue)
                                        {
                                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                            currentErrorMessages.Add("Waarde ontleden voor het element 'begin' of 'eind' in het element 'dekking/inTijd' niet gelukt.");
                                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                        }
                                        else
                                        {
                                            int result = DateTime.Compare(eindDateTime.Value, beginDateTime.Value);
                                            if (result < 0)
                                            {
                                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                                currentErrorMessages.Add("De waarde voor het element 'eind' moet groter of gelijk zijn aan de waarde van het element 'begin' voor het element 'dekking/inTijd'.");
                                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                        currentErrorMessages.Add(String.Format("De elementen 'begin' en 'eind' voor het element 'dekking/inTijd' zijn niet van hetzelfde datatype."));
                                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                    }
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleSubelementenBeginEindDekkingInTijd') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleSubelementenBeginEindDekkingInTijd') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        /// <summary>
        /// Validates the dekkingInTijd rule with dates. Only for MDTO.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="schemaResult">The schema result.</param>
        private void ValidateVoorwaardelijkeControleElementDekkingInTijd(string file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "informatieobject") == null)
                {
                    return;//if bestandsType, return immediate
                }

                Entities.MDTO.v1_0.mdtoType mdto = DeserializerHelper.DeSerializeObject<Entities.MDTO.v1_0.mdtoType>(File.ReadAllText(file));
                var informatieobject = mdto.Item as Entities.MDTO.v1_0.informatieobjectType;
                if (informatieobject == null)
                    throw new ApplicationException(String.Format("Omzetten naar informatieobject type niet gelukt. Valideren van beperkingGebruikTermijn is niet gelukt voor metadata '{0}'", file));

                string expression = @"^(Archief$|Dossier$|Zaak$)";
                var label = informatieobject.aggregatieniveau.begripLabel.Trim();

                bool isValid = Regex.IsMatch(label, expression, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (isValid)
                {
                    if (informatieobject.dekkingInTijd != null)
                    {
                        string looptijdExpression = @"^(Looptijd$)";
                        int count = informatieobject.dekkingInTijd.Count(item => (item.dekkingInTijdType != null && !String.IsNullOrEmpty(item.dekkingInTijdType.begripLabel)) && Regex.IsMatch(item.dekkingInTijdType.begripLabel.Trim(), looptijdExpression, RegexOptions.IgnoreCase | RegexOptions.Singleline));
                        if (count < 1)
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add(@"Het element 'dekkingInTijd/begripLabel' met de waarde 'Looptijd' ontbreekt.");
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                        if (count > 1)
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add("Er mag maximaal 1 keer het element 'dekkingInTijd/begripLabel' met 'Looptijd' aanwezig zijn.");
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                        if (count == 1)
                        {
                            var dekkingInTijdGegevens = informatieobject.dekkingInTijd.FirstOrDefault(item => (item.dekkingInTijdType != null && !String.IsNullOrEmpty(item.dekkingInTijdType.begripLabel)) && Regex.IsMatch(item.dekkingInTijdType.begripLabel.Trim(), looptijdExpression, RegexOptions.IgnoreCase | RegexOptions.Singleline));

                            if (String.IsNullOrEmpty(dekkingInTijdGegevens.dekkingInTijdBegindatum))
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add("Waarde 'dekkingInTijdBegindatum' ontbreekt voor het element 'dekkingInTijd' i.c.m. de waarde 'Looptijd' voor het subelement 'dekkingInTijdType/begripLabel'");
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                            if (String.IsNullOrEmpty(dekkingInTijdGegevens.dekkingInTijdEinddatum))
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add("Waarde 'dekkingInTijdEinddatum' ontbreekt voor het element 'dekkingInTijd' i.c.m. de waarde 'Looptijd' voor het subelement 'dekkingInTijdType/begripLabel'");
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                            if (String.IsNullOrEmpty(dekkingInTijdGegevens.dekkingInTijdBegindatum) || String.IsNullOrEmpty(dekkingInTijdGegevens.dekkingInTijdEinddatum))
                                return;

                            DateTime beginParseDate = DateTime.MinValue;
                            DateTime eindParseDate = DateTime.MinValue;
                            String beginDateWaarde = dekkingInTijdGegevens.dekkingInTijdBegindatum;
                            String eindDateWaarde = dekkingInTijdGegevens.dekkingInTijdEinddatum;

                            string yearFormat = @"^\d{4}$";
                            string yeahMonthFormat = @"^\d{4}-\d{2}$";
                            string yeahMonthDayFormat = @"^\d{4}-\d{2}-\d{2}$";

                            bool isDateBegin = DateTime.TryParse(beginDateWaarde, out beginParseDate);
                            bool isYearBegin = String.IsNullOrEmpty(beginDateWaarde) ? false : Regex.IsMatch(beginDateWaarde, yearFormat);
                            bool isYearMonthBegin = String.IsNullOrEmpty(beginDateWaarde) ? false : Regex.IsMatch(beginDateWaarde, yeahMonthFormat);

                            bool isDateEind = DateTime.TryParse(eindDateWaarde, out eindParseDate);
                            bool isYearEind = String.IsNullOrEmpty(eindDateWaarde) ? false : Regex.IsMatch(eindDateWaarde, yearFormat);
                            bool isYearMonthEind = String.IsNullOrEmpty(eindDateWaarde) ? false : Regex.IsMatch(eindDateWaarde, yeahMonthFormat);

                            if (!(Regex.IsMatch(beginDateWaarde, yearFormat) && Regex.IsMatch(eindDateWaarde, yearFormat)))
                                if (!(Regex.IsMatch(beginDateWaarde, yeahMonthFormat) && Regex.IsMatch(eindDateWaarde, yeahMonthFormat)))
                                    if (!(Regex.IsMatch(beginDateWaarde, yeahMonthDayFormat) && Regex.IsMatch(eindDateWaarde, yeahMonthDayFormat)))
                                    {
                                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                        currentErrorMessages.Add("Datum notatie voor dekkingInTijdBegindatum en dekkingInTijdEinddatum komen niet overeen.");
                                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                        return;
                                    }

                            if ((isDateBegin && isDateEind) || (isYearBegin && isYearEind) || (isYearMonthBegin && isYearMonthEind))
                            {
                                DateTime? beginDateTime = ParseTermijnDatum(beginDateWaarde);
                                DateTime? eindDateTime = ParseTermijnDatum(eindDateWaarde);

                                if (!beginDateTime.HasValue || !eindDateTime.HasValue)
                                {
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add("Waarde ontleden voor het element 'dekkingInTijdBegindatum' of 'dekkingInTijdEinddatum' in het element 'dekkingInTijd' niet gelukt.");
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                }
                                else
                                {
                                    int result = DateTime.Compare(eindDateTime.Value, beginDateTime.Value);
                                    if (result < 0)
                                    {
                                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                        currentErrorMessages.Add("Voor het element 'dekkingInTijd' met de waarde 'Looptijd' voor het subelement 'begripLabel' moet 'dekkingInTijdEinddatum' groter of gelijk zijn aan 'dekkingInTijdBegindatum'.");
                                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                    }
                                }
                            }
                            else
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add("Voor het element 'dekkingInTijd' met de waarde 'Looptijd' voor het subelement 'begripLabel' zijn de elementen 'dekkingInTijdBegindatum' en 'dekkingInTijdEinddatum' niet van hetzelfde datatype.");
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                        }
                    }
                    else
                    {
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add(@"Het element 'dekkingInTijd' ontbreekt. Het element 'dekkingInTijd/begripLabel' met de waarde 'Looptijd' ontbreekt.");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleElementDekkingInTijd') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateVoorwaardelijkeControleElementDekkingInTijd') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        private void ValidateOmvang(String file, MetadataValidationItem schemaResult)
        {
            try
            {
                XDocument xmlDocument = XDocument.Load(file);
                var ns = xmlDocument.Root.GetDefaultNamespace();
                if (xmlDocument.Root.Element(ns + "bestand") == null)
                {
                    return;//if not bestandsType, return immediate
                }

                if (xmlDocument.Root.Name.LocalName == "ToPX")
                {
                    Entities.ToPX.v2_3_2.topxType topx = DeserializerHelper.DeSerializeObject<Entities.ToPX.v2_3_2.topxType>(File.ReadAllText(file));
                    var bestandObject = topx.Item as Entities.ToPX.v2_3_2.bestandType;

                    if (bestandObject.formaat != null)
                    {
                        bestandObject.formaat.ToList().ForEach(item =>
                        {
                            int i = -1;
                            bool parsed = Int32.TryParse(item.omvang.Value, out i);
                            if (!parsed)
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add(String.Format("Waarde ontleden in element 'omvang' met identificatiekenmerk '{0}' is niet gelukt.", item.identificatiekenmerk.Value));
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }

                            var physicalFile = file.Replace(".metadata", string.Empty);//should remove .metadata
                            FileInfo info = new FileInfo(physicalFile);
                            if (!info.Exists)
                            {
                                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                currentErrorMessages.Add(String.Format("Fysieke content bestand niet gevonden '{0}'. Omvang vergelijken is niet gelukt.", physicalFile));
                                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                            }
                            else
                            {
                                if (info.Length != i)
                                {
                                    var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                                    currentErrorMessages.Add(String.Format("Omvang in metadata komt niet overeen met de omvang van fysieke content bestand. Omvang (metadata) = {0}, Omvang (fysieke bestand) = {1}", i, info.Length));
                                    schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                                }
                            }
                        });
                    }
                }

                if (xmlDocument.Root.Name.LocalName == "MDTO")
                {
                    Entities.MDTO.v1_0.mdtoType mdto = DeserializerHelper.DeSerializeObject<Entities.MDTO.v1_0.mdtoType>(File.ReadAllText(file));
                    var bestandObject = mdto.Item as Entities.MDTO.v1_0.bestandType;

                    int i = -1;
                    bool parsed = Int32.TryParse(bestandObject.omvang, out i);
                    if (!parsed)
                    {
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add("Waarde ontleden in element 'omvang' is niet gelukt.");
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }

                    var physicalFile = file.Replace(".bestand.mdto.xml", string.Empty);//should remove .xml and .bestand.mdto
                    FileInfo info = new FileInfo(physicalFile);
                    if (!info.Exists)
                    {
                        var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                        currentErrorMessages.Add(String.Format("Fysieke content bestand niet gevonden '{0}'. Omvang vergelijken is niet gelukt.", physicalFile));
                        schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                    }
                    else
                    {
                        if (info.Length != i)
                        {
                            var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                            currentErrorMessages.Add(String.Format("Omvang in metadata komt niet overeen met de omvang van fysieke content bestand. Omvang (metadata) = {0}, Omvang (fysieke bestand) = {1}", i, info.Length));
                            schemaResult.ErrorMessages = currentErrorMessages.ToArray();
                        }
                    }

                }
            }
            catch (Exception e)
            {
                var errorMessages = new List<String>();
                Logger.LogError(e, String.Format("Exception occured in metadata validation ('ValidateOmvang') for metadata file '{0}'!", file));
                errorMessages.Clear();
                errorMessages.Add(String.Format("Exception occured in metadata validation ('ValidateOmvang') for metadata file '{0}'!", file));
                errorMessages.Add(e.Message);
                errorMessages.Add(e.StackTrace);

                var currentErrorMessages = schemaResult.ErrorMessages.ToList();
                currentErrorMessages.AddRange(errorMessages);
                //error
                schemaResult.ErrorMessages = currentErrorMessages.ToArray();
            }
        }

        internal enum BeperkingCategorie
        {
            INTELLECTUELE_EIGENDOM_CREATIVE_COMMONS_LICENTIES,
            INTELLECTUELE_EIGENDOM_DATABANKWET,
            INTELLECTUELE_EIGENDOM_RIGHTS_STATEMENTS,
            INTELLECTUELE_EIGENDOM_SOFTWARE_LICENTIES,
            INTELLECTUELE_EIGENDOM_WET_OP_DE_NABURIGE_RECHTEN,
            OPENBAARHEID_ARCHIEFWET_1995,
            OPENBAARHEID_ARCHIEFWET_2021,
            OPENBAARHEID_WET_OPEN_OVERHEID,
            PERSOONSGEGEVENS_AVG,
            TRIGGERS,
            VOORKEURSFORMATEN
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        internal class Beperking : IEquatable<Beperking>
        {
            [JsonProperty("begripCode")]
            public string BegripCode { get; set; }

            [JsonProperty("begripLabel")]
            public string BegripLabel { get; set; }

            [JsonProperty("definitie")]
            public string Definitie { get; set; }

            public bool Equals(Beperking other)
            {
                bool sameLabel = (this.BegripLabel.Equals(other.BegripLabel, StringComparison.Ordinal));
                bool sameCode = (this.BegripCode.Equals(other.BegripCode, StringComparison.Ordinal));

                return sameCode && sameLabel;
            }

            public BeperkingResult IsItemValid(List<Beperking> beperkingenLijst)
            {
                bool valid = false;
                StringBuilder sb = new StringBuilder();
                //if label is empty, result fails immediate
                if (String.IsNullOrEmpty(this.BegripLabel))
                {
                    sb.Append("Element 'begripLabel' is niet voorzien van een waarde.");
                    return new BeperkingResult { IsSuccess = false, Results = new string[] { sb.ToString() } };
                }
                //if label is not empty, but code does, check only label
                if (!String.IsNullOrEmpty(this.BegripLabel) && String.IsNullOrEmpty(this.BegripCode))
                {
                    var beperkingGebruik = beperkingenLijst.FirstOrDefault(item => item.BegripLabel.Equals(this.BegripLabel, StringComparison.Ordinal));
                    if (beperkingGebruik == null)
                    {
                        sb.Append(String.Format("Element begripLabel met waarde '{0}' niet gevonden in de begrippenlijst", this.BegripLabel));
                    }
                    else
                    {
                        sb.Append(String.Format("Element begripLabel met waarde '{0}' gevonden in de begrippenlijst", this.BegripLabel));
                    }
                    sb.Append("Maar element begripCode is niet aanwezig of voorzien van een waarde");
                    return new BeperkingResult { IsSuccess = false, Results = new string[] { sb.ToString() } };
                }
                //check both if not empty
                if (!String.IsNullOrEmpty(this.BegripLabel) && !String.IsNullOrEmpty(this.BegripCode))
                {
                    bool contains = beperkingenLijst.Contains(this);
                    if (!contains)
                        sb.Append(String.Format("Element begripLabel: '{0}' in combinatie met element begripCode '{1}' niet gevonden in de begrippenlijst", this.BegripLabel, this.BegripCode));
                    else
                        sb.Append(String.Format("Gevonden in de begrippenlijst: {0}", this.ToString()));

                    return new BeperkingResult { IsSuccess = contains, Results = new string[] { sb.ToString() } };
                }

                sb.Append(String.Format("Controle niet succesvol uitgevoerd: {0}", this.ToString()));
                return new BeperkingResult { IsSuccess = valid, Results = new string[] { sb.ToString() } }; ;
            }

            public override string ToString()
            {
                return String.Format("begripCode={0}, begripLabel={1}", this.BegripCode, this.BegripLabel);
            }
        }

        internal class BeperkingResult
        {
            public bool? IsSuccess { get; set; }
            public String[] Results { get; set; }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }
    }
}
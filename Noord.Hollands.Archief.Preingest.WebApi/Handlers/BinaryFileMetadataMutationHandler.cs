using CsvHelper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Handler for adding fixity type and value in ToPX (bestand) and MDTO (bestand)
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class BinaryFileMetadataMutationHandler : AbstractPreingestHandler, IDisposable
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryFileMetadataMutationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public BinaryFileMetadataMutationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }

        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }
        public String DroidCsvOutputLocation()
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

        public override void Execute()
        {
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            OnTrigger(new PreingestEventArgs { Description = "Start mutation.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });
            bool isSucces = false;
            try
            {
                base.Execute();
                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                if (collection == null)
                    throw new DirectoryNotFoundException(String.Format("Folder '{0}' not found!", TargetFolder));

                string droidCsvFile = DroidCsvOutputLocation();
                if (String.IsNullOrEmpty(droidCsvFile))
                    throw new FileNotFoundException("CSV file not found! Run DROID first.", droidCsvFile);

                PreingestEventArgs execEventArgs = new PreingestEventArgs { Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel };

                var start = DateTime.Now;
                Logger.LogInformation("Start mutation in '{0}'.", TargetFolder);
                Logger.LogInformation("Start time {0}", start);

                List<ActionDataItem> results = DoMutatePronomValueAndFixity(droidCsvFile);               

                eventModel.Summary.Processed = results.Count();
                eventModel.Summary.Accepted = results.Count(item => item.IsSuccess);
                eventModel.Summary.Rejected = results.Count(item => !item.IsSuccess); 
                
                eventModel.ActionData = results.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                var end = DateTime.Now;
                Logger.LogInformation("End of the mutation.");
                Logger.LogInformation("End time {0}", end);
                TimeSpan processTime = (TimeSpan)(end - start);
                Logger.LogInformation(String.Format("Processed in {0} ms.", processTime));

                isSucces = true;
            }
            catch (Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "Exception occured in mutation!");

                var anyMessages = new List<String>();
                anyMessages.Clear();
                anyMessages.Add("Exception occured in mutation!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = -1;
                eventModel.Summary.Rejected = -1;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in mutation!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Mutation is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }

        private List<ActionDataItem> DoMutatePronomValueAndFixity(String droidCsvFile)
        {
            List<ActionDataItem> actionDataList = new List<ActionDataItem>();

            using (var reader = new StreamReader(droidCsvFile))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    var filesByDroid = this.IsToPX ?
                        records.Where(item => item.TYPE == "File" && item.EXT != "metadata").Select(item => new DataItem
                        {
                            Location = item.FILE_PATH,
                            Name = item.NAME,
                            Extension = item.EXT,
                            FormatName = item.FORMAT_NAME,
                            FormatVersion = item.FORMAT_VERSION,
                            MimeType = item.MIME_TYPE,
                            Puid = item.PUID,
                            IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                        }).ToList() :
                        records.Where(item => item.TYPE == "File" && !item.NAME.EndsWith(".mdto.xml")).Select(item => new DataItem
                        {
                            Location = item.FILE_PATH,
                            Name = item.NAME,
                            Extension = item.EXT,
                            FormatName = item.FORMAT_NAME,
                            FormatVersion = item.FORMAT_VERSION,
                            MimeType = item.MIME_TYPE,
                            Puid = item.PUID,
                            IsExtensionMismatch = Boolean.Parse(item.EXTENSION_MISMATCH)
                        }).ToList();

                    filesByDroid.ForEach(droidFileItem =>
                    {
                        FileInfo fileInfo = new FileInfo(droidFileItem.Location);
                        var dataItem = new ActionDataItem
                        {
                            FileName = fileInfo.Name,
                            Location = fileInfo.DirectoryName,
                            IsSuccess = true,
                            FileSize = fileInfo.Length,
                            Message = ""
                        };
                        StringBuilder outputMessage = new StringBuilder();

                        if (!fileInfo.Exists)
                        {
                            outputMessage.Append(String.Format("Bestand niet gevonden: {0}", droidFileItem.Location));
                            dataItem.IsSuccess = false;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(droidFileItem.Puid))
                            {
                                outputMessage.Append(String.Format("DROID kan het bestand {0} niet classificeren. PUID waarde uit PRONOM register via DROID is leeg. Metadata-bestand is niet bijgewerkt", fileInfo.FullName));
                                dataItem.IsSuccess = false;
                            }
                            else
                            {
                                try
                                {
                                    if (this.IsMDTO)
                                    {
                                        string metadata = String.Format("{0}.bestand.mdto.xml", droidFileItem.Location);
                                        if (!File.Exists(metadata))
                                            throw new FileNotFoundException(String.Format("Metadata bestand '{0}' niet gevonden voor binaire bestand '{1}'", metadata, droidFileItem.Location));

                                        Entities.MDTO.v1_0.mdtoType mdto = DeserializerHelper.DeSerializeObject<Entities.MDTO.v1_0.mdtoType>(File.ReadAllText(metadata));
                                        UpdateMDTO(mdto, fileInfo, droidFileItem);
                                        SerializerHelper.SerializeObjectToXmlFile<Entities.MDTO.v1_0.mdtoType>(mdto, metadata);

                                        outputMessage.Append(String.Format("Bestand {0} bijgewerkt", fileInfo.FullName));
                                        dataItem.IsSuccess = true;
                                    }
                                    if (this.IsToPX)
                                    {
                                        string metadata = String.Format("{0}.metadata", droidFileItem.Location);
                                        if (!File.Exists(metadata))
                                            throw new FileNotFoundException(String.Format("Metadata bestand '{0}' niet gevonden voor binaire bestand '{1}'", metadata, droidFileItem.Location));

                                        Entities.ToPX.v2_3_2.topxType topx = DeserializerHelper.DeSerializeObject<Entities.ToPX.v2_3_2.topxType>(File.ReadAllText(metadata));
                                        UpdateToPX(topx, fileInfo, droidFileItem);
                                        SerializerHelper.SerializeObjectToXmlFile<Entities.ToPX.v2_3_2.topxType>(topx, metadata);

                                        outputMessage.Append(String.Format("Bestand {0} bijgewerkt", fileInfo.FullName));
                                        dataItem.IsSuccess = true;
                                    }
                                }
                                catch (ApplicationException exc)
                                {
                                    outputMessage.Append(String.Format("Bestand {0} niet bijgewerkt. Er is een applicatie fout ontstaan: {1} - {2}", fileInfo.FullName, exc.Message, exc.StackTrace));
                                    dataItem.IsSuccess = false;
                                }
                            }
                            dataItem.Message = outputMessage.ToString();
                        }
                        actionDataList.Add(dataItem);
                    });
                }
            }

            return actionDataList;
        }

        private void UpdateToPX(Entities.ToPX.v2_3_2.topxType topx, FileInfo binaryFile, DataItem droidItem)
        {
            Entities.ToPX.v2_3_2.bestandType bestand = topx.Item as Entities.ToPX.v2_3_2.bestandType;
            if (bestand == null)
                return;

            string waarde = ChecksumHelper.CreateSHA256Checksum(binaryFile);

            if (droidItem != null && !String.IsNullOrEmpty(droidItem.Puid))
            {
                if (bestand.formaat == null || bestand.formaat.Count() == 0)
                {
                    bestand.formaat = new Entities.ToPX.v2_3_2.formaatType[]
                    {
                        new Entities.ToPX.v2_3_2.formaatType
                        {
                            identificatiekenmerk = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = droidItem.MimeType },
                            bestandsnaam = new Entities.ToPX.v2_3_2.bestandsnaamType 
                            {
                                naam =  new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = binaryFile.Name },
                                extensie = new Entities.ToPX.v2_3_2.@string { Value = droidItem.Extension }
                            },
                            type = new Entities.ToPX.v2_3_2.@string { Value = "digitaal" },
                            omvang = new Entities.ToPX.v2_3_2.formaatTypeOmvang { Value = binaryFile.Length.ToString() },
                            bestandsformaat = new Entities.ToPX.v2_3_2.@string { Value = droidItem.Puid },
                            creatieapplicatie = new Entities.ToPX.v2_3_2.creatieApplicatieType
                            {
                                naam = new Entities.ToPX.v2_3_2.@string { Value = droidItem.FormatName },
                                datumAanmaak = new Entities.ToPX.v2_3_2.creatieApplicatieTypeDatumAanmaak{ Value = binaryFile.CreationTime.ToString("yyyy-MM-ddThh:mm:ss") },
                                versie = new Entities.ToPX.v2_3_2.@string { Value = droidItem.FormatVersion} 
                            },
                            fysiekeIntegriteit = new Entities.ToPX.v2_3_2.fysiekeIntegriteitType 
                            {
                                algoritme = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = "SHA-256" },
                                datumEnTijd = new Entities.ToPX.v2_3_2.fysiekeIntegriteitTypeDatumEnTijd { Value = DateTime.Now },
                                waarde = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = waarde }
                            }
                        }
                    };
                    return;//create, new
                }

                for (int i=0,l=bestand.formaat.Length; i<l; i++)
                {
                    //if formaat elements exitst only update bestadnsformaat en fysiekeIntegriteit.
                    bestand.formaat[i].bestandsformaat = new Entities.ToPX.v2_3_2.@string { Value = droidItem.Puid };
                    bestand.formaat[i].fysiekeIntegriteit = new Entities.ToPX.v2_3_2.fysiekeIntegriteitType
                    {
                        algoritme = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = "SHA-256" },
                        datumEnTijd = new Entities.ToPX.v2_3_2.fysiekeIntegriteitTypeDatumEnTijd { Value = DateTime.Now },
                        waarde = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = waarde }
                    };                    
                }                
            }
            else
            {
                bestand.formaat = new Entities.ToPX.v2_3_2.formaatType[]
                {
                    new Entities.ToPX.v2_3_2.formaatType
                    {
                        identificatiekenmerk = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = "bestandsformaat kan niet worden geïdentificeerd door DROID" },
                        bestandsnaam = new Entities.ToPX.v2_3_2.bestandsnaamType {
                            naam =  new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = binaryFile.Name },
                            extensie = new Entities.ToPX.v2_3_2.@string { Value = "bestandsformaat kan niet worden geïdentificeerd door DROID" }
                            },
                         type = new Entities.ToPX.v2_3_2.@string { Value = "digitaal" },
                         omvang = new Entities.ToPX.v2_3_2.formaatTypeOmvang { Value = binaryFile.Length.ToString() },

                         bestandsformaat = new Entities.ToPX.v2_3_2.@string { Value = "bestandsformaat kan niet worden geïdentificeerd door DROID" },
                         creatieapplicatie = new Entities.ToPX.v2_3_2.creatieApplicatieType{
                             naam = new Entities.ToPX.v2_3_2.@string { Value = "bestandsformaat kan niet worden geïdentificeerd door DROID" },
                             datumAanmaak = new Entities.ToPX.v2_3_2.creatieApplicatieTypeDatumAanmaak{ Value = binaryFile.CreationTime.ToString("yyyy-MM-ddThh:mm:ss") },
                             versie = new Entities.ToPX.v2_3_2.@string { Value = "bestandsformaat kan niet worden geïdentificeerd door DROID"} },
                         fysiekeIntegriteit = new Entities.ToPX.v2_3_2.fysiekeIntegriteitType {
                             algoritme = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = "SHA-256" },
                             datumEnTijd = new Entities.ToPX.v2_3_2.fysiekeIntegriteitTypeDatumEnTijd { Value = DateTime.Now },
                             waarde = new Entities.ToPX.v2_3_2.nonEmptyStringTypeAttribuut { Value = waarde }
                           }
                    }
                };
            }
        }

        private void UpdateMDTO(Entities.MDTO.v1_0.mdtoType mdto, FileInfo binaryFile, DataItem droidItem)
        {
            Entities.MDTO.v1_0.bestandType bestand = mdto.Item as Entities.MDTO.v1_0.bestandType;
            if (bestand == null)
                return;

            string waarde = ChecksumHelper.CreateSHA256Checksum(binaryFile);
            bestand.checksum = new Entities.MDTO.v1_0.checksumGegevens[]
            {
                new Entities.MDTO.v1_0.checksumGegevens
                {
                    checksumAlgoritme = new Entities.MDTO.v1_0.begripGegevens
                    {
                        begripLabel = "SHA-256",
                        begripBegrippenlijst = new Entities.MDTO.v1_0.verwijzingGegevens
                        {
                            verwijzingNaam = "MDTO begrippenlijsten versie 1.0",
                            verwijzingIdentificatie = new Entities.MDTO.v1_0.identificatieGegevens
                            {
                                identificatieBron = "https://kia.pleio.nl/file/download/f5d72e77-b74a-4e31-92c2-c8529227b2c1",
                                identificatieKenmerk = "MDTO begrippenlijsten versie 1.0"
                            }
                        }
                    },
                    checksumDatum = DateTime.Now,
                    checksumWaarde = waarde
                }
            };
            if (droidItem != null && !String.IsNullOrEmpty(droidItem.Puid))
            {
                bestand.bestandsformaat = new Entities.MDTO.v1_0.begripGegevens
                {
                    begripLabel = String.Format("{0} - {1}", droidItem.FormatName, droidItem.FormatVersion),
                    begripCode = droidItem.Puid,
                    begripBegrippenlijst = new Entities.MDTO.v1_0.verwijzingGegevens
                    {
                        verwijzingNaam = "PRONOM-register",
                        verwijzingIdentificatie = new Entities.MDTO.v1_0.identificatieGegevens
                        {
                            identificatieBron = "https://www.nationalarchives.gov.uk/PRONOM/Default.aspx",
                            identificatieKenmerk = "The National Archives"
                        }
                    }
                };
            }
            else
            {
                bestand.bestandsformaat = new Entities.MDTO.v1_0.begripGegevens
                {
                    begripLabel = "bestandsformaat kan niet worden geïdentificeerd door DROID",
                    begripCode = "voor dit element wordt geen waarde geregistreerd",
                    begripBegrippenlijst = new Entities.MDTO.v1_0.verwijzingGegevens
                    {
                        verwijzingNaam = "PRONOM-register",
                        verwijzingIdentificatie = new Entities.MDTO.v1_0.identificatieGegevens
                        {
                            identificatieBron = "https://www.nationalarchives.gov.uk/PRONOM/Default.aspx",
                            identificatieKenmerk = "The National Archives"
                        }
                    }
                };
            }
        }

        internal class ActionDataItem
        {
            public bool IsSuccess { get; set; }
            public String FileName { get; set; }
            public String Location { get; set; }
            public long FileSize { get; set; }
            public String Message { get; set; }
        }
        internal class DataItem
        {
            public string Location { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public string FormatName { get; set; }
            public string FormatVersion { get; set; }
            public string MimeType { get; set; }
            public string Puid { get; set; }
            public bool IsExtensionMismatch { get; set; }
        }
    }
    
}
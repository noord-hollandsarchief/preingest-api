using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using MDTO = Noord.Hollands.Archief.Preingest.WebApi.Entities.MDTO.v1_0;
using ToPX = Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX.v2_3_2;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class ToPX2MDTOConverter: IDisposable
    {
        /// <summary>
        /// Entity for a CSV record 
        /// </summary>
        public class DataItem
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

        private ToPX.topxType _currentToPX = null;
        private MDTO.mdtoType _currentMDTO = null;
        /// <summary>
        /// Initializes a new instance of the <see cref="ToPX2MDTOConverter"/> class.
        /// </summary>
        /// <param name="topx">The topx.</param>
        public ToPX2MDTOConverter(ToPX.topxType topx)
        {
            _currentToPX = topx;
            _currentMDTO =  new MDTO.mdtoType();
        }

        public ToPX2MDTOConverter(string filename)
        {
            try
            {
                _currentToPX = DeserializerHelper.DeSerializeObjectFromXmlFile<ToPX.topxType>(new FileInfo(filename));
            }
            catch(Exception e)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(String.Format("Inlezen van XML bestand '{0}' is niet gelukt!", filename));
                sb.Append(e.Message);
                sb.Append(e.StackTrace);
                if(e.InnerException != null)
                {
                    sb.Append(e.InnerException.Message);
                    sb.Append(e.InnerException.StackTrace);
                }
                throw new ApplicationException(sb.ToString(), e);
            }
            finally
            {
                _currentMDTO = new MDTO.mdtoType();
            }
        }
        /// <summary>
        /// Gets the current ToPX object type.
        /// </summary>
        /// <value>
        /// The current ToPX object type.
        /// </value>
        public ToPX.topxType CurrentToPX { get => _currentToPX; }

        /// <summary>
        /// Gets a value indicating whether this instance is bestand.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is bestand; otherwise, <c>false</c>.
        /// </value>
        public bool IsBestand
        {
            get => (CurrentToPX.Item is ToPX.bestandType);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is informatie object.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is informatie object; otherwise, <c>false</c>.
        /// </value>
        public bool IsInformatieObject
        {
            get => (CurrentToPX.Item is ToPX.aggregatieType);
        }
        /// <summary>
        /// Converts this instance.
        /// </summary>
        /// <returns></returns>
        public MDTO.mdtoType Convert()//default
        {
            MDTO.mdtoType mdto = this.Build();
            return mdto;
        }

        /// <summary>
        /// Gets the verwijzing gegevens.
        /// </summary>
        /// <param name="verwijzingNaam">The verwijzing naam.</param>
        /// <param name="bron">The bron.</param>
        /// <param name="kenmerk">The kenmerk.</param>
        /// <returns></returns>
        /// <exception cref="System.ApplicationException">
        /// Argument 'verwijzingNaam' cannot be empty or null!
        /// or
        /// Argument 'bron' cannot be empty or null!
        /// or
        /// Argument 'kenmerk' cannot be empty or null!
        /// </exception>
        public MDTO.verwijzingGegevens GetVerwijzingGegevens(String verwijzingNaam, String bron, String kenmerk)
        {
            if (String.IsNullOrEmpty(verwijzingNaam))
                throw new ApplicationException("Argument 'verwijzingNaam' cannot be empty or null!");
            if (String.IsNullOrEmpty(bron))
                throw new ApplicationException("Argument 'bron' cannot be empty or null!");
            if (String.IsNullOrEmpty(kenmerk))
                throw new ApplicationException("Argument 'kenmerk' cannot be empty or null!");

            return new MDTO.verwijzingGegevens { verwijzingNaam = verwijzingNaam, verwijzingIdentificatie = new MDTO.identificatieGegevens { identificatieBron = bron, identificatieKenmerk = kenmerk } };
        }

        /// <summary>
        /// Gets the verwijzing gegevens.
        /// </summary>
        /// <param name="verwijzingNaam">The verwijzing naam.</param>
        /// <returns></returns>
        /// <exception cref="System.ApplicationException">Argument 'verwijzingNaam' cannot be empty or null!</exception>
        public MDTO.verwijzingGegevens GetVerwijzingGegevens(String verwijzingNaam)
        {
            if (String.IsNullOrEmpty(verwijzingNaam))
                throw new ApplicationException("Argument 'verwijzingNaam' cannot be empty or null!");

            return new MDTO.verwijzingGegevens { verwijzingNaam = verwijzingNaam, verwijzingIdentificatie = null };
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this._currentMDTO = null;
            this._currentToPX = null;
        }
        private MDTO.mdtoType Build()
        {
            if (this.IsInformatieObject)
                CurrentMDTO.Item = new MDTO.informatieobjectType();

            if (this.IsBestand)
                CurrentMDTO.Item = new MDTO.bestandType();

            if (CurrentMDTO.Item == null)
                throw new ApplicationException("Meerdere object elementen in ToPX wordt niet ondersteund!");

            this.IdentificatieKenmerk();
            this.Naam();
            if (IsInformatieObject)
            {
                this.AggregatieNiveau();
                this.Classificatie();
                this.Omschrijving();
                this.RaadpleegLocatie();
                this.DekkingInTijd();
                this.DekkingInRuimte();
                this.Taal();
                this.Event();
                this.BewaartTermijn();
                this.GerelateerdInformatieObject();
                this.ArchiefVormer();
                this.Activiteit();
                this.BeperkingGebruik();
                this.EventResultaat();
                this.Vorm();
                this.Integriteit();
            }
            if (IsBestand)
            {
                this.Omvang();
                this.Bestandsformaat();
                this.Checksum();
            }
            return _currentMDTO;
        }

        private MDTO.mdtoType CurrentMDTO { get => _currentMDTO; }

        private void IdentificatieKenmerk()
        {
            ToPX.aggregatieType topxObjectType = CurrentToPX.Item as ToPX.aggregatieType;
            if (topxObjectType != null)
            {
                if (topxObjectType.identificatiekenmerk == null)
                    throw new ApplicationException("ToPX heeft geen identificatiekenmerk element!");

                CurrentMDTO.Item.identificatie = new MDTO.identificatieGegevens[] { new MDTO.identificatieGegevens { identificatieKenmerk = topxObjectType.identificatiekenmerk.Value, identificatieBron = "ToPX" } };
                if (topxObjectType.externIdentificatiekenmerk != null)
                {
                    MDTO.identificatieGegevens[] extIdPropsList = topxObjectType.externIdentificatiekenmerk.Select(item => new MDTO.identificatieGegevens { identificatieKenmerk = item.nummerBinnenSysteem.Value, identificatieBron = (item.kenmerkSysteem == null) ? null : item.kenmerkSysteem.Value }).ToArray();
                    CurrentMDTO.Item.identificatie = CurrentMDTO.Item.identificatie.Concat(extIdPropsList).ToArray();
                }
            }

            ToPX.bestandType topxObjectTypeBestand = CurrentToPX.Item as ToPX.bestandType;
            if(topxObjectTypeBestand != null)
            {
                if (topxObjectTypeBestand.identificatiekenmerk == null)
                    throw new ApplicationException("ToPX heeft geen identificatiekenmerk element!");
                CurrentMDTO.Item.identificatie = new MDTO.identificatieGegevens[] { new MDTO.identificatieGegevens { identificatieKenmerk = topxObjectTypeBestand.identificatiekenmerk.Value, identificatieBron = "ToPX" } };
            }
        }
        private void Naam()
        {
            ToPX.aggregatieType topxObjectType = CurrentToPX.Item as ToPX.aggregatieType;
            ToPX.bestandType topxObjectTypeBestand = CurrentToPX.Item as ToPX.bestandType;

            if (topxObjectType != null && topxObjectType.naam.Length != 0)
                CurrentMDTO.Item.naam = topxObjectType.naam.Length == 1 ? topxObjectType.naam.FirstOrDefault().Value : String.Join(" / ", topxObjectType.naam.Select(item => item.Value));
            else if(topxObjectTypeBestand!= null && topxObjectTypeBestand.naam.Length != 0)
                    CurrentMDTO.Item.naam = topxObjectTypeBestand.naam.Length == 1 ? topxObjectTypeBestand.naam.FirstOrDefault().Value : String.Join(" / ", topxObjectTypeBestand.naam.Select(item => item.Value));
            else
                throw new ApplicationException("ToPX heeft geen naam element!");
            
        }
        private void AggregatieNiveau()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.aggregatieniveau == null)
                throw new ApplicationException("ToPX heeft geen aggregatie element!");

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.aggregatieniveau = new MDTO.begripGegevens();
            //optioneel, in overleg 03-05-2022 besloten
            //infObjType.aggregatieniveau.begripCode = topxObjectType.aggregatieniveau.Value.ToString().Equals("Record", StringComparison.InvariantCultureIgnoreCase) ? "Archiefstuk" : topxObjectType.aggregatieniveau.Value.ToString();
            infObjType.aggregatieniveau.begripLabel = topxObjectType.aggregatieniveau.Value.ToString().Equals("Record", StringComparison.InvariantCultureIgnoreCase) ? "Archiefstuk" : topxObjectType.aggregatieniveau.Value.ToString();
            infObjType.aggregatieniveau.begripBegrippenlijst = new MDTO.verwijzingGegevens();
            infObjType.aggregatieniveau.begripBegrippenlijst.verwijzingNaam = "MDTO begrippenlijsten versie 1.0";
            infObjType.aggregatieniveau.begripBegrippenlijst.verwijzingIdentificatie = null;//new MDTO.identificatieGegevens() { identificatieBron = "MDTO begrippenlijsten versie 1.0", identificatieKenmerk = "MDTO begrippenlijsten versie 1.0" };
        }
        private void Classificatie()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.classificatie != null)
            {
                MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
                infObjType.classificatie = topxObjectType.classificatie.Select(item => new MDTO.begripGegevens
                {
                    begripCode = null,
                    begripLabel = item.code.Value,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens()
                    {
                        verwijzingNaam = item.bron.Value,
                        verwijzingIdentificatie = null
                    }
                }
               ).ToArray();
            }
        }
        private void Omschrijving()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.omschrijving == null)            
                return;

             MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
             infObjType.omschrijving = topxObjectType.omschrijving.Select(item => item.Value).ToArray();
        }
        private void RaadpleegLocatie()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.plaats == null)
                return;
             
            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.raadpleeglocatie = new MDTO.raadpleeglocatieGegevens[] { new MDTO.raadpleeglocatieGegevens { raadpleeglocatieFysiek = new MDTO.verwijzingGegevens[] { new MDTO.verwijzingGegevens { verwijzingNaam = topxObjectType.plaats.Value, verwijzingIdentificatie = null } }, raadpleeglocatieOnline = null } };            
        }
        private void DekkingInTijd()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.dekking == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.dekkingInTijd = topxObjectType.dekking.Select(item => new MDTO.dekkingInTijdGegevens
            {
                dekkingInTijdBegindatum = item.inTijd == null ? null : CustomDateTimeParse(item.inTijd.begin.Item).ToString("yyyy-MM-ddTHH:mm:ss"),
                dekkingInTijdEinddatum = item.inTijd == null ? null : CustomDateTimeParse(item.inTijd.eind.Item).ToString("yyyy-MM-ddTHH:mm:ss"),
                dekkingInTijdType = new MDTO.begripGegevens
                {
                    begripLabel = "Niet van toepassing",
                    begripCode = null,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens
                    {
                        verwijzingNaam = "MDTO begrippenlijsten versie 1.0",
                        verwijzingIdentificatie = null
                    }
                }
            }).ToArray();
        }
        private void DekkingInRuimte()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.dekking == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.dekkingInRuimte = topxObjectType.dekking.SelectMany(item => item.geografischGebied != null ? item.geografischGebied : new ToPX.@string[] { new ToPX.@string { Value = String.Empty } }).Where(item => !String.IsNullOrEmpty(item.Value)).Select(item => new MDTO.verwijzingGegevens { verwijzingNaam = item.Value }).ToArray();
        }
        private void Taal()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.taal == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.taal = topxObjectType.taal.Select(item => item.Value.ToString()).ToArray();
        }
        private void Event()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.eventGeschiedenis == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.@event = topxObjectType.eventGeschiedenis.Select(item => new MDTO.eventGegevens
            {
                //eventResultaat = ""
                eventVerantwoordelijkeActor = new MDTO.verwijzingGegevens { verwijzingNaam = item.verantwoordelijkeFunctionaris.Value, verwijzingIdentificatie = null },
                eventTijd = CustomDateTimeParse(item.datumOfPeriode.Item).ToString("yyyy-MM-ddTHH:mm:ss"),
                eventType = new MDTO.begripGegevens
                {
                    begripLabel = item.type1,
                    begripCode = item.beschrijving == null ? null : item.beschrijving.Value,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens { verwijzingNaam = "MDTO begrippenlijsten versie 1.0", verwijzingIdentificatie = null }
                }
            }
            ).ToArray();
        } 
        private void BewaartTermijn()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.eventPlan == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            string aanleidingen = String.Join(" / ", topxObjectType.eventPlan.Select(item => item.aanleiding.Value).ToArray());
            DateTime outDateTime = DateTime.MinValue;
            DateTime max = topxObjectType.eventPlan.Select(item => CustomDateTimeParse(item.datumOfPeriode.Item)).Max();
            DateTime min = topxObjectType.eventPlan.Select(item => CustomDateTimeParse(item.datumOfPeriode.Item)).Min();
            infObjType.bewaartermijn = new MDTO.termijnGegevens { termijnTriggerStartLooptijd = new MDTO.begripGegevens { begripLabel = aanleidingen, begripCode = null, begripBegrippenlijst = new MDTO.verwijzingGegevens { verwijzingNaam = "MDTO begrippenlijsten versie 1.0", verwijzingIdentificatie = null } }, termijnStartdatumLooptijd = min, termijnLooptijd = null, termijnEinddatum = max.ToString(), termijnStartdatumLooptijdSpecified = false };
        }

        private void GerelateerdInformatieObject()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.relatie == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.gerelateerdInformatieobject = topxObjectType.relatie.Select(item => new MDTO.gerelateerdInformatieobjectGegevens
            {
                gerelateerdInformatieobjectTypeRelatie = new MDTO.begripGegevens
                {
                    begripLabel = item.typeRelatie.Value,
                    begripCode = item.datumOfPeriode == null ? null : item.datumOfPeriode.Item.ToString(),
                    begripBegrippenlijst = new MDTO.verwijzingGegevens
                    {
                        verwijzingNaam = "MDTO begrippenlijsten versie 1.0",
                        verwijzingIdentificatie = null
                    }
                },
                gerelateerdInformatieobjectVerwijzing = new MDTO.verwijzingGegevens
                {
                    verwijzingNaam = item.relatieID.Value,
                    verwijzingIdentificatie = null
                }
            }
            ).ToArray();
        }

        private void ArchiefVormer()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.context == null || topxObjectType.context.actor == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            string geautoriseerdeNaam = String.Join(" / ", topxObjectType.context.actor.Select(item => item.geautoriseerdeNaam.Value));
            string identificatieKenmerk = String.Join(" / ", topxObjectType.context.actor.Select(item => item.identificatiekenmerk.Value));
            infObjType.archiefvormer = new MDTO.verwijzingGegevens { verwijzingNaam = geautoriseerdeNaam, verwijzingIdentificatie = new MDTO.identificatieGegevens { identificatieKenmerk = identificatieKenmerk, identificatieBron = "Niet van toepassing" } };
        }

        private void Activiteit()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.context == null || topxObjectType.context.activiteit == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            string naam = String.Join(" / ", topxObjectType.context.activiteit.Select(item => item.naam.Value));
            string identificatieKenmerk = String.Join(" / ", topxObjectType.context.activiteit.Select(item => item.identificatiekenmerk.Value));
            infObjType.activiteit = new MDTO.verwijzingGegevens { verwijzingNaam = naam, verwijzingIdentificatie = new MDTO.identificatieGegevens { identificatieKenmerk = identificatieKenmerk, identificatieBron = "Niet van toepassing" } };
        }

        private void BeperkingGebruik()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);

            if (topxObjectType.gebruiksrechten != null)//array
            {//16
                var result = topxObjectType.gebruiksrechten.Select(item => new MDTO.beperkingGebruikGegevens { beperkingGebruikNadereBeschrijving = item.omschrijvingVoorwaarden.Value, beperkingGebruikTermijn = new MDTO.termijnGegevens { termijnEinddatum = null, termijnLooptijd = null, termijnStartdatumLooptijd = CustomDateTimeParse(item.datumOfPeriode.Item), termijnStartdatumLooptijdSpecified = false, termijnTriggerStartLooptijd = null }, beperkingGebruikType = null, beperkingGebruikDocumentatie = null }).ToArray();                
                infObjType.beperkingGebruik = infObjType.beperkingGebruik == null ? result : infObjType.beperkingGebruik.Concat(result).ToArray();
            }

            if (topxObjectType.vertrouwelijkheid != null)//array
            {
                //17
                var result = topxObjectType.vertrouwelijkheid.Select(item => new MDTO.beperkingGebruikGegevens { beperkingGebruikNadereBeschrijving = null, beperkingGebruikTermijn = new MDTO.termijnGegevens { termijnEinddatum = null, termijnLooptijd = null, termijnStartdatumLooptijd = CustomDateTimeParse(item.datumOfPeriode.Item), termijnStartdatumLooptijdSpecified = false, termijnTriggerStartLooptijd = null }, beperkingGebruikType = new MDTO.begripGegevens { begripLabel = item.classificatieNiveau.Value.ToString(), begripCode = null, begripBegrippenlijst = null }, beperkingGebruikDocumentatie = null }).ToArray();
                infObjType.beperkingGebruik = infObjType.beperkingGebruik == null ? result : infObjType.beperkingGebruik.Concat(result).ToArray();
            }

            if (topxObjectType.openbaarheid != null)//array
            {
                //18
                var result = topxObjectType.openbaarheid.Select(item => new MDTO.beperkingGebruikGegevens { beperkingGebruikNadereBeschrijving = null, beperkingGebruikTermijn = new MDTO.termijnGegevens { termijnEinddatum = null, termijnLooptijd = null, termijnStartdatumLooptijd = CustomDateTimeParse(item.datumOfPeriode.Item), termijnStartdatumLooptijdSpecified = false, termijnTriggerStartLooptijd = null }, beperkingGebruikType = new MDTO.begripGegevens { begripLabel = String.Join(" / ", item.omschrijvingBeperkingen.Select(s => s.Value)), begripCode = null, begripBegrippenlijst = null }, beperkingGebruikDocumentatie = null }).ToArray();
                infObjType.beperkingGebruik = infObjType.beperkingGebruik == null ? result : infObjType.beperkingGebruik.Concat(result).ToArray();
            }
        }
        private void EventResultaat() //Integriteit
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.integriteit == null)
                return;

            string eventResultaat = topxObjectType.integriteit.Value;
            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            var item = new MDTO.eventGegevens { eventResultaat = eventResultaat, eventTijd = null, eventType = new MDTO.begripGegevens { begripLabel = "Integriteit", begripCode = null, begripBegrippenlijst = null }, eventVerantwoordelijkeActor = new MDTO.verwijzingGegevens { verwijzingNaam = "MDTO begrippenlijsten versie 1.0", verwijzingIdentificatie = null } };
            if (infObjType.@event != null)
                infObjType.@event.Append(item);
            else
                infObjType.@event = new MDTO.eventGegevens[] { item };
        }

        private void Omvang(long omvang = -1)
        {
            ToPX.bestandType topxObjectTypeBestand = CurrentToPX.Item as ToPX.bestandType;
            MDTO.bestandType infObjType = (CurrentMDTO.Item as MDTO.bestandType);

            if (omvang == -1)
            {
                if (topxObjectTypeBestand.formaat.FirstOrDefault() == null)
                    throw new ApplicationException("ToPX heeft geen formaat element!");
                infObjType.omvang = topxObjectTypeBestand.formaat.FirstOrDefault().omvang.Value;
            }
            else
            {
                infObjType.omvang = omvang.ToString();
            }
        }

        private void Bestandsformaat(MDTO.begripGegevens begripGegevens = null)
        {
            ToPX.bestandType topxObjectTypeBestand = CurrentToPX.Item as ToPX.bestandType;
            MDTO.bestandType infObjType = (CurrentMDTO.Item as MDTO.bestandType);

            if (begripGegevens == null)
            {
                if (topxObjectTypeBestand.formaat.FirstOrDefault() == null)
                    throw new ApplicationException("ToPX heeft geen formaat element!");
                string bestandsformaat = topxObjectTypeBestand.formaat.FirstOrDefault().bestandsformaat.Value;
                infObjType.bestandsformaat = new MDTO.begripGegevens { begripLabel = bestandsformaat, begripCode = null, begripBegrippenlijst = new MDTO.verwijzingGegevens { verwijzingNaam = "MDTO begrippenlijsten versie 1.0", verwijzingIdentificatie = null } };
            }
            else
            {
                infObjType.bestandsformaat = begripGegevens;
            }
        }

        private void Vorm()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.vorm == null)
                return;

            ToPX.@string redactieGenre = topxObjectType.vorm.redactieGenre;
            ToPX.@string structuur = topxObjectType.vorm.structuur;
            ToPX.@string[] verschijningsvormen = topxObjectType.vorm.verschijningsvorm;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            List<MDTO.begripGegevens> currentClassificatieList = new List<MDTO.begripGegevens>();

            if (infObjType.classificatie != null)            
                currentClassificatieList.AddRange(infObjType.classificatie);

            if (redactieGenre != null && !String.IsNullOrEmpty(redactieGenre.Value))
                currentClassificatieList.Add(new MDTO.begripGegevens
                {
                    begripLabel = redactieGenre.Value,
                    begripCode = null,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens
                    {
                        verwijzingNaam = "Vorm / redactie genre",
                        verwijzingIdentificatie = null
                    }
                });

            if(structuur != null && !String.IsNullOrEmpty(structuur.Value))
                currentClassificatieList.Add(new MDTO.begripGegevens
                {
                    begripLabel = structuur.Value,
                    begripCode = null,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens
                    {
                        verwijzingNaam = "Vorm / structuur",
                        verwijzingIdentificatie = null
                    }
                });

            if(verschijningsvormen != null && verschijningsvormen.Length > 0)
                currentClassificatieList.Add(new MDTO.begripGegevens
                {
                    begripLabel = String.Join(" / ", verschijningsvormen.Select(item => item.Value).ToArray()),
                    begripCode = null,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens
                    {
                        verwijzingNaam = "Vorm / verschijningsvorm",
                        verwijzingIdentificatie = null
                    }
                });

            infObjType.classificatie = currentClassificatieList.ToArray();
        }

        private void Integriteit()
        {
            ToPX.aggregatieType topxObjectType = (CurrentToPX.Item as ToPX.aggregatieType);
            if (topxObjectType.integriteit == null)
                return;

            MDTO.informatieobjectType infObjType = (CurrentMDTO.Item as MDTO.informatieobjectType);
            infObjType.@event = topxObjectType.eventGeschiedenis.Select(item => new MDTO.eventGegevens
            {                
                eventType = new MDTO.begripGegevens
                {
                    begripLabel = topxObjectType.integriteit.Value,
                    begripBegrippenlijst = new MDTO.verwijzingGegevens { verwijzingNaam = "Integriteit", verwijzingIdentificatie = null }
                }
            }
            ).ToArray();
        }

        private void Checksum(MDTO.checksumGegevens[] checksumGegevens = null)
        {
            ToPX.bestandType topxObjectTypeBestand = CurrentToPX.Item as ToPX.bestandType;
            MDTO.bestandType infObjType = (CurrentMDTO.Item as MDTO.bestandType);

            if (checksumGegevens == null)
            {
                if (topxObjectTypeBestand.formaat == null)
                    throw new ApplicationException("ToPX heeft geen formaat element!");
                infObjType.checksum = topxObjectTypeBestand.formaat.Select(item => new MDTO.checksumGegevens { checksumAlgoritme = new MDTO.begripGegevens { begripLabel = item.fysiekeIntegriteit.algoritme.Value, begripCode = null, begripBegrippenlijst = new MDTO.verwijzingGegevens { verwijzingNaam = "MDTO begrippenlijsten versie 1.0", verwijzingIdentificatie = null } }, checksumWaarde = item.fysiekeIntegriteit.waarde.Value, checksumDatum = DateTime.Parse(item.fysiekeIntegriteit.datumEnTijd.Value.ToString()) }).ToArray();
            }
            else
            {
                infObjType.checksum = checksumGegevens;
            }
        }  
        
        private DateTime CustomDateTimeParse(object input)
        {
            if(input is ToPX.datumOfJaarTypeDatum)
            {
                var momentObject = (input as ToPX.datumOfJaarTypeDatum);
                return momentObject.Value;
            }
            if (input is ToPX.datumOfJaarTypeDatumEnTijd)
            {
                var momentObject = (input as ToPX.datumOfJaarTypeDatumEnTijd);
                return momentObject.Value;
            }
            if (input is ToPX.datumOfJaarTypeJaar)
            {
                var momentObject = (input as ToPX.datumOfJaarTypeJaar);
                int year = DateTime.Now.Year;
                Int32.TryParse(momentObject.Value, out year);
                return new DateTime(year, 1, 1);
            }
            if (input is ToPX.datumOfPeriodeTypeDatum)
            {
                var momentObject = (input as ToPX.datumOfPeriodeTypeDatum);
                return momentObject.Value;
            }
            if (input is ToPX.datumOfPeriodeTypeDatumEnTijd)
            {
                var momentObject = (input as ToPX.datumOfPeriodeTypeDatumEnTijd);
                return momentObject.Value;
            }
            if (input is ToPX.datumOfPeriodeTypeJaar)
            {
                var momentObject = (input as ToPX.datumOfPeriodeTypeJaar);
                int year = DateTime.Now.Year;
                Int32.TryParse(momentObject.Value, out year);
                return new DateTime(year, 1, 1);
            }

            return DateTime.MinValue;
        }
    }
}

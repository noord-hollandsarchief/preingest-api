
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.MDTO.v1_0
{
    public partial class verwijzingGegevens : IEquatable<verwijzingGegevens>
    {
        public bool Equals(verwijzingGegevens other)
        {
            if (other == null)
                return false;

            if (other.verwijzingIdentificatie == null)
                return false;

            bool naam = other.verwijzingNaam == this.verwijzingNaam;
            bool id = other.verwijzingIdentificatie.identificatieKenmerk.Equals(this.verwijzingIdentificatie.identificatieKenmerk);
            bool bron = other.verwijzingIdentificatie.identificatieBron.Equals(this.verwijzingIdentificatie.identificatieBron);

            return naam && id && bron;
        }
    }

    public partial class mdtoType
    {
        public void UpdateRelationshipReference(verwijzingGegevens[] upReferenceList, verwijzingGegevens[] downReferenceList)
        {
            if (IsBestand)
                return;  
            
            informatieobjectType informatieobject = (this.Item as informatieobjectType);

            if (upReferenceList == null)
            {
                informatieobject.isOnderdeelVan = null;
            }
            else 
            { 
                var currentUpList = informatieobject.isOnderdeelVan == null ? new List<verwijzingGegevens>() : new List<verwijzingGegevens>(informatieobject.isOnderdeelVan);                
                if(currentUpList.Count > 0)
                {
                    foreach (var item in upReferenceList)
                    {
                        bool exists = currentUpList.Exists(existing => existing.Equals(item));
                        if (!exists)
                            currentUpList.Add(item);
                    }
                    informatieobject.isOnderdeelVan = currentUpList.ToArray();
                }
                else
                {
                    currentUpList.AddRange(upReferenceList);
                    informatieobject.isOnderdeelVan = currentUpList.ToArray();
                }
            }            

            if (downReferenceList == null)
                return;

            if (IsArchiefstuk)
            {
                var currentDownList = informatieobject.heeftRepresentatie == null ? new List<verwijzingGegevens>() : new List<verwijzingGegevens>(informatieobject.heeftRepresentatie);
                if (currentDownList.Count > 0)
                {
                    foreach (var item in downReferenceList)
                    {
                        bool exists = currentDownList.Exists(existing => existing.Equals(item));
                        if (!exists)
                            currentDownList.Add(item);
                    }
                    informatieobject.heeftRepresentatie = currentDownList.ToArray();
                }
                else
                {
                    currentDownList.AddRange(downReferenceList);
                    informatieobject.heeftRepresentatie = currentDownList.ToArray();
                }
            }
            else
            { 
                var currentDownList = informatieobject.bevatOnderdeel == null ? new List<verwijzingGegevens>() : new List<verwijzingGegevens>(informatieobject.bevatOnderdeel);
                if (currentDownList.Count > 0)
                {
                    foreach (var item in downReferenceList)
                    {
                        bool exists = currentDownList.Exists(existing => existing.Equals(item));
                        if (!exists)
                            currentDownList.Add(item);
                    }
                    informatieobject.bevatOnderdeel = currentDownList.ToArray();
                }
                else
                {
                    currentDownList.AddRange(downReferenceList);
                    informatieobject.bevatOnderdeel = currentDownList.ToArray();
                }
            }            
        }

        public void UpdateRelationshipReference(verwijzingGegevens upReference)
        {
            if (!IsBestand)
                return;            
            bestandType bestand = (this.Item as bestandType);
            bestand.isRepresentatieVan = upReference;            
        }

        public bool IsBestand
        {
            get => (this.Item is bestandType);
        }

        public bool IsArchiefstuk
        {
            get
            {
                if (IsBestand)
                    return false;

                if ((this.Item as informatieobjectType).aggregatieniveau == null)
                    return false;

                if (String.IsNullOrEmpty((this.Item as informatieobjectType).aggregatieniveau.begripLabel))
                    return false;

                return (this.Item as informatieobjectType).aggregatieniveau.begripLabel.Equals("Archiefstuk", StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}

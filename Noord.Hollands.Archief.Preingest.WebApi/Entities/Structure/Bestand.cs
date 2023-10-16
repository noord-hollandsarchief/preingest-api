using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Noord.Hollands.Archief.Preingest.WebApi.Utilities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX.v2_3_2;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{
    [Serializable()]
    public class Bestand : Sidecar
    {   
        private String _binary = null;

        public Bestand(String name, String path, String binary, Record parent) : base(name, path, parent)
        {
            this._binary = binary;
        }
        public PronomItem PronomBinaryInfo { get; set; }
        public Boolean? BinaryFileIsInGreenList { get; set; }         
        public String BinaryFileLocation { get;set; }
        public Dictionary<String, String> ChecksumResultCollection { get; set; }
        public override bool CompareAggregationLevel
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                result = (aggregatieTypeObject.aggregatieniveau.Value == bestandAggregatieniveauType.Bestand);

                return result;
            }
        }
        public override bool HasIdentification
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                if (aggregatieTypeObject.identificatiekenmerk == null)
                    return result;

                result = !(string.IsNullOrEmpty(aggregatieTypeObject.identificatiekenmerk.Value));

                return result;
            }
        }
        public bool HasFilename
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                if (aggregatieTypeObject.formaat == null)
                    return result;

                if (aggregatieTypeObject.formaat.Count() == 0)
                    return result;

                var formaat = aggregatieTypeObject.formaat.First();

                if (formaat == null)
                    return result;

                    if (formaat.bestandsnaam == null)
                    return result;

                if (formaat.bestandsnaam.naam == null)
                    return result;            

                result = !(string.IsNullOrEmpty(formaat.bestandsnaam.naam.Value));

                return result;
            }
        }
        public bool HasFilesize
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                if (aggregatieTypeObject.formaat == null)
                    return result;

                if (aggregatieTypeObject.formaat.Count() == 0)
                    return result;

                var formaat = aggregatieTypeObject.formaat.First();

                if (formaat == null)
                    return result;

                if (formaat.omvang == null)
                    return result;

                if (string.IsNullOrEmpty(formaat.omvang.Value))
                    return result;

                long size = 0;
                result = Int64.TryParse(formaat.omvang.Value, out size);

                if (size <= 0)
                    result = false;

                return result;
            }
        }
        public bool HasAlgoritme
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;
   
                if (aggregatieTypeObject.formaat == null)
                    return result;

                if (aggregatieTypeObject.formaat.Count() == 0)
                    return result;

                var formaat = aggregatieTypeObject.formaat.First();

                if (formaat == null)
                    return result;

                if (formaat.fysiekeIntegriteit == null)
                    return result;

                if (formaat.fysiekeIntegriteit.algoritme == null)
                    return result;

                if (String.IsNullOrEmpty(formaat.fysiekeIntegriteit.algoritme.Value))
                    return result;

                switch (formaat.fysiekeIntegriteit.algoritme.Value.ToUpperInvariant())
                {
                    case "MD5":                        
                    case "SHA1":
                    case "SHA256":
                    case "SHA512":
                    case "SHA-1":
                    case "SHA-256":
                    case "SHA-512":
                        result = true;
                        break;
                    default:
                        result = false;
                        break;
                }

                return result;
            }
        }
        public bool HasChecksum
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.formaat == null)
                    return result;

                if (aggregatieTypeObject.formaat.Count() == 0)
                    return result;

                var formaat = aggregatieTypeObject.formaat.First();

                if (formaat == null)
                    return result;

                if (formaat.fysiekeIntegriteit == null)
                    return result;

                if (formaat.fysiekeIntegriteit.waarde == null)
                    return result;

                result = !String.IsNullOrEmpty(formaat.fysiekeIntegriteit.waarde.Value);

                return result;
            }
        }
        public bool CompareChecksum
        {
            get
            {
                bool result = false;
                if(!(HasAlgoritme && HasChecksum))                
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                var formaat = aggregatieTypeObject.formaat.First();

                string checksumValue = formaat.fysiekeIntegriteit.waarde.Value;
                string checksumType  = formaat.fysiekeIntegriteit.algoritme.Value;

                string currentCalculation = string.Empty;
                switch (checksumType.ToUpperInvariant())
                {
                    case "MD5":
                        currentCalculation = ChecksumHelper.CreateMD5Checksum(new FileInfo(_binary));
                        break;
                    case "SHA1":
                    case "SHA-1":
                        currentCalculation = ChecksumHelper.CreateSHA1Checksum(new FileInfo(_binary));
                        break;
                    case "SHA256":
                    case "SHA-256":
                        currentCalculation = ChecksumHelper.CreateSHA256Checksum(new FileInfo(_binary));
                        break;
                    case "SHA512":
                    case "SHA-512":
                        currentCalculation = ChecksumHelper.CreateSHA512Checksum(new FileInfo(_binary));
                        break;
                    default:{}
                        break;
                }

                result = currentCalculation.Equals(checksumValue, StringComparison.InvariantCultureIgnoreCase);
                                  
                return result;
            }
        }
        public override bool HasName
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;       

                if (aggregatieTypeObject.naam == null)
                    return result;

                var naam = aggregatieTypeObject.naam.FirstOrDefault();

                if (naam == null)
                    return result;

                result = !(string.IsNullOrEmpty(naam.Value));

                return result;
            }

        }        
        public bool IsParentEmpty { get => Parent == null; }
        public bool BinaryFileExists
        {
            get => System.IO.File.Exists(this.BinaryFileLocation);
        }
        public long BinaryFileLength { get; set; }
        public bool CompareFilesizeMetadataWithBinary
        {
            get {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as bestandType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                if (aggregatieTypeObject.formaat == null)
                    return result;

                if (aggregatieTypeObject.formaat.Count() == 0)
                    return result;

                var formaat = aggregatieTypeObject.formaat.First();

                if (formaat == null)
                    return result;

                if (formaat.omvang == null)
                    return result;

                if (string.IsNullOrEmpty(formaat.omvang.Value))
                    return result;

                long size = 0;
                result = Int64.TryParse(formaat.omvang.Value, out size);

                if (!result)
                    return result;

                if (size <= 0)
                    return result;

                return (size == BinaryFileLength);         
            }
        }
        public override string ToString()
        {
            return String.Format("{0} (Bestand)", this.Name);
        }
        public override void Validate()
        {
            if (!this.HasMetadata)
                LoadMetadata();

            if (!CompareAggregationLevel)
                this.ObjectExceptions().Add(new SidecarException("Aggregatie niveau onjuist."));

            if (!HasIdentification)
                this.ObjectExceptions().Add(new SidecarException("Geen identificatiekenmerk gevonden."));

            if (!HasName)
                this.ObjectExceptions().Add(new SidecarException("Geen naam gevonden."));

            if (!HasFilename)
                this.ObjectExceptions().Add(new SidecarException("Geen bestandsnaam gevonden."));

            if (!HasFilesize)
                this.ObjectExceptions().Add(new SidecarException("Geen bestandsgrootte gevonden."));

            if (!HasAlgoritme)
                this.ObjectExceptions().Add(new SidecarException("Geen algoritme gevonden voor de checksum."));

            if(!HasChecksum)
                this.ObjectExceptions().Add(new SidecarException("Geen checksum waarde gevonden."));

            if(!CompareChecksum)
                this.ObjectExceptions().Add(new SidecarException("Gecalculeerde checksum komt niet overeen met de checksum waarde."));

            if(!BinaryFileExists)
                this.ObjectExceptions().Add(new SidecarException(String.Format ("Bestand '{0}' niet gevonden naast metadata '{1}'.", this.BinaryFileLocation, this.MetadataFileLocation)));

            /** This check is taking too long with large files. Option is to move this to a seperate action.
            if (BinaryFileExists)
            {
                var fileObject = new FileInfo(this.BinaryFileLocation);
                if (fileObject.Length == 0)
                    this.ObjectExceptions().Add(new SidecarException(String.Format("Zero byte file gevonden: {0}.", this.BinaryFileLocation)));

                this.ChecksumResultCollection = new Dictionary<string, string>();
                string md5 = ChecksumHelper.CreateMD5Checksum(fileObject);
                string sha1 = ChecksumHelper.CreateSHA1Checksum(fileObject);
                string sha256 = ChecksumHelper.CreateSHA256Checksum(fileObject);
                string sha512 = ChecksumHelper.CreateSHA512Checksum(fileObject);

                this.ChecksumResultCollection.Add("MD5", md5);
                this.ChecksumResultCollection.Add("SHA1", sha1);
                this.ChecksumResultCollection.Add("SHA256", sha256);
                this.ChecksumResultCollection.Add("SHA512", sha512);
            }

            if (!CompareFilesizeMetadataWithBinary)
                this.ObjectExceptions().Add(new SidecarException("Omvangwaarde opgegeven in metadata komt niet overeen met bestandsgrootte van het bestand."));
            **/
            base.Dispose();
        }
    }
}

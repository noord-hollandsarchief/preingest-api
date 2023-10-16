using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex
{
    public class OpexNonMetadataItem
    {
        private string _prefixTargetFolder = string.Empty;
        internal OpexNonMetadataItem(String prefixTargetFolder, String keyLocation, bool isFile)
        {
            Id = Guid.NewGuid();
            KeyLocation = keyLocation;
            this._prefixTargetFolder = prefixTargetFolder;
            IsMultipleMetadataFilesFoundInCurrentFolder = false;
            IsFile = isFile;
            OpexXml = String.Empty;
        }

        public Guid Id { get; set; }

        public bool IsMultipleMetadataFilesFoundInCurrentFolder { get; set; }
        public String KeyOpexLocation
        {
            get
            {
                if (IsFile)
                    return String.Concat(KeyLocation, ".opex");
                else
                    return String.Concat(Path.Combine(KeyLocation, new DirectoryInfo(this.KeyLocation).Name), ".opex");
            }
        }

        public String KeyLocation { get; set; }
        public String OpexXml { get; set; }
        public void UpdateOpexFileLevel()
        {
            if (!IsFile)
                return;

            var currentOpexMetadataFile = new opexMetadata();

            //TODO fill out opex object
            FileInfo currentInfo = new FileInfo(this.KeyOpexLocation);

            string fixity = string.Empty;
            FileInfo binaryFilename = new FileInfo(this.KeyLocation);

            if (!binaryFilename.Exists)
                throw new FileNotFoundException(String.Format("Calculate checksum failed! Binary file '{0}' not found.", binaryFilename.FullName));

            fixity = Utilities.ChecksumHelper.CreateSHA256Checksum(binaryFilename);

            currentOpexMetadataFile.Transfer = new transfer();
            currentOpexMetadataFile.Transfer.Fixities = new fixity[] { new fixity { type = "SHA-256", value = fixity } };

            currentOpexMetadataFile.Properties = new Properties();
            currentOpexMetadataFile.Properties.Title = binaryFilename.Name;
            currentOpexMetadataFile.Properties.Description = String.Empty;
            //currentOpexMetadataFile.Properties.SecurityDescriptor = "closed";

            currentOpexMetadataFile.DescriptiveMetadata = new DescriptiveMetadata();

            OpexXml = Utilities.SerializerHelper.SerializeObjectToString(currentOpexMetadataFile);
        }

        public void UpdateOpexFolderLevel()
        {
            var currentOpexMetadataFile = new opexMetadata();
            currentOpexMetadataFile.Transfer = new transfer();
            currentOpexMetadataFile.Transfer.SourceID = Guid.NewGuid().ToString();

            currentOpexMetadataFile.Transfer.Manifest = new manifest();

            DirectoryInfo currentInfo = new DirectoryInfo(this.KeyLocation);

            var files = currentInfo.GetFiles().ToList();
            var directories = currentInfo.GetDirectories().ToList();
            //overwrite
            if (files.Count > 0)
            {                
                currentOpexMetadataFile.Transfer.Manifest.Files = files.Select(item => new fileItem
                {
                    size = item.Length,
                    typeSpecified = true,
                    type = item.Extension.Equals(".opex", StringComparison.InvariantCultureIgnoreCase) ? fileType.metadata : fileType.content,
                    sizeSpecified = true,
                    Value = item.Name
                }).ToArray();
            }
            //overwrite
            if (directories.Count > 0)
            {
                currentOpexMetadataFile.Transfer.Manifest.Folders = directories.Select(item => item.Name).ToArray();
            }
   
            currentOpexMetadataFile.Properties = new Properties();
            currentOpexMetadataFile.Properties.Title = currentInfo.Name;
            currentOpexMetadataFile.Properties.Description = String.Empty;
            currentOpexMetadataFile.Properties.SecurityDescriptor = "closed";

            OpexXml = Utilities.SerializerHelper.SerializeObjectToString(currentOpexMetadataFile);
        }

        public override string ToString()
        {
            return String.Format ("{0} - {1}", this.Level, this.KeyLocation);
        }

        public int Level
        {
            get
            {
                //skip count
                int skip = _prefixTargetFolder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count();
                //take = minus skip minus the file self
                int take = (KeyLocation.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count() - skip - 1);
                
                var result = KeyLocation.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList().Skip(skip).Take(take);
                
                return result.Count();
            }
        }

        public bool IsFile
        {
            get;set;
        }

        internal class UTF8StringWriter : StringWriter
        {
            public UTF8StringWriter() { }
            public UTF8StringWriter(IFormatProvider formatProvider) : base(formatProvider) { }
            public UTF8StringWriter(StringBuilder sb) : base(sb) { }
            public UTF8StringWriter(StringBuilder sb, IFormatProvider formatProvider) : base(sb, formatProvider) { }

            public override Encoding Encoding
            {
                get
                {
                    return Encoding.UTF8;
                }
            }
        }
    }
}

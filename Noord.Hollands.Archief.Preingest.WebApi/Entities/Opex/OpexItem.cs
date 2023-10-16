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
    public class OpexItem
    {
        public const string ADD_NAMESPACE = "01-opex-add-namespaces.xsl";
        public const string STRIP_NAMESPACE = "00-opex-strip-namespaces.xsl";
        public const string OPEX_FOLDERS = "02-opex-folders.xsl";
        public const string OPEX_FOLDER_FILES = "03-opex-folder-files.xsl";
        public const string OPEX_FILES = "04-opex-files.xsl";
        public const string OPEX_FINALIZE = "05-opex-finalize.xsl";
        private string _prefixTargetFolder = string.Empty;
        internal OpexItem(String prefixTargetFolder, String keyLocation, String metadata, int metadataFilesCount)
        {
            Id = Guid.NewGuid();
            KeyLocation = keyLocation;
            OriginalMetadata = metadata;
            this._prefixTargetFolder = prefixTargetFolder;

            IsMultipleMetadataFilesFoundInCurrentFolder = (metadataFilesCount > 1);
        }

        public Guid Id { get; set; }

        public bool IsMultipleMetadataFilesFoundInCurrentFolder { get; set; }
        public String KeyOpexLocation
        {
            get
            {
                return KeyLocation.EndsWith(".metadata") ? KeyLocation.Replace(".metadata", ".opex") : KeyLocation.EndsWith(".bestand.mdto.xml") ? KeyLocation.Replace(".bestand.mdto.xml", ".opex") : KeyLocation.Replace(".mdto.xml", ".opex");
            }
        }

        public String KeyLocation { get; set; }

        public String OriginalMetadata { get; set; }

        public String StrippedNamespaceMetadata { get; set; }

        public String AddOpexNamespaceMetadata { get; set; }

        public String InitialOpexMetadata { get; set; }

        public String FinalUpdatedOpexMetadata { get; set; }

        public String PolishOpexMetadata { get; set; }

        public void InitializeOpex(List<StylesheetItem> stylesheetList)
        {
            //00
            string stripXsl = stylesheetList.Where(xsl => xsl.KeyLocation.EndsWith(OpexItem.STRIP_NAMESPACE)).First().XmlContent;
            //01
            string addXsl = stylesheetList.Where(xsl => xsl.KeyLocation.EndsWith(OpexItem.ADD_NAMESPACE)).First().XmlContent;
            //02 or 03 or 04
            string opexXsl = stylesheetList.Where(xsl => xsl.KeyLocation.EndsWith(this.IsFile
                ? OpexItem.OPEX_FILES : this.IsMultipleMetadataFilesFoundInCurrentFolder
                ? OpexItem.OPEX_FOLDER_FILES : OpexItem.OPEX_FOLDERS)).First().XmlContent;
            //05
            string polishXsl = stylesheetList.Where(xsl => xsl.KeyLocation.EndsWith(OpexItem.OPEX_FINALIZE)).First().XmlContent;

            StrippedNamespaceMetadata = Transform(stripXsl, OriginalMetadata);
            AddOpexNamespaceMetadata = Transform(addXsl, StrippedNamespaceMetadata);
            InitialOpexMetadata = Transform(opexXsl, AddOpexNamespaceMetadata);
            PolishOpexMetadata = Transform(polishXsl, InitialOpexMetadata);
        }
        public void UpdateOpexFileLevel(String servername, String port, List<XmlElement> descriptiveMetadataList, bool overwriteChecksumHashValue = false)
        {
            FinalUpdatedOpexMetadata = PolishOpexMetadata;

            if (!IsFile)
                return;

            if (!overwriteChecksumHashValue)
                return;

            var currentOpexMetadataFile = Preingest.WebApi.Utilities.DeserializerHelper.DeSerializeObjectFromXmlFile<opexMetadata>(PolishOpexMetadata);
            FileInfo currentInfo = new FileInfo(this.KeyOpexLocation);

            string fixity = string.Empty;

            FileInfo metadata = new FileInfo(this.KeyLocation);
            string toReplace = metadata.Extension.Equals(".metadata") ? metadata.Extension : ".bestand.mdto.xml";
            FileInfo binaryFilename = new FileInfo(metadata.FullName.Replace(toReplace, String.Empty));

            if (!binaryFilename.Exists)
                throw new FileNotFoundException(String.Format("Calculate checksum failed! Binary file '{0}' not found.", binaryFilename.FullName));

            string encodedFilename = WebApi.Utilities.ChecksumHelper.Base64Encode(binaryFilename.FullName);
            string url = String.Format("http://{0}:{1}/fixity/sha256/{2}", servername, port, encodedFilename);

            fixity = Preingest.WebApi.Utilities.ChecksumHelper.CreateSHA256Checksum(binaryFilename, url);

            currentOpexMetadataFile.Transfer = new transfer();
            currentOpexMetadataFile.Transfer.Fixities = new fixity[] { new fixity { type = "SHA-256", value = fixity } };

            if(descriptiveMetadataList.Count > 0)
            {
                var currentItems = currentOpexMetadataFile.DescriptiveMetadata.Any.ToList();
                currentItems.AddRange(descriptiveMetadataList);
                currentOpexMetadataFile.DescriptiveMetadata.Any = currentItems.ToArray();
            }
                           
            FinalUpdatedOpexMetadata = Preingest.WebApi.Utilities.SerializerHelper.SerializeObjectToString(currentOpexMetadataFile);
        }
        public void UpdateOpexFolderLevel()
        {
            var currentOpexMetadataFile = Preingest.WebApi.Utilities.DeserializerHelper.DeSerializeObjectFromXmlFile<opexMetadata>(PolishOpexMetadata);
            FileInfo currentInfo = new FileInfo(this.KeyOpexLocation);

            var files = currentInfo.Directory.GetFiles().ToList();
            var directories = currentInfo.Directory.GetDirectories().ToList();
            //overwrite
            if (files.Count > 0 && currentOpexMetadataFile.Transfer.Fixities == null)
            {
                currentOpexMetadataFile.Transfer.Manifest = new manifest();
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
                if (currentOpexMetadataFile.Transfer.Manifest == null)
                    currentOpexMetadataFile.Transfer.Manifest = new manifest();
                
                currentOpexMetadataFile.Transfer.Manifest.Folders = directories.Select(item => item.Name).ToArray();
            }
            //generate default GUID. Overwrite it with stylesheet.
            currentOpexMetadataFile.Transfer.SourceID = Guid.NewGuid().ToString();
            FinalUpdatedOpexMetadata = Preingest.WebApi.Utilities.SerializerHelper.SerializeObjectToString(currentOpexMetadataFile);
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
            get
            {
                return this.OriginalMetadata.Contains("<bestand>", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        private String Transform(string xsl, string xml)
        {
            string output = String.Empty;

            using (StringReader srt = new StringReader(xsl)) // xslInput is a string that contains xsl
            using (StringReader sri = new StringReader(xml)) // xmlInput is a string that contains xml
            {
                using (XmlReader xrt = XmlReader.Create(srt))
                using (XmlReader xri = XmlReader.Create(sri))
                {
                    XslCompiledTransform xslt = new XslCompiledTransform();
                    xslt.Load(xrt);
                    using (UTF8StringWriter sw = new UTF8StringWriter())
                    using (XmlWriter xwo = XmlWriter.Create(sw, xslt.OutputSettings)) // use OutputSettings of xsl, so it can be output as HTML
                    {
                        xslt.Transform(xri, xwo);
                        output = sw.ToString();
                    }
                }
            }

            return output;
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

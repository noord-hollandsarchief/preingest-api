using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex
{
    public class MetadataContainerItem
    {
        public Guid Reference { get; set; }
        public String Key { get; set; }

        public String[] KeyParts
        {
            get
            {
                return this.Key.Split("/").ToArray();
            }
        }
        public String CurrentName
        {
            get
            {
                return this.KeyParts.Last();
            }
        }

        public String OriginalName
        {
            get
            {
                if (this.XIP == null)
                {
                    return Path.GetRandomFileName();
                }
                else
                {
                    if (XIP.Bitstream == null || XIP.Bitstream.Length == 0)
                        return Path.GetRandomFileName();

                    if (XIP.Bitstream.First() == null)
                        return Path.GetRandomFileName();

                    return XIP.Bitstream.First().Filename;
                }
            }
        }

        public MetadataContainerItem(String key, opexMetadata opex)
        {
            if (opex.Transfer != null)
                Reference = Guid.Parse(opex.Transfer.SourceID);
            else
                Reference = Guid.Empty;

            OPEX = opex;
            Key = key;
        }

        public MetadataContainerItem(XIPType xip)
        {
            if (xip.InformationObject != null && xip.InformationObject.FirstOrDefault() != null)
                Reference = Guid.Parse(xip.InformationObject.FirstOrDefault().Ref);
            else
                Reference = Guid.Empty;

            XIP = xip;
            Key = String.Empty;
        }

        public XIPType XIP { get; set; }

        public opexMetadata OPEX { get; set; }

        public bool IsCompletely
        {
            get
            {
                return (this.XIP != null && this.OPEX != null);
            }
        }

        public override string ToString()
        {
            return String.Format("Reference: {0}, Complete: {1}", Reference, IsCompletely ? "Yes" : "No");
        }

        public void SaveOpex(DirectoryInfo targetFolder)
        {
            if (OPEX != null)
            {
                string outputPath = string.Empty;
                if (IsCompletely)
                    outputPath = Path.Combine(targetFolder.FullName, Path.Combine(this.KeyParts.Skip(0).Take(this.KeyParts.Length - 1).ToArray()), String.Concat(this.OriginalName, ".opex"));
                else
                    outputPath = Path.Combine(targetFolder.FullName, this.Key);

                if (!IsCompletely)
                {
                    var currentDir = new DirectoryInfo(Path.Combine(targetFolder.FullName, Path.Combine(this.KeyParts.Skip(0).Take(this.KeyParts.Length - 1).ToArray())));
                    currentDir.Refresh();
                    var fileinfos = currentDir.GetFiles();
                    var dirinfos = currentDir.GetDirectories();
                    if (OPEX.Transfer != null && OPEX.Transfer.Manifest != null)
                    {
                        OPEX.Transfer.Manifest.Folders = dirinfos.Select(item => item.Name).ToArray();
                        OPEX.Transfer.Manifest.Files = fileinfos.Select(item => new fileItem
                        {
                            size = item.Length,
                            typeSpecified = true,
                            type = item.Extension.Equals(".opex", StringComparison.InvariantCultureIgnoreCase) ? fileType.metadata : fileType.content,
                            sizeSpecified = true,
                            Value = item.Name
                        }).ToArray();
                    }
                }
                Utilities.SerializerHelper.SerializeObjectToXmlFile<opexMetadata>(OPEX, outputPath);
            }
        }

        public void SaveXIP(DirectoryInfo targetFolder)
        {
            if (XIP != null)
            {
                string outputPath = Path.Combine(targetFolder.FullName, Path.Combine(this.KeyParts.Skip(0).Take(this.KeyParts.Length - 1).ToArray()), String.Concat(this.OriginalName, ".xip"));
                Utilities.SerializerHelper.SerializeObjectToXmlFile<XIPType>(XIP, outputPath);
            }
        }

        public String SaveDescriptiveMetadata(DirectoryInfo targetFolder)
        {
            string outputPath = "";
            if (OPEX != null && OPEX.DescriptiveMetadata != null && OPEX.DescriptiveMetadata.Any != null)
            {
                OPEX.DescriptiveMetadata.Any.ToList().ForEach(xml =>
                {
                    if (!(xml.Name == "ToPX" || xml.Name == "MDTO"))
                        return;

                    string content = xml.OuterXml;
                    string ext = ".xml";
                    if (xml.Name == "ToPX")
                        ext = ".metadata";
                    if (xml.Name == "MDTO" && xml.FirstChild.Name == "bestand")
                        ext = ".bestand.mdto.xml";
                    if (xml.Name == "MDTO" && xml.FirstChild.Name == "informatieobject")
                        ext = ".mdto.xml";
                                        
                    if (IsCompletely)
                        outputPath = Path.Combine(targetFolder.FullName, Path.Combine(this.KeyParts.Skip(0).Take(this.KeyParts.Length - 1).ToArray()), String.Concat(this.OriginalName, ext));
                    else
                        outputPath = Path.Combine(targetFolder.FullName, this.Key.Replace(".opex", ext));

                    XDocument metadata = XDocument.Parse(content);
                    metadata.Save(outputPath);
                });
            }
            return outputPath;
        }
    }
}


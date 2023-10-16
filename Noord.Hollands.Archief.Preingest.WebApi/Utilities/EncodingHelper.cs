using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// Helper class for retrieving file encoding
    /// </summary>
    public static class EncodingHelper
    {
        /// <summary>
        /// Gets the encoding by bom.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns></returns>
        public static Encoding GetEncodingByBom(string filename)
        {
            //into as a string
            var xmlStr = XDocument.Load(filename).ToString();    
            
            byte[] stringSide = Encoding.UTF8.GetBytes(xmlStr);
            byte[] memorySide = null;
            
            //into as a memory
            using (MemoryStream ms = new MemoryStream())
            {
                using (FileStream source = File.Open(filename, FileMode.Open))
                {
                    source.CopyTo(ms);
                }
                memorySide = ms.ToArray();
            }

            Encoding encodingResult = null;

            //compare content
            bool result = stringSide.SequenceEqual(memorySide);
            if (result)//the same then there is no BOM
            {
                //there is no BOM
                stringSide = null;
                memorySide = null;
                return encodingResult;
            }

            byte[] utf8 = Encoding.UTF8.GetPreamble();
            byte[] bom = memorySide.Take(utf8.Length).ToArray();            
            bool isUtf8 = utf8.SequenceEqual(bom);
            if (isUtf8)
            {
                encodingResult = Encoding.UTF8;
            }
            else
            {
                //byte[] latin = Encoding.Latin1.GetPreamble();
                //bom = memorySide.Take(latin.Length).ToArray();
                //bool isLatin = latin.SequenceEqual(bom);
                //if (isLatin)
                //    encodingResult = Encoding.Latin1;
                //maybe there is a different BOM            
                //if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
                    //encodingResult = Encoding.UTF7;
                if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                    encodingResult = Encoding.UTF8;
                if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0)
                    encodingResult = Encoding.UTF32; //UTF-32LE            
                if (bom[0] == 0xff && bom[1] == 0xfe)
                    encodingResult = Encoding.Unicode; //UTF-16LE            
                if (bom[0] == 0xfe && bom[1] == 0xff)
                    encodingResult = Encoding.BigEndianUnicode; //UTF-16BE            
                if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
                    encodingResult = new UTF32Encoding(true, true);  //UTF-32BE
            }
            stringSide = null;
            memorySide = null;
            bom = null;
            //hard to detect the BOM type
            return encodingResult;
        }

        /// <summary>
        /// Gets the encoding by stream.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns></returns>
        public static Encoding GetEncodingByStream(string filename)
        {
            // This is a direct quote from MSDN:  
            // The CurrentEncoding value can be different after the first
            // call to any Read method of StreamReader, since encoding
            // autodetection is not done until the first call to a Read method.

            using (var reader = new StreamReader(filename, Encoding.Default, true))
            {
                if (reader.Peek() >= 0) // you need this!
                    reader.Read();

                return reader.CurrentEncoding;
            }
        }

        /// <summary>
        /// Gets the XML encoding.
        /// </summary>
        /// <param name="xmlString">The XML string.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">
        /// The provided string value is null or empty.
        /// or
        /// The provided XML string does not contain enough data to be valid XML (see https://msdn.microsoft.com/en-us/library/system.xml.xmlreader.read)
        /// </exception>
        public static string GetXmlEncoding(string xmlString)
        {
            if (string.IsNullOrWhiteSpace(xmlString)) throw new ArgumentException("The provided string value is null or empty.");

            using (var stringReader = new StringReader(xmlString))
            {
                var settings = new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment };

                using (var xmlReader = XmlReader.Create(stringReader, settings))
                {
                    if (!xmlReader.Read()) throw new ArgumentException(
                        "The provided XML string does not contain enough data to be valid XML (see https://msdn.microsoft.com/en-us/library/system.xml.xmlreader.read)");

                    var result = xmlReader.GetAttribute("encoding");
                    return result;
                }
            }
        }
    }
}

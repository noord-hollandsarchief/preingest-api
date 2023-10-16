using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// A helper class to serialize object from/to XML
    /// </summary>
    public static class SerializerHelper
    {
        /// <summary>
        /// Serializes the object to string.
        /// </summary>
        /// <param name="objectToSerialize">The object to serialize.</param>
        /// <returns>In string format</returns>
        public static string SerializeObjectToString(Object objectToSerialize)
        {
            string result = null;

            if (objectToSerialize == null)
                return result;
            
            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(objectToSerialize.GetType());
                    serializer.Serialize(ms, objectToSerialize);
                    ms.Position = 0;
                    StreamReader rdr = new StreamReader(ms);
                    result = rdr.ReadToEnd();
                }
                catch (SerializationException)
                {
                    result = null;
                }
                finally { }
            }

            return result;
        }

        /// <summary>
        /// Serializes the object to XML element.
        /// </summary>
        /// <param name="objectToSerialize">The object to serialize.</param>
        /// <returns>XmlElement</returns>
        public static XmlElement SerializeObjectToXmlElement(Object objectToSerialize)
        {
            if (objectToSerialize == null)
                return null;

            XmlDocument doc = new XmlDocument();

            using (XmlWriter writer = doc.CreateNavigator().AppendChild())            
                new XmlSerializer(objectToSerialize.GetType()).Serialize(writer, objectToSerialize);
            
            return doc.DocumentElement;
        }
               
        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="fileName"></param>
        public static void SerializeObjectToXmlFile<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            XmlDocument xmlDocument = new XmlDocument();
            XmlSerializer serializer = new XmlSerializer(typeof(T));
                       
            string tempFileName = String.Format(@"{0}", System.IO.Path.GetTempFileName());

            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(stream, serializableObject);
                stream.Position = 0;
                xmlDocument.Load(stream);
                xmlDocument = RemoveXmlns(xmlDocument);

                XmlDeclaration xmldecl;
                xmldecl = xmlDocument.CreateXmlDeclaration("1.0", null, null);
                xmldecl.Encoding = "UTF-8";

                XmlElement root = xmlDocument.DocumentElement;
                xmlDocument.InsertBefore(xmldecl, root);

                xmlDocument.Save(tempFileName);
                stream.Close();
            }
            System.IO.File.Move(tempFileName, fileName, true);
        }

        /// <summary>
        /// Removes the Xml namespaces.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <returns></returns>
        public static XmlDocument RemoveXmlns(XmlDocument doc)
        {
            XDocument d;
            using (var nodeReader = new XmlNodeReader(doc))
                d = XDocument.Load(nodeReader);

            d.Root.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration).Remove();

            foreach (var elem in d.Descendants())
                elem.Name = elem.Name.LocalName;

            var xmlDocument = new XmlDocument();
            using (var xmlReader = d.CreateReader())
                xmlDocument.Load(xmlReader);

            return xmlDocument;
        } 
    }
}

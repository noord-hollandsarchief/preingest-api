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
    public static class DeserializerHelper
    {
        /// <summary>
        /// Deserialize object.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="type">The type.</param>
        /// <returns>Object</returns>
        public static object DeSerializeObject(string content, Type type)
        {
            XmlSerializer serializer = new XmlSerializer(type);
            StringReader sr = new StringReader(content);
            return serializer.Deserialize(sr);
        }

        /// <summary>
        /// Deserialize anonymous type object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content">The content.</param>
        /// <returns>Anonymous type</returns>
        public static T[] DeSerializeObjectAsArray<T>(string content)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T[]));
            StringReader sr = new StringReader(content);
            return (T[])serializer.Deserialize(sr);
        }

        /// <summary>
        /// Des the serialize object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        public static T DeSerializeObject<T>(string content)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            StringReader sr = new StringReader(content);
            return (T)serializer.Deserialize(sr);
        }

        /// <summary>
        /// Removes the XML namespace.
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

        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T DeSerializeObjectFromXmlFile<T>(FileInfo fileName)
        {
            if (fileName == null) { return default(T); }

            T objectOut = default(T);
            
            string attributeXml = string.Empty;

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(fileName.FullName);
            string xmlString = xmlDocument.OuterXml;

            using (StringReader read = new StringReader(xmlString))
            {
                Type outType = typeof(T);

                XmlSerializer serializer = new XmlSerializer(outType);
                using (XmlReader reader = new XmlTextReader(read))
                {
                    objectOut = (T)serializer.Deserialize(reader);
                    reader.Close();
                }
                read.Close();
            }
            return objectOut;
        }

        /// <summary>
        /// Deserializes an object from XML file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        public static T DeSerializeObjectFromXmlFile<T>(string content)
        {
            if (string.IsNullOrEmpty(content)) { return default(T); }

            T objectOut = default(T);

            string attributeXml = string.Empty;

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(content);
            string xmlString = xmlDocument.OuterXml;

            using (StringReader read = new StringReader(xmlString))
            {
                Type outType = typeof(T);

                XmlSerializer serializer = new XmlSerializer(outType);
                using (XmlReader reader = new XmlTextReader(read))
                {
                    objectOut = (T)serializer.Deserialize(reader);
                    reader.Close();
                }
                read.Close();
            }
            return objectOut;
        }

        /// <summary>
        /// Deserializes an object from XML file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content">The content.</param>
        /// <param name="namespaceCollection">The namespace collection.</param>
        /// <returns></returns>
        public static T DeSerializeObjectFromXmlFile<T>(string content, IDictionary<String, String> namespaceCollection)
        {
            if (string.IsNullOrEmpty(content)) { return default(T); }

            T objectOut = default(T);

            string attributeXml = string.Empty;

            XmlDocument xmlDocument = new XmlDocument();

            NameTable nt = new NameTable();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nt);

            namespaceCollection.ToList().ForEach(item =>
            {
                nsmgr.AddNamespace(item.Key, item.Value);
            });

            XmlParserContext context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);
            XmlReaderSettings xset = new XmlReaderSettings();
            xset.ConformanceLevel = ConformanceLevel.Fragment;
            XmlReader rd = XmlReader.Create(new StringReader(content), xset, context);

            xmlDocument.Load(rd);

            string xmlString = xmlDocument.OuterXml;

            using (StringReader read = new StringReader(xmlString))
            {
                Type outType = typeof(T);

                XmlSerializer serializer = new XmlSerializer(outType);
                using (XmlReader reader = new XmlTextReader(read))
                {
                    objectOut = (T)serializer.Deserialize(reader);
                    reader.Close();
                }
                read.Close();
            }
            return objectOut;
        }
    }
}

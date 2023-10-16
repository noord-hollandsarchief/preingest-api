using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Schema
{
    /// <summary>
    /// Helper handler with XSD schema in assembly (embedded)
    /// </summary>
    public class SchemaHandler
    {
        /// <summary>
        /// Gets the schema list.
        /// </summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetSchemaList()
        {
            SchemaHandler handler = new SchemaHandler();
            return handler.GetSchemas();
        }

        /// <summary>
        /// Gets the schemas from embedded resources
        /// </summary>
        /// <returns></returns>
        private IDictionary<String, String> GetSchemas()
        {
            IDictionary<String, String> returnList = new Dictionary<String, String>();

            string[] names = this.GetType().Assembly.GetManifestResourceNames();

            foreach (string name in names)
            {
                if (name.StartsWith("Noord.Hollands.Archief.Preingest.WebApi.Schema")) {
                    string result = string.Empty;
                    using (Stream stream = this.GetType().Assembly.GetManifestResourceStream(name))
                    {
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            result = sr.ReadToEnd();
                        }
                    }

                    returnList.Add(name, result);
                }
            }

            return returnList;            
        }
    }
}

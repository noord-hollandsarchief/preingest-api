using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Stylesheet
{
    /// <summary>
    /// Helper handler with stylesheets in assembly (embedded)
    /// </summary>
    public class StylesheetHandler
    {
        /// <summary>
        /// Gets the stylesheet list.
        /// </summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetStylesheetList()
        {
            StylesheetHandler handler = new StylesheetHandler();
            return handler.GetStylesheets();
        }

        /// <summary>
        /// Gets the stylesheets from embedded resource.
        /// </summary>
        /// <returns></returns>
        private IDictionary<String, String> GetStylesheets()
        {
            IDictionary<String, String> returnList = new Dictionary<String, String>();

            string[] names = this.GetType().Assembly.GetManifestResourceNames();

            foreach (string name in names)
            {
                if (name.StartsWith("Noord.Hollands.Archief.Preingest.WebApi.Stylesheet")) {
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

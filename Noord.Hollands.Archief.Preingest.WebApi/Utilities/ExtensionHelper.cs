using Newtonsoft.Json.Linq;

using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// Helper class for Dictionary conversion
    /// </summary>
    public static class ExtensionHelper
    {
        /// <summary>
        /// Converts to keyvalue.
        /// </summary>
        /// <param name="metaToken">The meta token.</param>
        /// <returns></returns>
        public static IDictionary<string, string> ToKeyValue(this object metaToken)
        {
            if (metaToken == null)            
                return null;
            

            JToken token = metaToken as JToken;
            if (token == null)            
                return ToKeyValue(JObject.FromObject(metaToken));
            

            if (token.HasValues)
            {
                var contentData = new Dictionary<string, string>();
                foreach (var child in token.Children().ToList())
                {
                    var childContent = child.ToKeyValue();
                    if (childContent != null)                    
                        contentData = contentData.Concat(childContent).ToDictionary(k => k.Key, v => v.Value);
                    
                }

                return contentData;
            }

            var jValue = token as JValue;
            if (jValue?.Value == null)            
                return null;            

            var value = jValue?.Type == JTokenType.Date ?
                            jValue?.ToString("o", CultureInfo.InvariantCulture) :
                            jValue?.ToString(CultureInfo.InvariantCulture);

            return new Dictionary<string, string> { { token.Path, value } };
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
  
    public class SchemaValidationHandler
    {
        /// <summary>
        /// Validation Error message. Null of none
        /// </summary>
        public string XsdValidationError { get; private set; }

        /// <summary>
        /// Validate using XSD
        /// </summary>
        /// <param name="toValidate">To validate.</param>
        /// <param name="mainXsdLocation">The main XSD location.</param>
        public static void Validate(object toValidate, String mainXsdLocation)
        {
            SchemaValidationHandler handler = new SchemaValidationHandler(toValidate, mainXsdLocation, new List<String>());
        }

        /// <summary>
        /// Validate using a main XSD and a number of helper XSDs
        /// </summary>
        /// <param name="toValidate">To validate.</param>
        /// <param name="mainXsdLocation">The main XSD location.</param>
        /// <param name="helperXsdLocation">The helper XSD location.</param>
        public static void Validate(object toValidate, String mainXsdLocation, String helperXsdLocation)
        {
            List<String> helperXsdLocations = new List<String>();
            helperXsdLocations.Add(helperXsdLocation);
            SchemaValidationHandler handler = new SchemaValidationHandler(toValidate, mainXsdLocation, helperXsdLocations);
        }

        /// <summary>
        /// Validate using a main XSD and a number of helper XSDs
        /// </summary>
        /// <param name="toValidate">To validate.</param>
        /// <param name="mainXsdLocation">The main XSD location.</param>
        /// <param name="helperXsdLocations">The helper XSD locations.</param>
        public static void Validate(object toValidate, String mainXsdLocation, List<String> helperXsdLocations)
        {
            SchemaValidationHandler handler = new SchemaValidationHandler(toValidate, mainXsdLocation, helperXsdLocations);
        }

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidationHandler"/> class.
        /// </summary>
        /// <param name="toValidate">To validate.</param>
        /// <param name="mainXsdLocation">The main XSD location.</param>
        /// <param name="helperXsdLocations">The helper XSD locations.</param>
        private SchemaValidationHandler(object toValidate, String mainXsdLocation, List<String> helperXsdLocations)
        {
            if (toValidate == null || String.IsNullOrEmpty(mainXsdLocation))
            {
                throw new ArgumentException(string.Format("Cannot validate without an object:{0} or schema:{1}", toValidate, mainXsdLocation));
            }

            // reset stuff
            InitSchemaValidationHandler();

            // get the schema
            List<XmlSchema> schemas = new List<XmlSchema>();
            schemas.Add(XmlSchema.Read(new XmlTextReader(mainXsdLocation), ValidationEvent));
            foreach (string schemaFile in helperXsdLocations)
            {
                // get the schema
                XmlSchema xmlSchema = XmlSchema.Read(new XmlTextReader(schemaFile), ValidationEvent);
                schemas.Add(xmlSchema);
            }
            ValidateToSchema(toValidate, schemas);
        }               
        #endregion

        #region Validations
        private void ValidateToSchema(object request, List<XmlSchema> xmlSchemas)
        {
            Type tpe = request.GetType();

            MemoryStream ms = new MemoryStream();
            if (tpe != typeof(String))
            {
                // first serialize the request into a memory stream

                //SoapFormatter serializer = new SoapFormatter();
                XmlSerializer serializer = new XmlSerializer(tpe, xmlSchemas[0].TargetNamespace);
                serializer.Serialize(ms, request);
            }
            else
            {
                StreamWriter streamWriter = new StreamWriter(ms);
                streamWriter.Write(request.ToString());
                streamWriter.Flush();
            }

            ms.Position = 0;

            // Create reader settings
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
            xmlReaderSettings.ValidationType = ValidationType.Schema;

            // load the schema's
            foreach (XmlSchema schema in xmlSchemas)
            {
                xmlReaderSettings.Schemas.Add(schema);
            }

            // set the flags
            xmlReaderSettings.ValidationFlags = XmlSchemaValidationFlags.ProcessSchemaLocation;
            xmlReaderSettings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
            xmlReaderSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;

            // Attach event handler whic will be fired when validation error occurs
            xmlReaderSettings.ValidationEventHandler += new ValidationEventHandler(ValidationEvent);

            #region some debugging stuff we don't use but shows us the serialized object
            StreamReader sr = new StreamReader(ms);
            String x = sr.ReadToEnd();
            ms.Position = 0;
            #endregion end of debugging

            try
            {
                // Create object of XmlReader using XmlReaderSettings
                using (XmlReader xmlReader = XmlReader.Create(new XmlTextReader(ms), xmlReaderSettings))
                {
                    while (xmlReader.Read())
                    {
                    }
                }
            }
            catch (Exception)
            {

                this.XsdValidationError = "Error tijdens het valideren";
            }

            if (this.XsdValidationError != null)
            {
                throw new XmlException(this.XsdValidationError);
            }
        }

        private void ValidationEvent(object source, ValidationEventArgs args)
        {
            this.XsdValidationError = args.Message;
        }
        #endregion

        #region Private
        /// <summary>
        /// Inits the validation handler.
        /// </summary>
        private void InitSchemaValidationHandler()
        {
            this.XsdValidationError = null;
        }
        #endregion
    }
}

using Newtonsoft.Json;

using System.Collections.Generic;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class ErrorSchema
    {
        public int line { get; set; }
        public int col { get; set; }
        public string message { get; set; }
    }

    public class SchemaValidationReport
    {
        public List<ErrorSchema> errors { get; set; }
    }

    public class ErrorSchematron
    {
        [JsonProperty("failed-assert-test")]
        public string FailedAssertTest { get; set; }
        [JsonProperty("fired-rule-context")]
        public string FiredRuleContext { get; set; }
        [JsonProperty("failed-assert-location")]
        public string FailedAssertLocation { get; set; }
        public string message { get; set; }
    }

    public class SchematronValidationReport
    {
        public List<ErrorSchematron> errors { get; set; }
    }

    public class Root
    {
        [JsonProperty("schema-validation-report")]
        public SchemaValidationReport SchemaValidationReport { get; set; }
        [JsonProperty("schematron-validation-report")]
        public SchematronValidationReport SchematronValidationReport { get; set; }
    }
}

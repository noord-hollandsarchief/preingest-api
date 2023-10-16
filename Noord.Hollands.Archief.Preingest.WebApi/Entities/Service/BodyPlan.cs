using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.ComponentModel;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Service
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ValidationActionType
    {
        SettingsHandler,
        ContainerChecksumHandler,
        ExportingHandler,
        ReportingPdfHandler,
        ReportingDroidXmlHandler,
        ReportingPlanetsXmlHandler,
        ProfilesHandler,
        EncodingHandler,
        UnpackTarHandler,
        MetadataValidationHandler,
        NamingValidationHandler,
        GreenListHandler,
        ExcelCreatorHandler,
        ScanVirusValidationHandler,
        SidecarValidationHandler,
        PrewashHandler,
        ShowBucketHandler,
        ClearBucketHandler,
        BuildOpexHandler,
        PolishHandler,
        UploadBucketHandler,
        FilesChecksumHandler,
        IndexMetadataHandler,
        PasswordDetectionHandler,
        ToPX2MDTOHandler,
        PronomPropsHandler,
        RelationshipHandler,
        FixityPropsHandler,
        BinaryFileObjectValidationHandler,
        BinaryFileMetadataMutationHandler,
        BuildNonMetadataOpexHandler,
        RevertCollectionHandler
    }


    public class BodyPlan : IEquatable<BodyPlan>
    {
        public ValidationActionType ActionName { get; set; }
        public bool ContinueOnFailed { get; set; }
        public bool ContinueOnError { get; set; }
        [DefaultValue(true)]
        public bool StartOnError { get; set; }
        public bool Equals(BodyPlan other)
        {
            return other != null && other.ActionName == this.ActionName;
        }

        public override int GetHashCode()
        {
            return this.ActionName.GetHashCode();
        }
    }
}

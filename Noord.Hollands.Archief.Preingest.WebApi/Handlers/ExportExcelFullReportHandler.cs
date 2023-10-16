using ClosedXML.Excel;
using Newtonsoft.Json;

using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Output;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Service;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers.OPEX;

using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Collections.Generic;

using static Noord.Hollands.Archief.Preingest.WebApi.Handlers.GreenListHandler;
using ClosedXML.Graphics;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Logic for creating a full Excel report. The logic loads all the JSON output file of each handler action.
    /// </summary>
    public class ExportExcelFullReportHandler
    {
        /// <summary>
        /// Builds the excel.
        /// </summary>
        /// <param name="targetFolder">The target folder.</param>
        /// <param name="actionArray">The action array.</param>
        /// <returns></returns>
        public static FileInfo BuildExcel(DirectoryInfo targetFolder, QueryResultAction[] actionArray)
        {
            Dictionary<String, DataTable> content = new Dictionary<string, DataTable>();

            var distinctActionNames = actionArray.Select(item => item.Name).Distinct().ToList();
            bool alreadyAnExcel = (distinctActionNames.Contains(ValidationActionType.IndexMetadataHandler.ToString()));

            //create new excel or use existing and make a copy
            FileInfo indexingMetadataExcelFile = null;
            string indexingMetadataExcelFilename = Path.Combine(targetFolder.FullName, String.Concat(ValidationActionType.IndexMetadataHandler.ToString(), ".xlsx"));
            bool exists = File.Exists(indexingMetadataExcelFilename) && distinctActionNames.Contains(ValidationActionType.IndexMetadataHandler.ToString());
            if (exists)
                indexingMetadataExcelFile = new FileInfo(indexingMetadataExcelFilename);

            int i = 1;

            DataTable summaryTable = LoadSummary(targetFolder, distinctActionNames, actionArray);
            content.Add(String.Format("P{0} - Overzicht", i.ToString("D2")), summaryTable);

            distinctActionNames.ForEach(item =>
            {
                var action = actionArray.Where(action => action.Name == item).LastOrDefault();

                bool hasJson = (action.ResultFiles.LastOrDefault(item => item.EndsWith(".json")) == null);

                ValidationActionType switchResult = (ValidationActionType)Enum.Parse(typeof(ValidationActionType), action.Name);

                if (switchResult == (ValidationActionType.ExcelCreatorHandler | ValidationActionType.ProfilesHandler | ValidationActionType.ReportingDroidXmlHandler | ValidationActionType.ReportingPdfHandler | ValidationActionType.ReportingPlanetsXmlHandler))
                    return;
                if (hasJson)
                    return;

                string jsonFilename = Path.Combine(targetFolder.FullName, action.ResultFiles.LastOrDefault(item => item.EndsWith(".json")));
                if (!File.Exists(jsonFilename))
                    return;

                string jsonContent = File.ReadAllText(jsonFilename);

                PreingestActionModel jsonDataFile = JsonConvert.DeserializeObject<PreingestActionModel>(jsonContent);
                
                switch (switchResult)
                {
                    case ValidationActionType.ContainerChecksumHandler:
                    case ValidationActionType.SettingsHandler:
                    case ValidationActionType.IndexMetadataHandler:
                        {
                            //this does actually nothing, just skip
                            if (ValidationActionType.SettingsHandler == switchResult) {}
                            if (ValidationActionType.ContainerChecksumHandler == switchResult){}
                            if (ValidationActionType.IndexMetadataHandler == switchResult){}
                        }
                        break;
                    case ValidationActionType.UnpackTarHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Uitpakken", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.ScanVirusValidationHandler:
                        {
                            DataTable table = LoadJson<VirusScanItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Virus scan", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.PrewashHandler:
                        {
                            DataTable table = LoadJson<WashedItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Voorbewerkingen", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.NamingValidationHandler:
                        {
                            DataTable table = LoadJson<NamingItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Tekens & karakters", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.SidecarValidationHandler:
                        {
                            DataTable table = LoadJson<SidecarValidationHandler.MessageResult>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Structuur", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.GreenListHandler:
                        {
                            DataTable table = LoadJson<DataItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Voorkeursbestanden", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.EncodingHandler:
                        {
                            DataTable table = LoadJson<EncodingItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Coderingen", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.MetadataValidationHandler:
                        {
                            DataTable table = LoadJson<MetadataValidationItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Validaties", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.FilesChecksumHandler:
                        {
                            DataTable table = LoadJson<FilesChecksumHandler.ResultType>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Controle getallen", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.BuildOpexHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - OPEX construeren", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.BuildNonMetadataOpexHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - OPEX construeren", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.PolishHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - OPEX bijwerken", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.ClearBucketHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Opschonen (bucket)", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.ShowBucketHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Raadplegen (bucket)", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.UploadBucketHandler:
                        {
                            DataTable table = LoadJson(action.Name, jsonDataFile, false, true);
                            content.Add(String.Format("P{0} - Upload (bucket)", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.PasswordDetectionHandler:
                        {
                            DataTable table = LoadJson<PasswordDetectionHandler.ResultItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Beveiliging", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.ToPX2MDTOHandler:
                        {
                            DataTable table = LoadJson<ToPX2MDTO.ToPX2MDTOHandler.ResultItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Conversie", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.PronomPropsHandler:
                        {
                            DataTable table = LoadJson<ToPX2MDTO.PronomPropsHandler.ResultItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Bestandsformaat", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.FixityPropsHandler:
                        {
                            DataTable table = LoadJson<ToPX2MDTO.FixityPropsHandler.ResultItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Checksum", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.RelationshipHandler:
                        {
                            DataTable table = LoadJson<ToPX2MDTO.RelationshipHandler.ResultItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Relaties", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.BinaryFileObjectValidationHandler:
                        {
                            DataTable table = LoadJson<BinaryFileObjectValidationHandler.ActionDataItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Bestanden", i.ToString("D2")), table);
                        }
                        break;
                    case ValidationActionType.BinaryFileMetadataMutationHandler:
                        {
                            DataTable table = LoadJson<BinaryFileMetadataMutationHandler.ActionDataItem>(action.Name, jsonDataFile);
                            content.Add(String.Format("P{0} - Mutaties", i.ToString("D2")), table);
                        }
                        break;
                    default:
                        { }
                        break;
                }
                i++;
            });

            FileInfo tmpResultFile = ExportToExcel(content, indexingMetadataExcelFile);

            string outputFilename = Path.Combine(targetFolder.FullName, String.Concat(ValidationActionType.ExcelCreatorHandler.ToString(), ".xlsx"));
            tmpResultFile.MoveTo(outputFilename, true);

            return new FileInfo(outputFilename);
        }

        /// <summary>
        /// Loads the summary.
        /// </summary>
        /// <param name="targetFolder">The target folder.</param>
        /// <param name="actions">The actions.</param>
        /// <param name="actionArray">The action array.</param>
        /// <returns></returns>
        private static DataTable LoadSummary(DirectoryInfo targetFolder, List<String> actions, QueryResultAction[] actionArray)
        {
            DataTable table = new DataTable("Summary");

            Type columnNamesResult = typeof(PreingestResult);
            Type columnNamesSummary = typeof(PreingestStatisticsSummary);            
            Type columnNamesProps = typeof(PreingestProperties);

            table.Columns.AddRange(columnNamesResult.GetProperties().Select(item 
                => new DataColumn(item.Name, typeof(String))).ToArray());
            table.Columns.AddRange(columnNamesProps.GetProperties().Select(item 
                => new DataColumn(item.Name, typeof(String))).ToArray());
            table.Columns.AddRange(columnNamesSummary.GetProperties().Select(item 
                => new DataColumn(item.Name, typeof(String))).ToArray());

            foreach (string actionName in actions)
            {
                var action = actionArray.Where(action => action.Name == actionName).LastOrDefault();
                bool hasJson = (action.ResultFiles.LastOrDefault(item => item.EndsWith(".json")) == null);
                ValidationActionType switchResult = (ValidationActionType)Enum.Parse(typeof(ValidationActionType), action.Name);

                if (switchResult == (ValidationActionType.ExcelCreatorHandler | ValidationActionType.ProfilesHandler | ValidationActionType.ReportingDroidXmlHandler | ValidationActionType.ReportingPdfHandler | ValidationActionType.ReportingPlanetsXmlHandler))
                    continue;
                if (hasJson)
                    continue;

                string jsonFilename = Path.Combine(targetFolder.FullName, action.ResultFiles.LastOrDefault(item => item.EndsWith(".json")));
                if (!File.Exists(jsonFilename))
                    continue;

                string jsonContent = File.ReadAllText(jsonFilename);
                PreingestActionModel jsonDataFile = JsonConvert.DeserializeObject<PreingestActionModel>(jsonContent);

                var summary = jsonDataFile.Summary;
                var result = jsonDataFile.ActionResult;
                var props = jsonDataFile.Properties;

                DataRow row = table.NewRow();

                var resultDataResult = result.GetType().GetProperties().Select(item => new
                {
                    Name = item.Name,
                    PropValue = item.GetValue(result) == null ? String.Empty : item.GetValue(result).ToString()
                }).ToList();
                resultDataResult.ForEach(item => row[item.Name] = item.PropValue);

                var resultDataProps = props.GetType().GetProperties().Select(item => new
                {
                    Name = item.Name,
                    PropValue = item.Name.Equals("Messages", StringComparison.InvariantCultureIgnoreCase) ? item.GetValue(props) == null ? String.Empty : String.Join(Environment.NewLine, (item.GetValue(props) as string[])) : item.GetValue(props) == null ? String.Empty : item.GetValue(props).ToString()
                }).ToList();
                resultDataProps.ForEach(item => row[item.Name] = item.PropValue);

                var resultDataSummary = summary.GetType().GetProperties().Select(item => new
                {
                    Name = item.Name,
                    PropValue = item.GetValue(summary).ToString()
                }).ToList();
                resultDataSummary.ForEach(item => row[item.Name] = item.PropValue);

                table.Rows.Add(row);
            }
            return table;
        }

        /// <summary>
        /// Load JSON file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="actionName">Name of the action.</param>
        /// <param name="jsonDataFile">The json data file.</param>
        /// <returns></returns>
        private static DataTable LoadJson<T>(String actionName, PreingestActionModel jsonDataFile)
        {
            string[] messages = jsonDataFile.Properties.Messages == null ? new string[] { } : jsonDataFile.Properties.Messages;
            T[] data = jsonDataFile.ActionData == null ? new T[] { } : JsonConvert.DeserializeObject<T[]>(jsonDataFile.ActionData.ToString());

            DataTable table = new DataTable(actionName);
            Type type = typeof(T);
            var names = type.GetProperties().Select(item => new DataColumn { ColumnName = item.Name, DataType = typeof(String) }).ToArray();
            table.Columns.AddRange(names);

            if (data.Count() == 0)
            {
                table.Columns.Add("Resultaat", typeof(String));
                DataRow row = table.NewRow();
                row["Resultaat"] = "";
                table.Rows.Add(row);
            }
            else
            {
                FillData<T>(data, table);
            }
            return table;
        }

        /// <summary>
        /// Load JSON file.
        /// </summary>
        /// <param name="actionName">Name of the action.</param>
        /// <param name="jsonDataFile">The json data file.</param>
        /// <param name="isArrayActionDataType">if set to <c>true</c> [is array action data type].</param>
        /// <param name="newLineSplit">if set to <c>true</c> [new line split].</param>
        /// <returns></returns>
        private static DataTable LoadJson(String actionName, PreingestActionModel jsonDataFile, bool isArrayActionDataType = false, bool newLineSplit = false)
        {
            string[] messages = jsonDataFile.Properties.Messages == null ? new string[] { } : jsonDataFile.Properties.Messages;

            Newtonsoft.Json.Linq.JArray data = Newtonsoft.Json.Linq.JArray.Parse(jsonDataFile.ActionData.ToString());
            DataTable table = new DataTable(actionName);
            table.Columns.Add("Resultaat", typeof(String));
            if(data.Count == 0)
            {                
                DataRow row = table.NewRow();
                row["Resultaat"] = "";
                table.Rows.Add(row);
            }

            foreach (var rowContent in data)
            {
                DataRow row = table.NewRow();
                string text = string.Empty;

                if (isArrayActionDataType)
                {
                    text = String.Join(Environment.NewLine, rowContent.ToObject<string[]>());
                }
                else
                {
                    text = rowContent.ToObject<string>();
                }

                if (newLineSplit)
                {
                    text = String.Join(Environment.NewLine, text.Split(@"\r", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }

                row["Resultaat"] = text.Trim();
                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Fills the data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">The data.</param>
        /// <param name="table">The table.</param>
        private static void FillData<T>(T[] data, DataTable table)
        {
            foreach (var rowContent in data)
            {
                DataRow row = table.NewRow();
                Type currentType = rowContent.GetType();
                var props = currentType.GetProperties();

                foreach (var prop in props)
                {
                    bool isNullField = (prop.GetValue(rowContent) == null);

                    Type valueType = isNullField ? typeof(String) : prop.GetValue(rowContent).GetType();
                    string rowValue = string.Empty;
                    if (valueType.IsArray)
                    {
                        string[] array = prop.GetValue(rowContent) as string[];
                        if (array == null)
                            array = new string[] { };

                        rowValue = String.Join(Environment.NewLine, array).Trim();
                    }
                    else
                    {
                        rowValue = isNullField ? "" : prop.GetValue(rowContent).ToString();
                    }

                    row[prop.Name] = rowValue;
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// Exports to excel.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="excel">The excel.</param>
        /// <returns></returns>

        //TODO : Optimize this function and move Excel-related tasks to a different function.
        private static FileInfo ExportToExcel(Dictionary<String, DataTable> content, FileInfo excel = null)
{
    var tempFile = String.Concat(System.IO.Path.GetTempFileName(), ".xlsx");
    LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");
    if (excel == null)
    {                
        using (XLWorkbook wb = new XLWorkbook())
        {                    
            foreach (KeyValuePair<String, DataTable> sheet in content)
            {
                var resultaat = wb.Worksheets.Add(sheet.Key);
                int headerIndex = 1;

             
                var headerRow = resultaat.Row(1);
                headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4676b5");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.White;
              

                foreach (DataColumn column in sheet.Value.Columns)
                {
                    resultaat.Cell(1, headerIndex).Value = column.ColumnName;
                    headerIndex++;
                }
                

                bool isEvenRow = false; 

                if (sheet.Key.Contains("Beveiliging"))
                {
                    int rowIndex = 2; 
                    foreach (DataRow row in sheet.Value.Rows)
                    {
                        int columnIndex = 1;
                        foreach (var cell in row.ItemArray)
                        {
                            string cellValue = cell.ToString();
                            int length = Math.Min(cellValue.Length, 32767);
                            resultaat.Cell(rowIndex, columnIndex).Value = cellValue.Substring(0, length);
                            columnIndex++;
                        }
                        
                
                        if (isEvenRow)
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.White;
                            resultaat.Row(rowIndex).Style.Border.RightBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.RightBorderColor = XLColor.FromHtml("#808080");
                            resultaat.Row(rowIndex).Style.Border.LeftBorderColor = XLColor.FromHtml("#808080");
                        }
                        else
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#d7e2f0");
                            resultaat.Row(rowIndex).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.BottomBorderColor = XLColor.FromHtml("#4676b5");
                            resultaat.Row(rowIndex).Style.Border.TopBorderColor = XLColor.FromHtml("#4676b5");
                        }
                        
                        isEvenRow = !isEvenRow; 
                        rowIndex++;
                    }
                }
                else
                {
                    var data = sheet.Value.AsEnumerable().Select(row => row.ItemArray.Select(cell => cell.ToString().Substring(0, Math.Min(cell.ToString().Length, 32767))));
                    int rowIndex = 2; 
                    foreach (var row in data)
                    {
                        int columnIndex = 1;
                        foreach (var cell in row)
                        {
                            resultaat.Cell(rowIndex, columnIndex).Value = cell;
                            columnIndex++;
                        }
                        
                        
                        if (isEvenRow)
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.White;
                            resultaat.Row(rowIndex).Style.Border.RightBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.RightBorderColor = XLColor.FromHtml("#808080");
                            resultaat.Row(rowIndex).Style.Border.LeftBorderColor = XLColor.FromHtml("#808080");
                        }
                        else
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#d7e2f0");
                            resultaat.Row(rowIndex).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.BottomBorderColor = XLColor.FromHtml("#4676b5");
                            resultaat.Row(rowIndex).Style.Border.TopBorderColor = XLColor.FromHtml("#4676b5");
                        }
                        
                        isEvenRow = !isEvenRow; 
                        rowIndex++;
                    }
                }
                resultaat.Columns().AdjustToContents();
                resultaat.Rows().AdjustToContents();
                resultaat.RangeUsed().SetAutoFilter();
            }
            wb.SaveAs(tempFile);
        }
    }
    else
    {
        using (XLWorkbook wb = new XLWorkbook(excel.FullName))
        {
            foreach (KeyValuePair<String, DataTable> sheet in content)
            {
                var resultaat = wb.Worksheets.Add(sheet.Key);
                if (sheet.Key == "P01 - Overzicht")
                {
                    resultaat.Position = 1;
                    resultaat.Select();
                }
                
                int headerIndex = 1;

              
                var headerRow = resultaat.Row(1);
                headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4676b5");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.White;

                foreach (DataColumn column in sheet.Value.Columns)
                {
                    resultaat.Cell(1, headerIndex).Value = column.ColumnName;
                    headerIndex++;
                }

                bool isEvenRow = false; 

                if (sheet.Key.Contains("Beveiliging")) 
                {
                    int rowIndex = 2; 
                    foreach (DataRow row in sheet.Value.Rows)
                    {
                        int columnIndex = 1;
                        foreach (var cell in row.ItemArray)
                        {
                            string cellValue = cell.ToString();
                            int length = Math.Min(cellValue.Length, 32767);
                            resultaat.Cell(rowIndex, columnIndex).Value = cellValue.Substring(0, length);
                            columnIndex++;
                        }
                        
                 
                        if (isEvenRow)
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.White;
                            resultaat.Row(rowIndex).Style.Border.RightBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.RightBorderColor = XLColor.FromHtml("#808080");
                            resultaat.Row(rowIndex).Style.Border.LeftBorderColor = XLColor.FromHtml("#808080");
                        }
                        else
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#d7e2f0");
                            resultaat.Row(rowIndex).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.BottomBorderColor = XLColor.FromHtml("#4676b5");
                            resultaat.Row(rowIndex).Style.Border.TopBorderColor = XLColor.FromHtml("#4676b5");
                        }
                        
                        isEvenRow = !isEvenRow; 
                        rowIndex++;
                    }
                }
                else
                {
                    var data = sheet.Value.AsEnumerable().Select(row => row.ItemArray.Select(cell => cell.ToString().Substring(0, Math.Min(cell.ToString().Length, 32767))));
                    int rowIndex = 2; 
                    foreach (var row in data)
                    {
                        int columnIndex = 1;
                        foreach (var cell in row)
                        {
                            resultaat.Cell(rowIndex, columnIndex).Value = cell;
                            columnIndex++;
                        }
                        
                        
                        if (isEvenRow)
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.White;
                            resultaat.Row(rowIndex).Style.Border.RightBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.RightBorderColor = XLColor.FromHtml("#808080");
                            resultaat.Row(rowIndex).Style.Border.LeftBorderColor = XLColor.FromHtml("#808080");
                        }
                        else
                        {
                            resultaat.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#d7e2f0");
                            resultaat.Row(rowIndex).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                            resultaat.Row(rowIndex).Style.Border.BottomBorderColor = XLColor.FromHtml("#4676b5");
                            resultaat.Row(rowIndex).Style.Border.TopBorderColor = XLColor.FromHtml("#4676b5");
                        }
                        
                        isEvenRow = !isEvenRow; 
                        rowIndex++;
                    }
                }
                resultaat.Columns().AdjustToContents();
                resultaat.Rows().AdjustToContents();
                resultaat.RangeUsed().SetAutoFilter();
            }
            wb.SaveAs(tempFile);
        }
    }

    return new FileInfo(tempFile);
}


    }
}

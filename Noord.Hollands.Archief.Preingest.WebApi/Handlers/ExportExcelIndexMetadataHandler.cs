using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Logic for creating an Excel report with all metadata (flatten).
    /// </summary>
    public class ExportExcelIndexMetadataHandler
    {
        /// <summary>
        /// Exports to excel.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="errorsList">The errors list.</param>
        /// <returns></returns>
        public static string ExportToExcel(List<IndexMetadataHandler.ResultPair> data, List<IndexMetadataHandler.ProcessResultItem> errorsList)
        {
            var tempFile = String.Concat(System.IO.Path.GetTempFileName(), ".xlsx");

            LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("DejaVu Sans");
            using (XLWorkbook wb = new XLWorkbook())
            {
                ExportExcelIndexMetadataHandler handler = new ExportExcelIndexMetadataHandler();
                foreach (IndexMetadataHandler.ResultPair pair in data)
                {
                    DataTable result = handler.FillDataTable(pair.TotalContentHeader, pair.TotalContentData);
                    var resultaat = wb.Worksheets.Add(result, String.Format("P02 - Indexeren ({0})", pair.Name));
                    resultaat.Columns().AdjustToContents();
                }

                DataTable errors = handler.FillDataTableErrors(errorsList);
                if (errorsList.Count > 0)
                {
                    var fouten = wb.Worksheets.Add(errors, "P02 - Indexeren (Fouten)");
                    fouten.Columns().AdjustToContents();
                }

                wb.SaveAs(tempFile);
            }
            return tempFile;
        }

        /// <summary>
        /// Fills the data table.
        /// </summary>
        /// <param name="columnNames">The column names.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="System.ApplicationException">
        /// No metadata files found!
        /// or
        /// No metadata files found!.
        /// </exception>
        /// <exception cref="System.Exception">Re-throw in FillDataTable</exception>
        private DataTable FillDataTable(HashSet<String> columnNames, Dictionary<String, Dictionary<String, String>> data)
        {
            if (columnNames == null || data == null)
                throw new ApplicationException("No metadata files found!");

            if (columnNames.Count == 0 || data.Count == 0)
                throw new ApplicationException("No metadata files found!.");

            DataTable table = new DataTable("Plat");

            try
            {
                columnNames.ToList().ForEach(item => table.Columns.Add(item, typeof(string)));

                foreach (var record in data)
                {
                    string filePath = record.Key;
                    var contentDictionary = record.Value;

                    DataRow row = table.NewRow();
                    row["Bestandslocatie"] = filePath;

                    foreach (var cell in contentDictionary)
                        row[cell.Key] = cell.Value;

                    table.Rows.Add(row);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Re-throw in FillDataTable", e);
            }

            return table;
        }
        /// <summary>
        /// Fills the data table errors.
        /// </summary>
        /// <param name="errors">The errors.</param>
        /// <returns></returns>
        private DataTable FillDataTableErrors(List<IndexMetadataHandler.ProcessResultItem> errors)
        {
            DataTable table = new DataTable("Fout");

            if (errors.Count == 0)
                return table;

            var columns = new string[] { "Bestandsnaam", "Foutmelding" };
            table.Columns.AddRange(columns.Select(item => new DataColumn(item, typeof(string))).ToArray());

            errors.ForEach(item =>
            {
                DataRow row = table.NewRow();
                row["Bestandsnaam"] = item.Metadata == null ? "Geen bestand(en) gevonden." : item.Metadata.FullName;
                row["Foutmelding"] = item.Error.Message;                
                table.Rows.Add(row);
            });

            return table;
        }
    }
}

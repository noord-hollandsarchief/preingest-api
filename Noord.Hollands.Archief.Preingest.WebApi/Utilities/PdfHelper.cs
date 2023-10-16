using iText.Kernel.Exceptions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// Helper class for PDF documents using iText
    /// </summary>
    public static class PdfHelper
    {
        public static bool IsPasswordProtected(string pdfFullname)
        {
            try
            {
                bool result = false;
                
                using (PdfReader pdfReader = new PdfReader(pdfFullname))
                {
                    using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
                    {
                        var pdfInfo = pdfDocument.GetDocumentInfo(); 
                        result = pdfReader.IsEncrypted();
                    }
                }
                return result;
            }
            catch (BadPasswordException)
            {
                return true;
            }
        }

        public static bool IsPasswordValid(string pdfFullname, byte[] password)
        {
            ReaderProperties props = new ReaderProperties();
            props.SetPassword(password);
            try
            {
                using (PdfReader pdfReader = new PdfReader(pdfFullname, props)) { }
                return false;
            }
            catch (BadPasswordException)
            {
                return true;
            }
        }
    }
}

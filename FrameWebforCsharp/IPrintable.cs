using PDF_Manager.Printing;

namespace PDF_Manager
{
    interface IPrintable
    {
        void printPDF(PdfDocument mc, PrintData data, ref int indexPage);
    }
}

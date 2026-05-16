using InnovaTecPOS.Backend.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Barcodes;
using iText.Kernel.Geom;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace InnovaTecPOS.Backend.Services;

public interface ILabelService
{
    byte[] GenerateLabelPdf(Producto product, string templateType, int quantity);
}

public class LabelService : ILabelService
{
    public byte[] GenerateLabelPdf(Producto product, string templateType, int quantity)
    {
        using (var stream = new MemoryStream())
        {
            var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            
            // Definir tamaño de página según plantilla (en mm, convertido a puntos: 1mm = 2.83465 pts)
            PageSize pageSize;
            switch (templateType.ToLower())
            {
                case "mini": // 80x20mm
                    pageSize = new PageSize(80 * 2.83465f, 20 * 2.83465f);
                    break;
                case "mediana": // 80x30mm
                    pageSize = new PageSize(80 * 2.83465f, 30 * 2.83465f);
                    break;
                case "grande": // 80x40mm
                    pageSize = new PageSize(80 * 2.83465f, 40 * 2.83465f);
                    break;
                default:
                    pageSize = new PageSize(80 * 2.83465f, 30 * 2.83465f);
                    break;
            }

            var document = new Document(pdf, pageSize);
            document.SetMargins(2, 2, 2, 2);

            PdfFont bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            for (int i = 0; i < quantity; i++)
            {
                if (i > 0) pdf.AddNewPage();

                RenderTemplate(document, pdf, product, templateType, bold, regular);
            }

            document.Close();
            return stream.ToArray();
        }
    }

    private void RenderTemplate(Document doc, PdfDocument pdf, Producto product, string templateType, PdfFont bold, PdfFont regular)
    {
        var pageSize = pdf.GetLastPage().GetPageSize();
        float width = pageSize.GetWidth();
        float height = pageSize.GetHeight();

        // 1. Nombre del Producto (Siempre presente)
        var namePara = new Paragraph(product.Nombre)
            .SetFont(bold)
            .SetFontSize(templateType == "grande" ? 10 : 8)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFixedLeading(templateType == "grande" ? 10 : 8)
            .SetMarginBottom(0);
        doc.Add(namePara);

        if (templateType == "grande")
        {
            // Modelo y Marca
            var metaPara = new Paragraph($"Modelo: {product.Modelo ?? "N/A"} | {product.Marca ?? "Genérico"}")
                .SetFont(regular)
                .SetFontSize(7)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(0)
                .SetMarginBottom(2);
            doc.Add(metaPara);
        }

        // 2. Precio (Si no es mini)
        if (templateType != "mini")
        {
            var pricePara = new Paragraph($"C$ {product.PrecioVenta:N2}")
                .SetFont(bold)
                .SetFontSize(12)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(2)
                .SetMarginBottom(2);
            
            if (templateType == "grande")
            {
                 // En la grande el precio va con etiqueta "Precio:" a la izquierda
                 Table priceTable = new Table(UnitValue.CreatePercentArray(new float[] { 30, 70 })).UseAllAvailableWidth();
                 priceTable.AddCell(new Cell().Add(new Paragraph("Precio:").SetFont(regular).SetFontSize(8)).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                 priceTable.AddCell(new Cell().Add(new Paragraph($"C$ {product.PrecioVenta:N2}").SetFont(bold).SetFontSize(12)).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT));
                 doc.Add(priceTable);
            }
            else
            {
                doc.Add(pricePara);
            }
        }

        // 3. Código de Barras (Siempre presente)
        if (!string.IsNullOrEmpty(product.CodigoBarras))
        {
            Barcode128 barcode = new Barcode128(pdf);
            barcode.SetCodeType(Barcode128.CODE128);
            barcode.SetCode(product.CodigoBarras);
            barcode.SetFont(null); 
            
            Image barcodeImg = new Image(barcode.CreateFormXObject(iText.Kernel.Colors.ColorConstants.BLACK, iText.Kernel.Colors.ColorConstants.WHITE, pdf))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER);
            
            // Ajustar altura del código de barras según el espacio restante
            float barcodeHeight = templateType switch {
                "mini" => 35f,
                "mediana" => 55f,
                "grande" => 75f,
                _ => 50f
            };
            barcodeImg.SetHeight(barcodeHeight);
            doc.Add(barcodeImg);

            // Número de código abajo
            var codeNumPara = new Paragraph(product.CodigoBarras)
                .SetFont(regular)
                .SetFontSize(7)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(-2);
            doc.Add(codeNumPara);
        }

        if (templateType == "grande")
        {
            // Footer
            Table footerTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth();
            footerTable.SetFixedPosition(2, 2, width - 4);
            footerTable.AddCell(new Cell().Add(new Paragraph("InnovaTec POS").SetFontSize(6).SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY)).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            footerTable.AddCell(new Cell().Add(new Paragraph(DateTime.Now.ToString("dd/MM/yyyy")).SetFontSize(6).SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY)).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT));
            doc.Add(footerTable);
        }
    }
}

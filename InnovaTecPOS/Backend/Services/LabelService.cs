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

            var pdf = new PdfDocument(writer);
            pdf.SetDefaultPageSize(pageSize);
            
            var document = new Document(pdf);
            document.SetMargins(2, 2, 2, 2); 

            PdfFont bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            for (int i = 0; i < quantity; i++)
            {
                // Crear página explícitamente para cada etiqueta
                pdf.AddNewPage(pageSize);
                
                // Mover el renderizado a la página recién creada
                RenderTemplate(document, pdf, product, templateType, bold, regular, pageSize, i + 1);
            }

            document.Close();
            return stream.ToArray();
        }
    }

    private void RenderTemplate(Document doc, PdfDocument pdf, Producto product, string templateType, PdfFont bold, PdfFont regular, PageSize pageSize, int pageNum)
    {
        float width = pageSize.GetWidth();
        float height = pageSize.GetHeight();

        // 1. Nombre del Producto (Muy pequeño para dar espacio)
        var namePara = new Paragraph(product.Nombre)
            .SetFont(bold)
            .SetFontSize(templateType == "grande" ? 8 : 6)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFixedLeading(templateType == "grande" ? 8 : 6)
            .SetMarginBottom(0);
        doc.Add(namePara);

        if (templateType == "grande")
        {
            // Modelo y Marca (Minúsculo)
            var metaPara = new Paragraph($"Mod: {product.Modelo ?? "N/A"} | {product.Marca ?? "Gen"}")
                .SetFont(regular)
                .SetFontSize(5)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(0)
                .SetFixedLeading(5)
                .SetMarginBottom(0);
            doc.Add(metaPara);
        }

        // 2. Precio (Si no es mini)
        if (templateType != "mini")
        {
            float priceSize = templateType == "grande" ? 9 : 8;
            var pricePara = new Paragraph($"C$ {product.PrecioVenta:N2}")
                .SetFont(bold)
                .SetFontSize(priceSize)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(0)
                .SetFixedLeading(priceSize)
                .SetMarginBottom(0);
            
            if (templateType == "grande")
            {
                 Table priceTable = new Table(UnitValue.CreatePercentArray(new float[] { 30, 70 })).UseAllAvailableWidth();
                 priceTable.SetMarginTop(0).SetMarginBottom(0);
                 priceTable.AddCell(new Cell().Add(new Paragraph("Precio:").SetFont(regular).SetFontSize(6).SetFixedLeading(6)).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                 priceTable.AddCell(new Cell().Add(new Paragraph($"C$ {product.PrecioVenta:N2}").SetFont(bold).SetFontSize(9).SetFixedLeading(9)).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT));
                 doc.Add(priceTable);
            }
            else
            {
                doc.Add(pricePara);
            }
        }

        // 3. Código de Barras (AJUSTADO PARA UNA SOLA PÁGINA)
        if (!string.IsNullOrEmpty(product.CodigoBarras))
        {
            Barcode128 barcode = new Barcode128(pdf);
            barcode.SetCodeType(Barcode128.CODE128);
            barcode.SetCode(product.CodigoBarras);
            barcode.SetFont(null); 
            
            Image barcodeImg = new Image(barcode.CreateFormXObject(iText.Kernel.Colors.ColorConstants.BLACK, iText.Kernel.Colors.ColorConstants.WHITE, pdf))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER);
            
            // Alturas calibradas para evitar saltos de página
            float barcodeHeight = templateType switch {
                "mini" => 32f,    
                "mediana" => 50f,  
                "grande" => 70f,  
                _ => 50f
            };
            barcodeImg.SetHeight(barcodeHeight);
            barcodeImg.SetMarginTop(1);
            doc.Add(barcodeImg);

            // Número de código abajo (Pequeño)
            var codeNumPara = new Paragraph(product.CodigoBarras)
                .SetFont(regular)
                .SetFontSize(5)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFixedLeading(5)
                .SetMarginTop(-1);
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

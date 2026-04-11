using InnovaTecPOS.Backend.Models;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Image;
using iText.Kernel.Geom;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace InnovaTecPOS.Backend.Services;

public interface IExportService
{
    byte[] GenerateInventoryExcel(List<Producto> products);
    byte[] GenerateInventoryPdf(List<Producto> products);
}

public class ExportService : IExportService
{
    private readonly IWebHostEnvironment _env;

    public ExportService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] GenerateInventoryExcel(List<Producto> products)
    {
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Inventario");
            
            // Estilo Encabezado
            var header = worksheet.Cell(1, 1);
            worksheet.Cell(1, 1).Value = "Producto";
            worksheet.Cell(1, 2).Value = "Marca";
            worksheet.Cell(1, 3).Value = "Modelo";
            worksheet.Cell(1, 4).Value = "Categoría";
            worksheet.Cell(1, 5).Value = "Precio Venta";
            worksheet.Cell(1, 6).Value = "Stock";
            worksheet.Cell(1, 7).Value = "Valoración";

            var headerRange = worksheet.Range("A1:G1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#3498db");
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var p in products)
            {
                worksheet.Cell(row, 1).Value = p.Nombre;
                worksheet.Cell(row, 2).Value = p.Marca;
                worksheet.Cell(row, 3).Value = p.Modelo;
                worksheet.Cell(row, 4).Value = p.IdCategoriaNavigation?.Nombre ?? "S/C";
                worksheet.Cell(row, 5).Value = p.PrecioVenta;
                worksheet.Cell(row, 6).Value = p.StockActual;
                worksheet.Cell(row, 7).FormulaA1 = $"E{row}*F{row}";
                
                worksheet.Cell(row, 5).Style.NumberFormat.Format = "C$ #,##0.00";
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "C$ #,##0.00";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    public byte[] GenerateInventoryPdf(List<Producto> products)
    {
        try
        {
            Console.WriteLine("PDF: Starting generation...");
            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4);
                document.SetMargins(20, 20, 20, 20);

                // Header con Logo
                string logoPath = System.IO.Path.Combine(_env.WebRootPath, "images", "logo.png");
                Console.WriteLine($"PDF: Looking for logo at {logoPath}");
                if (System.IO.File.Exists(logoPath))
                {
                    try
                    {
                        ImageData data = ImageDataFactory.Create(logoPath);
                        Image img = new Image(data).SetWidth(150).SetHorizontalAlignment(HorizontalAlignment.LEFT);
                        document.Add(img);
                        Console.WriteLine("PDF: Logo added successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PDF: Error adding logo: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("PDF: Logo not found, skipping.");
                }

                var title = new Paragraph("REPORTE DE INVENTARIO");
                title.SetFontSize(20);
                title.SetTextAlignment(TextAlignment.CENTER);
                document.Add(title);

                var datePara = new Paragraph($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}");
                datePara.SetTextAlignment(TextAlignment.RIGHT);
                datePara.SetFontSize(10);
                document.Add(datePara);
                
                document.Add(new Paragraph("\n"));

                // Tabla
                Table table = new Table(UnitValue.CreatePercentArray(new float[] { 30, 15, 20, 15, 20 }));
                table.UseAllAvailableWidth();
                
                table.AddHeaderCell(new Cell().Add(new Paragraph("Producto")).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Categoría")).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Precio")).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Stock")).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Total")).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.RIGHT));

                foreach (var p in products)
                {
                    table.AddCell(new Paragraph(p.Nombre ?? "Sin Nombre"));
                    table.AddCell(new Paragraph(p.IdCategoriaNavigation?.Nombre ?? "S/C"));
                    table.AddCell(new Paragraph($"C$ {p.PrecioVenta:N2}"));
                    table.AddCell(new Paragraph(p.StockActual.ToString()).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Paragraph($"C$ {(p.PrecioVenta * p.StockActual):N2}").SetTextAlignment(TextAlignment.RIGHT));
                }

                document.Add(table);
                document.Close();
                Console.WriteLine("PDF: Generation completed successfully.");
                return stream.ToArray();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            throw;
        }
    }
}

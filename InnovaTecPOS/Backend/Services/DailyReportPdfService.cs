using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using InnovaTecPOS.Backend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InnovaTecPOS.Backend.Services;

public class DailyReportPdfService
{
    private readonly IDbContextFactory<InnovaTecDbContext> _factory;
    private readonly IWebHostEnvironment _env;

    public DailyReportPdfService(IDbContextFactory<InnovaTecDbContext> factory, IWebHostEnvironment env)
    {
        _factory = factory;
        _env = env;
    }

    public async Task<byte[]> GenerateDailyReportPdfAsync(DateTime date)
    {
        using var context = await _factory.CreateDbContextAsync();
        
        // Usar el servicio de reportes para reutilizar toda la lógica
        var reportService = new ReportService(context);
        
        // Rango de fechas (todo el día)
        var dateStart = date.Date;
        var dateEnd = date.Date.AddDays(1).AddTicks(-1);

        // Obtener datos
        var dashboardStats = await reportService.GetDashboardStatsAsync(dateStart, dateEnd);
        var arqueos = await reportService.GetArqueoInsightsAsync(dateStart, dateEnd);

        // 1. Obtener configuraciones de la empresa
        var settings = await context.Configuracions.AsNoTracking().ToDictionaryAsync(c => c.Clave, c => c.Valor);
        var nombreEmpresa = settings.GetValueOrDefault("Empresa_Nombre", "INNOVATEC POS");
        var ruc = settings.GetValueOrDefault("Empresa_RUC", "");
        var telefono = settings.GetValueOrDefault("Empresa_Telefono", "");
        var direccion = settings.GetValueOrDefault("Empresa_Direccion", "");
        var logoSetting = settings.GetValueOrDefault("Empresa_Logo", "images/logo.png");
        
        // 2. Obtener Turnos del día para el desglose detallado
        var turnos = await context.Turnos
            .Include(t => t.IdUsuarioNavigation)
            .Include(t => t.MovimientosVarios)
            .Include(t => t.Venta)
                .ThenInclude(v => v.Pagos)
                    .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(t => t.FechaApertura <= dateEnd && (t.FechaCierre == null || t.FechaCierre >= dateStart))
            .OrderBy(t => t.FechaApertura)
            .ToListAsync();

        // 3. Ventas Totales y Métodos de Pago
        var ventasDia = await context.Ventas
            .Include(v => v.Pagos)
                .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(v => v.FechaVenta >= dateStart && v.FechaVenta <= dateEnd)
            .ToListAsync();

        var ventasActivas = ventasDia.Where(v => !v.Anulada).ToList();
        var ventasAnuladas = ventasDia.Where(v => v.Anulada).ToList();

        decimal ventasBrutas = ventasActivas.Sum(v => v.TotalNio + v.DescuentoNio); // Venta antes de descuento
        decimal descuentos = ventasActivas.Sum(v => v.DescuentoNio);
        decimal ventasNetas = ventasActivas.Sum(v => v.TotalNio); // Neto con descuento aplicado

        var desglosePagos = ventasActivas
            .SelectMany(v => v.Pagos)
            .GroupBy(p => p.IdMetodoPagoNavigation.Nombre)
            .Select(g => new
            {
                Metodo = g.Key,
                Total = g.Sum(p => p.MontoEnNio - (p.VueltoNio ?? 0))
            })
            .ToList();

        using (var stream = new MemoryStream())
        {
            var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            // Hacer la página Horizontal (Landscape)
            var document = new Document(pdf, PageSize.A4.Rotate());
            document.SetMargins(20, 20, 20, 20);

            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

            // Paleta de Colores
            Color primaryColor = new DeviceRgb(15, 23, 42); // Slate 900
            Color secondaryColor = new DeviceRgb(37, 99, 235); // Blue 600
            Color lightGray = new DeviceRgb(248, 250, 252); // Slate 50
            Color textDark = new DeviceRgb(51, 65, 85); // Slate 700
            Color cardBorderColor = new DeviceRgb(226, 232, 240); // Slate 200

            // --- CABECERA (Reutilizable para las páginas) ---
            Action<Document> AddHeader = (doc) => {
                Table headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 10, 90 })).UseAllAvailableWidth();
                headerTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                Image? img = null;
                if (!string.IsNullOrEmpty(logoSetting))
                {
                    try
                    {
                        if (logoSetting.StartsWith("data:image"))
                        {
                            var commaIndex = logoSetting.IndexOf(',');
                            if (commaIndex >= 0)
                            {
                                var bytes = Convert.FromBase64String(logoSetting.Substring(commaIndex + 1));
                                ImageData data = ImageDataFactory.Create(bytes);
                                img = new Image(data).SetWidth(40).SetHeight(40).SetHorizontalAlignment(HorizontalAlignment.LEFT);
                            }
                        }
                        else
                        {
                            string logoPath = System.IO.Path.Combine(_env.WebRootPath, logoSetting);
                            if (File.Exists(logoPath))
                            {
                                ImageData data = ImageDataFactory.Create(logoPath);
                                img = new Image(data).SetWidth(40).SetHeight(40).SetHorizontalAlignment(HorizontalAlignment.LEFT);
                            }
                        }
                    }
                    catch { }
                }

                if (img != null) headerTable.AddCell(new Cell().Add(img).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                else headerTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER));

                Cell bizInfoCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPaddingLeft(10);
                bizInfoCell.Add(new Paragraph(nombreEmpresa.ToUpper()).SetFontSize(14).SetFont(boldFont).SetFontColor(primaryColor));
                
                string subHeader = "";
                if (!string.IsNullOrEmpty(ruc)) subHeader += $"RUC: {ruc}";
                if (!string.IsNullOrEmpty(telefono)) subHeader += (subHeader == "" ? "" : "  |  ") + $"Tel: {telefono}";
                if (!string.IsNullOrEmpty(direccion)) subHeader += (subHeader == "" ? "" : "  |  ") + direccion;

                if (!string.IsNullOrEmpty(subHeader)) bizInfoCell.Add(new Paragraph(subHeader).SetFontSize(8f).SetFont(regularFont).SetFontColor(textDark));
                
                bizInfoCell.Add(new Paragraph($"REPORTE CONSOLIDADO DIARIO DE OPERACIONES").SetFontSize(10).SetFont(boldFont).SetFontColor(secondaryColor).SetMarginTop(2));
                bizInfoCell.Add(new Paragraph($"Fecha de Reporte: {date:dd/MM/yyyy}  |  Generado: {DateTime.Now:dd/MM/yyyy hh:mm tt}").SetFontSize(7).SetFont(italicFont).SetFontColor(ColorConstants.GRAY));

                headerTable.AddCell(bizInfoCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph("\n").SetFontSize(4));
            };

            // PÁGINA 1: DASHBOARD
            AddHeader(document);
            document.Add(new Paragraph("1. Dashboard de Resultados Financieros").SetFontSize(12).SetFont(boldFont).SetFontColor(primaryColor).SetMarginBottom(10));

            // Fila 1: Total Efectivo, Total Tarjeta, Total Transferencia, Fact. Reversadas
            Table cardsRow1 = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 })).UseAllAvailableWidth();
            cardsRow1.AddCell(CreateDashboardCard("TOTAL EFECTIVO", $"C$ {dashboardStats.TotalEfectivoNio:N2}", $"U$ {dashboardStats.TotalEfectivoUsd:N2}", new DeviceRgb(22, 163, 74), boldFont, regularFont));
            cardsRow1.AddCell(CreateDashboardCard("TOTAL TARJETA", $"C$ {dashboardStats.TotalTarjetaNio:N2}", $"U$ {dashboardStats.TotalTarjetaUsd:N2}", new DeviceRgb(59, 130, 246), boldFont, regularFont));
            cardsRow1.AddCell(CreateDashboardCard("TOTAL TRANSFERENCIA", $"C$ {dashboardStats.TotalTransferenciaNio:N2}", $"U$ {dashboardStats.TotalTransferenciaUsd:N2}", new DeviceRgb(168, 85, 247), boldFont, regularFont));
            cardsRow1.AddCell(CreateDashboardCard("FACT. REVERSADAS", $"{dashboardStats.FacturasReversadas}", "", new DeviceRgb(245, 158, 11), boldFont, regularFont));
            document.Add(cardsRow1);
            document.Add(new Paragraph("\n").SetFontSize(2));

            // Fila 2: Monto Reversado, Art. Reversados, Fact. Con Descuento, Fact. de Regalía
            Table cardsRow2 = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 })).UseAllAvailableWidth();
            cardsRow2.AddCell(CreateDashboardCard("MONTO REVERSADO", $"-C$ {dashboardStats.MontoReversadoNio:N2}", $"-U$ {dashboardStats.MontoReversadoUsd:N2}", new DeviceRgb(217, 119, 6), boldFont, regularFont));
            cardsRow2.AddCell(CreateDashboardCard("ART. REVERSADOS", $"{dashboardStats.ArticulosReversados}", "", new DeviceRgb(234, 88, 12), boldFont, regularFont));
            cardsRow2.AddCell(CreateDashboardCard("FACT. CON DESCUENTO", $"{dashboardStats.FacturasConDescuento}", "", new DeviceRgb(239, 68, 68), boldFont, regularFont));
            cardsRow2.AddCell(CreateDashboardCard("FACT. DE REGALÍA", $"{dashboardStats.FacturasRegalia}", "", new DeviceRgb(139, 92, 246), boldFont, regularFont));
            document.Add(cardsRow2);
            document.Add(new Paragraph("\n").SetFontSize(2));

            // Fila 3: Faltantes en Caja, Sobrantes en Caja, y 2 vacías
            Table cardsRow3 = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 })).UseAllAvailableWidth();
            cardsRow3.AddCell(CreateDashboardCard("FALTANTES EN CAJA", $"-C$ {Math.Abs(dashboardStats.FaltantesCajaNio):N2}", $"-U$ {Math.Abs(dashboardStats.FaltantesCajaUsd):N2}", new DeviceRgb(220, 38, 38), boldFont, regularFont));
            cardsRow3.AddCell(CreateDashboardCard("SOBRANTES EN CAJA", $"+C$ {dashboardStats.SobrantesCajaNio:N2}", $"+U$ {dashboardStats.SobrantesCajaUsd:N2}", new DeviceRgb(234, 179, 8), boldFont, regularFont));
            cardsRow3.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            cardsRow3.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            document.Add(cardsRow3);
            
            // Fin Pagina 1
            document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
            
            // PÁGINA 2: AUDITORÍA DE CAJAS
            AddHeader(document);
            document.Add(new Paragraph("2. Auditoría de Cajas y Turnos").SetFontSize(12).SetFont(boldFont).SetFontColor(primaryColor).SetMarginBottom(6));

            if (!arqueos.Any())
            {
                document.Add(new Paragraph("No se registraron turnos en este periodo.").SetFontSize(9).SetFont(italicFont).SetTextAlignment(TextAlignment.CENTER));
            }
            else
            {
                Table turnosTable = new Table(UnitValue.CreatePercentArray(new float[] { 5, 8, 8, 6, 4, 4, 6, 6, 6, 6, 5, 5, 5, 5, 6, 6, 6, 7 })).UseAllAvailableWidth();
                turnosTable.AddHeaderCell(CreateHeaderCell("Turno", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Cajero", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Moneda", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Inicial", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Efect.", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Anul.", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Netas", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Efectivo", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Transf.", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Tarj.", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Ingres.", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Retiros", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Reversos", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Vuelto", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Teórico", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Real", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Diferencia", primaryColor, boldFont, 7));
                turnosTable.AddHeaderCell(CreateHeaderCell("Estado", primaryColor, boldFont, 7));

                int rowIdx = 0;
                foreach (var a in arqueos)
                {
                    var bg = rowIdx % 2 == 0 ? ColorConstants.WHITE : lightGray;

                    bool isNio = a.Moneda.Contains("CORDOBA");

                    AddArqueoRow(turnosTable, $"#{a.IdTurno}\n{a.Apertura:dd/MM}", a.Usuario, a.Moneda,
                        a.MontoInicial, a.VentasEfectuadas, a.VentasAnuladas, a.VentasNetas,
                        a.CobrosEfectivo, a.CobrosTransferencia, a.CobrosTarjeta,
                        a.OtrosIngresos, a.OtrosRetiros, a.Reversos, a.VueltoEntregado,
                        a.SaldoTeorico, a.SaldoReal, a.Diferencia, a.EstadoCalculado,
                        bg, regularFont, boldFont, isNio);

                    rowIdx++;
                }
                document.Add(turnosTable);
                document.Add(new Paragraph("\n").SetFontSize(6));

                // 3. Desglose Operativo por Caja
                document.Add(new Paragraph("3. Desglose Operativo y de Inventario por Caja").SetFontSize(12).SetFont(boldFont).SetFontColor(primaryColor).SetMarginBottom(8));
                
                foreach (var t in turnos)
                {
                    // Título del turno
                    var titleParagraph = new Paragraph($"Historial de movimientos por turno #{t.IdTurno}  —  Cajero: {t.IdUsuarioNavigation?.Username ?? "N/A"}  (Apertura: {t.FechaApertura:hh:mm tt})")
                        .SetFontSize(9.5f)
                        .SetFont(boldFont)
                        .SetFontColor(primaryColor)
                        .SetBackgroundColor(lightGray)
                        .SetPadding(5)
                        .SetMarginTop(10)
                        .SetMarginBottom(4);
                    document.Add(titleParagraph);

                    // 3.1 Movimientos Varios (Ingreso / Egreso manual)
                    var movimientosVarios = t.MovimientosVarios.OrderBy(m => m.Fecha).ToList();
                    document.Add(new Paragraph("• Movimientos Varios de Efectivo (Entradas y Salidas Manuales)").SetFontSize(8.5f).SetFont(boldFont).SetFontColor(secondaryColor).SetMarginBottom(3));
                    
                    Table movsVariosTable = new Table(UnitValue.CreatePercentArray(new float[] { 15, 55, 15, 15 })).UseAllAvailableWidth();
                    movsVariosTable.AddHeaderCell(CreateHeaderCell("Tipo", primaryColor, boldFont));
                    movsVariosTable.AddHeaderCell(CreateHeaderCell("Concepto / Descripción", primaryColor, boldFont));
                    movsVariosTable.AddHeaderCell(CreateHeaderCell("Hora", primaryColor, boldFont).SetTextAlignment(TextAlignment.CENTER));
                    movsVariosTable.AddHeaderCell(CreateHeaderCell("Monto", primaryColor, boldFont).SetTextAlignment(TextAlignment.RIGHT));

                    if (!movimientosVarios.Any())
                    {
                        movsVariosTable.AddCell(new Cell(1, 4).Add(new Paragraph("No se registraron entradas o salidas manuales de efectivo en esta caja.").SetFontSize(8f).SetFont(italicFont).SetTextAlignment(TextAlignment.CENTER)).SetPadding(6));
                    }
                    else
                    {
                        int row = 0;
                        foreach (var m in movimientosVarios)
                        {
                            var bg = row % 2 == 0 ? ColorConstants.WHITE : lightGray;
                            var tipoColor = m.Tipo == "INGRESO" ? new DeviceRgb(22, 163, 74) : new DeviceRgb(220, 38, 38);
                            
                            movsVariosTable.AddCell(new Cell().Add(new Paragraph(m.Tipo).SetFontSize(8f).SetFont(boldFont).SetFontColor(tipoColor)).SetBackgroundColor(bg).SetPadding(3));
                            movsVariosTable.AddCell(new Cell().Add(new Paragraph(m.Concepto).SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3));
                            movsVariosTable.AddCell(new Cell().Add(new Paragraph(m.Fecha.ToString("hh:mm tt")).SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.CENTER));
                            
                            var pref = m.Tipo == "EGRESO" ? "-" : "";
                            movsVariosTable.AddCell(new Cell().Add(new Paragraph($"{pref}C$ {m.Monto:N2}").SetFontSize(8f).SetFont(boldFont).SetFontColor(tipoColor)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.RIGHT));
                            row++;
                        }
                    }
                    document.Add(movsVariosTable);
                    document.Add(new Paragraph("\n").SetFontSize(4));

                    // 3.2 Salidas de Inventario Detallado por Ventas de la Caja
                    var ventasCaja = t.Venta.Where(v => !v.Anulada).ToList();
                    document.Add(new Paragraph("• Detalle de Ventas y Salidas de Inventario").SetFontSize(8.5f).SetFont(boldFont).SetFontColor(secondaryColor).SetMarginBottom(3));

                    // Tabla columnas: Factura, Producto, Cantidad, Método Pago, Pago Con, Vuelto, Descuento, Subtotal
                    Table itemsTable = new Table(UnitValue.CreatePercentArray(new float[] { 12, 28, 8, 13, 10, 9, 10, 10 })).UseAllAvailableWidth();
                    itemsTable.AddHeaderCell(CreateHeaderCell("Factura", primaryColor, boldFont));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Producto / Artículo", primaryColor, boldFont));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Cant.", primaryColor, boldFont).SetTextAlignment(TextAlignment.CENTER));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Método", primaryColor, boldFont));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Pago Con", primaryColor, boldFont).SetTextAlignment(TextAlignment.RIGHT));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Vuelto", primaryColor, boldFont).SetTextAlignment(TextAlignment.RIGHT));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Descuento", primaryColor, boldFont).SetTextAlignment(TextAlignment.RIGHT));
                    itemsTable.AddHeaderCell(CreateHeaderCell("Subtotal", primaryColor, boldFont).SetTextAlignment(TextAlignment.RIGHT));

                    if (!ventasCaja.Any())
                    {
                        itemsTable.AddCell(new Cell(1, 8).Add(new Paragraph("No se registraron ventas en esta caja.").SetFontSize(8f).SetFont(italicFont).SetTextAlignment(TextAlignment.CENTER)).SetPadding(6));
                    }
                    else
                    {
                        int row = 0;
                        foreach (var v in ventasCaja)
                        {
                            var listDetalles = await context.VentaDetalles.Include(d => d.IdProductoNavigation).Where(d => d.IdVenta == v.IdVenta).ToListAsync();
                            var pagoPrincipal = v.Pagos.OrderByDescending(p => p.MontoEnNio).FirstOrDefault();
                            
                            string metodoStr = pagoPrincipal?.IdMetodoPagoNavigation?.Nombre ?? "N/A";
                            if (v.Pagos.Count > 1)
                            {
                                metodoStr = string.Join("+", v.Pagos.Select(p => p.IdMetodoPagoNavigation.Nombre).Distinct());
                            }

                            decimal totalPagoCon = v.Pagos.Sum(p => p.MontoRecibido == null || p.MontoRecibido == 0 ? p.MontoEnNio : p.MontoRecibido.Value);
                            decimal totalVuelto = v.Pagos.Sum(p => p.VueltoNio ?? 0);

                            // Si la venta tiene múltiples detalles, agrupamos la información para que sea legible
                            bool firstLineOfSale = true;
                            foreach (var det in listDetalles)
                            {
                                var bg = row % 2 == 0 ? ColorConstants.WHITE : lightGray;

                                // Factura
                                if (firstLineOfSale)
                                {
                                    itemsTable.AddCell(new Cell(listDetalles.Count, 1).Add(new Paragraph(v.NumeroFactura ?? $"FAC-{v.IdVenta}").SetFontSize(8f).SetFont(boldFont)).SetBackgroundColor(bg).SetPadding(3).SetVerticalAlignment(VerticalAlignment.MIDDLE));
                                }

                                // Producto
                                string prodDesc = det.DescripcionSnap;
                                itemsTable.AddCell(new Cell().Add(new Paragraph(prodDesc).SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3));

                                // Cantidad
                                itemsTable.AddCell(new Cell().Add(new Paragraph(det.Cantidad.ToString()).SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.CENTER));

                                // Método Pago, Pago con, Vuelto, Descuento (Una sola celda por Venta)
                                if (firstLineOfSale)
                                {
                                    itemsTable.AddCell(new Cell(listDetalles.Count, 1).Add(new Paragraph(metodoStr).SetFontSize(7.5f)).SetBackgroundColor(bg).SetPadding(3).SetVerticalAlignment(VerticalAlignment.MIDDLE));
                                    itemsTable.AddCell(new Cell(listDetalles.Count, 1).Add(new Paragraph($"C$ {totalPagoCon:N2}").SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
                                    itemsTable.AddCell(new Cell(listDetalles.Count, 1).Add(new Paragraph(totalVuelto > 0 ? $"C$ {totalVuelto:N2}" : "--").SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));

                                    var descText = v.DescuentoNio > 0 ? $"C$ {v.DescuentoNio:N2}" : "--";
                                    var descColor = v.DescuentoNio > 0 ? new DeviceRgb(220, 38, 38) : textDark;
                                    var descFont = v.DescuentoNio > 0 ? boldFont : regularFont;
                                    itemsTable.AddCell(new Cell(listDetalles.Count, 1).Add(new Paragraph(descText).SetFontSize(8f).SetFont(descFont).SetFontColor(descColor)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
                                }

                                // Subtotal del artículo
                                itemsTable.AddCell(new Cell().Add(new Paragraph($"C$ {det.SubtotalNio:N2}").SetFontSize(8f)).SetBackgroundColor(bg).SetPadding(3).SetTextAlignment(TextAlignment.RIGHT));

                                firstLineOfSale = false;
                                row++;
                            }
                        }
                    }
                    document.Add(itemsTable);
                    document.Add(new Paragraph("\n").SetFontSize(4));
                }
            }

            // --- PIE DE PÁGINA ---
            document.Add(new Paragraph("\n"));
            Paragraph footerNote = new Paragraph($"Reporte Diario Automatizado de Operaciones — Diseñado para {nombreEmpresa}\nFin de Reporte Consolidado Diario de Cajas e Inventario.");
            footerNote.SetFontSize(7.5f).SetFont(italicFont).SetTextAlignment(TextAlignment.CENTER).SetFontColor(ColorConstants.GRAY);
            document.Add(footerNote);

            document.Close();
            return stream.ToArray();
        }
    }

    private void AddArqueoRow(Table t, string turnoStr, string cajeroStr, string moneda, 
        decimal inicial, int efectuadas, int anuladas, decimal netas, 
        decimal efec, decimal transf, decimal tarj, 
        decimal ing, decimal ret, decimal rev, decimal vuelto, 
        decimal teorico, decimal real, decimal diff, string estado, 
        Color bg, PdfFont regular, PdfFont bold, bool isNio)
    {
        t.AddCell(new Cell().Add(new Paragraph(turnoStr).SetFontSize(7f).SetFont(bold).SetFontColor(new DeviceRgb(37, 99, 235))).SetBackgroundColor(bg).SetPadding(2).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(cajeroStr).SetFontSize(7f).SetFont(bold)).SetBackgroundColor(bg).SetPadding(2).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(moneda).SetFontSize(7f).SetFont(bold)).SetBackgroundColor(bg).SetPadding(2).SetVerticalAlignment(VerticalAlignment.MIDDLE));

        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(inicial)).SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(isNio ? efectuadas.ToString() : "--").SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.CENTER).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(isNio ? anuladas.ToString() : "--").SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.CENTER).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(netas)).SetFontSize(7f).SetFontColor(new DeviceRgb(37, 99, 235))).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(efec)).SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(transf)).SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(tarj)).SetFontSize(7f)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(ing)).SetFontSize(7f).SetFontColor(new DeviceRgb(22, 163, 74))).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(ret)).SetFontSize(7f).SetFontColor(new DeviceRgb(220, 38, 38))).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(rev)).SetFontSize(7f).SetFontColor(new DeviceRgb(220, 38, 38))).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(vuelto)).SetFontSize(7f).SetFontColor(new DeviceRgb(220, 38, 38))).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(teorico)).SetFontSize(7f).SetFont(bold)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        t.AddCell(new Cell().Add(new Paragraph(FormatNumber(real)).SetFontSize(7f).SetFont(bold)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        
        var diffStr = diff > 0 ? $"+{diff:N2}" : (diff < 0 ? $"{diff:N2}" : "0.00");
        var diffColor = diff < 0 ? new DeviceRgb(220, 38, 38) : (diff > 0 ? new DeviceRgb(217, 119, 6) : new DeviceRgb(22, 163, 74));
        t.AddCell(new Cell().Add(new Paragraph(diffStr).SetFontSize(7f).SetFont(bold).SetFontColor(diffColor)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).SetVerticalAlignment(VerticalAlignment.MIDDLE));

        if(isNio)
        {
            var stColor = estado == "FALTANTE" ? new DeviceRgb(220, 38, 38) : (estado == "SOBRANTE" ? new DeviceRgb(217, 119, 6) : new DeviceRgb(22, 163, 74));
            t.AddCell(new Cell(2,1).Add(new Paragraph(estado).SetFontSize(6f).SetFont(bold).SetFontColor(stColor)).SetBackgroundColor(bg).SetPadding(2).SetTextAlignment(TextAlignment.CENTER).SetVerticalAlignment(VerticalAlignment.MIDDLE));
        }
    }

    private string FormatNumber(decimal n) => n == 0 ? "--" : $"{n:N2}";

    private Cell CreateDashboardCard(string title, string valueNio, string valueUsd, Color iconColor, PdfFont bold, PdfFont regular)
    {
        var container = new Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(new DeviceRgb(226, 232, 240), 1));
        
        container.Add(new Paragraph(title).SetFontSize(8).SetFont(bold).SetFontColor(new DeviceRgb(100, 116, 139)).SetMarginBottom(4));
        container.Add(new Paragraph(valueNio).SetFontSize(14).SetFont(bold).SetFontColor(new DeviceRgb(15, 23, 42)));
        
        if(!string.IsNullOrEmpty(valueUsd))
        {
            container.Add(new Paragraph(valueUsd).SetFontSize(8).SetFont(regular).SetFontColor(new DeviceRgb(100, 116, 139)));
        }

        return container;
    }

    private Cell CreateHeaderCell(string text, Color bg, PdfFont font, float fontSize = 8.5f)
    {
        return new Cell().Add(new Paragraph(text).SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(fontSize)).SetBackgroundColor(bg).SetPadding(4);
    }
}

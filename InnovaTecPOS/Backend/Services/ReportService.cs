using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using InnovaTecPOS.Backend.Models;

namespace InnovaTecPOS.Backend.Services;

public interface IReportService
{
    Task<DashboardStatsDTO> GetDashboardStatsAsync(DateTime start, DateTime end);
    Task<List<TrendPointDTO>> GetSalesTrendsAsync(DateTime start, DateTime end);
    Task<List<PaymentMethodStatDTO>> GetPaymentMethodStatsAsync(DateTime start, DateTime end);
    Task<List<TopProductoDTO>> GetTopProductosAsync(DateTime start, DateTime end, int count = 5);
    Task<List<ResumenDiarioDTO>> GetResumenDiarioAsync(DateTime start, DateTime end);
    Task<List<HourlySalesDTO>> GetHourlySalesAsync(DateTime start, DateTime end);
    Task<InventoryInsightDTO> GetInventoryInsightsAsync();
    Task<List<ClientInsightDTO>> GetClientInsightsAsync(DateTime start, DateTime end);
    Task<List<CashierAuditDTO>> GetCashierAuditAsync(DateTime start, DateTime end);
    Task<List<ArqueoInsightDTO>> GetArqueoInsightsAsync(DateTime start, DateTime end);
    Task<List<MovimientoTurnoDTO>> GetMovimientosPorTurnoAsync(int idTurno);
    Task<GarantiaInsightDTO> GetGarantiaStatsAsync();
    Task<List<CategoryStatDTO>> GetCategorySalesAsync(DateTime start, DateTime end);
    Task<List<SystemAlertDTO>> GetSystemAlertsAsync();
    Task<VClienteDashboardStat> GetClienteDashboardStatsAsync();
    Task<List<Movimiento>> GetKardexGlobalAsync(DateTime start, DateTime end);
}

public class ReportService : IReportService
{
    private readonly InnovaTecDbContext _context;

    public ReportService(InnovaTecDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDTO> GetDashboardStatsAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var currentVentas = await _context.Ventas
            .Include(v => v.VentaDetalles)
                .ThenInclude(d => d.IdProductoNavigation)
            .Include(v => v.Pagos)
                .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end)
            .ToListAsync();

        // Calcular periodo anterior equivalente
        var days = (end - start).TotalDays;
        if (days < 1) days = 1; 
        
        var prevStart = start.AddDays(-days);
        var prevEnd = start.AddTicks(-1);

        var prevVentas = await _context.Ventas
            .Where(v => v.FechaVenta >= prevStart && v.FechaVenta <= prevEnd && !v.Anulada)
            .ToListAsync();

        var turnos = await _context.Turnos
            .Include(t => t.MovimientosVarios)
            .Include(t => t.Venta)
                .ThenInclude(v => v.Pagos)
                .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(t => t.FechaApertura <= end && t.FechaCierre != null && t.FechaCierre >= start)
            .ToListAsync();

        // Filtrar validas y reversadas (anuladas)
        var validVentas = currentVentas.Where(v => !v.Anulada).ToList();
        var reversedVentas = currentVentas.Where(v => v.Anulada).ToList();
        
        // Ventas por moneda usando Pagos (USD vs NIO)
        var usdPayments = validVentas.SelectMany(v => v.Pagos).Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).ToList();
        var nioPayments = validVentas.SelectMany(v => v.Pagos).Where(p => !p.IdMetodoPagoNavigation.Nombre.Contains("USD")).ToList();

        // Desglose de Ventas Brutas y Descuentos
        decimal ventasUsd = usdPayments.Sum(p => p.MontoRecibido ?? 0m); // Lo cobrado neto en USD
        decimal descuentoUsd = 0; // Descuentos se aplican gralmente al NIO, pero se puede aproximar
        decimal utilidadUsd = 0;
        
        decimal ventasNio = validVentas.Sum(v => v.TotalNio) - (ventasUsd * validVentas.FirstOrDefault()?.TasaCambioUsd ?? 36.5m);
        if (ventasNio < 0) ventasNio = 0; // Fallback en caso de redondeos extraños
        
        // Forma correcta: Dividimos cada venta proporcionalmente según sus pagos
        decimal vBrutasNio = 0, vBrutasUsd = 0, uNetaNio = 0, uNetaUsd = 0, dNio = 0, dUsd = 0;
        int facturasRegalia = 0;
        decimal valorRegaliasNio = 0, valorRegaliasUsd = 0;

        foreach (var v in validVentas)
        {
            decimal tasa = v.TasaCambioUsd > 0 ? v.TasaCambioUsd : 36.5m;
            decimal utilidadFactura = v.VentaDetalles.Sum(d => d.SubtotalNio - ((d.IdProductoNavigation?.PrecioCompra ?? 0) * d.Cantidad));

            // Si es regalía (Total == 0 pero Subtotal > 0)
            if (v.TotalNio == 0 && v.SubtotalNio > 0)
            {
                facturasRegalia++;
                // Regalías enteras a NIO por defecto ya que no hay pago
                valorRegaliasNio += v.SubtotalNio;
                continue;
            }

            // Calcular porcentajes en base a MontoEnNio de cada pago
            decimal pagoTotalNio = v.Pagos.Sum(p => p.MontoEnNio);
            decimal pagoUsdNio = v.Pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).Sum(p => p.MontoEnNio);
            decimal pagoNioNio = pagoTotalNio - pagoUsdNio;

            decimal porcentajeUsd = pagoTotalNio > 0 ? pagoUsdNio / pagoTotalNio : 0;
            decimal porcentajeNio = pagoTotalNio > 0 ? pagoNioNio / pagoTotalNio : (pagoTotalNio == 0 ? 1 : 0);

            vBrutasUsd += (v.TotalNio * porcentajeUsd) / tasa;
            vBrutasNio += v.TotalNio * porcentajeNio;

            uNetaUsd += (utilidadFactura * porcentajeUsd) / tasa;
            uNetaNio += utilidadFactura * porcentajeNio;

            dUsd += (v.DescuentoNio * porcentajeUsd) / tasa;
            dNio += v.DescuentoNio * porcentajeNio;
        }

        // Calcular Montos Reversados
        decimal montoReversadoNio = 0, montoReversadoUsd = 0;
        foreach (var v in reversedVentas)
        {
            decimal tasa = v.TasaCambioUsd > 0 ? v.TasaCambioUsd : 36.5m;
            decimal pagoTotalNio = v.Pagos.Sum(p => p.MontoEnNio);
            decimal pagoUsdNio = v.Pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).Sum(p => p.MontoEnNio);
            decimal porcentajeUsd = pagoTotalNio > 0 ? pagoUsdNio / pagoTotalNio : 0;
            decimal porcentajeNio = pagoTotalNio > 0 ? (pagoTotalNio - pagoUsdNio) / pagoTotalNio : (pagoTotalNio == 0 ? 1 : 0);
            
            montoReversadoUsd += (v.TotalNio * porcentajeUsd) / tasa;
            montoReversadoNio += v.TotalNio * porcentajeNio;
        }

        foreach (var v in validVentas)
        {
            var devueltos = v.VentaDetalles.Where(d => d.Devuelto).ToList();
            if (devueltos.Any())
            {
                decimal totalDevueltoNio = devueltos.Sum(d => d.SubtotalNio);
                decimal tasa = v.TasaCambioUsd > 0 ? v.TasaCambioUsd : 36.5m;
                decimal pagoTotalNio = v.Pagos.Sum(p => p.MontoEnNio);
                decimal pagoUsdNio = v.Pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).Sum(p => p.MontoEnNio);
                decimal porcentajeUsd = pagoTotalNio > 0 ? pagoUsdNio / pagoTotalNio : 0;
                decimal porcentajeNio = pagoTotalNio > 0 ? (pagoTotalNio - pagoUsdNio) / pagoTotalNio : (pagoTotalNio == 0 ? 1 : 0);
                
                montoReversadoUsd += (totalDevueltoNio * porcentajeUsd) / tasa;
                montoReversadoNio += totalDevueltoNio * porcentajeNio;
            }
        }

        decimal faltantesNio = 0, sobrantesNio = 0;
        decimal faltantesUsd = 0, sobrantesUsd = 0;

        foreach (var t in turnos)
        {
            var pagos = t.Venta.Where(v => !v.Anulada).SelectMany(v => v.Pagos).ToList();
            
            // NIO
            decimal ingresosNio = t.MovimientosVarios.Where(m => m.Tipo == "INGRESO" && m.IdMoneda == 1).Sum(m => m.Monto);
            decimal retirosNio = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 1 && !m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal reversosNio = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 1 && m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal vueltoEntregadoNio = pagos.Sum(p => p.VueltoNio ?? 0);
            decimal cobroEfectivoNio = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("EFECTIVO_NIO")).Sum(p => p.MontoRecibido ?? 0m);
            decimal cobroTarjeta = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("TARJETA")).Sum(p => p.MontoEnNio);
            decimal cobroTransferencia = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("TRANSFERENCIA")).Sum(p => p.MontoEnNio);

            decimal saldoTeoricoNio = t.MontoInicialNio + cobroEfectivoNio + cobroTransferencia + cobroTarjeta + ingresosNio - retirosNio - reversosNio - vueltoEntregadoNio;
            decimal difNio = (t.MontoContadoNio ?? 0) - saldoTeoricoNio;

            if (difNio > 0) sobrantesNio += difNio;
            if (difNio < 0) faltantesNio += Math.Abs(difNio);

            // USD
            decimal ingresosUsd = t.MovimientosVarios.Where(m => m.Tipo == "INGRESO" && m.IdMoneda == 2).Sum(m => m.Monto);
            decimal retirosUsd = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 2 && !m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal reversosUsd = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 2 && m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal cobroEfectivoUsd = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("EFECTIVO_USD")).Sum(p => p.MontoRecibido ?? 0m);
            
            decimal saldoTeoricoUsd = t.MontoInicialUsd + cobroEfectivoUsd + ingresosUsd - retirosUsd - reversosUsd;
            decimal difUsd = (t.MontoContadoUsd ?? 0) - saldoTeoricoUsd;

            if (difUsd > 0) sobrantesUsd += difUsd;
            if (difUsd < 0) faltantesUsd += Math.Abs(difUsd);
        }

        var stats = new DashboardStatsDTO
        {
            // Entradas por Método de Pago
            TotalEfectivoNio = nioPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("EFECTIVO")).Sum(p => p.MontoRecibido ?? 0m),
            TotalEfectivoUsd = usdPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("EFECTIVO")).Sum(p => p.MontoRecibido ?? 0m),
            TotalTarjetaNio = nioPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("TARJETA")).Sum(p => p.MontoRecibido ?? 0m),
            TotalTarjetaUsd = usdPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("TARJETA")).Sum(p => p.MontoRecibido ?? 0m),
            TotalTransferenciaNio = nioPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("TRANSFERENCIA")).Sum(p => p.MontoRecibido ?? 0m),
            TotalTransferenciaUsd = usdPayments.Where(p => p.IdMetodoPagoNavigation.Nombre.StartsWith("TRANSFERENCIA")).Sum(p => p.MontoRecibido ?? 0m),
            
            // Diferencias de Caja
            FaltantesCajaNio = faltantesNio,
            FaltantesCajaUsd = faltantesUsd,
            SobrantesCajaNio = sobrantesNio,
            SobrantesCajaUsd = sobrantesUsd,

            // NIO
            VentasBrutasNio = vBrutasNio,
            UtilidadNetaNio = uNetaNio,
            DescuentosNio = dNio,
            ValorRegaliasNio = valorRegaliasNio,
            
            // USD
            VentasBrutasUsd = vBrutasUsd,
            UtilidadNetaUsd = uNetaUsd,
            DescuentosUsd = dUsd,
            ValorRegaliasUsd = valorRegaliasUsd,
            
            MontoReversadoNio = montoReversadoNio,
            MontoReversadoUsd = montoReversadoUsd,

            // Conteos
            TotalFacturas = validVentas.Count,
            FacturasReversadas = reversedVentas.Count,
            ArticulosReversados = reversedVentas.Sum(v => v.VentaDetalles.Sum(d => d.Cantidad)),
            FacturasRegalia = facturasRegalia,
            FacturasConDescuento = validVentas.Count(v => v.DescuentoNio > 0),
            
            ProductosVendidos = validVentas.SelectMany(v => v.VentaDetalles).Sum(d => d.Cantidad),
            Anulaciones = reversedVentas.Count,
            ClientesNuevos = await _context.Personas.CountAsync(p => p.FechaCreacion >= start && p.FechaCreacion <= end && p.EsCliente)
        };

        // Porcentajes
        decimal prevVentasTotal = prevVentas.Sum(v => v.TotalNio);
        stats.PorcentajeVentas = CalcularVariacion(stats.VentasTotalesCalculadasNio, prevVentasTotal);
        stats.PorcentajeFacturas = CalcularVariacion(stats.TotalFacturas, prevVentas.Count);
        stats.PorcentajeDescuentos = CalcularVariacion(dNio + (dUsd * 36.5m), prevVentas.Sum(v => v.DescuentoNio));

        return stats;
    }

    public async Task<List<TrendPointDTO>> GetSalesTrendsAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var ventas = await _context.Ventas
            .Include(v => v.Pagos)
                .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .OrderBy(v => v.FechaVenta)
            .ToListAsync();

        var result = new List<TrendPointDTO>();

        foreach (var group in ventas.GroupBy(v => v.FechaVenta.Date))
        {
            decimal totalNio = 0;
            decimal totalUsd = 0;

            foreach (var v in group)
            {
                decimal tasa = v.TasaCambioUsd > 0 ? v.TasaCambioUsd : 36.5m;

                decimal pagoTotalNio = v.Pagos.Sum(p => p.MontoEnNio);
                decimal pagoUsdNio = v.Pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).Sum(p => p.MontoEnNio);
                decimal pagoNioNio = pagoTotalNio - pagoUsdNio;

                decimal porcentajeUsd = pagoTotalNio > 0 ? pagoUsdNio / pagoTotalNio : 0;
                decimal porcentajeNio = pagoTotalNio > 0 ? pagoNioNio / pagoTotalNio : (pagoTotalNio == 0 ? 1 : 0);

                totalUsd += (v.TotalNio * porcentajeUsd) / tasa;
                totalNio += v.TotalNio * porcentajeNio;
            }

            result.Add(new TrendPointDTO
            {
                Label = group.Key.ToString("dd MMM"),
                ValorNio = totalNio,
                ValorUsd = totalUsd
            });
        }

        return result;
    }

    public async Task<List<PaymentMethodStatDTO>> GetPaymentMethodStatsAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var pagos = await _context.Pagos
            .Include(p => p.IdMetodoPagoNavigation)
            .Include(p => p.IdVentaNavigation)
            .Where(p => p.FechaPago >= start && p.FechaPago <= end && !p.IdVentaNavigation.Anulada)
            .ToListAsync();

        decimal total = pagos.Sum(p => p.MontoEnNio);

        return pagos.GroupBy(p => p.IdMetodoPagoNavigation.Nombre)
            .Select(g => new PaymentMethodStatDTO
            {
                Metodo = g.Key,
                Total = g.Sum(p => p.MontoEnNio - (p.VueltoNio ?? 0)),
                Porcentaje = (double)(g.Sum(p => p.MontoEnNio - (p.VueltoNio ?? 0)) / (total > 0 ? total : 1) * 100)
            })
            .ToList();
    }

    public async Task<List<TopProductoDTO>> GetTopProductosAsync(DateTime start, DateTime end, int count = 5)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        return await _context.VentaDetalles
            .Include(d => d.IdVentaNavigation)
            .Where(d => d.IdVentaNavigation.FechaVenta >= start && d.IdVentaNavigation.FechaVenta <= end && !d.IdVentaNavigation.Anulada)
            .GroupBy(d => d.DescripcionSnap)
            .Select(g => new TopProductoDTO
            {
                Nombre = g.Key,
                Unidades = g.Sum(d => d.Cantidad),
                TotalVentas = g.Sum(d => d.SubtotalNio)
            })
            .OrderByDescending(x => x.TotalVentas)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<ResumenDiarioDTO>> GetResumenDiarioAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var ventas = await _context.Ventas
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .ToListAsync();

        return ventas.GroupBy(v => v.FechaVenta.Date)
            .Select(g => new ResumenDiarioDTO
            {
                Fecha = g.Key,
                VentasBrutas = g.Sum(v => v.TotalNio),
                Devoluciones = 0, // Por implementar lógica de devoluciones real si existe
                VentasNetas = g.Sum(v => v.TotalNio),
                Facturas = g.Count(),
                TicketPromedio = g.Average(v => v.TotalNio)
            })
            .OrderByDescending(x => x.Fecha)
            .ToList();
    }

    public async Task<List<HourlySalesDTO>> GetHourlySalesAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var ventas = await _context.Ventas
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .ToListAsync();

        var result = new List<HourlySalesDTO>();
        for (int d = 0; d < 7; d++)
        {
            for (int h = 7; h <= 21; h++)
            {
                var total = ventas.Where(v => (int)v.FechaVenta.DayOfWeek == d && v.FechaVenta.Hour == h).Sum(v => v.TotalNio);
                result.Add(new HourlySalesDTO
                {
                    DayOfWeek = d,
                    Hour = h,
                    Total = total,
                    Intensity = 0 // Se calcula abajo
                });
            }
        }

        // Calcular intensidad relativa al máximo del dataset
        var maxTotal = result.Any() ? result.Max(r => r.Total) : 0;
        if (maxTotal > 0)
        {
            foreach (var item in result)
            {
                if (item.Total > 0)
                {
                    // Escala de 1-10 proporcional al máximo
                    item.Intensity = Math.Max(1, (int)Math.Ceiling((double)(item.Total / maxTotal * 10)));
                }
            }
        }

        return result;
    }

    public async Task<InventoryInsightDTO> GetInventoryInsightsAsync()
    {
        var critico = await _context.Productos
            .Include(p => p.IdCategoriaNavigation)
            .Where(p => p.Activo && p.StockActual <= p.StockMinimo)
            .Select(p => new VStockValorizadoDTO
            {
                IdProducto = p.IdProducto,
                Nombre = p.Nombre,
                Categoria = p.IdCategoriaNavigation != null ? p.IdCategoriaNavigation.Nombre : "Sin categoría",
                Marca = p.Marca ?? "",
                Modelo = p.Modelo ?? "",
                StockActual = p.StockActual,
                StockMinimo = p.StockMinimo,
                EstadoStock = p.EstadoStock,
                PrecioCompra = p.PrecioCompra ?? 0m,
                PrecioVenta = p.PrecioVenta
            })
            .ToListAsync();
        
        var valorCosto = critico.Sum(p => p.ValorCostoTotal);
        var valorVenta = critico.Sum(p => p.ValorVentaTotal);
        var fechaLimite = DateTime.Today.AddDays(-30);
        var productosSinVenta = await _context.Productos
            .Where(p => p.Activo && !_context.VentaDetalles.Any(d => d.IdProducto == p.IdProducto && d.IdVentaNavigation.FechaVenta >= fechaLimite))
            .Select(p => new
            {
                p.Nombre,
                p.FechaCreacion,
                UltimaVenta = _context.VentaDetalles
                    .Where(d => d.IdProducto == p.IdProducto)
                    .OrderByDescending(d => d.IdVentaNavigation.FechaVenta)
                    .Select(d => (DateTime?)d.IdVentaNavigation.FechaVenta)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var result = productosSinVenta.Select(p => new ProductNoMovementDTO
        {
            Nombre = p.Nombre,
            UltimaVenta = p.UltimaVenta,
            DiasSinVenta = (DateTime.Today - (p.UltimaVenta ?? p.FechaCreacion)).Days
        })
        .OrderByDescending(x => x.DiasSinVenta)
        .Take(10)
        .ToList();

        return new InventoryInsightDTO
        {
            StockCritico = critico,
            SinMovimiento = result,
            ValorTotalCosto = valorCosto,
            ValorTotalVenta = valorVenta
        };
    }

    public async Task<List<ClientInsightDTO>> GetClientInsightsAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        return await _context.Ventas
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada && v.IdPersona != null)
            .GroupBy(v => v.IdPersonaNavigation!.NombreCompleto)
            .Select(g => new ClientInsightDTO
            {
                Nombre = g.Key ?? "Cliente General",
                TotalCompras = g.Count(),
                MontoTotal = g.Sum(v => v.TotalNio)
            })
            .OrderByDescending(x => x.MontoTotal)
            .Take(10)
            .ToListAsync();
    }

    public async Task<List<CashierAuditDTO>> GetCashierAuditAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var data = await _context.Ventas
            .Include(v => v.IdUsuarioNavigation)
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .GroupBy(v => v.IdUsuarioNavigation.Username)
            .Select(g => new
            {
                Username = g.Key ?? "N/A",
                FacturasGeneradas = g.Count(),
                TotalVentas = g.Sum(v => v.TotalNio),
                DescuentosAplicados = g.Sum(v => v.DescuentoNio)
            })
            .OrderByDescending(x => x.TotalVentas)
            .ToListAsync();

        return data.Select(g => new CashierAuditDTO
        {
            Cajero = g.Username,
            Facturas = g.FacturasGeneradas,
            TotalVentas = g.TotalVentas,
            Descuentos = g.DescuentosAplicados
        }).ToList();
    }

    public async Task<List<ArqueoInsightDTO>> GetArqueoInsightsAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        var turnos = await _context.Turnos
            .Include(t => t.IdUsuarioNavigation)
            .Include(t => t.MovimientosVarios)
            .Include(t => t.Venta)
                .ThenInclude(v => v.Pagos)
                    .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(t => t.FechaApertura <= end && (t.FechaCierre == null || t.FechaCierre >= start))
            .OrderByDescending(t => t.FechaApertura)
            .ToListAsync();

        var result = new List<ArqueoInsightDTO>();

        foreach (var t in turnos)
        {
            var ventasValidas = t.Venta.Where(v => !v.Anulada).ToList();
            var ventasAnuladas = t.Venta.Where(v => v.Anulada).ToList();
            
            var pagos = ventasValidas.SelectMany(v => v.Pagos).ToList();
            
            // Movimientos manuales
            decimal ingresosNio = t.MovimientosVarios.Where(m => m.Tipo == "INGRESO" && m.IdMoneda == 1).Sum(m => m.Monto);
            decimal retirosNio = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 1 && !m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal reversosNio = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 1 && m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            
            decimal ingresosUsd = t.MovimientosVarios.Where(m => m.Tipo == "INGRESO" && m.IdMoneda == 2).Sum(m => m.Monto);
            decimal retirosUsd = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 2 && !m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);
            decimal reversosUsd = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO" && m.IdMoneda == 2 && m.Concepto.StartsWith("Reverso")).Sum(m => m.Monto);

            // Vueltos entregados desde pagos (normalmente en NIO)
            decimal vueltoEntregadoNio = pagos.Sum(p => p.VueltoNio ?? 0);
            decimal vueltoEntregadoUsd = 0; // Asumimos vuelto en dolares es 0, a menos que haya registro de vuelto en dolares en el sistema
            
            // Cobros (ingresos fisicos por ventas)
            decimal cobroEfectivoNio = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("EFECTIVO_NIO")).Sum(p => p.MontoRecibido ?? 0m); // Suma todo lo recibido en efectivo NIO. El vuelto se descuenta luego en VueltoEntregado.
            decimal cobroEfectivoUsd = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("EFECTIVO_USD")).Sum(p => p.MontoRecibido ?? 0m); // Suma todo lo recibido en USD fisicamente
            decimal cobroTarjeta = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("TARJETA")).Sum(p => p.MontoEnNio);
            decimal cobroTransferencia = pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("TRANSFERENCIA")).Sum(p => p.MontoEnNio);

            string estadoStr = t.FechaCierre == null ? "EN CURSO" : (t.EstadoCuadre ?? "CERRADO");

            // Fila CÓRDOBAS (Fila Principal)
            result.Add(new ArqueoInsightDTO
            {
                IdTurno = t.IdTurno,
                Usuario = t.IdUsuarioNavigation?.Username ?? "Sistema",
                Moneda = "C$ CORDOBAS",
                Apertura = t.FechaApertura,
                Cierre = t.FechaCierre,
                MontoInicial = t.MontoInicialNio,
                
                // Las métricas de ventas se agrupan en la moneda local
                VentasEfectuadas = ventasValidas.Count,
                VentasAnuladas = ventasAnuladas.Count,
                VentasNetas = ventasValidas.Sum(v => v.TotalNio),
                
                CobrosEfectivo = cobroEfectivoNio,
                CobrosTransferencia = cobroTransferencia,
                CobrosTarjeta = cobroTarjeta,
                
                OtrosIngresos = ingresosNio,
                OtrosRetiros = retirosNio,
                Reversos = reversosNio,
                VueltoEntregado = vueltoEntregadoNio,
                
                SaldoTeorico = t.MontoInicialNio + cobroEfectivoNio + cobroTransferencia + cobroTarjeta + ingresosNio - retirosNio - reversosNio - vueltoEntregadoNio,
                SaldoReal = t.MontoContadoNio ?? 0,
                Estado = estadoStr,
                EsFilaPrincipal = true
            });

            // Fila DÓLARES
            result.Add(new ArqueoInsightDTO
            {
                IdTurno = t.IdTurno,
                Usuario = t.IdUsuarioNavigation?.Username ?? "Sistema",
                Moneda = "$ DOLARES",
                Apertura = t.FechaApertura,
                Cierre = t.FechaCierre,
                MontoInicial = t.MontoInicialUsd,
                
                VentasEfectuadas = 0,
                VentasAnuladas = 0,
                VentasNetas = 0,
                
                CobrosEfectivo = cobroEfectivoUsd,
                CobrosTransferencia = 0,
                CobrosTarjeta = 0,
                
                OtrosIngresos = ingresosUsd,
                OtrosRetiros = retirosUsd,
                Reversos = reversosUsd,
                VueltoEntregado = vueltoEntregadoUsd,
                
                SaldoTeorico = t.MontoInicialUsd + cobroEfectivoUsd + ingresosUsd - retirosUsd - reversosUsd - vueltoEntregadoUsd,
                SaldoReal = t.MontoContadoUsd ?? 0,
                Estado = estadoStr,
                EsFilaPrincipal = false
            });
        }

        return result;
    }

    public async Task<List<MovimientoTurnoDTO>> GetMovimientosPorTurnoAsync(int idTurno)
    {
        var ventas = await _context.Ventas
            .Include(v => v.IdPersonaNavigation)
            .Include(v => v.Pagos)
                .ThenInclude(p => p.IdMetodoPagoNavigation)
            .Where(v => v.IdTurno == idTurno)
            .ToListAsync();

        var movimientos = await _context.MovimientosVarios
            .Where(m => m.IdTurno == idTurno)
            .ToListAsync();

        var result = new List<MovimientoTurnoDTO>();

        foreach (var v in ventas)
        {
            var pagoPrincipal = v.Pagos.OrderByDescending(p => p.MontoEnNio).FirstOrDefault();
            var metodoPago = pagoPrincipal?.IdMetodoPagoNavigation?.Nombre ?? "N/A";
            if (v.Pagos.Count > 1)
            {
                var metodos = v.Pagos.Select(p => p.IdMetodoPagoNavigation.Nombre).Distinct();
                metodoPago = string.Join(", ", metodos);
            }

            bool pagoPrincipalUsd = pagoPrincipal?.IdMetodoPagoNavigation?.Nombre.Contains("USD") ?? false;
            string simPago = pagoPrincipalUsd ? "$" : "C$";
            decimal pagadoFisico = pagoPrincipalUsd ? v.Pagos.Where(p => p.IdMetodoPagoNavigation.Nombre.Contains("USD")).Sum(p => p.MontoRecibido ?? 0m) : v.Pagos.Sum(p => (p.MontoRecibido ?? 0m) > 0 ? (p.MontoRecibido ?? 0m) : p.MontoEnNio);

            var totalVuelto = v.Pagos.Sum(p => p.VueltoNio ?? 0);
            
            bool esRegalia = v.TotalNio == 0;

            result.Add(new MovimientoTurnoDTO
            {
                TipoMovimiento = esRegalia ? "Regalía" : "Venta",
                Referencia = v.NumeroFactura ?? $"FAC-{v.IdVenta}",
                Fecha = v.FechaVenta,
                Cliente = v.IdPersonaNavigation?.NombreCompleto ?? "Cliente de Contado",
                Monto = v.SubtotalNio > 0 ? v.SubtotalNio : v.TotalNio, // El monto base
                Descuento = v.DescuentoNio,
                MontoPagado = pagadoFisico,
                Vuelto = totalVuelto,
                MontoReverso = 0,
                MontoTotal = v.TotalNio,
                MetodoPago = metodoPago,
                Estado = v.Anulada ? "ANULADA" : "EFECTUADA",
                SimboloMonedaMonto = "C$",
                SimboloMonedaPago = simPago,
                SimboloMonedaVuelto = "C$"
            });
        }

        foreach (var m in movimientos)
        {
            bool isReverso = m.Concepto.StartsWith("Reverso");
            string tipo = isReverso ? "Reverso" : (m.Tipo == "INGRESO" ? "Ingreso" : "Egreso");
            string simMoneda = m.IdMoneda == 2 ? "$" : "C$";

            result.Add(new MovimientoTurnoDTO
            {
                TipoMovimiento = tipo,
                Referencia = m.Concepto,
                Fecha = m.Fecha,
                Cliente = "--",
                Monto = 0,
                Descuento = 0,
                MontoPagado = m.Tipo == "INGRESO" ? m.Monto : 0,
                Vuelto = 0,
                MontoReverso = isReverso ? m.Monto : 0,
                MontoTotal = m.Monto,
                MetodoPago = "EFECTIVO",
                Estado = "COMPLETADO",
                SimboloMonedaMonto = simMoneda,
                SimboloMonedaPago = simMoneda,
                SimboloMonedaVuelto = "C$"
            });
        }

        return result.OrderByDescending(x => x.Fecha).ToList();
    }

    public async Task<GarantiaInsightDTO> GetGarantiaStatsAsync()
    {
        var now = DateTime.Now;
        var nowOnly = DateOnly.FromDateTime(now);
        var detalles = await _context.VentaDetalles
            .Where(d => d.FechaVenceGarantia != null)
            .ToListAsync();

        var activas = detalles.Count(d => d.FechaVenceGarantia > nowOnly);
        var porVencer = detalles.Count(d => d.FechaVenceGarantia > nowOnly && d.FechaVenceGarantia < nowOnly.AddDays(7));

        return new GarantiaInsightDTO
        {
            Activas = activas,
            PorVencer = porVencer,
            Reclamadas = 0,
            Recientes = detalles.OrderByDescending(d => d.FechaVenceGarantia)
                .Take(5)
                .Select(d => new GarantiaDetalleDTO
                {
                    Factura = "FAC-" + d.IdVenta,
                    Producto = d.DescripcionSnap,
                    Vencimiento = d.FechaVenceGarantia.HasValue ? new DateTime(d.FechaVenceGarantia.Value.Year, d.FechaVenceGarantia.Value.Month, d.FechaVenceGarantia.Value.Day) : now,
                    Estado = d.FechaVenceGarantia > nowOnly ? "Activa" : "Vencida"
                }).ToList()
        };
    }

    public async Task<List<CategoryStatDTO>> GetCategorySalesAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        return await _context.VentaDetalles
            .Include(d => d.IdVentaNavigation)
            .Include(d => d.IdProductoNavigation)
                .ThenInclude(p => p.IdCategoriaNavigation)
            .Where(d => d.IdVentaNavigation.FechaVenta >= start && d.IdVentaNavigation.FechaVenta <= end && !d.IdVentaNavigation.Anulada)
            .GroupBy(d => d.IdProductoNavigation.IdCategoriaNavigation != null ? d.IdProductoNavigation.IdCategoriaNavigation.Nombre : "Sin categoría")
            .Select(g => new CategoryStatDTO
            {
                Categoria = g.Key ?? "Otros",
                Total = g.Sum(d => d.SubtotalNio)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();
    }

    public async Task<List<SystemAlertDTO>> GetSystemAlertsAsync()
    {
        var alerts = new List<SystemAlertDTO>();
        
        var stockCritico = await _context.VStockCriticos.CountAsync();
        if (stockCritico > 0)
        {
            alerts.Add(new SystemAlertDTO {
                Titulo = "Stock Crítico Detectado",
                Mensaje = $"Hay {stockCritico} productos por debajo del mínimo.",
                Tipo = "danger",
                Fecha = DateTime.Now
            });
        }

        return alerts;
    }

    public async Task<VClienteDashboardStat> GetClienteDashboardStatsAsync()
    {
        return await _context.VClienteDashboardStats.FirstOrDefaultAsync() 
            ?? new VClienteDashboardStat { TotalClientes = 0, TotalGarantiasActivas = 0, ClientesConComprasRecientes = 0 };
    }

    private decimal CalcularVariacion(decimal actual, decimal anterior)
    {
        if (anterior == 0) return actual > 0 ? 100 : 0;
        return ((actual - anterior) / anterior) * 100;
    }

    public async Task<List<Movimiento>> GetKardexGlobalAsync(DateTime start, DateTime end)
    {
        end = end.Date.AddDays(1).AddTicks(-1);
        return await _context.Movimientos
            .Include(m => m.IdProductoNavigation)
            .Include(m => m.IdTipoMovNavigation)
            .Where(m => m.FechaMov >= start && m.FechaMov <= end)
            .OrderByDescending(m => m.FechaMov)
            .ToListAsync();
    }
}

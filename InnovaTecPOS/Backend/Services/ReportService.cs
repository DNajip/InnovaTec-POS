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
    Task<GarantiaInsightDTO> GetGarantiaStatsAsync();
    Task<List<SystemAlertDTO>> GetSystemAlertsAsync();
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
        var currentVentas = await _context.Ventas
            .Include(v => v.VentaDetalles)
                .ThenInclude(d => d.IdProductoNavigation)
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .ToListAsync();

        // Calcular periodo anterior equivalente
        var days = (end - start).TotalDays;
        if (days < 1) days = 1; 
        
        var prevStart = start.AddDays(-days);
        var prevEnd = start.AddTicks(-1);

        var prevVentas = await _context.Ventas
            .Where(v => v.FechaVenta >= prevStart && v.FechaVenta <= prevEnd && !v.Anulada)
            .ToListAsync();

        var stats = new DashboardStatsDTO
        {
            VentasBrutas = currentVentas.Sum(v => v.TotalNio),
            TotalFacturas = currentVentas.Count,
            ProductosVendidos = currentVentas.SelectMany(v => v.VentaDetalles).Sum(d => d.Cantidad),
            TicketPromedio = currentVentas.Any() ? currentVentas.Average(v => v.TotalNio) : 0,
            UtilidadNeta = currentVentas.SelectMany(v => v.VentaDetalles).Sum(d => 
                d.SubtotalNio - ((d.IdProductoNavigation?.PrecioCompra ?? 0) * d.Cantidad)),
            ClientesNuevos = await _context.Personas.CountAsync(p => p.FechaCreacion >= start && p.FechaCreacion <= end && p.EsCliente)
        };

        // Calcular porcentajes
        decimal prevVentasTotal = prevVentas.Sum(v => v.TotalNio);
        stats.PorcentajeVentas = CalcularVariacion(stats.VentasBrutas, prevVentasTotal);
        stats.PorcentajeFacturas = CalcularVariacion(stats.TotalFacturas, prevVentas.Count);

        return stats;
    }

    public async Task<List<TrendPointDTO>> GetSalesTrendsAsync(DateTime start, DateTime end)
    {
        var ventas = await _context.Ventas
            .Where(v => v.FechaVenta >= start && v.FechaVenta <= end && !v.Anulada)
            .OrderBy(v => v.FechaVenta)
            .ToListAsync();

        return ventas.GroupBy(v => v.FechaVenta.Date)
            .Select(g => new TrendPointDTO
            {
                Label = g.Key.ToString("dd MMM"),
                ValorNio = g.Sum(v => v.TotalNio),
                ValorUsd = g.Sum(v => v.TotalNio / 36.5m) // Asumiendo tasa fija por ahora para el gráfico
            })
            .ToList();
    }

    public async Task<List<PaymentMethodStatDTO>> GetPaymentMethodStatsAsync(DateTime start, DateTime end)
    {
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
                Total = g.Sum(p => p.MontoEnNio),
                Porcentaje = (double)(g.Sum(p => p.MontoEnNio) / (total > 0 ? total : 1) * 100)
            })
            .ToList();
    }

    public async Task<List<TopProductoDTO>> GetTopProductosAsync(DateTime start, DateTime end, int count = 5)
    {
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
        var critico = await _context.VStockCriticos.ToListAsync();
        
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
            SinMovimiento = result
        };
    }

    public async Task<List<ClientInsightDTO>> GetClientInsightsAsync(DateTime start, DateTime end)
    {
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
        var turnos = await _context.Turnos
            .Include(t => t.IdUsuarioNavigation)
            .Include(t => t.MovimientosVarios)
            .Where(t => t.FechaApertura <= end && (t.FechaCierre == null || t.FechaCierre >= start))
            .OrderByDescending(t => t.FechaApertura)
            .ToListAsync();

        return turnos.Select(t => {
            decimal ingresosVarios = t.MovimientosVarios.Where(m => m.Tipo == "INGRESO").Sum(m => m.Monto);
            decimal salidasVarias = t.MovimientosVarios.Where(m => m.Tipo == "EGRESO").Sum(m => m.Monto);
            
            return new ArqueoInsightDTO
            {
                IdTurno = t.IdTurno,
                Usuario = t.IdUsuarioNavigation?.Username ?? "Sistema",
                Apertura = t.FechaApertura,
                Cierre = t.FechaCierre,
                SaldoTeorico = t.MontoInicialNio + t.TotalVentasNio + ingresosVarios - salidasVarias,
                SaldoReal = t.MontoContadoNio ?? 0
            };
        }).ToList();
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

    private decimal CalcularVariacion(decimal actual, decimal anterior)
    {
        if (anterior == 0) return actual > 0 ? 100 : 0;
        return ((actual - anterior) / anterior) * 100;
    }
}

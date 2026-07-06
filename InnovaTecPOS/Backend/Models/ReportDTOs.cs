using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public class DashboardStatsDTO
{
    // Córdobas
    public decimal VentasBrutasNio { get; set; }
    public decimal UtilidadNetaNio { get; set; }
    public decimal DescuentosNio { get; set; }
    public decimal ValorRegaliasNio { get; set; }

    // Dólares
    public decimal VentasBrutasUsd { get; set; }
    public decimal UtilidadNetaUsd { get; set; }
    public decimal DescuentosUsd { get; set; }
    public decimal ValorRegaliasUsd { get; set; }

    // Conteos y Métricas de Operaciones
    public int TotalFacturas { get; set; }
    public int FacturasReversadas { get; set; }
    public int ArticulosReversados { get; set; }
    public int FacturasRegalia { get; set; }

    public int ClientesNuevos { get; set; }
    public int ProductosVendidos { get; set; }
    public int Anulaciones { get; set; }
    
    // Comparativas con periodo anterior (generalizadas para no complicar en exceso)
    public decimal PorcentajeVentas { get; set; }
    public decimal PorcentajeUtilidad { get; set; }
    public decimal PorcentajeFacturas { get; set; }
    public decimal PorcentajeDescuentos { get; set; }
    public decimal PorcentajeRegalias { get; set; }
    
    // Opcional para gráficas combinadas (Total Globalizado)
    public decimal VentasTotalesCalculadasNio => VentasBrutasNio + (VentasBrutasUsd * 36.5m); // Para propósitos de ordenamiento general
}

public class TrendPointDTO
{
    public string Label { get; set; } = string.Empty;
    public decimal ValorNio { get; set; }
    public decimal ValorUsd { get; set; }
}

public class PaymentMethodStatDTO
{
    public string Metodo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public double Porcentaje { get; set; }
}

public class TopProductoDTO
{
    public string Nombre { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public decimal TotalVentas { get; set; }
}

public class ResumenDiarioDTO
{
    public DateTime Fecha { get; set; }
    public decimal VentasBrutas { get; set; }
    public decimal Devoluciones { get; set; }
    public decimal VentasNetas { get; set; }
    public int Facturas { get; set; }
    public decimal TicketPromedio { get; set; }
}

public class HourlySalesDTO
{
    public int DayOfWeek { get; set; } // 0=Sun, 1=Mon...
    public int Hour { get; set; }
    public decimal Total { get; set; }
    public int Intensity { get; set; } // 0-10 for heatmap
}

public class InventoryInsightDTO
{
    public List<VStockValorizadoDTO> StockCritico { get; set; } = new();
    public List<ProductNoMovementDTO> SinMovimiento { get; set; } = new();
    public decimal ValorTotalCosto { get; set; }
    public decimal ValorTotalVenta { get; set; }
}

public class VStockValorizadoDTO
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public string EstadoStock { get; set; } = "";
    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal ValorCostoTotal => StockActual * PrecioCompra;
    public decimal ValorVentaTotal => StockActual * PrecioVenta;
}

public class ProductNoMovementDTO
{
    public string Nombre { get; set; } = string.Empty;
    public DateTime? UltimaVenta { get; set; }
    public int DiasSinVenta { get; set; }
}

public class ClientInsightDTO
{
    public string Nombre { get; set; } = string.Empty;
    public int TotalCompras { get; set; }
    public decimal MontoTotal { get; set; }
}

public class CashierAuditDTO
{
    public string Cajero { get; set; } = "";
    public int Facturas { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal Descuentos { get; set; }
    public decimal TicketPromedio => Facturas > 0 ? TotalVentas / Facturas : 0;
}

public class ArqueoInsightDTO
{
    public int IdTurno { get; set; }
    public string Usuario { get; set; } = "";
    public string Moneda { get; set; } = ""; // "C$ CORDOBAS" o "$ DOLARES"
    public DateTime Apertura { get; set; }
    public DateTime? Cierre { get; set; }
    public decimal MontoInicial { get; set; }
    
    // VENTAS
    public int VentasEfectuadas { get; set; }
    public int VentasAnuladas { get; set; }
    public decimal VentasNetas { get; set; }
    
    // COBROS VENTAS
    public decimal CobrosEfectivo { get; set; }
    public decimal CobrosTransferencia { get; set; }
    public decimal CobrosTarjeta { get; set; }
    
    // OTROS MOVIMIENTOS
    public decimal OtrosIngresos { get; set; }
    public decimal OtrosRetiros { get; set; }
    public decimal Reversos { get; set; }
    public decimal VueltoEntregado { get; set; }
    
    // CAJA
    public decimal SaldoTeorico { get; set; }
    public decimal SaldoReal { get; set; }
    public decimal Diferencia => SaldoReal - SaldoTeorico;
    public string Estado { get; set; } = ""; // EN CURSO, CUADRADO, DESCUADRE
    
    public bool EsFilaPrincipal { get; set; }
}

public class GarantiaInsightDTO
{
    public int Activas { get; set; }
    public int PorVencer { get; set; }
    public int Reclamadas { get; set; }
    public List<GarantiaDetalleDTO> Recientes { get; set; } = new();
}

public class GarantiaDetalleDTO
{
    public string Factura { get; set; } = "";
    public string Producto { get; set; } = "";
    public DateTime Vencimiento { get; set; }
    public string Estado { get; set; } = "";
}

public class SystemAlertDTO
{
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public string Tipo { get; set; } = "info"; // info, warning, danger
    public DateTime Fecha { get; set; }
}

public class VentaTurnoDTO
{
    public int IdVenta { get; set; }
    public string NumeroFactura { get; set; } = "";
    public DateTime FechaVenta { get; set; }
    public string Cliente { get; set; } = "";
    public decimal TotalNio { get; set; }
    public string MetodoPago { get; set; } = "";
    public bool Anulada { get; set; }
}

public class CategoryStatDTO
{
    public string Categoria { get; set; } = "";
    public decimal Total { get; set; }
}

public class MovimientoTurnoDTO
{
    public string TipoMovimiento { get; set; } = ""; // "Venta", "Regalía", "Ingreso", "Egreso", "Reverso"
    public string Referencia { get; set; } = ""; // Factura # o Concepto
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; } = ""; 
    public string MetodoPago { get; set; } = "";
    public decimal Descuento { get; set; }
    public decimal Monto { get; set; }
    public decimal MontoPagado { get; set; }
    public decimal Vuelto { get; set; } 
    public decimal MontoReverso { get; set; }
    public decimal MontoTotal { get; set; }
    
    public string SimboloMonedaMonto { get; set; } = "C$"; 
    public string SimboloMonedaPago { get; set; } = "C$";
    public string SimboloMonedaVuelto { get; set; } = "C$";
    
    public string Estado { get; set; } = ""; // EFECTUADA, ANULADA, COMPLETADO
}


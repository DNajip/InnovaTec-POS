using InnovaTecPOS.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface ICheckoutService
{
    Task<Venta> ProcessCheckoutAsync(int userId, int? idPersona, decimal discount, List<CartItem> items, List<PaymentInput> payments);
    Task<List<PeriodosGarantium>> GetPeriodosGarantiaAsync();
    Task<List<MetodosPago>> GetMetodosPagoAsync();
    Task<EquiposImei?> ValidateImeiAsync(int idProducto, string imei);
}

public class CheckoutService : ICheckoutService
{
    private readonly InnovaTecDbContext _context;
    private readonly IShiftService _shiftService;

    public CheckoutService(InnovaTecDbContext context, IShiftService shiftService)
    {
        _context = context;
        _shiftService = shiftService;
    }

    public async Task<List<PeriodosGarantium>> GetPeriodosGarantiaAsync()
    {
        return await _context.PeriodosGarantia
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Meses)
            .ToListAsync();
    }

    public async Task<List<MetodosPago>> GetMetodosPagoAsync()
    {
        return await _context.MetodosPagos
            .Include(m => m.IdMonedaNavigation)
            .OrderBy(m => m.Nombre)
            .ToListAsync();
    }

    public async Task<EquiposImei?> ValidateImeiAsync(int idProducto, string imei)
    {
        return await _context.EquiposImeis
            .FirstOrDefaultAsync(i => i.IdProducto == idProducto && i.Imei == imei && i.EstadoImei == "DISPONIBLE");
    }

    public async Task<Venta> ProcessCheckoutAsync(int userId, int? idPersona, decimal discount, List<CartItem> items, List<PaymentInput> payments)
    {
        // 1. Validate Shift
        var turno = await _shiftService.GetActiveShiftAsync(userId);
        if (turno == null)
            throw new Exception("Debe abrir un turno de caja antes de facturar.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 2. Create Sale
            var sTotal = items.Sum(i => i.SubTotal);
            var totalVentaNio = sTotal - discount;
            var tasaCambio = 36.60m; // Hardcoded for now, could come from DB

            var venta = new Venta
            {
                IdTurno = turno.IdTurno,
                IdUsuario = userId,
                IdPersona = idPersona,
                FechaVenta = DateTime.Now,
                NumeroFactura = $"FAC-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                TasaCambioUsd = tasaCambio,
                SubtotalNio = sTotal,
                DescuentoNio = discount,
                TotalNio = totalVentaNio,
                Anulada = false
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            // 3. Process Payments
            var totalPagadoNio = payments.Sum(p => p.MontoEnNio);
            if (totalPagadoNio < totalVentaNio)
                throw new Exception($"El monto pagado (C$ {totalPagadoNio:N2}) es insuficiente para cubrir el total (C$ {totalVentaNio:N2}).");

            var vueltoPendienteNio = totalPagadoNio - totalVentaNio;

            foreach (var pInput in payments)
            {
                var pago = new Pago
                {
                    IdVenta = venta.IdVenta,
                    IdMetodoPago = pInput.IdMetodoPago,
                    MontoPagado = pInput.Monto,
                    TasaAplicada = pInput.TasaCambio,
                    MontoEnNio = pInput.MontoEnNio,
                    CodReferencia = pInput.Referencia,
                    FechaPago = DateTime.Now
                };

                // Assign change to the first cash payment found
                if (vueltoPendienteNio > 0 && (pInput.MetodoNombre.Contains("EFECTIVO", StringComparison.OrdinalIgnoreCase)))
                {
                    pago.VueltoNio = vueltoPendienteNio;
                    vueltoPendienteNio = 0; // Vuelto assigned
                }

                _context.Pagos.Add(pago);

                // Update Turno Balances
                var metodo = await _context.MetodosPagos.Include(m => m.IdMonedaNavigation)
                                     .FirstOrDefaultAsync(m => m.IdMetodo == pInput.IdMetodoPago);
                
                if (metodo != null)
                {
                    if (metodo.Nombre.Contains("EFECTIVO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (metodo.IdMonedaNavigation?.Codigo == "NIO")
                            turno.TotalEfectivoNio += (pInput.MontoEnNio - (pago.VueltoNio ?? 0));
                        else if (metodo.IdMonedaNavigation?.Codigo == "USD")
                            turno.TotalEfectivoUsd += pInput.Monto;
                    }
                    else if (metodo.Nombre.Contains("TARJETA", StringComparison.OrdinalIgnoreCase))
                        turno.TotalTarjeta += pInput.MontoEnNio;
                    else if (metodo.Nombre.Contains("TRANSFERENCIA", StringComparison.OrdinalIgnoreCase))
                        turno.TotalTransferencia += pInput.MontoEnNio;
                }
            }

            // 4. Process Details
            foreach (var item in items)
            {
                var product = await _context.Productos.FindAsync(item.IdProducto);
                if (product == null) throw new Exception($"Producto {item.Description} no encontrado.");

                foreach (var detail in item.Details)
                {
                    var periodo = await _context.PeriodosGarantia.FindAsync(detail.IdPeriodoGarantia);
                    DateTime? fechaVence = null;
                    if (periodo != null && periodo.Meses > 0)
                        fechaVence = DateTime.Now.AddMonths(periodo.Meses);

                    var vDetail = new VentaDetalle
                    {
                        IdVenta = venta.IdVenta,
                        IdProducto = item.IdProducto,
                        DescripcionSnap = item.Description,
                        Cantidad = 1,
                        PrecioUnitarioNio = item.UnitPrice,
                        SubtotalNio = item.UnitPrice,
                        IdPeriodoGarantia = detail.IdPeriodoGarantia,
                        FechaVenceGarantia = fechaVence != null ? DateOnly.FromDateTime(fechaVence.Value) : null
                    };

                    _context.VentaDetalles.Add(vDetail);
                    await _context.SaveChangesAsync();

                    int? imeiId = null;
                    if (item.RequiresImei)
                    {
                        if (string.IsNullOrWhiteSpace(detail.Imei))
                            throw new Exception($"El producto {item.Description} requiere IMEI.");

                        var imeiRecord = await _context.EquiposImeis
                            .FirstOrDefaultAsync(i => i.IdProducto == item.IdProducto && i.Imei == detail.Imei);

                        if (imeiRecord == null)
                        {
                            imeiRecord = new EquiposImei
                            {
                                IdProducto = item.IdProducto,
                                Imei = detail.Imei,
                                EstadoImei = "VENDIDO",
                                FechaIngreso = DateTime.Now,
                                IngresadoPor = userId
                            };
                            _context.EquiposImeis.Add(imeiRecord);
                        }
                        else
                        {
                            if (imeiRecord.EstadoImei == "VENDIDO")
                                throw new Exception($"El IMEI '{detail.Imei}' ya fue vendido anteriormente.");
                            
                            imeiRecord.EstadoImei = "VENDIDO";
                            _context.EquiposImeis.Update(imeiRecord);
                        }
                        
                        await _context.SaveChangesAsync();
                        imeiId = imeiRecord.IdImei;

                        var vImei = new VentaDetalleImei
                        {
                            IdDetalle = vDetail.IdDetalle,
                            IdEquipoImei = imeiId.Value,
                            ImeiSnap = detail.Imei
                        };
                        _context.VentaDetalleImeis.Add(vImei);
                    }

                    if (fechaVence != null && idPersona.HasValue)
                    {
                        var warranty = new Garantia
                        {
                            IdDetalleVenta = vDetail.IdDetalle,
                            IdPersona = idPersona.Value,
                            IdProducto = item.IdProducto,
                            IdEquipoImei = imeiId,
                            MesesGarantia = periodo?.Meses ?? 0,
                            FechaInicio = DateOnly.FromDateTime(DateTime.Now),
                            FechaVencimiento = DateOnly.FromDateTime(fechaVence.Value),
                            EstadoGarantia = "ACTIVA"
                        };
                        _context.Garantias.Add(warranty);
                    }

                    await _context.SaveChangesAsync();
                }

                product.StockActual -= item.Quantity;
                _context.Productos.Update(product);

                _context.Movimientos.Add(new Movimiento
                {
                    IdProducto = item.IdProducto,
                    IdTipoMov = 2,
                    Cantidad = -item.Quantity,
                    IdReferencia = venta.IdVenta,
                    TablaReferencia = "VEN.VENTAS",
                    Observacion = $"Venta {venta.NumeroFactura}",
                    FechaMov = DateTime.Now,
                    RegistradoPor = userId
                });
            }

            // 5. Update Global Turno Sales
            turno.TotalVentasNio += venta.TotalNio;
            turno.TotalVentasUsd += (venta.TotalNio / venta.TasaCambioUsd);
            _context.Turnos.Update(turno);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return venta;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

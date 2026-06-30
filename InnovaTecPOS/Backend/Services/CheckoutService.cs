using InnovaTecPOS.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface ICheckoutService
{
    Task<Venta> ProcessCheckoutAsync(int userId, int? idPersona, decimal discount, List<CartItem> items, List<PaymentInput> payments);
    Task<List<PeriodosGarantium>> GetPeriodosGarantiaAsync();
    Task<List<MetodosPago>> GetMetodosPagoAsync();
    Task<EquiposImei?> ValidateImeiAsync(int idProducto, string imei);
    Task ReversarTransaccionAsync(int idVenta, int idUsuario, string motivo, string? detalleJson);
}

public class CheckoutService : ICheckoutService
{
    private readonly IDbContextFactory<InnovaTecDbContext> _factory;
    private readonly IShiftService _shiftService;

    public CheckoutService(IDbContextFactory<InnovaTecDbContext> factory, IShiftService shiftService)
    {
        _factory = factory;
        _shiftService = shiftService;
    }

    public async Task<List<PeriodosGarantium>> GetPeriodosGarantiaAsync()
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.PeriodosGarantia
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Meses)
            .ToListAsync();
    }

    public async Task<List<MetodosPago>> GetMetodosPagoAsync()
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.MetodosPagos
            .Include(m => m.IdMonedaNavigation)
            .OrderBy(m => m.Nombre)
            .ToListAsync();
    }

    public async Task<EquiposImei?> ValidateImeiAsync(int idProducto, string imei)
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.EquiposImeis
            .FirstOrDefaultAsync(i => i.IdProducto == idProducto && i.Imei == imei && i.EstadoImei == "DISPONIBLE");
    }

    public async Task<Venta> ProcessCheckoutAsync(int userId, int? idPersona, decimal discount, List<CartItem> items, List<PaymentInput> payments)
    {
        using var context = await _factory.CreateDbContextAsync();
        // 1. Serializar colecciones a JSON para enviarlas al SP
        var mappedItems = items.Select(i => new {
            i.IdProducto,
            i.Code,
            i.Description,
            UnitPrice = i.IsRegalia ? 0 : i.UnitPrice,
            i.IdCategoria,
            i.StockMax,
            i.RequiresImei,
            i.IsRegalia,
            i.Quantity,
            SubTotal = i.IsRegalia ? 0 : (i.UnitPrice * i.Quantity),
            i.Details
        });
        var itemsJson = System.Text.Json.JsonSerializer.Serialize(mappedItems);
        
        // Mapeamos pagos para asegurar que las propiedades coincidan con el SP
        var paymentsMapped = payments.Select(p => new {
            p.IdMetodoPago,
            p.Monto,
            p.TasaCambio,
            MontoEnNio = p.Monto * p.TasaCambio, // Este es el monto bruto recibido
            p.Referencia
        });
        var paymentsJson = System.Text.Json.JsonSerializer.Serialize(paymentsMapped);

        try 
        {
            // 2. Ejecutar el SP Maestro de Venta
            var result = await context.Ventas
                .FromSqlRaw("EXEC VEN.sp_ProcesarVenta @IdUsuario={0}, @IdPersona={1}, @DescuentoNio={2}, @TasaCambioUsd={3}, @ItemsJson={4}, @PaymentsJson={5}",
                    userId,
                    idPersona ?? (object)DBNull.Value,
                    discount,
                    36.60m, // Podría venir de configuración
                    itemsJson,
                    paymentsJson)
                .AsNoTracking()
                .ToListAsync();

            if (!result.Any())
                throw new Exception("La base de datos no devolvió el registro de la venta.");

            return result.First();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error en Checkout (DB): {ex.Message}");
        }
    }

    public async Task ReversarTransaccionAsync(int idVenta, int idUsuario, string motivo, string? detalleJson)
    {
        using var context = await _factory.CreateDbContextAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "EXEC VEN.sp_ReversoTransaccion @p0, @p1, @p2, @p3",
                idVenta, 
                idUsuario, 
                motivo, 
                string.IsNullOrWhiteSpace(detalleJson) ? (object)DBNull.Value : detalleJson);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            // The SP uses THROW with errors > 50000, which SqlException captures nicely
            throw new Exception(ex.Message);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error interno al reversar: {ex.Message}");
        }
    }
}

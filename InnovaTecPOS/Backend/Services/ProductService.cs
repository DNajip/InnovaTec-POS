using InnovaTecPOS.Backend.Models;
using InnovaTecPOS.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface IProductService
{
    Task<List<Producto>> SearchProductsAsync(string term);
    Task<List<EquiposImei>> GetAvailableImeisAsync(int idProducto);
    
    // Inventory methods
    Task<List<Producto>> GetAllProductsAsync(string? search = null, string? tipoFiltro = null);
    Task<InventoryStatsDto> GetInventoryStatsAsync();
    Task AdjustStockAsync(int idProducto, int cantidad, string observacion);
}

public class ProductService : IProductService
{
    private readonly InnovaTecDbContext _context;

    public ProductService(InnovaTecDbContext context)
    {
        _context = context;
    }

    public async Task<List<Producto>> SearchProductsAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return new List<Producto>();

        term = term.ToLower();

        return await _context.Productos
            .Where(p => p.Activo == true &&
                        p.StockActual > 0 &&
                        (p.Nombre.ToLower().Contains(term) ||
                         (p.CodigoBarras != null && p.CodigoBarras.Contains(term))))
            .Take(10)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<EquiposImei>> GetAvailableImeisAsync(int idProducto)
    {
        return await _context.EquiposImeis
            .Where(i => i.IdProducto == idProducto && i.EstadoImei == "DISPONIBLE")
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Producto>> GetAllProductsAsync(string? search = null, string? tipoFiltro = null)
    {
        var query = _context.Productos.Where(p => p.Activo == true);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(p => p.Nombre.ToLower().Contains(search) || 
                                    (p.CodigoBarras != null && p.CodigoBarras.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(tipoFiltro) && tipoFiltro != "TODOS")
        {
            query = query.Where(p => p.TipoProducto.Trim() == tipoFiltro);
        }

        return await query.OrderBy(p => p.Nombre).ToListAsync();
    }

    public async Task<InventoryStatsDto> GetInventoryStatsAsync()
    {
        var productos = await _context.Productos.Where(p => p.Activo == true).ToListAsync();

        return new InventoryStatsDto
        {
            TotalProductos = productos.Count,
            StockBajo = productos.Count(p => p.EstadoStock == "CRITICO"),
            SinStock = productos.Count(p => p.EstadoStock == "AGOTADO"),
            Valorizacion = productos.Sum(p => p.PrecioVenta * p.StockActual)
        };
    }

    public async Task AdjustStockAsync(int idProducto, int cantidad, string observacion)
    {
        var producto = await _context.Productos.FindAsync(idProducto);
        if (producto == null) return;

        // Simple adjustment for now. In a real scenario, this should create a Movimiento record.
        producto.StockActual = cantidad;
        
        await _context.SaveChangesAsync();
    }
}

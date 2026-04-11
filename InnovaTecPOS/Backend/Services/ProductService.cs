using InnovaTecPOS.Backend.Models;
using InnovaTecPOS.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface IProductService
{
    Task<List<Producto>> SearchProductsAsync(string term);
    Task<List<EquiposImei>> GetAvailableImeisAsync(int idProducto);
    
    // Inventory methods
    Task<List<Producto>> GetAllProductsAsync(string? search = null, int? idCategoria = null);
    Task<InventoryStatsDto> GetInventoryStatsAsync();
    Task<List<Categoria>> GetCategoriasAsync();
    Task<Producto?> GetProductByIdAsync(int id);
    Task CreateProductAsync(Producto producto);
    Task UpdateProductAsync(Producto producto);
    Task AdjustStockAsync(int idProducto, int nuevaCantidad, string observacion);
}

public class ProductService : IProductService
{
    private readonly InnovaTecDbContext _context;
    private readonly UserSession _userSession;

    public ProductService(InnovaTecDbContext context, UserSession userSession)
    {
        _context = context;
        _userSession = userSession;
    }

    public async Task<List<Producto>> SearchProductsAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return new List<Producto>();

        term = term.ToLower();

        return await _context.Productos
            .Include(p => p.IdCategoriaNavigation)
            .Where(p => p.Activo == true &&
                        p.StockActual > 0 &&
                        (p.Nombre.ToLower().Contains(term) ||
                         (p.CodigoBarras != null && p.CodigoBarras.Contains(term))))
            .Take(10)
            .ToListAsync();
    }

    public async Task<List<EquiposImei>> GetAvailableImeisAsync(int idProducto)
    {
        return await _context.EquiposImeis
            .Where(i => i.IdProducto == idProducto && i.EstadoImei == "DISPONIBLE")
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Producto>> GetAllProductsAsync(string? search = null, int? idCategoria = null)
    {
        var query = _context.Productos
            .Include(p => p.IdCategoriaNavigation)
            .Where(p => p.Activo == true);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(p => p.Nombre.ToLower().Contains(search) || 
                                    (p.CodigoBarras != null && p.CodigoBarras.Contains(search)));
        }

        if (idCategoria.HasValue && idCategoria.Value > 0)
        {
            query = query.Where(p => p.IdCategoria == idCategoria.Value);
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

    public async Task<List<Categoria>> GetCategoriasAsync()
    {
        return await _context.Categorias.Where(c => c.IdEstado == 1).OrderBy(c => c.Nombre).ToListAsync();
    }

    public async Task<Producto?> GetProductByIdAsync(int id)
    {
        return await _context.Productos.FindAsync(id);
    }

    public async Task CreateProductAsync(Producto producto)
    {
        _userSession.CurrentObservation = $"Creación inicial de producto: {producto.Nombre}";
        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateProductAsync(Producto producto)
    {
        _userSession.CurrentObservation = $"Edición de datos básicos de producto: {producto.Nombre}";
        _context.Productos.Update(producto);
        await _context.SaveChangesAsync();
    }

    public async Task AdjustStockAsync(int idProducto, int nuevaCantidad, string observacion)
    {
        var producto = await _context.Productos.FindAsync(idProducto);
        if (producto == null) return;

        // Establecer la observación en la sesión antes de guardar. 
        // El trigger de la BD la capturará desde SESSION_CONTEXT.
        _userSession.CurrentObservation = observacion;
        
        producto.StockActual = nuevaCantidad;
        
        await _context.SaveChangesAsync();
    }
}

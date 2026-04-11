using InnovaTecPOS.Backend.Models;
using InnovaTecPOS.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface IProductService
{
    Task<List<Producto>> SearchProductsAsync(string term);
    Task<List<EquiposImei>> GetAvailableImeisAsync(int idProducto);
    Task<Producto?> GetProductByBarcodeAsync(string barcode);
    
    // Inventory methods
    Task<List<Producto>> GetAllProductsAsync(string? search = null, int? idCategoria = null, bool includeInactive = false);
    Task<InventoryStatsDto> GetInventoryStatsAsync();
    Task<List<Categoria>> GetCategoriasAsync();
    Task<Producto?> GetProductByIdAsync(int id);
    Task CreateProductAsync(Producto producto);
    Task UpdateProductAsync(Producto producto);
    Task AdjustStockAsync(int idProducto, int nuevaCantidad, string observacion);
    Task<List<Movimiento>> GetProductMovementsAsync(int idProducto);
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
            .AsNoTracking()
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
    
    public async Task<Producto?> GetProductByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        return await _context.Productos
            .Include(p => p.IdCategoriaNavigation)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Activo == true && p.CodigoBarras == barcode);
    }

    public async Task<List<Producto>> GetAllProductsAsync(string? search = null, int? idCategoria = null, bool includeInactive = false)
    {
        var query = _context.Productos
            .Include(p => p.IdCategoriaNavigation)
            .Include(p => p.Movimientos)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.Activo == true);
        }

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

        return await query.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
    }

    public async Task<InventoryStatsDto> GetInventoryStatsAsync()
    {
        Console.WriteLine("Service: GetInventoryStatsAsync called");
        var allProducts = await _context.Productos.AsNoTracking().ToListAsync();
        var activeProducts = allProducts.Where(p => p.Activo == true).ToList();

        return new InventoryStatsDto
        {
            TotalProductos = activeProducts.Count,
            StockBajo = activeProducts.Count(p => p.EstadoStock == "CRITICO"),
            SinStock = activeProducts.Count(p => p.EstadoStock == "AGOTADO"),
            Valorizacion = allProducts.Sum(p => p.PrecioVenta * p.StockActual)
        };
    }

    public async Task<List<Categoria>> GetCategoriasAsync()
    {
        return await _context.Categorias.Where(c => c.IdEstado == 1).OrderBy(c => c.Nombre).ToListAsync();
    }

    public async Task<Producto?> GetProductByIdAsync(int id)
    {
        return await _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProducto == id);
    }

    public async Task CreateProductAsync(Producto producto)
    {
        _userSession.CurrentObservation = $"Creación inicial de producto: {producto.Nombre}";
        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateProductAsync(Producto producto)
    {
        Console.WriteLine($"Service: UpdateProductAsync for ID {producto.IdProducto}");
        // Load the existing entity from DB (tracked)
        var existing = await _context.Productos.FirstOrDefaultAsync(p => p.IdProducto == producto.IdProducto);
        
        if (existing == null)
            throw new Exception($"Producto con ID {producto.IdProducto} no encontrado.");

        _userSession.CurrentObservation = $"Edición de datos básicos de producto: {producto.Nombre}";

        // Map ONLY editable fields (ignore computed columns like EstadoStock and Default columns like FechaCreacion)
        existing.Nombre = producto.Nombre;
        existing.CodigoBarras = producto.CodigoBarras;
        existing.Marca = producto.Marca;
        existing.Modelo = producto.Modelo;
        existing.Almacenamiento = producto.Almacenamiento;
        existing.Color = producto.Color;
        existing.IdCategoria = producto.IdCategoria;
        existing.PrecioCompra = producto.PrecioCompra;
        existing.PrecioVenta = producto.PrecioVenta;
        existing.StockMinimo = producto.StockMinimo;
        existing.Activo = producto.Activo;

        await _context.SaveChangesAsync();
        Console.WriteLine("Service: SaveChangesAsync completed.");
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

    public async Task<List<Movimiento>> GetProductMovementsAsync(int idProducto)
    {
        return await _context.Movimientos
            .Include(m => m.IdTipoMovNavigation)
            .Include(m => m.RegistradoPorNavigation)
            .Where(m => m.IdProducto == idProducto)
            .OrderByDescending(m => m.FechaMov)
            .AsNoTracking()
            .ToListAsync();
    }
}

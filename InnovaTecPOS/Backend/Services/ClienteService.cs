using InnovaTecPOS.Backend.Models;
using InnovaTecPOS.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface IClienteService
{
    Task<List<Persona>> GetAllClientesAsync(string? search = null);
    Task<Persona?> GetClienteByIdAsync(int id);
    Task CreateClienteAsync(Persona persona);
    Task UpdateClienteAsync(Persona persona);
    Task<List<Venta>> GetComprasClienteAsync(int idPersona);
    Task<List<Garantia>> GetGarantiasClienteAsync(int idPersona);
    Task<ClienteStatsDto> GetClienteStatsAsync();
    Task<List<TipoIdentificacion>> GetTiposIdentificacionAsync();
}

public class ClienteService : IClienteService
{
    private readonly InnovaTecDbContext _context;

    public ClienteService(InnovaTecDbContext context)
    {
        _context = context;
    }

    public async Task<List<Persona>> GetAllClientesAsync(string? search = null)
    {
        var query = _context.Personas
            .Include(p => p.IdTipo)
            .Include(p => p.Venta)
            .Where(p => p.EsCliente == true && p.IdEstado == 1);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(p =>
                (p.NombreCompleto != null && p.NombreCompleto.ToLower().Contains(search)) ||
                p.NumIdentificacion.Contains(search) ||
                (p.Telefono != null && p.Telefono.Contains(search)));
        }

        return await query
            .OrderBy(p => p.PrimerNombre)
            .ThenBy(p => p.PrimerApellido)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Persona?> GetClienteByIdAsync(int id)
    {
        return await _context.Personas
            .Include(p => p.IdTipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPersona == id && p.EsCliente == true);
    }

    public async Task CreateClienteAsync(Persona persona)
    {
        persona.EsCliente = true;
        persona.EsEmpleado = false;
        persona.FechaCreacion = DateTime.Now;
        persona.IdEstado = 1; // Activo
        _context.Personas.Add(persona);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateClienteAsync(Persona persona)
    {
        var existing = await _context.Personas
            .FirstOrDefaultAsync(p => p.IdPersona == persona.IdPersona);
        if (existing == null)
            throw new Exception($"Cliente con ID {persona.IdPersona} no encontrado.");

        existing.PrimerNombre = persona.PrimerNombre;
        existing.SegundoNombre = persona.SegundoNombre;
        existing.PrimerApellido = persona.PrimerApellido;
        existing.SegundoApellido = persona.SegundoApellido;
        existing.IdTipoId = persona.IdTipoId;
        existing.NumIdentificacion = persona.NumIdentificacion;
        existing.IdGenero = persona.IdGenero;
        existing.Telefono = persona.Telefono;
        existing.Email = persona.Email;
        existing.Direccion = persona.Direccion;

        await _context.SaveChangesAsync();
    }

    public async Task<List<Venta>> GetComprasClienteAsync(int idPersona)
    {
        return await _context.Ventas
            .Include(v => v.VentaDetalles)
                .ThenInclude(d => d.IdProductoNavigation)
            .Include(v => v.VentaDetalles)
                .ThenInclude(d => d.IdPeriodoGarantiaNavigation)
            .Include(v => v.IdUsuarioNavigation)
            .Where(v => v.IdPersona == idPersona && !v.Anulada)
            .OrderByDescending(v => v.FechaVenta)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Garantia>> GetGarantiasClienteAsync(int idPersona)
    {
        return await _context.Garantias
            .Include(g => g.IdProductoNavigation)
            .Include(g => g.IdEquipoImeiNavigation)
            .Include(g => g.IdDetalleVentaNavigation)
            .Where(g => g.IdPersona == idPersona)
            .OrderByDescending(g => g.FechaInicio)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ClienteStatsDto> GetClienteStatsAsync()
    {
        var clientes = await _context.Personas
            .Where(p => p.EsCliente == true && p.IdEstado == 1)
            .AsNoTracking()
            .ToListAsync();

        var clienteIds = clientes.Select(c => c.IdPersona).ToList();

        var garantiasActivas = await _context.Garantias
            .Where(g => clienteIds.Contains(g.IdPersona) && g.EstadoGarantia == "ACTIVA")
            .Select(g => g.IdPersona)
            .Distinct()
            .CountAsync();

        var hace30Dias = DateTime.Now.AddDays(-30);
        var comprasRecientes = await _context.Ventas
            .Where(v => v.IdPersona.HasValue && clienteIds.Contains(v.IdPersona.Value)
                        && v.FechaVenta >= hace30Dias && !v.Anulada)
            .Select(v => v.IdPersona)
            .Distinct()
            .CountAsync();

        return new ClienteStatsDto
        {
            TotalClientes = clientes.Count,
            ConGarantiasActivas = garantiasActivas,
            ConComprasRecientes = comprasRecientes
        };
    }

    public async Task<List<TipoIdentificacion>> GetTiposIdentificacionAsync()
    {
        return await _context.TipoIdentificacions
            .Where(t => t.IdEstado == 1)
            .OrderBy(t => t.DescTipo)
            .AsNoTracking()
            .ToListAsync();
    }
}

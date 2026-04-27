using InnovaTecPOS.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace InnovaTecPOS.Backend.Services;

public interface IShiftService
{
    Task<Turno?> GetActiveShiftAsync(int userId);
    Task<Turno> OpenShiftAsync(int userId, decimal initialNio, decimal initialUsd, List<ConteoDenominacione> counts);
    Task CloseShiftAsync(int turnoId, decimal finalNio, decimal finalUsd, List<ConteoDenominacione> counts, string? observations);
    Task<List<Denominacione>> GetDenominationsAsync();
}

public class ShiftService : IShiftService
{
    private readonly InnovaTecDbContext _context;

    public ShiftService(InnovaTecDbContext context)
    {
        _context = context;
    }

    public async Task<Turno?> GetActiveShiftAsync(int userId)
    {
        // Must match the filter in UX_TURNO_ACTIVO: [ID_ESTADO]=(1)
        // Also check for null FechaCierre to be sure it's the current one
        return await _context.Turnos
            .Include(t => t.IdUsuarioNavigation)
            .FirstOrDefaultAsync(t => t.IdUsuario == userId && (t.IdEstado == 1 || t.FechaCierre == null));
    }

    public async Task<Turno> OpenShiftAsync(int userId, decimal initialNio, decimal initialUsd, List<ConteoDenominacione> counts)
    {
        var existing = await GetActiveShiftAsync(userId);
        if (existing != null) return existing;

        var activeState = await _context.Set<Estado>().FirstOrDefaultAsync(e => e.Codigo == "ACTIVO" || e.DescEstado == "ACTIVO") 
                          ?? await _context.Set<Estado>().FirstOrDefaultAsync();
        
        var turno = new Turno
        {
            IdUsuario = userId,
            FechaApertura = DateTime.Now,
            MontoInicialNio = initialNio,
            MontoInicialUsd = initialUsd,
            TotalVentasNio = 0,
            TotalVentasUsd = 0,
            TotalEfectivoNio = 0,
            TotalEfectivoUsd = 0,
            TotalTarjeta = 0,
            TotalTransferencia = 0,
            IdEstado = activeState?.IdEstado ?? 1 // Fallback to 1 if not found
        };

        _context.Turnos.Add(turno);
        await _context.SaveChangesAsync();

        if (counts != null && counts.Any())
        {
            foreach (var count in counts)
            {
                count.IdTurno = turno.IdTurno;
                count.TipoConteo = "APERTURA";
                _context.ConteoDenominaciones.Add(count);
            }
            await _context.SaveChangesAsync();
        }

        return turno;
    }

    public async Task<List<Denominacione>> GetDenominationsAsync()
    {
        return await _context.Denominaciones
            .Include(d => d.IdMonedaNavigation)
            .OrderBy(d => d.IdMoneda)
            .ThenByDescending(d => d.Orden) // Denominations usually ordered by value descending
            .ToListAsync();
    }

    public async Task CloseShiftAsync(int turnoId, decimal finalNio, decimal finalUsd, List<ConteoDenominacione> counts, string? observations)
    {
        var turno = await _context.Turnos.FindAsync(turnoId);
        if (turno == null) return;

        var closedState = await _context.Set<Estado>().FirstOrDefaultAsync(e => e.Codigo == "CERRADO" || e.DescEstado == "CERRADO") 
                          ?? await _context.Set<Estado>().FirstOrDefaultAsync(e => e.IdEstado == 2);

        turno.FechaCierre = DateTime.Now;
        turno.MontoContadoNio = finalNio;
        turno.MontoContadoUsd = finalUsd;
        turno.Observaciones = observations;
        turno.IdEstado = closedState?.IdEstado ?? 2; // Fallback to 2
        
        // Calculate differences compared to system totals
        turno.DiferenciaNio = finalNio - (turno.MontoInicialNio + turno.TotalVentasNio);
        turno.DiferenciaUsd = finalUsd - (turno.MontoInicialUsd + turno.TotalVentasUsd);
        turno.EstadoCuadre = (turno.DiferenciaNio == 0 && turno.DiferenciaUsd == 0) ? "CUADRADO" : "DESCUADRE";

        // Save closing counts
        if (counts != null && counts.Any())
        {
            foreach (var count in counts)
            {
                count.IdTurno = turno.IdTurno;
                count.TipoConteo = "CIERRE";
                _context.ConteoDenominaciones.Add(count);
            }
        }

        await _context.SaveChangesAsync();
    }
}

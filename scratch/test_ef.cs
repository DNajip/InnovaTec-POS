using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using InnovaTecPOS.Backend.Models;
using Microsoft.Extensions.DependencyInjection;

public class TestEF
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddDbContext<InnovaTecDbContext>(options =>
            options.UseSqlServer("Data Source=.\\MSSQLSERVER22;Initial Catalog=InnovaTecBD;Integrated Security=True;TrustServerCertificate=True;"));
        
        var provider = services.BuildServiceProvider();
        using var context = provider.GetRequiredService<InnovaTecDbContext>();

        try
        {
            var start = new DateTime(2026, 5, 1);
            var end = new DateTime(2026, 7, 31);
            
            var query = context.Ventas
                .Include(v => v.IdPersonaNavigation)
                .Include(v => v.IdUsuarioNavigation)
                .Include(v => v.VentaDetalles)
                    .ThenInclude(d => d.VentaDetalleImeis)
                .Where(v => v.FechaVenta >= start && v.FechaVenta <= end)
                .OrderByDescending(v => v.FechaVenta)
                .ToList();
                
            Console.WriteLine($"SUCCESS! Found {query.Count} ventas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null) {
                Console.WriteLine($"INNER: {ex.InnerException.Message}");
            }
        }
    }
}

using InnovaTecPOS.Backend.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InnovaTecPOS.Backend.Services;

public interface IAuthService
{
    Task<LoginResponse> IniciarSesionAsync(string username, string password);
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? UserId { get; set; }
}

public class AuthService : IAuthService
{
    private readonly InnovaTecDbContext _context;

    public AuthService(InnovaTecDbContext context)
    {
        _context = context;
    }

    public async Task<LoginResponse> IniciarSesionAsync(string username, string password)
    {
        var successParam = new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output };
        var messageParam = new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output };
        var userIdParam = new SqlParameter("@UserId", SqlDbType.Int) { Direction = ParameterDirection.Output };

        // Aunque el SP devuelve un SELECT en mi implementación previa, 
        // para mayor robustez en .NET suelo usar parámetros de salida o mapear el resultado del SELECT.
        // Dado que el SP anterior hace un SELECT, usaremos SqlQuery o FromSqlRaw.
        
        try 
        {
            var result = await _context.Database
                .SqlQueryRaw<LoginResponse>("EXEC ADM.sp_IniciarSesion @Username={0}, @Password={1}", username, password)
                .ToListAsync();

            return result.FirstOrDefault() ?? new LoginResponse { Success = false, Message = "Error inesperado en el servidor" };
        }
        catch (Exception ex)
        {
            return new LoginResponse { Success = false, Message = $"Error de conexión: {ex.Message}" };
        }
    }
}

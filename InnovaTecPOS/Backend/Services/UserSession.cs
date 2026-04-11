namespace InnovaTecPOS.Backend.Services;

public class UserSession
{
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? NombreCompleto { get; set; }
    public string? Rol { get; set; }
    
    // Metadata for current operation
    public string? CurrentObservation { get; set; }

    public bool IsAuthenticated => UserId.HasValue;

    public void Clear()
    {
        UserId = null;
        Username = null;
        NombreCompleto = null;
        Rol = null;
        CurrentObservation = null;
    }
}

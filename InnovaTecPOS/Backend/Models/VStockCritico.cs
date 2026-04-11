using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public partial class VStockCritico
{
    public int IdProducto { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public string EstadoStock { get; set; } = null!;

    public string Categoria { get; set; } = null!;

    public int? ImeiDisponibles { get; set; }
}

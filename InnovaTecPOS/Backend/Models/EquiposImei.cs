using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public partial class EquiposImei
{
    public int IdImei { get; set; }

    public int IdProducto { get; set; }

    public string Imei { get; set; } = null!;

    public string EstadoImei { get; set; } = null!;

    public DateTime FechaIngreso { get; set; }

    public int? IngresadoPor { get; set; }

    public string? Observacion { get; set; }

    public virtual ICollection<Garantia> Garantia { get; set; } = new List<Garantia>();

    public virtual Producto IdProductoNavigation { get; set; } = null!;

    public virtual Usuario? IngresadoPorNavigation { get; set; }

    public virtual VentaDetalleImei? VentaDetalleImei { get; set; }
}

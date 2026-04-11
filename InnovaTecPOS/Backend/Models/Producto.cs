using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public partial class Producto
{
    public int IdProducto { get; set; }

    public string? CodigoBarras { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public string? Almacenamiento { get; set; }

    public string? Color { get; set; }

    public int? IdCategoria { get; set; }

    public string TipoProducto { get; set; } = "ARTICULO";

    public decimal? PrecioCompra { get; set; }

    public decimal PrecioVenta { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }

    public int? CreadoPor { get; set; }

    public string EstadoStock { get; set; } = null!;

    public virtual Usuario? CreadoPorNavigation { get; set; }

    public virtual ICollection<EquiposImei> EquiposImeis { get; set; } = new List<EquiposImei>();

    public virtual ICollection<Garantia> Garantia { get; set; } = new List<Garantia>();

    public virtual Categoria? IdCategoriaNavigation { get; set; }

    public virtual ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();

    public virtual ICollection<ReclamosGarantium> ReclamosGarantia { get; set; } = new List<ReclamosGarantium>();

    public virtual ICollection<VentaDetalle> VentaDetalles { get; set; } = new List<VentaDetalle>();
}

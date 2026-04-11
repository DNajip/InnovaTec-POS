using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public partial class VentaDetalleImei
{
    public int Id { get; set; }

    public int IdDetalle { get; set; }

    public int IdEquipoImei { get; set; }

    public string ImeiSnap { get; set; } = null!;

    public virtual VentaDetalle IdDetalleNavigation { get; set; } = null!;

    public virtual EquiposImei IdEquipoImeiNavigation { get; set; } = null!;
}

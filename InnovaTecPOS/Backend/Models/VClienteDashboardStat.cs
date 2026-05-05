using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Models;

public partial class VClienteDashboardStat
{
    public int TotalClientes { get; set; }

    public int TotalGarantiasActivas { get; set; }

    public int ClientesConComprasRecientes { get; set; }
}

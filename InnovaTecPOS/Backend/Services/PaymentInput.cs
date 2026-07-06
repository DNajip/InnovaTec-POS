using System;
using System.Collections.Generic;

namespace InnovaTecPOS.Backend.Services;

public class PaymentInput
{
    public int IdMetodoPago { get; set; }
    public string MetodoNombre { get; set; } = "";
    public string SimboloMoneda { get; set; } = "C$";
    public decimal Monto { get; set; } // Monto en la moneda original (ej. $100)
    public decimal TasaCambio { get; set; }
    public decimal MontoEnNio => Monto * TasaCambio;
    public string? Referencia { get; set; }
}

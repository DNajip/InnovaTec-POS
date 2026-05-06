using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InnovaTecPOS.Backend.Services;

public class AppState
{
    public event Action? OnChange;

    private string _businessName = "InnovaTec POS";
    public string BusinessName
    {
        get => _businessName;
        set
        {
            if (_businessName != value)
            {
                _businessName = value;
                NotifyStateChanged();
            }
        }
    }

    private string _businessLogo = "images/logo.png";
    public string BusinessLogo
    {
        get => _businessLogo;
        set
        {
            if (_businessLogo != value)
            {
                _businessLogo = value;
                NotifyStateChanged();
            }
        }
    }

    private string _businessRuc = "";
    public string BusinessRuc
    {
        get => _businessRuc;
        set
        {
            if (_businessRuc != value)
            {
                _businessRuc = value;
                NotifyStateChanged();
            }
        }
    }

    private string _businessPhone = "";
    public string BusinessPhone
    {
        get => _businessPhone;
        set
        {
            if (_businessPhone != value)
            {
                _businessPhone = value;
                NotifyStateChanged();
            }
        }
    }

    private string _businessAddress = "";
    public string BusinessAddress
    {
        get => _businessAddress;
        set
        {
            if (_businessAddress != value)
            {
                _businessAddress = value;
                NotifyStateChanged();
            }
        }
    }

    private string _ticketMessage = "¡Gracias por su compra!";
    public string TicketMessage
    {
        get => _ticketMessage;
        set
        {
            if (_ticketMessage != value)
            {
                _ticketMessage = value;
                NotifyStateChanged();
            }
        }
    }

    public void UpdateFromDictionary(Dictionary<string, string> settings)
    {
        if (settings.TryGetValue("Empresa_Nombre", out var name)) BusinessName = name;
        if (settings.TryGetValue("Empresa_Logo", out var logo)) BusinessLogo = logo;
        if (settings.TryGetValue("Empresa_RUC", out var ruc)) BusinessRuc = ruc;
        if (settings.TryGetValue("Empresa_Telefono", out var phone)) BusinessPhone = phone;
        if (settings.TryGetValue("Empresa_Direccion", out var address)) BusinessAddress = address;
        if (settings.TryGetValue("Ventas_MensajeTicket", out var msg)) TicketMessage = msg;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

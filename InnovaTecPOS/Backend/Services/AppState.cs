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

    private string _printerName = "";
    public string PrinterName
    {
        get => _printerName;
        set
        {
            if (_printerName != value)
            {
                _printerName = value;
                NotifyStateChanged();
            }
        }
    }

    private bool _openCashDrawer = true;
    public bool OpenCashDrawer
    {
        get => _openCashDrawer;
        set
        {
            if (_openCashDrawer != value)
            {
                _openCashDrawer = value;
                NotifyStateChanged();
            }
        }
    }

    private DateTime _reportStartDate = DateTime.Today;
    public DateTime ReportStartDate
    {
        get => _reportStartDate;
        set
        {
            if (_reportStartDate != value)
            {
                _reportStartDate = value;
                NotifyStateChanged();
            }
        }
    }

    private DateTime _reportEndDate = DateTime.Today;
    public DateTime ReportEndDate
    {
        get => _reportEndDate;
        set
        {
            if (_reportEndDate != value)
            {
                _reportEndDate = value;
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
        if (settings.TryGetValue("Hardware_Impresora", out var printer)) PrinterName = printer;
        if (settings.TryGetValue("Hardware_AbrirCajon", out var openDrawer)) OpenCashDrawer = openDrawer == "SI";
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

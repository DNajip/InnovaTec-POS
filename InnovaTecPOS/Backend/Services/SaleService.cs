using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InnovaTecPOS.Backend.Services
{
    public class SaleService : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set
            {
                if (_discount != value)
                {
                    _discount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public List<CartItem> Items { get; } = new();

        public int TotalUnits => Items.Sum(i => i.Quantity);
        public decimal SubTotal => Items.Sum(i => i.SubTotal);
        public decimal Total => Math.Max(0, SubTotal - Discount);

        public void AddItem(CartItem item)
        {
            Items.Add(item);
            NotifyAll();
        }

        public void RemoveItem(CartItem item)
        {
            Items.Remove(item);
            NotifyAll();
        }

        public void NotifyAll()
        {
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalUnits));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(Total));
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CartItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int IdProducto { get; set; }
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal UnitPrice { get; set; }
        
        // Properties for IMEI handling
        public bool RequiresImei { get; set; }
        public string? SelectedImei { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SubTotal));
                }
            }
        }

        public decimal SubTotal => UnitPrice * Quantity;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

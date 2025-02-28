using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrawingRegister.App.Models
{
    public class DistributionCompanyViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private readonly DistributionCompany _company;
        
        public DistributionCompanyViewModel(DistributionCompany company, bool isSelected = false)
        {
            _company = company;
            _isSelected = isSelected;
        }
        
        public string Id => _company.Id;
        
        public string Name => _company.Name;
        
        public string Category => _company.Category;
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 
using System.Collections.ObjectModel;
using System.Windows;

namespace DrawingRegister.App
{
    public partial class FolderProgressWindow : Window
    {
        public ObservableCollection<FolderStatusViewModel> FolderStatuses { get; } = new();

        public FolderProgressWindow()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
} 
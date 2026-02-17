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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
    }
} 
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DrawingRegister.App
{
    public class FolderStatusViewModel : INotifyPropertyChanged
    {
        private string _folderName = string.Empty;
        private string _status = string.Empty;
        private string _statusIcon = string.Empty;
        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Black;

        public string FolderName
        {
            get => _folderName;
            set { _folderName = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); UpdateIconAndColor(); }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            private set { _statusIcon = value; OnPropertyChanged(); }
        }

        public System.Windows.Media.Brush StatusColor
        {
            get => _statusColor;
            private set { _statusColor = value; OnPropertyChanged(); }
        }

        public FolderStatusViewModel(string folderName, string status)
        {
            _folderName = folderName;
            Status = status;
        }

        private void UpdateIconAndColor()
        {
            switch (Status)
            {
                case "Processed":
                    StatusIcon = "✔";
                    StatusColor = System.Windows.Media.Brushes.Green;
                    break;
                case "Skipped":
                    StatusIcon = "⏩";
                    StatusColor = System.Windows.Media.Brushes.Orange;
                    break;
                case "Error":
                    StatusIcon = "❌";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    break;
                default:
                    StatusIcon = "";
                    StatusColor = System.Windows.Media.Brushes.Black;
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 
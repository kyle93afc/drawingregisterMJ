using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DrawingRegisterMJ.Models;
using DrawingRegisterMJ.Services;
using Microsoft.Win32;

namespace DrawingRegisterMJ.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly FileProcessingService _fileProcessingService;
        private ObservableCollection<Drawing> _drawings;
        private string _discipline;
        private string _regNo;
        private string _projectNo;
        private string _projectName;
        private string _client;
        private string _architect;
        private string _localAuthority;
        private string _sfs;
        private string _selectedPurposeOfIssue;
        private string _selectedMethodOfIssue;

        public ICommand SelectFolderCommand { get; }
        public ICommand RefreshCommand { get; }

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _fileProcessingService = new FileProcessingService(_databaseService);
            
            SelectFolderCommand = new RelayCommand(_ => SelectFolder());
            RefreshCommand = new RelayCommand(_ => LoadDrawings());

            LoadDrawings();

            // Initialize with default values from the image
            Discipline = "Architecture";
            RegNo = "240378-M+J-00-XX-RE-A-00-01";
            ProjectNo = "240378";
            ProjectName = "JOHNSTONS OF ELGIN, NEWMILL, ELGIN, IV30 4AF";
            Client = "JOHNSTONS OF ELGIN";
            LocalAuthority = "MORAY COUNCIL";
            SFS = "CHRIS";
        }

        private void SelectFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Project Root Folder"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _fileProcessingService.ProcessDirectory(dialog.SelectedPath);
                LoadDrawings();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<Drawing> Drawings
        {
            get => _drawings;
            set
            {
                _drawings = value;
                OnPropertyChanged();
            }
        }

        public string Discipline
        {
            get => _discipline;
            set
            {
                _discipline = value;
                OnPropertyChanged();
            }
        }

        public string RegNo
        {
            get => _regNo;
            set
            {
                _regNo = value;
                OnPropertyChanged();
            }
        }

        public string ProjectNo
        {
            get => _projectNo;
            set
            {
                _projectNo = value;
                OnPropertyChanged();
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                _projectName = value;
                OnPropertyChanged();
            }
        }

        public string Client
        {
            get => _client;
            set
            {
                _client = value;
                OnPropertyChanged();
            }
        }

        public string Architect
        {
            get => _architect;
            set
            {
                _architect = value;
                OnPropertyChanged();
            }
        }

        public string LocalAuthority
        {
            get => _localAuthority;
            set
            {
                _localAuthority = value;
                OnPropertyChanged();
            }
        }

        public string SFS
        {
            get => _sfs;
            set
            {
                _sfs = value;
                OnPropertyChanged();
            }
        }

        public string SelectedPurposeOfIssue
        {
            get => _selectedPurposeOfIssue;
            set
            {
                _selectedPurposeOfIssue = value;
                OnPropertyChanged();
            }
        }

        public string SelectedMethodOfIssue
        {
            get => _selectedMethodOfIssue;
            set
            {
                _selectedMethodOfIssue = value;
                OnPropertyChanged();
            }
        }

        private void LoadDrawings()
        {
            var drawingsList = _databaseService.GetAllDrawings();
            Drawings = new ObservableCollection<Drawing>(drawingsList);
        }
    }
} 
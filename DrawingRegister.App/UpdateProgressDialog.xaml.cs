using System.Windows;

namespace DrawingRegister.App;

/// <summary>
/// Dialog that shows download progress during application updates.
/// </summary>
public partial class UpdateProgressDialog : Window
{
    public UpdateProgressDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the progress bar and text with the current download percentage.
    /// </summary>
    /// <param name="percentage">The download progress (0-100).</param>
    public void UpdateProgress(int percentage)
    {
        ProgressBar.Value = percentage;
        ProgressText.Text = $"{percentage}%";
    }

    private void Header_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }
}

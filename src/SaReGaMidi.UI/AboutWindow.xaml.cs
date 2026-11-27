using System.Windows;

namespace SaReGaMidi.UI;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)}";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();
}

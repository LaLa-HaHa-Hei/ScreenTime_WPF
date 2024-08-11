using System.Windows;

namespace ScreenTime
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void RestoreDefaultSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            GetTopWindowInterval_sTextBox.Text = DefaultSettings.Default.GetTopWindowInterval_s.ToString();

            HideWhenStartTextBox.Text = DefaultSettings.Default.HideWhenStart.ToString();
            RefreshListBoxInterval_sTextBox.Text = DefaultSettings.Default.RefreshListBoxInterval_s.ToString();

            UserDataDirectionTextBox.Text = DefaultSettings.Default.UserDataDirectory;
        }
        private void LoadSettings()
        {
            GetTopWindowInterval_sTextBox.Text = Settings.Default.GetTopWindowInterval_s.ToString();

            HideWhenStartTextBox.Text = Settings.Default.HideWhenStart.ToString();
            RefreshListBoxInterval_sTextBox.Text = Settings.Default.RefreshListBoxInterval_s.ToString();

            UserDataDirectionTextBox.Text = Settings.Default.UserDataDirectory;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveAndExitButton_Click(object sender, RoutedEventArgs e)
        {
            //如果修改前后值相同，不会触发Settings_PropertyChanged
            Settings.Default.GetTopWindowInterval_s = Int32.Parse(GetTopWindowInterval_sTextBox.Text);

            switch (HideWhenStartTextBox.Text)
            {
                case "True":
                case "true":
                case "TRUE":
                    Settings.Default.HideWhenStart = true;
                    break;
                case "False":
                case "false":
                case "FALSE":
                    Settings.Default.HideWhenStart = false;
                    break;
            }
            Settings.Default.RefreshListBoxInterval_s = Int32.Parse(RefreshListBoxInterval_sTextBox.Text);

            Settings.Default.UserDataDirectory = UserDataDirectionTextBox.Text;
            Settings.Default.Save();
            Close();
        }
    }
}

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
            ExeIconFolderPathTextBox.Text = DefaultSettings.Default.ExeIconFolderPath;

            ScreenshotTextBox.Text = DefaultSettings.Default.Screenshot.ToString();
            ScreenshotInterval_sTextBox.Text = DefaultSettings.Default.ScreenshotInterval_s.ToString();
            JpegQualityTextBox.Text = DefaultSettings.Default.JpegQuality.ToString();
            ScreenshotFolderPathTextBox.Text = DefaultSettings.Default.ScreenshotFolderPath;

            HideWhenStartTextBox.Text = DefaultSettings.Default.HideWhenStart.ToString();
            RefreshListBoxInterval_sTextBox.Text = DefaultSettings.Default.RefreshListBoxInterval_s.ToString();

            JsonDataFolderPathTextBox.Text = DefaultSettings.Default.JsonDataFolderPath;
        }
        private void LoadSettings()
        {
            GetTopWindowInterval_sTextBox.Text = Settings.Default.GetTopWindowInterval_s.ToString();
            ExeIconFolderPathTextBox.Text = Settings.Default.ExeIconFolderPath;
            //截屏
            ScreenshotTextBox.Text = Settings.Default.Screenshot.ToString();
            ScreenshotInterval_sTextBox.Text = Settings.Default.ScreenshotInterval_s.ToString();
            JpegQualityTextBox.Text = Settings.Default.JpegQuality.ToString();
            ScreenshotFolderPathTextBox.Text = Settings.Default.ScreenshotFolderPath;

            HideWhenStartTextBox.Text = Settings.Default.HideWhenStart.ToString();
            RefreshListBoxInterval_sTextBox.Text = Settings.Default.RefreshListBoxInterval_s.ToString();

            JsonDataFolderPathTextBox.Text = Settings.Default.JsonDataFolderPath;
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
            Settings.Default.ExeIconFolderPath = ExeIconFolderPathTextBox.Text;
            //截屏
            switch (ScreenshotTextBox.Text)
            {
                case "True":
                case "true":
                case "TRUE":
                    Settings.Default.Screenshot = true;
                    break;
                case "False":
                case "false":
                case "FALSE":
                    Settings.Default.Screenshot = false;
                    break;
            }
            Settings.Default.ScreenshotInterval_s = Int32.Parse(ScreenshotInterval_sTextBox.Text);
            Settings.Default.JpegQuality = Int32.Parse(JpegQualityTextBox.Text);
            Settings.Default.ScreenshotFolderPath = ScreenshotFolderPathTextBox.Text;

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

            Settings.Default.JsonDataFolderPath = JsonDataFolderPathTextBox.Text;
            Settings.Default.Save();
            Close();
        }
    }
}

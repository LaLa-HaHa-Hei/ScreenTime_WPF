using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace ScreenTime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        private int _totalSecond = 0;
        private SortDescription _sortDescription; //ListBox排序
        private DateTime _today = DateTime.Now; // 记录开始运行时的日期，在新一天后重启程序
        //各种路径
        //System.Windows.Forms.Application.ExecutablePath
        private readonly string _appDirectory = AppDomain.CurrentDomain.BaseDirectory; //程序所在文件夹的路径，后面有"\"
        private readonly string _exePath= Assembly.GetExecutingAssembly().Location;
        private readonly string _exeIconFolder = "exe_icon";
        private readonly string _jsonDataFolder = "history";
        private string _absluteExeIconFolderPath;
        private string _absluteJsonDataFolderPath;
        private string _absluteJsonDataPath;
        //定时器
        private readonly AccurateDispatcherTimer _getTopWindowTimer = new();
        private readonly DispatcherTimer _refreshListBoxTimer = new();
        //托盘
        private readonly System.Windows.Forms.NotifyIcon TrayNotifyIcon;
        private readonly ContextMenuStrip TrayContextMenuStrip;
        //数据
        private ObservableCollection<ExeItemInfo> ExeItemList { get; set; } //所有有焦点的窗口对应的exe

        public MainWindow()
        {
            InitializeComponent();
            //处理文件路径
            _absluteExeIconFolderPath = Path.Combine(_appDirectory, _exeIconFolder);
            _absluteJsonDataFolderPath = Path.Combine(_appDirectory, _jsonDataFolder);
            _absluteJsonDataPath = Path.Combine(_absluteJsonDataFolderPath, _today.ToString("yyyy-MM-dd") + ".json");
            //创建文件夹
            if (!Directory.Exists(_absluteExeIconFolderPath))
                Directory.CreateDirectory(_absluteExeIconFolderPath);
            if (!Directory.Exists(_absluteJsonDataFolderPath))
                Directory.CreateDirectory(_absluteJsonDataFolderPath);
            
            Settings.Default.PropertyChanged += Settings_PropertyChanged;//配置被改变时执行
            _sortDescription = new SortDescription("Seconds", ListSortDirection.Descending);
            //加载数据,绑定列表
            var data = LoadData(_absluteJsonDataPath);
            if (data != null)
            {
                ExeItemList = data;
                for (int i = 0; i < ExeItemList.Count; i++)
                    _totalSecond += ExeItemList[i].Seconds;
            }
            else
            {
                ExeItemList = [];
            }
            TimeListBox.ItemsSource = ExeItemList;
            //计时器
            _getTopWindowTimer.Tick += (sender, e) => { GetTopwindow(); } ;
            _getTopWindowTimer.Interval_ms = Settings.Default.GetTopWindowInterval_s * 1000;
            _getTopWindowTimer.Start();
            _refreshListBoxTimer.Interval = TimeSpan.FromSeconds(Settings.Default.RefreshListBoxInterval_s);
            _refreshListBoxTimer.Tick += (sender, e) => { RefreshListBox(); };
            //托盘
            TrayNotifyIcon = new NotifyIcon();
            TrayContextMenuStrip = new();
            InitializeTray();
        }
        private void CheckDate() //检查如果发现是新的一天，就重启程序
        {
            if (DateTime.Now.Date > _today.Date)
            {
                SaveData(_absluteJsonDataPath);
                Process.Start(System.Windows.Forms.Application.ExecutablePath);
                TrayNotifyIcon.Visible = false;
                ExitApp();
            }
        }
        private void RefreshListBox()
        {
            TotalTimeTextBlock.Text = "总时间：" + ExeItemInfo.SecondToTime(_totalSecond);
            if (_totalSecond == 0) { return; } // 防止0做除数
            foreach (ExeItemInfo item in ExeItemList)
            {
                item.Percentage = item.Seconds * 100 / _totalSecond;
                item.TimeText = ExeItemInfo.SecondToTime(item.Seconds);
            }
            TimeListBox.Items.SortDescriptions.Add(_sortDescription);
        }
        // 获取当前焦点窗口并记录时间
        private void GetTopwindow()
        {
            CheckDate();
            _totalSecond += Settings.Default.GetTopWindowInterval_s;
            IntPtr activeWindowHandle = GetForegroundWindow();
            _ = GetWindowThreadProcessId(activeWindowHandle, out int pid);
            if (pid == IntPtr.Zero) { return; }
            // 获取程序路径
            string? filePath;
            try
            {
                filePath = Process.GetProcessById(pid).MainModule?.FileName.ToLower();//全变成小写，不然大小写不同字符串会不一样
            }
            catch (System.ComponentModel.Win32Exception) { return; }
            if (filePath == null) { return; }
            //查找已有，是否已经使用过该程序
            var item = ExeItemList.Where(x => x.ExePath == filePath).FirstOrDefault();
            if (item != null)
            {
                item.Seconds += Settings.Default.GetTopWindowInterval_s;
            }
            else
            {
                //获取程序图标
                string iconPath = $"{_absluteExeIconFolderPath}\\{filePath.Replace('\\', '$').Replace(":", "")}.png";
                if (!File.Exists(iconPath)) // 不存在图标则获取
                {
                    Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                    if (icon != null)
                    {
                        using Bitmap bitmap = icon.ToBitmap();
                        bitmap.Save(iconPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    else
                    {
                        iconPath = "pack://application:,,,/ScreenTime;component/img/unknowfile.png";
                    }
                }
                ExeItemInfo exeItem = new()
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    ExePath = filePath,
                    IconPath = iconPath,
                    Seconds = Settings.Default.GetTopWindowInterval_s,
                    TimeText = ExeItemInfo.SecondToTime(Settings.Default.GetTopWindowInterval_s),
                    Percentage = Settings.Default.GetTopWindowInterval_s * 100 / (_totalSecond != 0 ? _totalSecond : Settings.Default.GetTopWindowInterval_s)
                };
                ExeItemList.Add(exeItem);
            }
        }
        public static void SaveBitmapAsJpeg(BitmapSource bitmap, string filePath, int quality)
        {
            JpegBitmapEncoder jpegEncoder = new()
            {
                QualityLevel = quality,
            };
            jpegEncoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream fs = new(filePath, FileMode.Create);
            jpegEncoder.Save(fs);
        }

        // 配置被改变时
        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "GetTopWindowInterval_s":
                    _getTopWindowTimer.Interval_ms = Settings.Default.GetTopWindowInterval_s * 1000;
                    break;
                case "RefreshListBoxInterval_s":
                    _refreshListBoxTimer.Interval = TimeSpan.FromSeconds(Settings.Default.RefreshListBoxInterval_s);
                    break;
            }
        }
        /// <summary>
        /// 将总秒数转换为“x小时x分钟x秒”的形式
        /// </summary>
        public void SessionEnding() //关机时通过app.xaml.cs执行
        {
            SaveData(_absluteJsonDataPath);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            HideWindow();
            e.Cancel = true;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _refreshListBoxTimer.Start();
            if (Settings.Default.HideWhenStart)
            {
                HideWindow();
            }
        }
        private void ShowWindow()
        {
            Show();
            Activate();
            RefreshListBox();
            _refreshListBoxTimer.Start();
        }
        private void HideWindow()
        {
            Hide();
            SaveData(_absluteJsonDataPath);
            _refreshListBoxTimer.Stop();
        }
        private void ExitApp()
        {
            SaveData(_absluteJsonDataPath);
            TrayNotifyIcon.Visible = false;
            App.Current.Shutdown();
        }
        private void InitializeTray()
        {
            //初始化图标，如果找不到，就用纯色代替
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream? stream = assembly.GetManifestResourceStream("ScreenTime.img.32.ico");
            if (stream != null) { TrayNotifyIcon.Icon = new Icon(stream); }
            else
            {
                Bitmap bitmap = new(16, 16);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    SolidBrush blueBrush = new(System.Drawing.Color.FromArgb(255, 236, 161));
                    graphics.FillRectangle(blueBrush, 0, 0, 16, 16);
                }
                TrayNotifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                bitmap.Dispose();
            }
            // 鼠标左键单击托盘图标
            TrayNotifyIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (Visibility == Visibility.Visible)
                    {
                        HideWindow();
                    }
                    else
                    {
                        ShowWindow();
                    }
                }
            };

            ToolStripMenuItem selfStartingItem = new("开机自启动");
            selfStartingItem.Font = new Font(selfStartingItem.Font.FontFamily.Name, 9F);
            selfStartingItem.CheckOnClick = true;
            //检测是否已经开机自启动
            RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (runKey?.GetValue("ScreenTime") != null)
            { selfStartingItem.Checked = true; }
            selfStartingItem.Click += (sender, arg) =>
            {
                RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                //if (runKey == null) { System.Windows.MessageBox.Show("失败，无法获取开机自启动注册表", "错误"); return; }
                if (selfStartingItem.Checked == false) // 按下后已经改变了打钩状态，现在没钩说明按之前有钩
                {
                    runKey?.DeleteValue("ScreenTime", false);
                }
                else
                {
                    runKey?.SetValue("ScreenTime", _exePath);
                }
            };
            TrayContextMenuStrip.Items.Add(selfStartingItem);

            ToolStripMenuItem openInstallationDirectoryItem = new("安装目录");
            openInstallationDirectoryItem.Font = new Font(openInstallationDirectoryItem.Font.FontFamily.Name, 9F);
            openInstallationDirectoryItem.Click += (sender, arg) =>
            {
                Process.Start("explorer.exe", $"/select,{_exePath}");
            };
            TrayContextMenuStrip.Items.Add(openInstallationDirectoryItem);

            ToolStripMenuItem exitItem = new("退出");
            exitItem.Font = new Font(exitItem.Font.FontFamily.Name, 9F);
            exitItem.Click += (sender, arg) =>
            {
                TrayNotifyIcon.Visible = false;
                ExitApp();
            };
            TrayContextMenuStrip.Items.Add(exitItem);

            TrayNotifyIcon.ContextMenuStrip = TrayContextMenuStrip;
            TrayNotifyIcon.ContextMenuStrip.Opening += ContextMenuStripOnOpening;

            TrayNotifyIcon.Visible = true;
        }
        private void ContextMenuStripOnOpening(object? sender, CancelEventArgs cancelEventArgs)
        {
            System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
            p.Y -= TrayNotifyIcon?.ContextMenuStrip?.Height ?? 27 * 5;
            TrayNotifyIcon?.ContextMenuStrip?.Show(p);
        }
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow w = new()
            {
                Owner = this
            };
            w.Show();
        }

        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
        public void SaveData(string filePath)
        {
            List<ExeItemInfo> listdata = ExeItemList.OrderByDescending(item => item.Seconds).ToList();
            string jsonString = JsonSerializer.Serialize(listdata, _options);
            File.WriteAllText(filePath, jsonString);
        }
        public static ObservableCollection<ExeItemInfo>? LoadData(string filePath)
        {
            if (File.Exists(filePath))
            {
                string jsonString = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<ObservableCollection<ExeItemInfo>>(jsonString);
            }
            else
                return null;
        }
        private void ViewHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Title = "选择查看的历史",
                Filter = "JSON files (*.json)|*.json", // 只显示.json文件
                InitialDirectory = _absluteJsonDataFolderPath // 设置初始文件夹
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                ViewHistoryWindow w = new(selectedFilePath); //里面决定是否show
                w.Show();
            }
        }

        private void OpenUserDataDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", Settings.Default.UserDataDirectory);
        }

        private void OpenAboutWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow w = new();
            {
                Owner = this;
            }
            w.Show();
        }
    }
    public class ExeItemInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        private string _exePath = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string IconPath { get; set; } = String.Empty;
        private int _percentage = 0;
        public int Percentage
        {
            get => _percentage;
            set
            {
                _percentage = value;
                OnPropertyChanged(nameof(Percentage));
            }
        }
        private string _timeText = string.Empty;
        public string TimeText
        {
            get => _timeText;
            set
            {
                _timeText = value;
                OnPropertyChanged(nameof(TimeText));
            }
        }
        public int Seconds { get; set; } = 0;
        protected internal virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public static string SecondToTime(int second)
        {
            string time = "";
            int hour = second / 3600;
            int minute = second % 3600 / 60;
            if (hour != 0) { time = hour.ToString() + "小时"; }
            if (minute != 0) { time = time + minute.ToString() + "分钟"; }
            time = time + (second % 60).ToString() + "秒";
            return time;
        }
    }
    /// <summary>
    /// 自带校准的DispatcherTimer
    /// </summary>
    class AccurateDispatcherTimer
    {
        public AccurateDispatcherTimer()
        {
            _dispatcherTimer = new();
            _dispatcherTimer.Tick += Timer_Tick;
            //系统睡眠事件
            SystemEvents.PowerModeChanged += PowerModeChangedEventHandler;
            IsRunning = false;
        }

        private readonly System.Windows.Threading.DispatcherTimer _dispatcherTimer;
        private DateTime _beginTime;
        private long _times = 0;
        public event EventHandler? Tick;
        public bool IsRunning { get; private set; }
        private int _interval_ms;
        public int Interval_ms
        {
            get => _interval_ms;
            set
            {
                if (_interval_ms != value)
                {
                    _interval_ms = value;
                    _beginTime = DateTime.Now;
                    _times = 0;
                }
            }
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            Tick?.Invoke(this, EventArgs.Empty);
            // 补偿误差
            _times++;
            TimeSpan timeSpan = DateTime.Now - _beginTime;
            double timerInterval = Interval_ms * (_times + 1) - timeSpan.TotalMilliseconds;
            //Debug.WriteLine(timerInterval.ToString());
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(timerInterval > 0 ? timerInterval : 1);
        }
        public void Start()
        {
            IsRunning = true;
            _beginTime = DateTime.Now;
            _times = 0;
            _dispatcherTimer.Start();
        }
        private void PowerModeChangedEventHandler(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                //睡眠恢复后从_begingTime开始计算的理论等待时间就错了
                case PowerModes.Resume: //1.操作系统即将从挂起状态继续。
                    if (IsRunning == true)
                    {
                        _beginTime = DateTime.Now;
                        _times = 0;
                        Start();
                    }
                    break;
                //case PowerModes.StatusChange: //2.一个电源模式状态的通知事件已由操作系统引发。 这可能指示电池电力不足或正在充电、电源正由交流电转换为电池或相反，或系统电源状态的其他更改。
                //    break;
                case PowerModes.Suspend: //3.操作系统即将挂起。
                    Pause();
                    break;
            }
        }
        public void Pause()
        {
            _dispatcherTimer.Stop();
        }
    }
}
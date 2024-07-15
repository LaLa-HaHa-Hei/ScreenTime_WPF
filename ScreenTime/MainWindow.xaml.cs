using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Threading;

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
        private SortDescription _sortDescription; //按照使用时间多到少给窗口ListBox排序
        private readonly DateTime _today = DateTime.Now; // 记录开始运行时的日期，在新一天后重启程序
        private readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory; //程序所在文件夹的路径，后面有"\"
        private readonly string _executablePath = System.Windows.Forms.Application.ExecutablePath;//自己exe的完整路径包括exe，也可是Process.GetCurrentProcess().MainModule?.FileName
        private string _absluteExeIconFolderPath;
        private string _absluteScreenshotFolderPath;
        //定时器
        private readonly AccurateDispatcherTimer _getTopWindowTimer = new();
        private readonly DispatcherTimer _screenshotTimer = new();
        private readonly DispatcherTimer _refreshListBoxTimer = new();
        //截屏
        private readonly EncoderParameters _encoderParams = new(1);// 创建Encoder参数对象来指定JPEG的质量
        private readonly ImageCodecInfo _jpegCodec = GetEncoderInfo("image/jpeg");// 获取JPEG编码器信息
        //托盘
        private readonly System.Windows.Forms.NotifyIcon TrayNotifyIcon;
        private readonly ContextMenuStrip TrayContextMenuStrip;
        private ObservableCollection<ExeItemInfo> ExeItemList { get; set; } = [];//所有有焦点的窗口对应的exe

        public MainWindow()
        {
            InitializeComponent();
            //截屏
            _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Settings.Default.JpegQuality); //quality是0-100之间的数值，100为最高质量，0为最低质量
            //处理Settings中的路径，如果是相对路径，则转为绝对路径
            Settings.Default.ScreenshotFolderPath = Settings.Default.ScreenshotFolderPath.Replace('/', '\\');
            if (Settings.Default.ScreenshotFolderPath[0] == '.' && Settings.Default.ScreenshotFolderPath[1] == '\\')
                _absluteScreenshotFolderPath = _baseDirectory + Settings.Default.ScreenshotFolderPath.Substring(2);
            else
                _absluteScreenshotFolderPath = Settings.Default.ScreenshotFolderPath;
            Settings.Default.ExeIconFolderPath = Settings.Default.ExeIconFolderPath.Replace('/', '\\');
            if (Settings.Default.ExeIconFolderPath[0] == '.' && Settings.Default.ExeIconFolderPath[1] == '\\')
                _absluteExeIconFolderPath = string.Concat(_baseDirectory, Settings.Default.ExeIconFolderPath.AsSpan(2));
            else
                _absluteExeIconFolderPath = Settings.Default.ScreenshotFolderPath;

            Settings.Default.ScreenshotFolderPath = Settings.Default.ScreenshotFolderPath.Replace('/', '\\');
            if (Settings.Default.ScreenshotFolderPath[0] == '.' && Settings.Default.ScreenshotFolderPath[1] == '\\')
                _absluteScreenshotFolderPath = string.Concat(_baseDirectory, Settings.Default.ScreenshotFolderPath.AsSpan(2));
            else
                _absluteScreenshotFolderPath = Settings.Default.ScreenshotFolderPath;
            Settings.Default.Save();//保存用 \ 替换掉 / 后的路径
            Settings.Default.PropertyChanged += Settings_PropertyChanged;//配置被改变时执行，一定在处理路径后
            //TimeListBox
            _sortDescription = new SortDescription("Seconds", ListSortDirection.Ascending);
            TimeListBox.ItemsSource = ExeItemList;
            //创建必要文件夹
            if (!Directory.Exists(_absluteExeIconFolderPath)) 
                Directory.CreateDirectory(_absluteExeIconFolderPath);
            if (!Directory.Exists(_absluteScreenshotFolderPath + "/" + _today.ToString("yyyy-MM-dd")))  
                Directory.CreateDirectory(_absluteScreenshotFolderPath + "/" + _today.ToString("yyyy-MM-dd")); 
            //计时器
            _getTopWindowTimer.Tick += TimerTick_GetTopwindow;
            _getTopWindowTimer.Interval_ms = Settings.Default.GetTopWindowInterval_s * 1000;
            _getTopWindowTimer.Start();
            _screenshotTimer.Tick += (sender, e) => { Screenshot(); };
            _screenshotTimer.Interval = TimeSpan.FromSeconds(Settings.Default.ScreenshotInterval_s);
            _screenshotTimer.Start();
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
                Process.Start(System.Windows.Forms.Application.ExecutablePath);
                TrayNotifyIcon.Visible = false;
                ExitApp();
            }
        }
        private void RefreshListBox()
        {
            TotalTimeTextBlock.Text = "总时间：" + SecondToTime(_totalSecond);
            if (_totalSecond == 0) { return; } // 防止0做除数
            foreach (ExeItemInfo item in ExeItemList)
            {
                item.Percentage = item.Seconds * 100 / _totalSecond;
                item.TimeText = SecondToTime(item.Seconds);
            }
            TimeListBox.Items.SortDescriptions.Add(_sortDescription);
        }
        // 获取当前焦点窗口并记录时间
        private void TimerTick_GetTopwindow(object? sender, EventArgs e)
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
                    ExePath = filePath,
                    IconPath = iconPath,
                    Seconds = Settings.Default.GetTopWindowInterval_s,
                };
                exeItem.TimeText = SecondToTime(exeItem.Seconds);
                exeItem.Percentage = exeItem.Seconds * 100 / (_totalSecond != 0 ? _totalSecond : exeItem.Seconds);
                ExeItemList.Add(exeItem);
            }
        }
        private void OpenInstallationDirectoryMenuItem_Click(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", $"/select,{_executablePath}");
        private void Screenshot()
        {
            if (Settings.Default.Screenshot == false) { return; }
            System.Drawing.Rectangle bounds = Screen.GetBounds(System.Drawing.Point.Empty);
            using Bitmap bitmap = new(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 将屏幕内容绘制到Bitmap上  
                g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);
            }
            bitmap.Save($"{_absluteScreenshotFolderPath}\\{_today:yyyy-MM-dd}\\{DateTime.Now:HH：mm}.jpeg", _jpegCodec, _encoderParams);
        }
        // 配置被改变时
        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "JpegQuality":
                    _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Settings.Default.JpegQuality);
                    break;
                case "GetTopWindowInterval_s":
                    _getTopWindowTimer.Interval_ms = Settings.Default.GetTopWindowInterval_s * 1000;
                    break;
                case "ScreenshotInterval_s":
                    _screenshotTimer.Interval = TimeSpan.FromSeconds(Settings.Default.ScreenshotInterval_s);
                    break;
                case "RefreshListBoxInterval_s":
                    _refreshListBoxTimer.Interval = TimeSpan.FromSeconds(Settings.Default.RefreshListBoxInterval_s);
                    break;
                case "ExeIconFolderPath":
                    Settings.Default.PropertyChanged -= Settings_PropertyChanged; //否则死循环
                    Settings.Default.ExeIconFolderPath = Settings.Default.ExeIconFolderPath.Replace('/', '\\');
                    Settings.Default.PropertyChanged += Settings_PropertyChanged;
                    //相对转绝对
                    if (Settings.Default.ExeIconFolderPath[0] == '.' && Settings.Default.ExeIconFolderPath[1] == '\\')
                        _absluteExeIconFolderPath = string.Concat(_baseDirectory, Settings.Default.ExeIconFolderPath.AsSpan(2));
                    else
                        _absluteExeIconFolderPath = Settings.Default.ExeIconFolderPath;
                    //创建文件夹
                    if (!Directory.Exists(_absluteExeIconFolderPath))
                        Directory.CreateDirectory(_absluteExeIconFolderPath);
                    break;
                case "ScreenshotFolderPath":
                    Settings.Default.PropertyChanged -= Settings_PropertyChanged;
                    Settings.Default.ScreenshotFolderPath = Settings.Default.ScreenshotFolderPath.Replace('/', '\\');
                    Settings.Default.PropertyChanged += Settings_PropertyChanged;
                    //相对转绝对
                    if (Settings.Default.ScreenshotFolderPath[0] == '.' && Settings.Default.ScreenshotFolderPath[1] == '\\')
                        _absluteScreenshotFolderPath = _baseDirectory + Settings.Default.ScreenshotFolderPath.Substring(2);
                    else
                        _absluteScreenshotFolderPath = Settings.Default.ScreenshotFolderPath;
                    //创建文件夹
                    if (!Directory.Exists(_absluteScreenshotFolderPath + "/" + _today.ToString("yyyy-MM-dd")))
                        Directory.CreateDirectory(_absluteScreenshotFolderPath + "/" + _today.ToString("yyyy-MM-dd"));
                    break;
            }
        }
        /// <summary>
        /// 将总秒数转换为“x小时x分钟x秒”的形式
        /// </summary>
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
            _refreshListBoxTimer.Start();
        }
        private void HideWindow()
        {
            Hide();
            _refreshListBoxTimer.Stop();
        }
        private void ExitApp()
        {
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
/*            //托盘菜单
            ToolStripMenuItem memoryRecyclingItem = new("强制回收内存");
            memoryRecyclingItem.Font = new Font(memoryRecyclingItem.Font.FontFamily.Name, 9F);
            memoryRecyclingItem.Click += (sender, arg) =>
            {
                GC.Collect();
            };
            TrayContextMenuStrip.Items.Add(memoryRecyclingItem);*/

            ToolStripMenuItem restartItem = new("重启程序");
            restartItem.Font = new Font(restartItem.Font.FontFamily.Name, 9F);
            restartItem.Click += (sender, arg) =>
            {
                Process.Start(_executablePath);
                TrayNotifyIcon.Visible = false;
                ExitApp();
            };
            TrayContextMenuStrip.Items.Add(restartItem);

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
                    runKey?.SetValue("ScreenTime", _executablePath);
                }
            };
            TrayContextMenuStrip.Items.Add(selfStartingItem);

            ToolStripMenuItem screenshotItem = new("截图");
            screenshotItem.Font = new Font(screenshotItem.Font.FontFamily.Name, 9F);
            screenshotItem.Checked = true;
            screenshotItem.CheckOnClick = true;
            screenshotItem.Click += (sender, arg) =>
            {
            };
            TrayContextMenuStrip.Items.Add(screenshotItem);

            ToolStripMenuItem openInstallationDirectoryItem = new("安装目录");
            openInstallationDirectoryItem.Font = new Font(openInstallationDirectoryItem.Font.FontFamily.Name, 9F);
            openInstallationDirectoryItem.Click += (sender, arg) =>
            {
                Process.Start("explorer.exe", $"/select,{_executablePath}");
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
        // 获取指定文件扩展名对应的编码器
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            throw new Exception($"No encoder for mime type: {mimeType}");
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow w = new()
            {
                Owner = this
            };
            w.Show();
        }
    }
    public class ExeItemInfo : INotifyPropertyChanged
    {
        public string Name { get; private set; } = string.Empty;
        private string _exePath = string.Empty;
        public string ExePath
        {
            get => _exePath;
            set
            {
                _exePath = value;
                Name = System.IO.Path.GetFileNameWithoutExtension(ExePath);
            }
        }
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
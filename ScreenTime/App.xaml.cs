using System;
using System.Windows;
using Microsoft.Win32;

namespace ScreenTime
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // 订阅系统关机事件
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            (Current.MainWindow as MainWindow)?.SessionEnding();
            // 取消关机
            // e.Cancel = true;
        }
        protected override void OnExit(ExitEventArgs e)
        {
            // 确保取消订阅事件
            SystemEvents.SessionEnding -= new SessionEndingEventHandler(SystemEvents_SessionEnding);
            base.OnExit(e);
        }
    }
}
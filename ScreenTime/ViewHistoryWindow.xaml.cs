using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace ScreenTime
{
    /// <summary>
    /// ViewHistoryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ViewHistoryWindow : Window
    {
        //弹出对话框选择要查看的json文件
        public ViewHistoryWindow(string absluteJsonDataFolderPath)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Title = "选择查看的历史",
                Filter = "JSON files (*.json)|*.json", // 只显示.json文件
                InitialDirectory = absluteJsonDataFolderPath // 设置初始文件夹
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                List<ExeItemInfo>? exeItemInfos = LoadData(selectedFilePath);
                if (exeItemInfos != null)
                {
                    exeItemInfos.Sort((x, y) => y.Seconds.CompareTo(x.Seconds));
                    InitializeComponent();
                    for (int i = 0; i < exeItemInfos.Count; i++)
                    {
                        TimeListBox.Items.Add(exeItemInfos[i]);
                    }
                    Show();
                }
                else
                {
                    Close();
                }
            }
            else
            {
                Close();
            }
        }
        public static List<ExeItemInfo>? LoadData(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<ExeItemInfo>>(jsonString);
                }
                catch
                {
                    System.Windows.MessageBox.Show("无法加载选择的JSON文件!", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            else
                return null;
        }
    }
}

﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Serein
{
    public partial class Ui : Form
    {
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void InitWebBrowser()
        {
            PanelConsoleWebBrowser.Navigate(@"file:\\\" + AppDomain.CurrentDomain.BaseDirectory + "console\\console.html?type=panel");
            BotWebBrowser.Navigate(@"file:\\\" + AppDomain.CurrentDomain.BaseDirectory + "console\\console.html?type=bot");
        }
        private void Initialize()
        {
            ShowTutorial();
            InitWebBrowser();
            Settings.ReadSettings();
            LoadSettings();
            LoadPlugins();
            LoadRegex();
            LoadTask();
            Task UpdateInfoThread = new Task(UpdateInfo);
            UpdateInfoThread.Start();
            Settings.StartSaveSettings();
            TaskManager.RunnerThread.Start();
            GetInfo.GetAnnouncementThread.Start();
            GetInfo.GetVersionThread.Start();
            SetWindowTheme(RegexList.Handle, "Explorer", null);
            SetWindowTheme(TaskList.Handle, "Explorer", null);
            SendMessage(RegexList.Handle, 4158, IntPtr.Zero, Cursors.Arrow.Handle);
            SendMessage(TaskList.Handle, 4158, IntPtr.Zero, Cursors.Arrow.Handle);
            new Task(() => Debug_Append("[Serein]Loaded.  " + SystemInfo.CPUPercentage.Replace('.','w'))).Start();
        }
        private void ShowTutorial()
        {
            if (Global.FirstOpen)
            {
                if ((int)MessageBox.Show(
                        "欢迎使用Serein！！qwq\n" +
                        "是否打开教程页面（https://zaitonn.github.io/Serein/Tutorial.html）？\n" +
                        "内含极其详细的配置教程哦",
                        "Serein",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Information
                    ) == 1)
                {
                    Process.Start(new ProcessStartInfo("https://zaitonn.github.io/Serein/Tutorial.html") { UseShellExecute = true });
                }
            }
        }
        private void MultiOpenCheck()
        {
            Process[] Processes = Process.GetProcessesByName("Serein");
            string CurrentName = Process.GetCurrentProcess().MainModule.FileName;
            foreach (Process process in Processes)
            {
                try
                {
                    if (process.MainModule.FileName == CurrentName && process.Id != Process.GetCurrentProcess().Id)
                    {
                        if ((int)MessageBox.Show(
                            $"检测到位于相同目录下的进程（PID={process.Id}）\n" +
                            "同时运行多个进程可能导致数据无法保存甚至崩溃\n\n" +
                            "是否继续运行？\n\n" +
                            "Tip:你可以将Serein的整个运行目录复制多份隔离运行以解决此问题", "Serein",
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning
                        ) != 1)
                        {
                            Environment.Exit(0);
                        }
                        break;
                    }
                }
                catch
                {
                }
            }
        }
    }
}

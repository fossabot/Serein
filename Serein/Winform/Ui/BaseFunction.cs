﻿using Serein.Core.Server;
using Serein.Utils;
using System;
using System.Windows.Forms;

namespace Serein.Ui
{
    public partial class Ui : Form
    {
        private bool _isDragging;
        private ListViewItem? _itemDraged;

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl.SelectedIndex)
            {
                case 2:
                    LoadRegex();
                    break;
                case 5:
                    LoadMember();
                    break;
                case 6:
                    LoadJSPlugin();
                    break;
            }
        }

        private void SettingSereinShowWelcomePage_Click(object sender, EventArgs e) =>
            Runtime.ShowWelcomePage();

        private void Ui_Shown(object sender, EventArgs e) => Runtime.Start();

        private void Ui_FormClosing(object sender, FormClosingEventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            if (ServerManager.Status)
            {
                e.Cancel = true;
                ShowBalloonTip("服务器进程仍在运行中\n已自动最小化至托盘，点击托盘图标即可复原窗口");
                return;
            }
            Runtime.Exit();
            SereinIcon.Dispose();
        }

        public void ShowBalloonTip(string text)
        {
            SereinIcon.BalloonTipTitle = "Serein";
            SereinIcon.BalloonTipText = text;
            SereinIcon.ShowBalloonTip(10000);
        }

        public void Debug_Append(string text)
        {
            if (Global.Settings.Serein.DevelopmentTool.EnableDebug && !IsDisposed)
            {
                Invoke(() =>
                {
                    if (DebugTextBox.Text.Length > 50000)
                    {
                        DebugTextBox.Text = "";
                    }
                    DebugTextBox.Text = DebugTextBox.Text + text + "\r\n";
                });
            }
        }

        private void Ui_DragDrop(object sender, DragEventArgs e)
        {
            FocusWindow();
            FileImportHandler.Trigger((Array)e.Data!.GetData(DataFormats.FileDrop));
        }

        private void UpdateVersion()
        {
            SettingSereinVersion.Text = $"当前版本：{Global.VERSION}";
            UpdateStatusLabel(Global.VERSION);
            DebugTextBox.Text = Global.VERSION + "\r\n";
        }

        private void FocusWindow()
        {
            Visible = true;
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void SereinIcon_BalloonTipClicked(object sender, EventArgs e) => FocusWindow();

        private void SereinIcon_MouseClick(object sender, MouseEventArgs e) => FocusWindow();

        private void UpdateStatusLabel(string text) => StripStatusLabel.Text = text;

        private void Ui_DragEnter(object sender, DragEventArgs e) =>
            e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.All
                : DragDropEffects.None;
    }
}

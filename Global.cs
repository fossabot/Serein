﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

namespace Serein
{
    class Global
    {
        public static string Path = AppDomain.CurrentDomain.BaseDirectory;
        public static string SettingPath = AppDomain.CurrentDomain.BaseDirectory + "settings";
        public static string VERSION = "Beta 1.0";
        public static List<RegexItem> RegexItems = new List<RegexItem>();
        public static List<TaskItem> TaskItems = new List<TaskItem>();
        public static Ui Ui;
        public static WebBrowser PanelConsoleWebBrowser;
        public static WebBrowser BotWebBrowser;
        public static Settings_Server Settings_server = new Settings_Server();
        public static Settings_Bot Settings_bot = new Settings_Bot();
        public static Settings_Serein Settings_serein = new Settings_Serein();
    }
}
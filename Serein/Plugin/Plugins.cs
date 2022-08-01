﻿using Serein.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace Serein.Plugin
{
    internal class Plugins
    {
        public static Event Event = new Event();
        public static List<CommandItem> CommandItems = new List<CommandItem>();
        public static List<PluginItem> PluginItems = new List<PluginItem>();
        public static Dictionary<int, Timer> Timers = new Dictionary<int, Timer>();
        public static List<string> PluginNames
        {
            get
            {
                List<string> list = new List<string>();
                foreach (PluginItem Item in PluginItems)
                {
                    list.Add(Item.Name);
                }
                return list;
            }
        }

        /// <summary>
        /// 加载插件
        /// </summary>
        public static void Load()
        {
            string PluginPath = Global.Path + "\\plugins";
            if (!Directory.Exists(PluginPath))
                Directory.CreateDirectory(PluginPath);
            else
            {
                List<string> ErrorFiles = new List<string>();
                string[] Files = Directory.GetFiles(PluginPath, "*.js", SearchOption.TopDirectoryOnly);
                long Temp;
                foreach (string Filename in Files)
                {
                    Global.Ui.SereinPluginsWebBrowser_Invoke(
                        $"<span style=\"color:#4B738D;font-weight: bold;\">[Serein]</span>正在加载{Log.EscapeLog(Path.GetFileName(Filename))}"
                        );
                    Temp = PluginItems.Count;
                    try
                    {
                        JSEngine.engine = JSEngine.Init();
                        string ExceptionMessage = JSEngine.Run(File.ReadAllText(Filename, Encoding.UTF8));
                        bool Success = string.IsNullOrEmpty(ExceptionMessage);
                        if (!Success)
                        {
                            ErrorFiles.Add(Path.GetFileName(Filename));
                            Global.Ui.SereinPluginsWebBrowser_Invoke(
                                "<span style=\"color:#BA4A00;font-weight: bold;\">[×]</span>"
                                + Log.EscapeLog(ExceptionMessage)
                                );
                        }
                        else
                        {
                            if (Temp == PluginItems.Count - 1)
                            {
                                PluginItems[PluginItems.Count - 1].Path = Filename;
                                PluginItems[PluginItems.Count - 1].Engine = JSEngine.engine;
                            }
                            else
                            {
                                PluginItems.Add(
                                new PluginItem()
                                {
                                    Name = Path.GetFileNameWithoutExtension(Filename),
                                    Version = "-",
                                    Author = "-",
                                    Description = "-",
                                    Path = Filename,
                                    Engine = JSEngine.engine
                                });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorFiles.Add(Path.GetFileName(Filename));
                        Global.Ui.SereinPluginsWebBrowser_Invoke("<span style=\"color:#BA4A00;font-weight: bold;\">[×]</span>" + Log.EscapeLog(e.ToString()));
                    }
                }
                Global.Ui.SereinPluginsWebBrowser_Invoke(
                    $"<span style=\"color:#4B738D;font-weight: bold;\">[Serein]</span>插件加载完毕，共加载{Files.Length}个插件，其中{ErrorFiles.Count}个加载失败"
                    );
                if (ErrorFiles.Count > 0)
                {
                    Global.Ui.SereinPluginsWebBrowser_Invoke(
                        "<span style=\"color:#4B738D;font-weight: bold;\">[Serein]</span>" +
                        $"以下插件加载出现问题，请咨询原作者获取更多信息<br>" +
                        Log.EscapeLog(string.Join(" ,", ErrorFiles)) +
                        "<br>"
                        );
                }
            }
        }

        /// <summary>
        /// 重新加载插件
        /// </summary>
        public static void Reload()
        {
            Global.Ui.SereinPluginsWebBrowser_Invoke("#clear");
            CommandItems.Clear();
            PluginItems.Clear();
            Event = new Event();
            JSFunc.ClearAllTimers();
            Load();
        }
    }
}

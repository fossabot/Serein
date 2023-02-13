﻿using Newtonsoft.Json.Linq;
using Serein.Base.Motd;
using Serein.Core.JSPlugin;
using Serein.Core.Server;
using Serein.Extensions;
using Serein.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Serein.Core
{
    internal static class Command
    {
        public static readonly string[] Sexs = { "unknown", "male", "female" };
        public static readonly string[] Sexs_Chinese = { "未知", "男", "女" };
        public static readonly string[] Roles = { "owner", "admin", "member" };
        public static readonly string[] Roles_Chinese = { "群主", "管理员", "成员" };

        /// <summary>
        /// 启动cmd.exe
        /// </summary>
        /// <param name="Command">执行的命令</param>
        public static void StartCmd(string command)
        {
            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/bash",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Global.PATH
                }
            };
            process.Start();
            StreamWriter commandWriter = new(process.StandardInput.BaseStream, Encoding.Default)
            {
                AutoFlush = true
            };
            commandWriter.WriteLine(command.TrimEnd('\r', '\n'));
            commandWriter.Close();
            Task.Run(() =>
            {
                process.WaitForExit(600000);
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            });
        }

        /// <summary>
        /// 处理Serein命令
        /// </summary>
        /// <param name="inputType">输入类型</param>
        /// <param name="command">命令</param>
        /// <param name="json">消息JSON对象</param>
        /// <param name="msgMatch">消息匹配对象</param>
        /// <param name="userID">用户ID</param>
        /// <param name="groupID">群聊ID</param>
        /// <param name="disableMotd">禁用Motd获取</param>
        public static void Run(
            int inputType,
            string command,
            JObject json = null,
            Match msgMatch = null,
            long userID = -1,
            long groupID = -1,
            bool disableMotd = false
            )
        {
            /*
                1   QQ消息
                2   控制台输出
                3   定时任务
                4   EventTrigger
                5   Javascript
            */
            Logger.Output(
                Base.LogType.Debug,
                    "命令运行",
                    $"inputType:{inputType} ",
                    $"command:{command}",
                    $"userID:{userID}",
                    $"groupID:{groupID}");
            if (groupID == -1 && Global.Settings.Bot.GroupList.Count >= 1)
            {
                groupID = Global.Settings.Bot.GroupList[0];
            }
            Base.CommandType type = GetType(command);
            if (type == Base.CommandType.Invalid || ((type == Base.CommandType.RequestMotdpe || type == Base.CommandType.RequestMotdje) && disableMotd))
            {
                return;
            }
            string value = GetValue(command, msgMatch);
            value = ApplyVariables(value, json, disableMotd);
            switch (type)
            {
                case Base.CommandType.ExecuteCmd:
                    StartCmd(value);
                    break;
                case Base.CommandType.ServerInput:
                case Base.CommandType.ServerInputWithUnicode:
                    if (Global.Settings.Bot.EnbaleParseAt
                        && inputType == 1
                        )
                    {
                        value = ParseAt(value, groupID);
                    }
                    value = Regex.Replace(Regex.Replace(value, @"\[CQ:face.+?\]", "[表情]"), @"\[CQ:([^,]+?),.+?\]", "[$1]");
                    ServerManager.InputCommand(value, type == Base.CommandType.ServerInputWithUnicode, true);
                    break;
                case Base.CommandType.SendGivenGroupMsg:
                    Websocket.Send(false, value, Regex.Match(command, @"(\d+)\|").Groups[1].Value, inputType != 4);
                    break;
                case Base.CommandType.SendGivenPrivateMsg:
                    Websocket.Send(true, value, Regex.Match(command, @"(\d+)\|").Groups[1].Value, inputType != 4);
                    break;
                case Base.CommandType.SendGroupMsg:
                    Websocket.Send(false, value, groupID, inputType != 4);
                    break;
                case Base.CommandType.SendPrivateMsg:
                    if ((inputType == 1 || inputType == 4))
                    {
                        Websocket.Send(true, value, userID, inputType != 4);
                    }
                    break;
                case Base.CommandType.SendTempMsg:
                    if (inputType == 1 && groupID != -1 && userID != -1)
                    {
                        Websocket.Send(groupID, userID, value);
                    }
                    break;
                case Base.CommandType.Bind:
                    if ((inputType == 1 || inputType == 4) && groupID != -1)
                    {
                        Binder.Bind(json, value, userID, groupID);
                    }
                    break;
                case Base.CommandType.Unbind:
                    if ((inputType == 1 || inputType == 4) && groupID != -1)
                    {
                        Binder.UnBind(long.TryParse(value, out long i) ? i : -1, groupID);
                    }
                    break;
                case Base.CommandType.RequestMotdpe:
                    if (inputType == 1 && (groupID != -1 || userID != -1))
                    {
                        Motd motd = new Motdpe(value);
                        EventTrigger.Trigger(
                            motd.IsSuccessful ? Base.EventType.RequestingMotdpeSucceed : Base.EventType.RequestingMotdFail,
                            groupID, userID, motd);
                    }
                    break;
                case Base.CommandType.RequestMotdje:
                    if (inputType == 1 && (groupID != -1 || userID != -1))
                    {
                        Motd motd = new Motdje(value);
                        EventTrigger.Trigger(
                            motd.IsSuccessful ? Base.EventType.RequestingMotdjeSucceed : Base.EventType.RequestingMotdFail,
                            groupID, userID, motd);
                    }
                    break;
                case Base.CommandType.ExecuteJavascriptCodes:
                    if (inputType != 5)
                    {
                        Task.Run(() => JSEngine.Init().Execute(value));
                    }
                    break;
                case Base.CommandType.ExecuteJavascriptCodesWithNamespace:
                    if (inputType != 5)
                    {
                        string key = Regex.Match(command, @"^(javascript|js):([^\|]+)\|").Groups[2].Value;
                        Task.Run(() =>
                        {
                            if (JSPluginManager.PluginDict.ContainsKey(key))
                            {
                                string e;
                                lock (JSPluginManager.PluginDict[key].Engine)
                                {
                                    JSPluginManager.PluginDict[key].Engine = JSPluginManager.PluginDict[key].Engine.Run(value, out e);
                                }
                                if (!string.IsNullOrEmpty(e))
                                {
                                    Logger.Output(Base.LogType.Plugin_Error, $"[{key}]", "通过命令执行时错误：\n", e);
                                }
                            }
                        });
                    }
                    break;
                case Base.CommandType.DebugOutput:
                    Logger.Output(Base.LogType.Debug, "[DebugOutput]", value);
                    break;
                default:
                    Logger.Output(Base.LogType.Debug, "[Unknown]", value);
                    break;
            }
            if (inputType == 1 && type != Base.CommandType.Bind && type != Base.CommandType.Unbind && groupID != -1)
            {
                Binder.Update(json, userID);
            }
        }

        /// <summary>
        /// 获取命令类型
        /// </summary>
        /// <param name="command">命令</param>
        /// <returns>类型</returns>
        public static Base.CommandType GetType(string command)
        {
            if (string.IsNullOrEmpty(command) ||
                !command.Contains("|") ||
                !Regex.IsMatch(command, @"^.+?\|[\s\S]+$", RegexOptions.IgnoreCase))
            {
                return Base.CommandType.Invalid;
            }
            switch (Regex.Match(command, @"^([^\|]+?)\|").Groups[1].Value.ToLowerInvariant())
            {
                case "cmd":
                    return Base.CommandType.ExecuteCmd;
                case "s":
                case "server":
                    return Base.CommandType.ServerInput;
                case "s:unicode":
                case "server:unicode":
                case "s:u":
                case "server:u":
                    return Base.CommandType.ServerInputWithUnicode;
                case "g":
                case "group":
                    return Base.CommandType.SendGroupMsg;
                case "p":
                case "private":
                    return Base.CommandType.SendPrivateMsg;
                case "t":
                case "temp":
                    return Base.CommandType.SendTempMsg;
                case "b":
                case "bind":
                    return Base.CommandType.Bind;
                case "ub":
                case "unbind":
                    return Base.CommandType.Unbind;
                case "motdpe":
                    return Base.CommandType.RequestMotdpe;
                case "motdje":
                    return Base.CommandType.RequestMotdje;
                case "js":
                case "javascript":
                    return Base.CommandType.ExecuteJavascriptCodes;
                case "debug":
                    return Base.CommandType.DebugOutput;
                default:
                    if (Regex.IsMatch(command, @"^(g|group):\d+\|", RegexOptions.IgnoreCase))
                    {
                        return Base.CommandType.SendGivenGroupMsg;
                    }
                    if (Regex.IsMatch(command, @"^(p|private):\d+\|", RegexOptions.IgnoreCase))
                    {
                        return Base.CommandType.SendGivenPrivateMsg;
                    }
                    if (Regex.IsMatch(command, @"^(js|javascript):[^\|]+\|", RegexOptions.IgnoreCase))
                    {
                        return Base.CommandType.ExecuteJavascriptCodesWithNamespace;
                    }
                    return Base.CommandType.Invalid;
            }
        }

        /// <summary>
        /// 获取命令的值
        /// </summary>
        /// <param name="command">命令</param>
        /// <param name="match">消息匹配对象</param>
        /// <returns>值</returns>
        public static string GetValue(string command, Match match = null)
        {
            string value = command.Substring(command.IndexOf('|') + 1);
            if (match != null)
            {
                for (int i = match.Groups.Count; i >= 0; i--)
                {
                    value = value.Replace($"${i}", match.Groups[i].Value);
                }
            }
            Logger.Output(Base.LogType.Debug, value);
            return value;
        }

        /// <summary>
        /// 应用变量
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="jsonObject">消息JSON对象</param>
        /// <param name="disableMotd">禁用Motd获取</param>
        /// <returns>应用变量后的文本</returns>
        public static string ApplyVariables(string text, JObject jsonObject = null, bool disableMotd = false)
        {
            if (!text.Contains("%"))
            {
                return text.Replace("\\n", "\n");
            }
            bool serverStatus = ServerManager.Status;
            DateTime currentTime = DateTime.Now;
            text = Patterns.Variable.Replace(text, (match) =>
                match.Groups[1].Value.ToLowerInvariant() switch
                {
                    #region 时间
                    "year" => currentTime.Year.ToString(),
                    "month" => currentTime.Month.ToString(),
                    "day" => currentTime.Day.ToString(),
                    "hour" => currentTime.Hour.ToString(),
                    "minute" => currentTime.Minute.ToString(),
                    "second" => currentTime.Second.ToString(),
                    "time" => currentTime.ToString("T"),
                    "date" => currentTime.Date.ToString("d"),
                    "dayofweek" => currentTime.DayOfWeek.ToString(),
                    "datetime" => currentTime.ToString(),
                    #endregion

                    "sereinversion" => Global.VERSION,

                    #region motd
                    "gamemode" => ServerManager.Motd.GameMode,
                    "description" => ServerManager.Motd.Description,
                    "protocol" => ServerManager.Motd.Protocol,
                    "onlineplayer" => ServerManager.Motd.OnlinePlayer.ToString(),
                    "maxplayer" => ServerManager.Motd.MaxPlayer.ToString(),
                    "original" => ServerManager.Motd.Origin,
                    "delay" => ServerManager.Motd.Delay.TotalMilliseconds.ToString("N1"),
                    "version" => ServerManager.Motd.Version,
                    "favicon" => ServerManager.Motd.Favicon,
                    "exception" => ServerManager.Motd.Exception,
                    #endregion

                    #region 系统信息
                    "net" => Environment.Version.ToString(),
                    "cpuusage" => SystemInfo.CPUUsage.ToString("N1"),
                    "os" => SystemInfo.OS,
                    "uploadspeed" => SystemInfo.UploadSpeed,
                    "downloadspeed" => SystemInfo.DownloadSpeed,
                    "cpuname" => SystemInfo.CPUName,
                    "cpubrand" => SystemInfo.CPUBrand,
                    "cpufrequency" => SystemInfo.CPUFrequency.ToString("N1"),
                    "usedram" => SystemInfo.UsedRAM.ToString(),
                    "usedramgb" => (SystemInfo.UsedRAM / 1024).ToString("N1"),
                    "totalram" => SystemInfo.TotalRAM.ToString(),
                    "totalramgb" => (SystemInfo.TotalRAM / 1024).ToString("N1"),
                    "freeram" => (SystemInfo.Info.Hardware.RAM.Free / 1024 / 1024).ToString("N1"),
                    "freeramgb" => (SystemInfo.Info.Hardware.RAM.Free / 1024 / 1024 / 1024).ToString("N1"),
                    "ramusage" => SystemInfo.RAMUsage.ToString("N1"),
                    #endregion

                    #region 服务器
                    "levelname" => serverStatus ? ServerManager.LevelName : "-",
                    "difficulty" => serverStatus ? ServerManager.Difficulty : "-",
                    "runtime" => serverStatus ? ServerManager.Time : "-",
                    "servercpuusage" => serverStatus ? ServerManager.CPUUsage.ToString("N1") : "-",
                    "filename" => serverStatus ? ServerManager.StartFileName : "-",
                    "status" => serverStatus ? "已启动" : "未启动",
                    #endregion

                    #region 消息
                    "id" => jsonObject.TryGetString("sender", "user_id"),
                    "gameid" => Binder.GetGameID(long.TryParse(jsonObject.TryGetString("sender", "user_id"), out long result) ? result : -1),
                    "sex" => Sexs_Chinese[Array.IndexOf(Sexs, jsonObject.TryGetString("sender", "sex").ToLowerInvariant())],
                    "nickname" => jsonObject.TryGetString("sender", "nickname"),
                    "age" => jsonObject.TryGetString("sender", "age"),
                    "area" => jsonObject.TryGetString("sender", "area"),
                    "card" => jsonObject.TryGetString("sender", "card"),
                    "level" => jsonObject.TryGetString("sender", "level"),
                    "title" => jsonObject.TryGetString("sender", "title"),
                    "role" => Roles_Chinese[Array.IndexOf(Roles, jsonObject.TryGetString("sender", "role"))],
                    "shownname" => string.IsNullOrEmpty(jsonObject.TryGetString("sender", "card")) ? jsonObject.TryGetString("sender", "nickname") : jsonObject.TryGetString("sender", "card"),
                    #endregion

                    _ => JSPluginManager.CommandVariablesDict.TryGetValue(match.Groups[1].Value, out string variable) ? variable : match.Groups[0].Value
                }
            );
            text = Patterns.GameID.Replace(text,
                (match) => Binder.GetGameID(long.Parse(match.Groups[1].Value)));
            text = Patterns.ID.Replace(text,
                (match) => Binder.GetID(match.Groups[1].Value).ToString());
            return text.Replace("\\n", "\n");
        }

        public static string ParseAt(string text, long groupID)
        {
            text = text.Replace("[CQ:at,qq=all]", "@全体成员");
            text = Patterns.CQAt.Replace(text, "@$1");
            if (groupID <= 0)
            {
                return text;
            }
            text = Regex.Replace(text, @"(?<=@)(\d+)", (match) =>
            {
                long userID = long.TryParse(match.Groups[1].Value, out long result) ? result : 0;
                return Global.GroupCache.TryGetValue(groupID, out Dictionary<long, Base.Member> groupinfo) &&
                    groupinfo.TryGetValue(userID, out Base.Member member) ? member.ShownName : match.Groups[1].Value;
            });
            return text;
        }

        public static class Patterns
        {
            public static readonly Regex CQAt = new(@"\[CQ:at,qq=(\d+)\]", RegexOptions.Compiled);

            /// <summary>
            /// 变量正则
            /// </summary>
            public static readonly Regex Variable = new(@"%(\w+)%", RegexOptions.Compiled);

            /// <summary>
            /// 游戏ID正则
            /// </summary>
            public static readonly Regex GameID = new(@"%GameID:(\d+)%", RegexOptions.Compiled);

            /// <summary>
            /// ID正则
            /// </summary>
            public static readonly Regex ID = new(@"%ID:(.+)%", RegexOptions.Compiled);
        }
    }
}

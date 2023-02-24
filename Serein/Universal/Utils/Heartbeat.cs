using Serein.Base;
using Serein.Core.Server;
using System;
using System.Timers;

namespace Serein.Utils
{
    internal static class Heartbeat
    {
        /// <summary>
        /// GUID
        /// </summary>
        private readonly static string GUID = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 在线统计计时器
        /// </summary>
        private static readonly Timer HeartbeatTimer = new(600_000) { AutoReset = true }; // 十分钟一次心跳事件

        /// <summary>
        /// 开始心跳事件
        /// </summary>
        public static void Start()
        {
            HeartbeatTimer.Elapsed += (_, _) => Request();
            HeartbeatTimer.Start();
        }

        /// <summary>
        /// 心跳请求
        /// </summary>
        private static void Request()
        {
            if (!Global.Settings.Serein.Invisible)
            {
                return;
            }
            try
            {
                Logger.Output(LogType.Debug,
                    Net.Get($"https://count.serein.cc/heartbeat?guid={GUID}&type={Global.TYPE}&version={Global.VERSION}&isrunningserver={(ServerManager.Status ? 1 : 0)}&runtime={(DateTime.Now - Global.StartTime).TotalMinutes:N1}")
                    .GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            catch (Exception e)
            {
                Logger.Output(LogType.Debug, e);
            }
        }
    }
}
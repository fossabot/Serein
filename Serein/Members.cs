﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Serein
{
    internal class Members
    {
        public static List<long> IDs
        {
            get
            {
                List<long> list = new List<long>();
                foreach (MemberItem Item in Global.MemberItems)
                {
                    list.Add(Item.ID);
                }
                return list;
            }
        }
        public static List<string> GameIDs
        {
            get
            {
                List<string> list = new List<string>();
                foreach (MemberItem Item in Global.MemberItems)
                {
                    list.Add(Item.GameID);
                }
                return list;
            }
        }
        public static void Load()
        {
            if (!Directory.Exists(Global.Path + "\\data"))
            {
                Directory.CreateDirectory(Global.Path + "\\data");
            }
            if (File.Exists($"{Global.Path}\\data\\regex.json"))
            {
                StreamReader Reader = new StreamReader(
                    File.Open(
                        $"{Global.Path}\\data\\members.json",
                        FileMode.Open
                    ),
                    Encoding.UTF8);
                string Text = Reader.ReadToEnd();
                if (!string.IsNullOrEmpty(Text))
                {
                    try
                    {
                        JObject JsonObject = (JObject)JsonConvert.DeserializeObject(Text);
                        if (JsonObject["type"].ToString().ToUpper() != "MEMBERS")
                        {
                            return;
                        }
                        Global.MemberItems = ((JArray)JsonObject["data"]).ToObject<List<MemberItem>>();
                    }
                    catch { }
                }
                Reader.Close();
            }
            Global.MemberItems.Sort(
                (Item1, Item2) =>
                {
                    return Item1.ID > Item2.ID ? 1 : -1;
                }
                );
        }
        public static void Save()
        {
            Global.MemberItems.Sort(
                (Item1, Item2) =>
                {
                    return Item1.ID > Item2.ID ? 1 : -1;
                }
                );
            if (!Directory.Exists(Global.Path + "\\data"))
            {
                Directory.CreateDirectory(Global.Path + "\\data");
            }
            StreamWriter MembersWriter = new StreamWriter(
                File.Open(
                    $"{Global.Path}\\data\\members.json",
                    FileMode.Create,
                    FileAccess.Write
                    ),
                Encoding.UTF8
                );
            JObject ListJObject = new JObject();
            JArray ListJArray = new JArray();
            foreach (MemberItem Item in Global.MemberItems)
            {
                ListJArray.Add(JObject.FromObject(Item));
            }
            ListJObject.Add("type", "MEMBERS");
            ListJObject.Add("comment", "非必要请不要直接修改文件，语法错误可能导致数据丢失");
            ListJObject.Add("data", ListJArray);
            MembersWriter.Write(ListJObject.ToString());
            MembersWriter.Flush();
            MembersWriter.Close();
        }
        public static string Bind(JObject JsonObject, string Value, long UserId)
        {
            if (IDs.Contains(UserId))
            {
                return "你已经绑定过了";
            }
            else if (!Regex.IsMatch(Value, @"^[a-zA-Z0-9_\s-]{4,16}$"))
            {
                return "该游戏名称无效";
            }
            else if (GameIDs.Contains(Value))
            {
                return "该游戏名称被占用";
            }
            else
            {
                MemberItem Item = new MemberItem()
                {
                    ID = UserId,
                    Card = JsonObject["sender"]["card"].ToString(),
                    Nickname = JsonObject["sender"]["nickname"].ToString(),
                    Role = Array.IndexOf(Command.Roles, JsonObject["sender"]["role"].ToString()),
                    GameID = Value
                };
                Global.MemberItems.Add(Item);
                Save();
                return "绑定成功";
            }
        }
        public static string UnBind(long UserId)
        {
            if (!IDs.Contains(UserId))
            {
                return "该账号未绑定";
            }
            else
            {
                foreach (MemberItem Item in Global.MemberItems)
                {
                    if (Item.ID == UserId && Global.MemberItems.Remove(Item))
                    {
                        Save();
                        return "解绑成功";
                    }
                }
                Save();
                return "解绑失败";
            }
        }
        public static void Update(JObject JsonObject, long UserId)
        {
            if (IDs.Contains(UserId))
            {
                foreach (MemberItem Item in Global.MemberItems)
                {
                    if (Item.ID == UserId && Global.MemberItems.Remove(Item))
                    {
                        Item.Role = Array.IndexOf(Command.Roles, JsonObject["sender"]["role"].ToString());
                        Item.Nickname = JsonObject["sender"]["nickname"].ToString();
                        Item.Card = JsonObject["sender"]["card"].ToString();
                        Global.MemberItems.Add(Item);
                        Save();
                        break;
                    }
                }
            }
        }
    }
}

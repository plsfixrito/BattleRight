using System;
using System.Collections.Generic;
using System.IO;
using BattleRight.Core;
using BattleRight.Helper;
using BattleRight.Sandbox;
using BattleRight.SDK.UI;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;

namespace AimBot
{
    public class MenuConfig : IAddon
    {
        internal static Menu ScriptMenu;
        internal static MenuCheckBox AddNewAimKey;
        internal static List<AimbotKey> AimbotKeys;
        
        public void OnInit()
        {
            AimbotKeys = new List<AimbotKey>();

            ScriptMenu = new Menu("AimBot", "AimBot");
            AddNewAimKey = ScriptMenu.Add(new MenuCheckBox("addnewkey", "Add a new Key", false));
            AddNewAimKey.OnValueChange += args =>
            {
                if (!args.NewValue)
                    return;
                AimbotKeys.Add(new AimbotKey(AimbotKeys.Count + 1));
                AddNewAimKey.CurrentValue = false;
            };
            
            MainMenu.AddMenu(ScriptMenu);

            var path = Sandbox.AppDataFolder + "\\MenuData\\AimBotKeys.json";
            if (File.Exists(path))
            {
                Logs.Debug("Loading " + path);
                var loaded = JsonHelper.DeserializeObject<List<AimbotKey>>(path);
                if(loaded != null)
                foreach (var key in loaded)
                {
                    if(key.Id != 0)
                        AimbotKeys.Add(new AimbotKey(key.Id));
                }
            }

            MainMenu.OnSaveConfig += OnSaveConfig;
        }

        private static void OnSaveConfig()
        {
            JsonHelper.SaveJsonFile(Sandbox.AppDataFolder + "\\MenuData\\AimBotKeys.json", AimbotKeys);
        }

        public void OnUnload()
        {
        }
    }
}

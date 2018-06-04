using System;
using BattleRight.Core;
using BattleRight.Core.GameObjects;
using BattleRight.SDK;
using BattleRight.SDK.Enumeration;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;

namespace AimBot
{
    public class AimbotKey
    {
        internal MenuSlider AbilityRange, AbilitySpeed, AbilityRadius;
        internal MenuCheckBox UseAbility, AutoAim, DrawRange;
        internal MenuKeybind Keybind;
        public int Id;
        internal static int Num;

        internal PredictionOutput LastPredOutput;

        internal Player LastTarget;

        public AimbotKey()
        {
            
        }

        public AimbotKey(int id = -1)
        {
            Id = id != -1 ? id : Num++;
            var newmenu = new Menu("AimbotKey" + Id, "Key " + Id);
            Keybind = newmenu.Add(new MenuKeybind("AimbotKey_" + Id, "Key (" + Id + ")", KeyCode.None));
            AutoAim = newmenu.Add(new MenuCheckBox("AimbotKey_AutoAim_" + Id, "Automated Aim"));
            UseAbility = newmenu.Add(new MenuCheckBox("AimbotKey_UseAbility_" + Id, "Use Ability", false));
            DrawRange = newmenu.Add(new MenuCheckBox("AimbotKey_DrawRange_" + Id, "Draw Ability Range", false));
            AbilityRange = newmenu.Add(new MenuSlider("AimbotKey_AbilityRange_" + Id, "Ability Range", 8.5f, 20, 0.01f));
            AbilitySpeed = newmenu.Add(new MenuSlider("AimbotKey_AbilitySpeed_" + Id, "Ability Speed", 4, 20, 0.01f));
            AbilityRadius = newmenu.Add(new MenuSlider("AimbotKey_AbilityRadius_" + Id, "Ability Radius", .2f, 10, 0.01f));
            MenuConfig.ScriptMenu.Add(newmenu);

            Game.OnUpdate += OnUpdate;
            Game.OnDraw += OnDraw;
        }

        private void OnDraw(EventArgs eventArgs)
        {
            if(!DrawRange.CurrentValue)
                return;

            Drawing.DrawCircle(LocalPlayer.Instance.WorldPosition, AbilityRange.CurrentValue, Color.cyan);
        }

        private void OnUpdate(EventArgs eventArgs)
        {
            if (!Game.IsInGame)
                return;

            if (!Keybind.CurrentValue)
                return;
            
            LastTarget = TargetSelector.GetTarget(TargetingMode.LowestHealth, AbilityRange.CurrentValue);
            if (LastTarget == null)
                return;
            
            LastPredOutput = LocalPlayer.Instance.GetPrediction(LastTarget, AbilitySpeed.CurrentValue, AbilityRange.CurrentValue, AbilityRadius.CurrentValue, SkillType.Line);

            TryAutoAim();
            TryUseAbility();
        }

        private void TryAutoAim()
        {
            if (!AutoAim.CurrentValue || LastPredOutput == null)
                return;

            if (LastPredOutput.HitChancePercent > 20)
                LocalPlayer.UpdateCursorPosition(LastPredOutput.MoveMousePosition);
        }

        private void TryUseAbility()
        {
            if (!UseAbility.CurrentValue || LastPredOutput == null)
                return;

        }
    }
}

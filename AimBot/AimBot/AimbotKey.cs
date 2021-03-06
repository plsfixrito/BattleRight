﻿using System;
using BattleRight.Core;
using BattleRight.Core.GameObjects;
using BattleRight.SDK;
using BattleRight.SDK.Enumeration;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;
using CollisionFlags = BattleRight.Core.Enumeration.CollisionFlags;

namespace AimBot
{
    public class AimbotKey
    {
        internal MenuSlider AbilityRange, AbilitySpeed, AbilityRadius;
        internal MenuCheckBox UseAbility, AutoAim, DrawRange, DrawPrediction, DrawTargetInfo;
        internal MenuKeybind Keybind;
        public int Id;
        internal static int Num;
        internal static bool EditingAim;

        internal PredictionOutput LastPredOutput;

        internal Character LastTarget;
        
        public AimbotKey()
        {
            Create(Id);
        }

        public AimbotKey(int id)
        {
            Create(id);
        }

        private void Create(int i)
        {
            Id = i != 0 ? i : Num++;
            var newmenu = new Menu("AimbotKey" + Id, "Key " + Id);
            Keybind = newmenu.Add(new MenuKeybind("AimbotKey_" + Id, "Key (" + Id + ")", KeyCode.None));
            AutoAim = newmenu.Add(new MenuCheckBox("AimbotKey_AutoAim_" + Id, "Automated Aim"));
            UseAbility = newmenu.Add(new MenuCheckBox("AimbotKey_UseAbility_" + Id, "Use Ability", false));
            DrawRange = newmenu.Add(new MenuCheckBox("AimbotKey_DrawRange_" + Id, "Draw Ability Range", false));
            DrawPrediction = newmenu.Add(new MenuCheckBox("AimbotKey_DrawPrediction_" + Id, "Draw Prediction", false));
            DrawTargetInfo = newmenu.Add(new MenuCheckBox("AimbotKey_DrawTargetInfo_" + Id, "Draw Target Info", false));
            AbilityRange = newmenu.Add(new MenuSlider("AimbotKey_AbilityRange_" + Id, "Ability Range", 8.5f, 20, 0.01f));
            AbilitySpeed = newmenu.Add(new MenuSlider("AimbotKey_AbilitySpeed_" + Id, "Ability Speed", 4, 20, 0.01f));
            AbilityRadius = newmenu.Add(new MenuSlider("AimbotKey_AbilityRadius_" + Id, "Ability Radius", .2f, 10, 0.01f));
            MenuConfig.ScriptMenu.Add(newmenu);

            Game.OnUpdate += OnUpdate;
            Game.OnDraw += OnDraw;
        }


		private void OnDraw(EventArgs eventArgs)
        {
            if(DrawRange.CurrentValue && LocalPlayer.Instance != null)
                Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, AbilityRange.CurrentValue, Color.cyan);
            if(DrawPrediction.CurrentValue && LastPredOutput != null)
                Drawing.DrawCircle(LastPredOutput.PredictedPosition, .75f, Color.yellow);
            if (Keybind.CurrentValue && LastPredOutput != null && LastTarget != null)
            {
                GUI.Label(new Rect(0, Game.ScreenHeight * 0.3f, 400, 400),   "Aimbot " + Id + " Target:" 
                                                                           + "\n - Name: " + LastTarget.Name + " (" + LastTarget.ChampionEnum
                                                                           + ")\n - Health: " + LastTarget.Living.Health + " (" + LastTarget.Living.HealthPercent
                                                                           + ")\n - HitChance: " + LastPredOutput.HitChance + " (" + LastPredOutput.HitChancePercent + ")");
            }
        }

        private void OnUpdate(EventArgs eventArgs)
        {
            if (!Game.IsInGame)
            {
                StopEditingAim();
                return;
            }

            LastPredOutput = null;

            if (!Keybind.CurrentValue)
            {
                StopEditingAim();
                return;
            }

            if (LastTarget == null || !LastTarget.IsValid || LastTarget.PhysicsCollision.IsImmaterial || LastTarget.IsCountering ||
                LastTarget.Distance(LocalPlayer.Instance) > AbilityRange.CurrentValue)
            {
                LastTarget = TargetSelector.GetTarget(TargetingMode.LowestHealth, AbilityRange.CurrentValue);
            }

            if (LastTarget == null)
            {
                StopEditingAim();
                return;
            }
            
            LastPredOutput = LocalPlayer.Instance.GetPrediction(LastTarget, AbilitySpeed.CurrentValue, AbilityRange.CurrentValue, AbilityRadius.CurrentValue, SkillType.Line, 0f,
                                                                CollisionFlags.Bush | CollisionFlags.NPCBlocker |
                                                                (LocalPlayer.Instance.BaseObject.TeamId == 1 ? CollisionFlags.Team1 : CollisionFlags.Team2));

            TryAutoAim();
            TryUseAbility();
        }

        private void TryAutoAim()
        {
            if (!AutoAim.CurrentValue || LastPredOutput == null)
            {
                return;
            }

            if (LastPredOutput.HitChancePercent > 20 &&
                !LastPredOutput.CollisionResult.CollisionFlags.HasFlag(CollisionFlags.LowBlock) &&
                !LastPredOutput.CollisionResult.CollisionFlags.HasFlag(CollisionFlags.HighBlock))
            {
                EditingAim = true;
                LocalPlayer.Aim(LastPredOutput.PredictedPosition);
            } else
            {
                StopEditingAim();
            }
        }

        private void TryUseAbility()
        {
            if (!UseAbility.CurrentValue || LastPredOutput == null)
                return;

        }

        private void StopEditingAim()
        {
            if (EditingAim)
            {
                EditingAim = false;
                LocalPlayer.EditAimPosition = false;
            }
        }
    }
}

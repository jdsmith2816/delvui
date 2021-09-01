﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Actors.Types;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Structs.JobGauge;
using Dalamud.Plugin;
using ImGuiNET;
using DelvUI.Helpers;

namespace DelvUI.Interface
{
    public class DragoonHudWindow : HudWindow
    {
        public override uint JobId => Jobs.DRG;

        protected int EyeOfTheDragonBarHeight => PluginConfiguration.DRGEyeOfTheDragonHeight;
        protected int EyeOfTheDragonBarWidth => PluginConfiguration.DRGEyeOfTheDragonBarWidth;
        protected int EyeOfTheDragonPadding => PluginConfiguration.DRGEyeOfTheDragonPadding;
        protected new int XOffset => PluginConfiguration.DRGBaseXOffset;
        protected new int YOffset => PluginConfiguration.DRGBaseYOffset;
        protected int BloodBarHeight => PluginConfiguration.DRGBloodBarHeight;
        protected int DisembowelBarHeight => PluginConfiguration.DRGDisembowelBarHeight;
        protected int ChaosThrustBarHeight => PluginConfiguration.DRGChaosThrustBarHeight;
        protected int InterBarOffset => PluginConfiguration.DRGInterBarOffset;
        protected bool ShowChaosThrustTimer => PluginConfiguration.DRGShowChaosThrustTimer;
        protected bool ShowDisembowelTimer => PluginConfiguration.DRGShowDisembowelBuffTimer;
        protected bool ShowChaosThrustText => PluginConfiguration.DRGShowChaosThrustText;
        protected bool ShowBloodText => PluginConfiguration.DRGShowBloodText;
        protected bool ShowDisembowelText => PluginConfiguration.DRGShowDisembowelText;
        protected Dictionary<string, uint> EyeOfTheDragonColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000];
        protected Dictionary<string, uint> BloodOfTheDragonColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000 + 1];
        protected Dictionary<string, uint> LifeOftheDragonColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000 + 2];
        protected Dictionary<string, uint> DisembowelColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000 + 3];
        protected Dictionary<string, uint> ChaosThrustColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000 + 4];
        protected Dictionary<string, uint> EmptyColor => PluginConfiguration.JobColorMap[Jobs.DRG * 1000 + 5];


        public DragoonHudWindow(DalamudPluginInterface pluginInterface, PluginConfiguration pluginConfiguration) : base(pluginInterface, pluginConfiguration) { }

        protected override void Draw(bool _)
        {
            var nextHeight = 0;
            if (ShowChaosThrustTimer)
            {
                nextHeight = DrawChaosThrustBar(nextHeight);
            }
            if (ShowDisembowelTimer)
            {
                nextHeight = DrawDisembowelBar(nextHeight);
            }
            nextHeight = DrawEyeOfTheDragonBars(nextHeight);
            DrawBloodOfTheDragonBar(nextHeight);
        }

        protected override void DrawPrimaryResourceBar()
        {
            // Never draw the mana bar for Dragoons as it's useless.
            return;
        }

        private int DrawChaosThrustBar(int initialHeight)
        {
            Debug.Assert(PluginInterface.ClientState.LocalPlayer != null, "PluginInterface.ClientState.LocalPlayer != null");
            var target = PluginInterface.ClientState.Targets.SoftTarget ?? PluginInterface.ClientState.Targets.CurrentTarget;
            var scale = 0f;
            var duration = 0;
            if ((target is Chara))
            {
                var chaosThrust = target.StatusEffects.FirstOrDefault(o => (o.EffectId == 1312 || o.EffectId == 118) && o.OwnerId == PluginInterface.ClientState.LocalPlayer.ActorId);
                scale = chaosThrust.Duration / 24f;
                duration = (int) Math.Round(chaosThrust.Duration);
                if (scale < 0f)
                {
                    scale = 0f;
                    duration = 0;
                }
            }
            var barWidth = EyeOfTheDragonBarWidth * 2 + EyeOfTheDragonPadding;
            var barSize = new Vector2(barWidth, ChaosThrustBarHeight);
            var xPos = CenterX - XOffset;
            var yPos = CenterY + YOffset + initialHeight;
            var cursorPos = new Vector2(xPos, yPos);
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(cursorPos, cursorPos + barSize, EmptyColor["background"]);
            drawList.AddRect(cursorPos, cursorPos + barSize, 0xFF000000);
            var chaosThrustBarSize = new Vector2(barWidth * scale, ChaosThrustBarHeight);
            
            drawList.AddRectFilledMultiColor(
                cursorPos, cursorPos + chaosThrustBarSize,
                ChaosThrustColor["gradientLeft"], ChaosThrustColor["gradientRight"], ChaosThrustColor["gradientRight"], ChaosThrustColor["gradientLeft"]);

            if (ShowChaosThrustText && duration > 0f)
            {
                var durationText = duration.ToString();
                var textSize = ImGui.CalcTextSize(durationText);
                DrawOutlinedText(duration.ToString(), new Vector2(cursorPos.X + 5f, cursorPos.Y + ChaosThrustBarHeight / 2f - textSize.Y / 2f));
            }

            return initialHeight + ChaosThrustBarHeight + InterBarOffset;

        }

        private int DrawEyeOfTheDragonBars(int initialHeight)
        {
            var gauge = PluginInterface.ClientState.JobGauges.Get<DRGGauge>();

            var barSize = new Vector2(EyeOfTheDragonBarWidth, EyeOfTheDragonBarHeight);
            var xPos = CenterX - XOffset;
            var yPos = CenterY + YOffset + initialHeight;
            var cursorPos = new Vector2(xPos, yPos);
            var eyeCount = gauge.EyeCount;
            var drawList = ImGui.GetWindowDrawList();

            for (byte i = 0; i < 2; i++)
            {
                cursorPos = new Vector2(cursorPos.X + (EyeOfTheDragonBarWidth + EyeOfTheDragonPadding) * i, cursorPos.Y);
                if (eyeCount >= (i + 1))
                {
                    drawList.AddRectFilledMultiColor(
                        cursorPos, cursorPos + barSize,
                        EyeOfTheDragonColor["gradientLeft"], EyeOfTheDragonColor["gradientRight"], EyeOfTheDragonColor["gradientRight"], EyeOfTheDragonColor["gradientLeft"]
                    );
                }
                else
                {
                    drawList.AddRectFilled(cursorPos, cursorPos + barSize, EmptyColor["background"]);
                }
                drawList.AddRect(cursorPos, cursorPos + barSize, 0xFF000000);
            }

            return EyeOfTheDragonBarHeight + initialHeight + InterBarOffset;
        }

        private int DrawBloodOfTheDragonBar(int initialHeight)
        {
            var gauge = PluginInterface.ClientState.JobGauges.Get<DRGGauge>();

            var xPos = CenterX - XOffset;
            var yPos = CenterY + YOffset + initialHeight;
            var barWidth = EyeOfTheDragonBarWidth * 2 + EyeOfTheDragonPadding;
            var cursorPos = new Vector2(xPos, yPos);
            var barSize = new Vector2(barWidth, BloodBarHeight);

            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(cursorPos, cursorPos + barSize, EmptyColor["background"]);
            drawList.AddRect(cursorPos, cursorPos + barSize, 0xFF000000);

            var maxTimerMs = 30 * 1000;
            var currTimerMs = gauge.BOTDTimer;
            if (currTimerMs == 0)
            {
                return initialHeight + BloodBarHeight + InterBarOffset;
            }
            var scale = (float)currTimerMs / maxTimerMs;
            var botdBarSize = new Vector2(barWidth * scale, BloodBarHeight);
            if (gauge.BOTDState == BOTDState.LOTD)
            {
                drawList.AddRectFilledMultiColor(
                    cursorPos, cursorPos + botdBarSize,
                    LifeOftheDragonColor["gradientLeft"], LifeOftheDragonColor["gradientRight"], LifeOftheDragonColor["gradientRight"], LifeOftheDragonColor["gradientLeft"]);
            }
            else
            {
                drawList.AddRectFilledMultiColor(
                    cursorPos, cursorPos + botdBarSize,
                    BloodOfTheDragonColor["gradientLeft"], BloodOfTheDragonColor["gradientRight"], BloodOfTheDragonColor["gradientRight"], BloodOfTheDragonColor["gradientLeft"]);
            }
            if (ShowBloodText)
            {
                var durationText = ((int)(currTimerMs / 1000f)).ToString();
                var textSize = ImGui.CalcTextSize(durationText);
                DrawOutlinedText(durationText, new Vector2(cursorPos.X + 5f, cursorPos.Y + BloodBarHeight / 2f - textSize.Y / 2f));
            }
            return initialHeight + BloodBarHeight + InterBarOffset;
        }

        private int DrawDisembowelBar(int initialHeight)
        {
            Debug.Assert(PluginInterface.ClientState.LocalPlayer != null, "PluginInterface.ClientState.LocalPlayer != null");
            var xPos = CenterX - XOffset;
            var yPos = CenterY + YOffset + initialHeight;
            var barWidth = EyeOfTheDragonBarWidth * 2 + EyeOfTheDragonPadding;
            var barSize = new Vector2(barWidth, DisembowelBarHeight);
            var cursorPos = new Vector2(xPos, yPos);
            var drawList = ImGui.GetWindowDrawList();
            var disembowelBuff = PluginInterface.ClientState.LocalPlayer.StatusEffects.Where(o => o.EffectId == 1914 || o.EffectId == 121);
            drawList.AddRectFilled(cursorPos, cursorPos + barSize, EmptyColor["background"]);
            drawList.AddRect(cursorPos, cursorPos + barSize, 0xFF000000);
            if (disembowelBuff.Count() == 0)
            {
                return initialHeight + DisembowelBarHeight + InterBarOffset;
            }
            var buff = disembowelBuff.First();
            if (buff.Duration <= 0)
            {
                return initialHeight + DisembowelBarHeight + InterBarOffset;
            }
            var scale = buff.Duration / 30f;
            var disembowelBarSize = new Vector2(barWidth * scale, DisembowelBarHeight);
            drawList.AddRectFilledMultiColor(
                cursorPos, cursorPos + disembowelBarSize,
                DisembowelColor["gradientLeft"], DisembowelColor["gradientRight"], DisembowelColor["gradientRight"], DisembowelColor["gradientLeft"]);

            if (ShowDisembowelText)
            {
                var durationText = ((int)buff.Duration).ToString();
                var textSize = ImGui.CalcTextSize(durationText);
                DrawOutlinedText(durationText, new Vector2(cursorPos.X + 5f, cursorPos.Y + BloodBarHeight / 2f - textSize.Y / 2f));
            }

            return initialHeight + DisembowelBarHeight + InterBarOffset;
        }
    }
}
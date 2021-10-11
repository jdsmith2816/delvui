﻿using Dalamud.Game.ClientState.Objects.Types;
using DelvUI.Config;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Logging;

namespace DelvUI.Interface.GeneralElements
{
    public class LimitBreakHud : DraggableHudElement, IHudElementWithActor
    {
        private LimitBreakConfig Config => (LimitBreakConfig)_config;

        public GameObject? Actor { get; set; } = null;

        public LimitBreakHud(LimitBreakConfig config, string displayName) : base(config, displayName) { }

        protected override (List<Vector2>, List<Vector2>) ChildrenPositionsAndSizes()
        {
            return (new List<Vector2>() { Config.Position }, new List<Vector2>() { Config.Size });
        }

        public override void DrawChildren(Vector2 origin)
        {
            LimitBreakHelper helper = LimitBreakHelper.Instance;
            
            Config.Label.SetText("");

            if (!Config.Enabled)
            {
                return;
            }

            if (Config.HideWhenInactive && !helper.LimitBreakActive)
            {
                return;
            }
            
            int currentLimitBreak = helper.LimitBreakBarWidth.Sum();
            int maxLimitBreak = helper.LimitBreakMaxLevel * helper.MaxLimitBarWidth;
            
            Config.Label.SetText($"{helper.LimitBreakLevel} / {helper.LimitBreakMaxLevel}");
            
            BarUtilities.GetChunkedProgressBars(Config, helper.LimitBreakMaxLevel, currentLimitBreak, maxLimitBreak).Draw(origin);
        }
    }


}

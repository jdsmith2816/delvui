using DelvUI.Config;
using DelvUI.Config.Attributes;
using DelvUI.Enums;
using DelvUI.Helpers;
using DelvUI.Interface.Bars;
using DelvUI.Interface.GeneralElements;
using DelvUI.Interface.StatusEffects;
using ImGuiNET;
using System.Numerics;

namespace DelvUI.Interface.EnemyList
{
    public enum EnemyListGrowthDirection
    {
        Down = 0,
        Up
    }

    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("General", 0)]
    public class EnemyListConfig : MovablePluginConfigObject
    {
        public new static EnemyListConfig DefaultConfig()
        {
            var config = new EnemyListConfig();
            Vector2 screenSize = ImGui.GetMainViewport().Size;
            config.Position = new Vector2(screenSize.X * 0.2f, -screenSize.Y * 0.2f);

            return config;
        }

        [Checkbox("Preview", isMonitored = true)]
        [Order(4)]
        public bool Preview = false;

        [Checkbox("Hide Current Target", isMonitored = true)]
        [Order(5)]
        public bool HideCurrentTarget = false;

        [Combo("Growth Direction", "Down", "Up", spacing = true)]
        [Order(20)]
        public EnemyListGrowthDirection GrowthDirection = EnemyListGrowthDirection.Down;

        [DragInt("Vertical Padding", min = 0, max = 500)]
        [Order(25)]
        public int VerticalPadding = 10;
    }

    [Exportable(false)]
    [DisableParentSettings("Position", "Anchor", "HideWhenInactive")]
    [Section("Enemy List", true)]
    [SubSection("Health Bar", 0)]
    public class EnemyListHealthBarConfig : BarConfig
    {
        [NestedConfig("Name Label", 70)]
        public EditableLabelConfig NameLabel = new EditableLabelConfig(new Vector2(-5, 12), "[name]", DrawAnchor.TopRight, DrawAnchor.BottomRight);

        [NestedConfig("Health Label", 80)]
        public EditableLabelConfig HealthLabel = new EditableLabelConfig(new Vector2(30, 0), "[health:percent]%", DrawAnchor.Left, DrawAnchor.Left);

        [NestedConfig("Order Label", 90)]
        public DefaultFontLabelConfig OrderLabel = new DefaultFontLabelConfig(new Vector2(5, 0), "", DrawAnchor.Left, DrawAnchor.Left);

        [NestedConfig("Colors", 100)]
        public EnemyListHealthBarColorsConfig Colors = new EnemyListHealthBarColorsConfig();

        [NestedConfig("Change Alpha Based on Range", 110)]
        public EnemyListRangeConfig RangeConfig = new EnemyListRangeConfig();

        [NestedConfig("Use Smooth Transitions", 120)]
        public SmoothHealthConfig SmoothHealthConfig = new SmoothHealthConfig();

        [NestedConfig("Custom Mouseover Area", 130)]
        public MouseoverAreaConfig MouseoverAreaConfig = new MouseoverAreaConfig();

        public new static EnemyListHealthBarConfig DefaultConfig()
        {
            Vector2 size = new Vector2(180, 40);

            var config = new EnemyListHealthBarConfig(Vector2.Zero, size, new PluginConfigColor(new(233f / 255f, 4f / 255f, 4f / 255f, 100f / 100f)));
            config.Colors.ColorByHealth.Enabled = false;

            config.NameLabel.FontID = FontsConfig.DefaultMediumFontKey;
            config.HealthLabel.FontID = FontsConfig.DefaultMediumFontKey;

            config.MouseoverAreaConfig.Enabled = false;

            return config;
        }

        public EnemyListHealthBarConfig(Vector2 position, Vector2 size, PluginConfigColor fillColor, BarDirection fillDirection = BarDirection.Right)
            : base(position, size, fillColor, fillDirection)
        {
        }
    }

    [Disableable(false)]
    [Exportable(false)]
    public class EnemyListHealthBarColorsConfig : PluginConfigObject
    {
        [NestedConfig("Color Based On Health Value", 30, collapsingHeader = false)]
        public ColorByHealthValueConfig ColorByHealth = new ColorByHealthValueConfig();

        [Checkbox("Highlight When Hovering With Cursor Or Soft Targeting", spacing = true)]
        [Order(40)]
        public bool ShowHighlight = true;

        [ColorEdit4("Highlight Color")]
        [Order(41, collapseWith = nameof(ShowHighlight))]
        public PluginConfigColor HighlightColor = new PluginConfigColor(new Vector4(255f / 255f, 255f / 255f, 255f / 255f, 5f / 100f));

        [Checkbox("Missing Health Color", spacing = true)]
        [Order(45)]
        public bool UseMissingHealthBar = false;

        [ColorEdit4("Color" + "##MissingHealth")]
        [Order(46, collapseWith = nameof(UseMissingHealthBar))]
        public PluginConfigColor HealthMissingColor = new PluginConfigColor(new Vector4(255f / 255f, 0f / 255f, 0f / 255f, 100f / 100f));

        [ColorEdit4("Target Border Color", spacing = true)]
        [Order(50)]
        public PluginConfigColor TargetBordercolor = new PluginConfigColor(new Vector4(255f / 255f, 255f / 255f, 255f / 255f, 100f / 100f));

        [DragInt("Target Border Thickness", min = 1, max = 10)]
        [Order(51)]
        public int TargetBorderThickness = 1;

        [Checkbox("Show Enmity Border Colors", spacing = true)]
        [Order(60)]
        public bool ShowEnmityBorderColors = true;

        [ColorEdit4("Enmity Leader Color")]
        [Order(61, collapseWith = nameof(ShowEnmityBorderColors))]
        public PluginConfigColor EnmityLeaderBorderColor = new PluginConfigColor(new Vector4(255f / 255f, 40f / 255f, 40f / 255f, 100f / 100f));

        [ColorEdit4("Enmity Close To Leader Color")]
        [Order(62, collapseWith = nameof(ShowEnmityBorderColors))]
        public PluginConfigColor EnmitySecondBorderColor = new PluginConfigColor(new Vector4(255f / 255f, 175f / 255f, 40f / 255f, 100f / 100f));
    }

    [Exportable(false)]
    public class EnemyListRangeConfig : PluginConfigObject
    {
        [DragInt("Range (yalms)", min = 1, max = 500)]
        [Order(5)]
        public int Range = 30;

        [DragFloat("Alpha", min = 1, max = 100)]
        [Order(10)]
        public float Alpha = 25;


        public float AlphaForDistance(int distance, float alpha = 100f)
        {
            if (!Enabled)
            {
                return 100f;
            }

            return distance > Range ? Alpha : alpha;
        }
    }

    [DisableParentSettings("FrameAnchor")]
    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("Enmity Icon", 0)]
    public class EnemyListEnmityIconConfig : IconConfig
    {
        [Anchor("Health Bar Anchor")]
        [Order(16)]
        public DrawAnchor HealthBarAnchor = DrawAnchor.TopLeft;

        public new static EnemyListEnmityIconConfig DefaultConfig() =>
            new EnemyListEnmityIconConfig(new Vector2(5), new Vector2(24), DrawAnchor.Center, DrawAnchor.TopLeft);

        public EnemyListEnmityIconConfig(Vector2 position, Vector2 size, DrawAnchor anchor, DrawAnchor frameAnchor)
            : base(position, size, anchor, frameAnchor)
        {
            HealthBarAnchor = frameAnchor;
        }
    }

    [DisableParentSettings("FrameAnchor")]
    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("Sign Icon", 0)]
    public class EnemyListSignIconConfig : SignIconConfig
    {
        [Anchor("Health Bar Anchor")]
        [Order(16)]
        public DrawAnchor HealthBarAnchor = DrawAnchor.TopLeft;

        [Checkbox("Replace Order Label", help = "When enabled and if the enemy has a sign assigned, the sign icon will be drawn instead of the order label.")]
        [Order(30)]
        public bool ReplaceOrderLabel = true;

        public new static EnemyListSignIconConfig DefaultConfig() =>
            new EnemyListSignIconConfig(new Vector2(0), new Vector2(30), DrawAnchor.Center, DrawAnchor.Left);

        public EnemyListSignIconConfig(Vector2 position, Vector2 size, DrawAnchor anchor, DrawAnchor frameAnchor)
            : base(position, size, anchor, frameAnchor)
        {
            HealthBarAnchor = frameAnchor;
        }
    }

    [DisableParentSettings("AnchorToUnitFrame", "UnitFrameAnchor", "HideWhenInactive", "FillDirection")]
    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("Castbar", 0)]
    public class EnemyListCastbarConfig : TargetCastbarConfig
    {
        public new static EnemyListCastbarConfig DefaultConfig()
        {
            var size = new Vector2(180, 10);

            var castNameConfig = new LabelConfig(new Vector2(0, 0), "", DrawAnchor.Center, DrawAnchor.Center);
            castNameConfig.FontID = FontsConfig.DefaultMediumFontKey;
            var castTimeConfig = new NumericLabelConfig(new Vector2(-5, 0), "", DrawAnchor.Right, DrawAnchor.Right);
            castTimeConfig.Enabled = false;
            castTimeConfig.FontID = FontsConfig.DefaultMediumFontKey;
            castTimeConfig.NumberFormat = 1;

            var config = new EnemyListCastbarConfig(Vector2.Zero, size, castNameConfig, castTimeConfig);
            config.HealthBarAnchor = DrawAnchor.Bottom;
            config.Anchor = DrawAnchor.Bottom;
            config.ShowIcon = false;

            return config;
        }

        [Anchor("Health Bar Anchor")]
        [Order(16)]
        public DrawAnchor HealthBarAnchor = DrawAnchor.BottomLeft;

        public EnemyListCastbarConfig(Vector2 position, Vector2 size, LabelConfig castNameConfig, NumericLabelConfig castTimeConfig)
            : base(position, size, castNameConfig, castTimeConfig)
        {

        }
    }

    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("Buffs", 0)]
    public class EnemyListBuffsConfig : EnemyListStatusEffectsListConfig
    {
        public new static EnemyListBuffsConfig DefaultConfig()
        {
            var durationConfig = new LabelConfig(new Vector2(0, -4), "", DrawAnchor.Bottom, DrawAnchor.Center);
            var stacksConfig = new LabelConfig(new Vector2(-3, 4), "", DrawAnchor.TopRight, DrawAnchor.Center);
            stacksConfig.Color = new(Vector4.UnitW);
            stacksConfig.OutlineColor = new(Vector4.One);

            var iconConfig = new StatusEffectIconConfig(durationConfig, stacksConfig);
            iconConfig.DispellableBorderConfig.Enabled = false;
            iconConfig.Size = new Vector2(24, 24);

            var pos = new Vector2(5, 8);
            var size = new Vector2(iconConfig.Size.X * 4 + 6, iconConfig.Size.Y);

            var config = new EnemyListBuffsConfig(DrawAnchor.TopRight, pos, size, true, false, false, GrowthDirections.Right | GrowthDirections.Down, iconConfig);
            config.Limit = 4;
            config.ShowPermanentEffects = true;
            config.IconConfig.DispellableBorderConfig.Enabled = false;

            return config;
        }

        public EnemyListBuffsConfig(DrawAnchor anchor, Vector2 position, Vector2 size, bool showBuffs, bool showDebuffs, bool showPermanentEffects,
            GrowthDirections growthDirections, StatusEffectIconConfig iconConfig)
            : base(anchor, position, size, showBuffs, showDebuffs, showPermanentEffects, growthDirections, iconConfig)
        {
        }
    }

    [Exportable(false)]
    [Section("Enemy List", true)]
    [SubSection("Debuffs", 0)]
    public class EnemyListDebuffsConfig : EnemyListStatusEffectsListConfig
    {
        public new static EnemyListDebuffsConfig DefaultConfig()
        {
            var durationConfig = new LabelConfig(new Vector2(0, -4), "", DrawAnchor.Bottom, DrawAnchor.Center);
            var stacksConfig = new LabelConfig(new Vector2(-3, 4), "", DrawAnchor.TopRight, DrawAnchor.Center);
            stacksConfig.Color = new(Vector4.UnitW);
            stacksConfig.OutlineColor = new(Vector4.One);

            var iconConfig = new StatusEffectIconConfig(durationConfig, stacksConfig);
            iconConfig.Size = new Vector2(24, 24);

            var pos = new Vector2(-5, 8);
            var size = new Vector2(iconConfig.Size.X * 4 + 6, iconConfig.Size.Y);

            var config = new EnemyListDebuffsConfig(DrawAnchor.TopLeft, pos, size, false, true, false, GrowthDirections.Left | GrowthDirections.Down, iconConfig);
            config.Limit = 4;
            config.ShowPermanentEffects = true;
            config.IconConfig.DispellableBorderConfig.Enabled = false;

            return config;
        }

        public EnemyListDebuffsConfig(DrawAnchor anchor, Vector2 position, Vector2 size, bool showBuffs, bool showDebuffs, bool showPermanentEffects,
            GrowthDirections growthDirections, StatusEffectIconConfig iconConfig)
            : base(anchor, position, size, showBuffs, showDebuffs, showPermanentEffects, growthDirections, iconConfig)
        {
        }
    }

    public class EnemyListStatusEffectsListConfig : StatusEffectsListConfig
    {
        [Anchor("Health Bar Anchor")]
        [Order(4)]
        public DrawAnchor HealthBarAnchor = DrawAnchor.BottomLeft;

        public EnemyListStatusEffectsListConfig(DrawAnchor anchor, Vector2 position, Vector2 size, bool showBuffs, bool showDebuffs, bool showPermanentEffects,
            GrowthDirections growthDirections, StatusEffectIconConfig iconConfig)
            : base(position, size, showBuffs, showDebuffs, showPermanentEffects, growthDirections, iconConfig)
        {
            HealthBarAnchor = anchor;
        }
    }
}

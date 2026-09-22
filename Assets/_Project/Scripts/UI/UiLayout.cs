using UnityEngine;

namespace ServerGame.UI
{
    public enum LayoutKind
    {
        Wide,
        Compact,
        CompactLandscape
    }

    public sealed class UiLayout
    {
        public const float MinWideCssWidth = 820f;

        public const float MinWideCssHeight = 560f;

        public const float MinWideAspect = 1.2f;

        public readonly LayoutKind Kind;
        public readonly Vector2 Reference;
        public readonly float Match;

        public readonly float Margin;
        public readonly float Gap;

        public readonly float HudBarHeight;
        public readonly float HudStripHeight;

        public readonly float InspectorWidth;
        public readonly float SheetHeight;

        public readonly float LogHeight;
        public readonly int LogLines;

        public readonly Vector2 CardSize;
        public readonly int CardColumns;
        public readonly float CardSpacing;

        public readonly float ActionHeight;
        public readonly bool ScrollShop;
        public readonly bool InspectorTwoColumn;
        public readonly bool DenseCards;
        public readonly float ControlBarHeight;

        public readonly Vector2 ModalSize;
        public readonly Vector2 UpgradesSize;

        public bool Compact => Kind != LayoutKind.Wide;
        public bool Landscape => Kind == LayoutKind.CompactLandscape;
        public float HudTotalHeight => HudBarHeight + HudStripHeight + 6f;

        UiLayout(LayoutKind kind)
        {
            Kind = kind;

            if (kind == LayoutKind.CompactLandscape)
            {
                Reference = new Vector2(860f, 390f);
                Match = 1f;

                Margin = 8f;
                Gap = 6f;

                HudBarHeight = 52f;
                HudStripHeight = 22f;

                InspectorWidth = 0f;
                SheetHeight = 300f;

                LogHeight = 40f;
                LogLines = 2;

                CardSize = new Vector2(198f, 106f);
                CardColumns = 4;
                CardSpacing = 8f;

                ActionHeight = 40f;
                ControlBarHeight = 44f;

                ScrollShop = true;
                InspectorTwoColumn = true;
                DenseCards = true;

                ModalSize = new Vector2(620f, 370f);
                UpgradesSize = new Vector2(700f, 356f);
                return;
            }

            if (kind == LayoutKind.Compact)
            {
                Reference = new Vector2(420f, 900f);
                Match = 0f;

                Margin = 10f;
                Gap = 8f;

                HudBarHeight = 96f;
                HudStripHeight = 24f;

                InspectorWidth = 0f;
                SheetHeight = 700f;

                LogHeight = 58f;
                LogLines = 3;

                CardSize = new Vector2(185f, 148f);
                CardColumns = 2;
                CardSpacing = 10f;

                ActionHeight = 42f;
                ControlBarHeight = 56f;

                ScrollShop = false;
                InspectorTwoColumn = false;
                DenseCards = false;

                ModalSize = new Vector2(400f, 620f);
                UpgradesSize = new Vector2(400f, 800f);
                return;
            }

            Reference = new Vector2(1600f, 900f);
            Match = 0.5f;

            Margin = 16f;
            Gap = 12f;

            HudBarHeight = 76f;
            HudStripHeight = 26f;

            InspectorWidth = 344f;
            SheetHeight = 0f;

            LogHeight = 170f;
            LogLines = 8;

            CardSize = new Vector2(224f, 162f);
            CardColumns = 5;
            CardSpacing = 12f;

            ActionHeight = 42f;
            ControlBarHeight = 0f;

            ScrollShop = false;
            InspectorTwoColumn = false;
            DenseCards = false;

            ModalSize = new Vector2(760f, 540f);
            UpgradesSize = new Vector2(820f, 700f);
        }

        public static readonly UiLayout Wide = new UiLayout(LayoutKind.Wide);
        public static readonly UiLayout CompactLayout = new UiLayout(LayoutKind.Compact);
        public static readonly UiLayout LandscapeLayout = new UiLayout(LayoutKind.CompactLandscape);

        public static UiLayout Of(LayoutKind kind)
        {
            switch (kind)
            {
                case LayoutKind.Compact: return CompactLayout;
                case LayoutKind.CompactLandscape: return LandscapeLayout;
                default: return Wide;
            }
        }

        public static LayoutKind KindFor(float cssWidth, float cssHeight)
        {
            if (cssWidth <= 0f || cssHeight <= 0f) return LayoutKind.Wide;
            if (IsPortrait(cssWidth, cssHeight)) return LayoutKind.Compact;

            return cssWidth >= MinWideCssWidth && cssHeight >= MinWideCssHeight
                ? LayoutKind.Wide
                : LayoutKind.CompactLandscape;
        }

        public static bool IsPortrait(float cssWidth, float cssHeight) =>
            cssWidth / cssHeight < MinWideAspect;

        public static bool IsTooSmall(float cssWidth, float cssHeight)
        {
            if (cssWidth <= 0f || cssHeight <= 0f) return false;
            return cssWidth < 300f || cssHeight < 240f;
        }
    }
}

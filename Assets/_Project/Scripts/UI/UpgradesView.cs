using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Ventana modal de mejoras permanentes.</summary>
    public sealed class UpgradesView
    {
        readonly GameSession _session;
        readonly UiLayout _layout;
        readonly RectTransform _root;
        readonly Text _money;
        readonly Row[] _rows;

        /// <summary>Velocidad a la que se volverá al cerrar la ventana.</summary>
        float _speedBeforeOpen = 1f;

        public bool IsOpen => _root.gameObject.activeSelf;

        sealed class Row
        {
            public UpgradeDef Def;
            public Text Name;
            public Text Description;
            public Text Effect;
            public Text Levels;
            public UiButton Buy;
        }

        public UpgradesView(Transform parent, GameSession session, UiLayout layout)
        {
            _session = session;
            _layout = layout;

            bool compact = layout.Compact;
            float pad = compact ? 16f : 28f;

            _root = Ui.NewRect("UpgradesModal", parent);
            Ui.Stretch(_root);

            var dim = Ui.NewPanel("Dim", _root, UiTheme.Overlay, 0);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform);

            var panel = Ui.NewPanel("Panel", _root, UiTheme.Panel, UiTheme.RadiusPanel);
            panel.raycastTarget = true;
            var panelRect = Ui.Center(panel.rectTransform, layout.UpgradesSize.x, layout.UpgradesSize.y);

            var title = Ui.NewText("Title", panelRect,
                compact ? "MEJORAS" : "MEJORAS DE LA INSTALACIÓN", compact ? 18 : 20,
                UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, pad, compact ? 18f : 22f, compact ? 220f : 480f, 26f);

            var subtitle = Ui.NewText("Subtitle", panelRect,
                "Compras permanentes que se mantienen entre turnos.", compact ? 11 : 12,
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            Ui.Place(subtitle.rectTransform, pad, compact ? 44f : 48f, compact ? 360f : 520f, 16f);

            _money = Ui.NewText("Money", panelRect, string.Empty, compact ? 18 : 20, UiTheme.Money,
                TextAnchor.MiddleRight, FontStyle.Bold);
            var moneyRect = _money.rectTransform;
            moneyRect.anchorMin = new Vector2(1f, 1f);
            moneyRect.anchorMax = new Vector2(1f, 1f);
            moneyRect.pivot = new Vector2(1f, 1f);
            moneyRect.anchoredPosition = new Vector2(-pad, compact ? -18f : -22f);
            moneyRect.sizeDelta = new Vector2(compact ? 150f : 240f, 26f);

            float listSide = compact ? 14f : 24f;
            float listTop = compact ? 70f : 78f;
            float listBottom = compact ? 70f : 74f;

            RectTransform list;
            if (layout.ScrollShop)
            {
                var viewport = Ui.NewRect("ListViewport", panelRect);
                Ui.Stretch(viewport, listSide, listSide, listTop, listBottom);
                list = Ui.VScroll("Scroll", viewport);
            }
            else
            {
                list = Ui.NewRect("List", panelRect);
                Ui.Stretch(list, listSide, listSide, listTop, listBottom);
            }
            Ui.VBox(list, 6f);

            var catalog = UpgradeState.Catalog;
            _rows = new Row[catalog.Length];
            for (int i = 0; i < catalog.Length; i++) _rows[i] = BuildRow(list, catalog[i]);

            var close = Ui.NewButton("Close", panelRect,
                compact ? "VOLVER AL RACK" : "VOLVER AL RACK  [Esc]",
                UiTheme.PanelRaised, UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
            var closeRect = close.Rect;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, compact ? 16f : 22f);
            closeRect.sizeDelta = new Vector2(compact ? layout.UpgradesSize.x - 32f : 280f,
                compact ? 46f : 40f);
            close.OnClick(Close);

            _root.gameObject.SetActive(false);
        }

        Row BuildRow(Transform parent, UpgradeDef def)
        {
            bool compact = _layout.Compact;
            bool stacked = compact && !_layout.Landscape;
            float pad = compact ? 12f : 16f;
            float buyWidth = stacked ? 96f : _layout.Landscape ? 140f : 170f;
            float levelsWidth = stacked ? 84f : _layout.Landscape ? 100f : 120f;
            float textWidth = _layout.UpgradesSize.x - pad * 2f - 28f -
                              (stacked ? buyWidth + pad : buyWidth + levelsWidth + 28f);
            float rowHeight = stacked ? 74f : 58f;

            var container = Ui.NewPanel("Row_" + def.Id, parent, UiTheme.PanelDeep, UiTheme.RadiusSmall);
            Ui.Fixed(container, height: rowHeight);
            var rect = container.rectTransform;

            var name = Ui.NewText("Name", rect, def.Name, compact ? 14 : 15, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(name.rectTransform, pad, 7f, textWidth, 18f);

            var description = Ui.NewText("Desc", rect, def.Description, compact ? 10 : 11,
                UiTheme.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, wrap: stacked);
            Ui.Place(description.rectTransform, pad, 25f, textWidth, stacked ? 28f : 15f);

            var effect = Ui.NewText("Effect", rect, string.Empty, compact ? 10 : 11, UiTheme.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(effect.rectTransform, pad, stacked ? 54f : 40f, textWidth, 14f);

            var levels = Ui.NewText("Levels", rect, string.Empty, compact ? 13 : 16, UiTheme.Accent,
                TextAnchor.MiddleRight, FontStyle.Bold);
            var levelsRect = levels.rectTransform;
            levelsRect.anchorMin = new Vector2(1f, stacked ? 1f : 0.5f);
            levelsRect.anchorMax = new Vector2(1f, stacked ? 1f : 0.5f);
            levelsRect.pivot = new Vector2(1f, stacked ? 1f : 0.5f);
            levelsRect.anchoredPosition = stacked
                ? new Vector2(-pad, -8f)
                : new Vector2(-(buyWidth + pad + 14f), 0f);
            levelsRect.sizeDelta = new Vector2(levelsWidth, stacked ? 18f : 20f);

            var buy = Ui.NewButton("Buy", rect, "—", UiTheme.AccentDeep, UiTheme.TextPrimary,
                14, UiTheme.RadiusSmall);
            var buyRect = buy.Rect;
            buyRect.anchorMin = new Vector2(1f, stacked ? 1f : 0.5f);
            buyRect.anchorMax = new Vector2(1f, stacked ? 1f : 0.5f);
            buyRect.pivot = new Vector2(1f, stacked ? 1f : 0.5f);
            buyRect.anchoredPosition = stacked ? new Vector2(-pad, -30f) : new Vector2(-pad, 0f);
            buyRect.sizeDelta = new Vector2(buyWidth, stacked ? 36f : 40f);
            buy.OnClick(() => _session.TryBuyUpgrade(def));

            return new Row { Def = def, Name = name, Description = description, Effect = effect, Levels = levels, Buy = buy };
        }

        public void Open()
        {
            if (IsOpen) return;
            _speedBeforeOpen = _session.Speed;
            _session.SetSpeed(0f);
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _root.gameObject.SetActive(false);
            _session.SetSpeed(_speedBeforeOpen);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Refresh()
        {
            if (!IsOpen) return;

            _money.text = Fmt.Money(_session.Money);

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                int level = _session.Upgrades.Level(row.Def.Id);
                int max = _session.Upgrades.MaxLevelOf(row.Def, _session.Config);
                int cost = _session.Upgrades.NextCost(row.Def, _session.Config);

                row.Levels.text = LevelDots(level, max);
                row.Effect.text = _session.Upgrades.EffectSummary(row.Def.Id, _session.Config);

                if (cost < 0)
                {
                    row.Buy.Label.text = "AL MÁXIMO";
                    row.Buy.SetBaseColor(UiTheme.PanelRaised);
                    row.Buy.Label.color = UiTheme.TextDim;
                    row.Buy.SetInteractable(false);
                }
                else
                {
                    bool affordable = _session.CanBuy(row.Def);
                    row.Buy.Label.text = Fmt.Money(cost);
                    row.Buy.SetBaseColor(affordable ? UiTheme.AccentDeep : UiTheme.PanelRaised);
                    row.Buy.Label.color = affordable ? UiTheme.TextPrimary : UiTheme.TextDim;
                    row.Buy.SetInteractable(affordable);
                }
            }
        }

        static string LevelDots(int level, int max)
        {
            if (max > 6) return level + " / " + max;

            var filled = new System.Text.StringBuilder(max * 2);
            for (int i = 0; i < max; i++) filled.Append(i < level ? "●" : "○");
            return filled.ToString();
        }
    }
}

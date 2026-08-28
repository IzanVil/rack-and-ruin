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

        public UpgradesView(Transform parent, GameSession session)
        {
            _session = session;

            _root = Ui.NewRect("UpgradesModal", parent);
            Ui.Stretch(_root);

            var dim = Ui.NewPanel("Dim", _root, UiTheme.Overlay, 0);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform);

            var panel = Ui.NewPanel("Panel", _root, UiTheme.Panel, UiTheme.RadiusPanel);
            panel.raycastTarget = true;
            var panelRect = Ui.Center(panel.rectTransform, 820f, 700f);

            var title = Ui.NewText("Title", panelRect, "MEJORAS DE LA INSTALACIÓN", 20,
                UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, 28f, 22f, 480f, 26f);

            var subtitle = Ui.NewText("Subtitle", panelRect,
                "Compras permanentes que se mantienen entre turnos.", 12,
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            Ui.Place(subtitle.rectTransform, 28f, 48f, 520f, 16f);

            _money = Ui.NewText("Money", panelRect, string.Empty, 20, UiTheme.Money,
                TextAnchor.MiddleRight, FontStyle.Bold);
            var moneyRect = _money.rectTransform;
            moneyRect.anchorMin = new Vector2(1f, 1f);
            moneyRect.anchorMax = new Vector2(1f, 1f);
            moneyRect.pivot = new Vector2(1f, 1f);
            moneyRect.anchoredPosition = new Vector2(-28f, -22f);
            moneyRect.sizeDelta = new Vector2(240f, 26f);

            var list = Ui.NewRect("List", panelRect);
            Ui.Stretch(list, 24f, 24f, 78f, 74f);
            Ui.VBox(list, 6f);

            var catalog = UpgradeState.Catalog;
            _rows = new Row[catalog.Length];
            for (int i = 0; i < catalog.Length; i++) _rows[i] = BuildRow(list, catalog[i]);

            var close = Ui.NewButton("Close", panelRect, "VOLVER AL RACK  [Esc]",
                UiTheme.PanelRaised, UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
            var closeRect = close.Rect;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeRect.sizeDelta = new Vector2(280f, 40f);
            close.OnClick(Close);

            _root.gameObject.SetActive(false);
        }

        Row BuildRow(Transform parent, UpgradeDef def)
        {
            var container = Ui.NewPanel("Row_" + def.Id, parent, UiTheme.PanelDeep, UiTheme.RadiusSmall);
            Ui.Fixed(container, height: 58f);
            var rect = container.rectTransform;

            var name = Ui.NewText("Name", rect, def.Name, 15, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(name.rectTransform, 16f, 7f, 330f, 18f);

            var description = Ui.NewText("Desc", rect, def.Description, 11, UiTheme.TextMuted,
                TextAnchor.MiddleLeft);
            Ui.Place(description.rectTransform, 16f, 25f, 430f, 15f);

            var effect = Ui.NewText("Effect", rect, string.Empty, 11, UiTheme.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(effect.rectTransform, 16f, 40f, 430f, 14f);

            var levels = Ui.NewText("Levels", rect, string.Empty, 16, UiTheme.Accent,
                TextAnchor.MiddleRight, FontStyle.Bold);
            var levelsRect = levels.rectTransform;
            levelsRect.anchorMin = new Vector2(1f, 0.5f);
            levelsRect.anchorMax = new Vector2(1f, 0.5f);
            levelsRect.pivot = new Vector2(1f, 0.5f);
            levelsRect.anchoredPosition = new Vector2(-200f, 0f);
            levelsRect.sizeDelta = new Vector2(120f, 20f);

            var buy = Ui.NewButton("Buy", rect, "—", UiTheme.AccentDeep, UiTheme.TextPrimary,
                14, UiTheme.RadiusSmall);
            var buyRect = buy.Rect;
            buyRect.anchorMin = new Vector2(1f, 0.5f);
            buyRect.anchorMax = new Vector2(1f, 0.5f);
            buyRect.pivot = new Vector2(1f, 0.5f);
            buyRect.anchoredPosition = new Vector2(-16f, 0f);
            buyRect.sizeDelta = new Vector2(170f, 40f);
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

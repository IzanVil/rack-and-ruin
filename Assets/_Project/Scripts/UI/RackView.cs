using System.Collections.Generic;
using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    public sealed class RackView
    {
        readonly GameSession _session;
        readonly UiLayout _layout;
        readonly System.Action<ServerUnit> _onSelect;
        readonly RectTransform _grid;
        readonly List<ServerCardView> _cards = new List<ServerCardView>();
        readonly List<RectTransform> _emptyBays = new List<RectTransform>();
        readonly Text _subtitle;

        public RackView(Transform parent, GameSession session, UiLayout layout,
            System.Action<ServerUnit> onSelect, System.Action onEmptyBayClick)
        {
            _session = session;
            _layout = layout;
            _onSelect = onSelect;

            bool compact = layout.Compact;
            float pad = compact ? 10f : 16f;
            float headerHeight = compact ? 34f : 44f;

            var panel = Ui.NewPanel("RackPanel", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Stretch(panel.rectTransform);

            var title = Ui.NewText("Title", panel.rectTransform,
                compact ? "RACK" : "RACK PRINCIPAL", compact ? 13 : 15,
                UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, pad + 2f, compact ? 10f : 14f, 300f, 20f);

            _subtitle = Ui.NewText("Subtitle", panel.rectTransform, string.Empty,
                compact ? 11 : 12, UiTheme.TextMuted, TextAnchor.MiddleRight);
            var subRect = _subtitle.rectTransform;
            subRect.anchorMin = new Vector2(1f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(1f, 1f);
            subRect.anchoredPosition = new Vector2(-(pad + 2f), compact ? -10f : -14f);
            subRect.sizeDelta = new Vector2(compact ? 280f : 420f, 20f);

            if (compact)
            {
                var viewport = Ui.NewRect("Viewport", panel.rectTransform);
                Ui.Stretch(viewport, pad, pad, headerHeight, pad);
                _grid = Ui.VScroll("Scroll", viewport);
            }
            else
            {
                _grid = Ui.NewRect("Grid", panel.rectTransform);
                Ui.Stretch(_grid, pad, pad, headerHeight, 14f);
            }

            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = layout.CardSize;
            grid.spacing = new Vector2(layout.CardSpacing, layout.CardSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = layout.CardColumns;

            // bahías libres: muestran cuánto puede crecer el rack y llevan a la tienda
            for (int i = 0; i < session.Config.maxServers; i++)
                _emptyBays.Add(BuildEmptyBay(onEmptyBayClick));

            Rebuild();
        }

        RectTransform BuildEmptyBay(System.Action onClick)
        {
            bool compact = _layout.Compact;

            var slot = Ui.NewPanel("EmptyBay", _grid, UiTheme.PanelDeep, UiTheme.RadiusCard);
            slot.raycastTarget = true;

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            slot.gameObject.AddComponent<PointerCursorHint>().Button = button;

            var label = Ui.NewText("Label", slot.rectTransform, "BAHÍA LIBRE", compact ? 11 : 12,
                UiTheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(label.rectTransform, 0f, 0f, 0f, 22f);

            var hint = Ui.NewText("Hint", slot.rectTransform, "+ instalar servidor",
                compact ? 10 : 11, UiTheme.AccentDeep, TextAnchor.MiddleCenter);
            Ui.Stretch(hint.rectTransform, 0f, 0f, 30f, 0f);

            return slot.rectTransform;
        }

        public void Rebuild()
        {
            for (int i = _cards.Count; i < _session.Rack.Count; i++)
            {
                var card = new ServerCardView(_grid, _session.Rack[i], _onSelect, _layout);
                card.Root.SetSiblingIndex(i);
                _cards.Add(card);
            }

            int free = Mathf.Max(0, _session.Config.maxServers - _session.Rack.Count);
            for (int i = 0; i < _emptyBays.Count; i++)
                _emptyBays[i].gameObject.SetActive(i < free);
        }

        public void Refresh()
        {
            if (_cards.Count != _session.Rack.Count) Rebuild();

            for (int i = 0; i < _cards.Count; i++) _cards[i].Refresh(_session);

            float capacity = _session.Capacity;
            float demand = _session.Demand;
            string headroom = capacity >= demand
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Ok) + ">margen +" +
                  Fmt.Compact(capacity - demand) + " req/s</color>"
                : "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Critical) + ">déficit " +
                  Fmt.Compact(demand - capacity) + " req/s</color>";

            _subtitle.text = _layout.Compact
                ? _session.Rack.OnlineCount + "/" + _session.Rack.Count + " en línea · " + headroom
                : _session.Rack.OnlineCount + " en línea · " + _session.Rack.Count +
                  " instalados · capacidad " + Fmt.Rate(capacity) + " · " + headroom;
        }
    }
}

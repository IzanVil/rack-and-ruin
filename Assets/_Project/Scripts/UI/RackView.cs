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
        readonly RectTransform _grid;
        readonly List<ServerCardView> _cards = new List<ServerCardView>();
        readonly List<RectTransform> _emptyBays = new List<RectTransform>();
        readonly Text _subtitle;

        public RackView(Transform parent, GameSession session, System.Action onEmptyBayClick)
        {
            _session = session;

            var panel = Ui.NewPanel("RackPanel", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Stretch(panel.rectTransform);

            var title = Ui.NewText("Title", panel.rectTransform, "RACK PRINCIPAL", 15,
                UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, 18f, 14f, 300f, 20f);

            _subtitle = Ui.NewText("Subtitle", panel.rectTransform, string.Empty, 12,
                UiTheme.TextMuted, TextAnchor.MiddleRight);
            var subRect = _subtitle.rectTransform;
            subRect.anchorMin = new Vector2(1f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(1f, 1f);
            subRect.anchoredPosition = new Vector2(-18f, -14f);
            subRect.sizeDelta = new Vector2(420f, 20f);

            _grid = Ui.NewRect("Grid", panel.rectTransform);
            Ui.Stretch(_grid, 16f, 16f, 44f, 14f);

            var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(ServerCardView.Width, ServerCardView.Height);
            layout.spacing = new Vector2(12f, 12f);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;

            // bahías libres: muestran cuánto puede crecer el rack y llevan a la tienda
            for (int i = 0; i < session.Config.maxServers; i++)
                _emptyBays.Add(BuildEmptyBay(onEmptyBayClick));

            Rebuild();
        }

        RectTransform BuildEmptyBay(System.Action onClick)
        {
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

            var label = Ui.NewText("Label", slot.rectTransform, "BAHÍA LIBRE", 12, UiTheme.TextDim,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(label.rectTransform, 0f, 0f, 0f, 22f);

            var hint = Ui.NewText("Hint", slot.rectTransform, "+ instalar servidor", 11, UiTheme.AccentDeep,
                TextAnchor.MiddleCenter);
            Ui.Stretch(hint.rectTransform, 0f, 0f, 30f, 0f);

            return slot.rectTransform;
        }

        public void Rebuild()
        {
            for (int i = _cards.Count; i < _session.Rack.Count; i++)
            {
                var card = new ServerCardView(_grid, _session.Rack[i], _session.Select);
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

            _subtitle.text = _session.Rack.OnlineCount + " en línea · " +
                             _session.Rack.Count + " instalados · capacidad " +
                             Fmt.Rate(capacity) + " · " + headroom;
        }
    }
}

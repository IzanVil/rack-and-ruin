
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    public sealed class TooSmallView
    {
        public Canvas Canvas { get; }

        public TooSmallView(Transform parent)
        {
            var go = new GameObject("TooSmallCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 400f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var rect = (RectTransform)go.transform;

            var background = Ui.NewPanel("Background", rect, UiTheme.Background, 0);
            background.raycastTarget = true;
            Ui.Stretch(background.rectTransform);

            var panel = Ui.NewPanel("Panel", rect, UiTheme.Panel, UiTheme.RadiusPanel);
            var panelRect = Ui.Center(panel.rectTransform, 520f, 200f);

            var title = Ui.NewText("Title", panelRect, "LA VENTANA SE HA QUEDADO PEQUEÑA", 22,
                UiTheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Place(title.rectTransform, 28f, 40f, 464f, 30f);

            var body = Ui.NewText("Body", panelRect,
                "Por debajo de 300 × 240 píxeles no hay forma de que el rack se lea. " +
                "Agranda la ventana y el juego se recompone solo.",
                14, UiTheme.TextMuted, TextAnchor.UpperCenter, FontStyle.Normal, wrap: true);
            Ui.Place(body.rectTransform, 28f, 82f, 464f, 80f);

            var note = Ui.NewText("Note", panelRect, "La partida se ha quedado en pausa.", 12,
                UiTheme.TextDim, TextAnchor.MiddleCenter);
            Ui.Place(note.rectTransform, 28f, 156f, 464f, 20f);
        }

        public void Dispose()
        {
            if (Canvas != null) Object.Destroy(Canvas.gameObject);
        }
    }
}

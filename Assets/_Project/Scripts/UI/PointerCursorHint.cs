using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>
    /// En WebGL, uGUI no cambia el cursor del sistema al pasar sobre un botón, así que
    /// el juego parece menos clicable de lo que es. Este componente avisa a la página
    /// (vía UiCursor.jslib) para mostrar la manita mientras el puntero está sobre un
    /// botón interactuable; en el editor y en builds nativas no hace nada, porque el
    /// cursor del sistema ya se comporta como se espera.
    /// </summary>
    public sealed class PointerCursorHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void SgSetPointerCursor(bool isPointer);
#endif
        // Cuenta cuántos botones están bajo el puntero a la vez, para no apagar la
        // manita al salir de uno si el ratón ya está entrando en el siguiente.
        static int _hoverCount;

        public Button Button;
        bool _hovering;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Button != null && !Button.interactable) return;
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData) => SetHover(false);

        void OnDisable() => SetHover(false);

        void SetHover(bool hovering)
        {
            if (hovering == _hovering) return;
            _hovering = hovering;
            _hoverCount = Mathf.Max(0, _hoverCount + (hovering ? 1 : -1));
#if UNITY_WEBGL && !UNITY_EDITOR
            SgSetPointerCursor(_hoverCount > 0);
#endif
        }
    }
}

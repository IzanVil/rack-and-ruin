// Unity WebGL no toca el cursor del sistema operativo: uGUI resalta el botón
// pero el puntero se queda como flecha. Este puente deja que la página (que sí
// tiene acceso al DOM) ponga la manita mientras el ratón está sobre un botón,
// como en cualquier web.
mergeInto(LibraryManager.library, {
  SgSetPointerCursor: function (isPointer) {
    var target = document.querySelector("#unity-canvas") || document.body;
    target.style.cursor = isPointer ? "pointer" : "";
  }
});

// El navegador estrangula requestAnimationFrame en las pestañas que no se ven, así que
// mientras el jugador está en otra pestaña el juego no recibe ni un frame. Al volver,
// Unity entrega de una sola vez todo el tiempo transcurrido: un delta enorme que la
// simulación no puede integrar con la resolución de diseño.
//
// No sirve consultar document.hidden desde Update, porque cuando Update vuelve a correr
// la pestaña ya es visible otra vez. Hace falta un pestillo: la página anota que hubo un
// escondite y el juego lo consume en el primer frame que recibe al volver.
mergeInto(LibraryManager.library, {
  SgWatchPageVisibility: function () {
    if (window.sgVisibilityWatched) return;
    window.sgVisibilityWatched = true;
    window.sgPageWasHidden = 0;
    document.addEventListener("visibilitychange", function () {
      if (document.hidden) window.sgPageWasHidden = 1;
    });
  },

  SgConsumePageWasHidden: function () {
    var wasHidden = window.sgPageWasHidden ? 1 : 0;
    window.sgPageWasHidden = 0;
    return wasHidden;
  }
});

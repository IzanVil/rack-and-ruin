mergeInto(LibraryManager.library, {
  SgWatchViewport: function () {
    if (window.sgViewportWatched) return;
    window.sgViewportWatched = true;

    function measure() {
      var el = document.querySelector("#unity-canvas");
      var rect = el ? el.getBoundingClientRect() : null;
      window.sgViewportW = Math.round(rect && rect.width ? rect.width : window.innerWidth);
      window.sgViewportH = Math.round(rect && rect.height ? rect.height : window.innerHeight);
    }

    measure();
    window.addEventListener("resize", measure);
    window.addEventListener("orientationchange", function () { setTimeout(measure, 150); });
  },

  SgViewportCssWidth: function () {
    return window.sgViewportW || 0;
  },

  SgViewportCssHeight: function () {
    return window.sgViewportH || 0;
  }
});

mergeInto(LibraryManager.library, {
  SgCopyToClipboard: function (ptr) {
    var text = UTF8ToString(ptr);

    function legacy() {
      try {
        var area = document.createElement("textarea");
        area.value = text;
        area.style.position = "fixed";
        area.style.top = "-1000px";
        area.setAttribute("readonly", "");
        document.body.appendChild(area);
        area.select();
        var ok = document.execCommand("copy");
        document.body.removeChild(area);
        return ok;
      } catch (e) {
        return false;
      }
    }

    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(legacy);
        return 1;
      }
    } catch (e) {}

    return legacy() ? 1 : 0;
  }
});

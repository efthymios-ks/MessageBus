// Theme toggle. The pre-paint init lives inline in the Razor _Layout <head> so the correct
// theme is set before any CSS is evaluated. This file wires the click handler on the toolbar
// button and persists the chosen value.
(function () {
    var root = document.documentElement;
    var button = document.getElementById("theme-toggle");
    if (!button) return;

    function setIcon() {
        var icon = document.getElementById("theme-icon");
        if (!icon) return;
        icon.textContent = root.getAttribute("data-theme") === "dark" ? "☾" : "☀";
    }
    setIcon();

    button.addEventListener("click", function () {
        var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
        root.setAttribute("data-theme", next);
        try { localStorage.setItem("ops-theme", next); } catch (_) { /* localStorage disabled */ }
        setIcon();
    });
})();

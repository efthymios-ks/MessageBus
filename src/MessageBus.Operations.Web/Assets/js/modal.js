// Generic modal open/close driven by data attributes.
//   Trigger:   <button data-modal-open="rotate-modal">Rotate</button>
//   Close:     <button data-modal-close>Cancel</button>
// Any element inside a .modal-backdrop with data-modal-close closes the enclosing backdrop.
// Clicking the backdrop itself also closes it. Escape closes whichever modal is open.
(function () {
    document.addEventListener("click", function (e) {
        var opener = e.target.closest("[data-modal-open]");
        if (opener) {
            var id = opener.getAttribute("data-modal-open");
            var target = document.getElementById(id);
            if (target) {
                target.classList.add("open");
                var focusEl = target.querySelector("[autofocus], input, textarea, select");
                if (focusEl) setTimeout(function () { focusEl.focus(); }, 0);
            }
            return;
        }

        var closer = e.target.closest("[data-modal-close]");
        if (closer) {
            var backdrop = closer.closest(".modal-backdrop");
            if (backdrop) backdrop.classList.remove("open");
            return;
        }

        // Clicked directly on the backdrop (not the modal card inside).
        if (e.target.classList && e.target.classList.contains("modal-backdrop")) {
            e.target.classList.remove("open");
        }
    });

    document.addEventListener("keydown", function (e) {
        if (e.key !== "Escape") return;
        var open = document.querySelector(".modal-backdrop.open");
        if (open) open.classList.remove("open");
    });
})();

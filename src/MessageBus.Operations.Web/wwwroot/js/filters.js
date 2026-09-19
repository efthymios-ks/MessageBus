// Auto-submit a filter form on change or debounced typing. The form is server-driven — the JS
// is just there to save a click. Add `data-auto-submit` to the <form> to opt in.
(function () {
    document.querySelectorAll("form[data-auto-submit]").forEach(function (form) {
        var timer = null;
        var submit = function () {
            if (timer) clearTimeout(timer);
            timer = setTimeout(function () { form.submit(); }, 250);
        };

        form.querySelectorAll("input, select").forEach(function (input) {
            if (input.type === "text" || input.type === "search") {
                input.addEventListener("input", submit);
            } else {
                input.addEventListener("change", function () { form.submit(); });
            }
        });
    });
})();

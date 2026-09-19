// Reusable in-app confirm. Intercepts submission on any <form data-confirm ...> and shows the
// global modal in _Layout.cshtml instead of calling window.confirm().
//
// Usage on a form:
//   <form method="post" data-confirm
//         data-confirm-title="Delete this message?"
//         data-confirm-message="Cannot be undone."
//         data-confirm-label="Delete permanently"
//         data-confirm-danger>
//       ...
//   </form>
//
// data-confirm-danger flips the confirm button to the .danger style.
(function () {
    var modal = document.getElementById("global-confirm-modal");
    if (!modal) return;

    var titleEl = document.getElementById("global-confirm-title");
    var messageEl = document.getElementById("global-confirm-message");
    var okBtn = document.getElementById("global-confirm-ok");

    var pendingForm = null;

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || form.tagName !== "FORM") return;
        if (!form.hasAttribute("data-confirm")) return;
        if (form.dataset.confirmProceed === "1") {
            form.dataset.confirmProceed = "";
            return;
        }

        event.preventDefault();

        titleEl.textContent = form.getAttribute("data-confirm-title") || "Confirm";
        messageEl.textContent = form.getAttribute("data-confirm-message") || "Are you sure?";
        okBtn.textContent = form.getAttribute("data-confirm-label") || "Confirm";
        okBtn.classList.toggle("danger", form.hasAttribute("data-confirm-danger"));
        okBtn.classList.toggle("primary", !form.hasAttribute("data-confirm-danger"));
        pendingForm = form;
        modal.classList.add("open");
    });

    okBtn.addEventListener("click", function () {
        if (!pendingForm) {
            modal.classList.remove("open");
            return;
        }
        var form = pendingForm;
        pendingForm = null;
        modal.classList.remove("open");
        // Bypass the interceptor on the next submit.
        form.dataset.confirmProceed = "1";
        form.submit();
    });
})();

// Chat page behaviour. Everything here is an enhancement: the page still works with JavaScript off.
(function () {
    "use strict";

    const form = document.getElementById("chatForm");
    const box = document.getElementById("Input_Question");
    const counter = document.getElementById("questionCount");
    const log = document.getElementById("chatLog");

    // Show the newest message.
    if (log) {
        log.scrollTop = log.scrollHeight;
    }

    // Show times in the visitor's own time zone.
    document.querySelectorAll("time.local-time").forEach(function (el) {
        const d = new Date(el.getAttribute("datetime"));
        if (!isNaN(d)) {
            el.textContent = d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        }
    });

    if (!form || !box) return;

    // Character counter.
    const max = parseInt(counter?.dataset.max || "500", 10);
    function updateCount() {
        if (!counter) return;
        const n = box.value.length;
        counter.textContent = n + " / " + max;
        counter.classList.toggle("near-limit", n > max * 0.9);
    }
    box.addEventListener("input", updateCount);
    updateCount();

    // Enter sends, Shift+Enter adds a new line.
    box.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
            e.preventDefault();
            if (typeof form.requestSubmit === "function") form.requestSubmit();
            else form.submit();
        }
    });

    // Prevent double-sends and show "Thinking…" while the server works.
    function lockWhileSending(f) {
        f.addEventListener("submit", function (e) {
            // jQuery Validate cancels invalid submits; respect that.
            if (window.jQuery && jQuery(f).valid && !jQuery(f).valid()) return;
            if (f.dataset.sending === "true") { e.preventDefault(); return; }
            f.dataset.sending = "true";
            f.querySelectorAll("button[type=submit]").forEach(function (b) {
                b.disabled = true;
                b.classList.add("is-sending");
            });
            document.querySelectorAll(".suggestion").forEach(function (b) { b.disabled = true; });
        });
    }
    lockWhileSending(form);
    document.querySelectorAll(".suggestions form").forEach(lockWhileSending);

    // Re-enable buttons if the user comes back with the browser's Back button.
    window.addEventListener("pageshow", function (e) {
        if (!e.persisted) return;
        document.querySelectorAll("form[data-sending]").forEach(function (f) { delete f.dataset.sending; });
        document.querySelectorAll("button[type=submit]").forEach(function (b) {
            b.disabled = false;
            b.classList.remove("is-sending");
        });
    });
})();

// VAP UNIVERSITY — Campus Event Hub
// Shared public-site behaviour. Page-specific interactions live in their own
// files (for example homepage.js) so this file stays small.

(function () {
    'use strict';

    // In-page anchors: after the browser scrolls, move focus to the target so
    // keyboard and screen-reader users continue from the section they jumped to.
    document.addEventListener('click', function (event) {
        var link = event.target.closest('a[href^="#"]');
        if (!link) {
            return;
        }

        var hash = link.getAttribute('href');
        if (!hash || hash === '#') {
            return;
        }

        var target = document.getElementById(hash.slice(1));
        if (!target) {
            return;
        }

        window.setTimeout(function () {
            if (!target.hasAttribute('tabindex')) {
                target.setAttribute('tabindex', '-1');
            }
            target.focus({ preventScroll: true });
        }, 380);
    });
})();

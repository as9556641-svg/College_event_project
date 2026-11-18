// VAP UNIVERSITY — Campus Event Hub
// Homepage interactions: mobile navigation, category rail controls and the
// institutional tab panel. Progressive enhancement only: every control has a
// working default state without JavaScript.

(function () {
    'use strict';

    var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

    function scrollBehavior() {
        return prefersReducedMotion.matches ? 'auto' : 'smooth';
    }

    /* ---------------------------------------------------------------------
       Mobile navigation
       --------------------------------------------------------------------- */
    function initNavigation() {
        var toggle = document.querySelector('[data-nav-toggle]');
        var panel = document.querySelector('[data-nav-panel]');

        if (!toggle || !panel) {
            return;
        }

        function setOpen(isOpen) {
            panel.classList.toggle('is-open', isOpen);
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
            toggle.setAttribute('aria-label', isOpen ? 'Close menu' : 'Open menu');
        }

        toggle.addEventListener('click', function () {
            setOpen(!panel.classList.contains('is-open'));
        });

        panel.addEventListener('click', function (event) {
            if (event.target.closest('a')) {
                setOpen(false);
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && panel.classList.contains('is-open')) {
                setOpen(false);
                toggle.focus();
            }
        });

        document.addEventListener('click', function (event) {
            if (!panel.contains(event.target) && !toggle.contains(event.target)) {
                setOpen(false);
            }
        });

        window.addEventListener('resize', function () {
            if (window.innerWidth > 1023 && panel.classList.contains('is-open')) {
                setOpen(false);
            }
        });
    }

    /* ---------------------------------------------------------------------
       Horizontal category rail
       --------------------------------------------------------------------- */
    function initRail() {
        document.querySelectorAll('[data-rail]').forEach(function (rail) {
            var section = rail.closest('section');
            if (!section) {
                return;
            }

            var previous = section.querySelector('[data-rail-prev]');
            var next = section.querySelector('[data-rail-next]');
            if (!previous || !next) {
                return;
            }

            function stepSize() {
                var firstItem = rail.querySelector('.hp-rail__item');
                if (!firstItem) {
                    return 296;
                }

                var gap = parseFloat(window.getComputedStyle(rail).columnGap || '18');
                return firstItem.getBoundingClientRect().width + (isNaN(gap) ? 18 : gap);
            }

            function syncControls() {
                var maxScroll = rail.scrollWidth - rail.clientWidth - 2;
                var atStart = rail.scrollLeft <= 2;
                var atEnd = rail.scrollLeft >= maxScroll;

                previous.disabled = atStart;
                next.disabled = atEnd;
                previous.setAttribute('aria-disabled', atStart ? 'true' : 'false');
                next.setAttribute('aria-disabled', atEnd ? 'true' : 'false');
            }

            previous.addEventListener('click', function () {
                rail.scrollBy({ left: -stepSize(), behavior: scrollBehavior() });
            });

            next.addEventListener('click', function () {
                rail.scrollBy({ left: stepSize(), behavior: scrollBehavior() });
            });

            rail.addEventListener('scroll', syncControls, { passive: true });
            window.addEventListener('resize', syncControls);
            syncControls();
        });
    }

    /* ---------------------------------------------------------------------
       Institutional tab panel
       --------------------------------------------------------------------- */
    function initTabs() {
        document.querySelectorAll('[role="tablist"]').forEach(function (tabList) {
            var tabs = Array.prototype.slice.call(tabList.querySelectorAll('[role="tab"]'));
            if (tabs.length === 0) {
                return;
            }

            var vertical = tabList.getAttribute('aria-orientation') === 'vertical';

            function activate(tab, moveFocus) {
                tabs.forEach(function (item) {
                    var pane = document.getElementById(item.getAttribute('aria-controls'));
                    var isActive = item === tab;

                    item.classList.toggle('is-active', isActive);
                    item.setAttribute('aria-selected', isActive ? 'true' : 'false');
                    item.setAttribute('tabindex', isActive ? '0' : '-1');

                    if (pane) {
                        pane.classList.toggle('is-active', isActive);
                        if (isActive) {
                            pane.removeAttribute('hidden');
                        } else {
                            pane.setAttribute('hidden', 'hidden');
                        }
                    }
                });

                if (moveFocus) {
                    tab.focus();
                }
            }

            tabList.addEventListener('click', function (event) {
                var tab = event.target.closest('[role="tab"]');
                if (tab && tabList.contains(tab)) {
                    activate(tab, false);
                }
            });

            tabList.addEventListener('keydown', function (event) {
                var currentIndex = tabs.indexOf(document.activeElement);
                if (currentIndex === -1) {
                    return;
                }

                var nextIndex = null;
                var previousKey = vertical ? 'ArrowUp' : 'ArrowLeft';
                var nextKey = vertical ? 'ArrowDown' : 'ArrowRight';

                if (event.key === nextKey) {
                    nextIndex = (currentIndex + 1) % tabs.length;
                } else if (event.key === previousKey) {
                    nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
                } else if (event.key === 'Home') {
                    nextIndex = 0;
                } else if (event.key === 'End') {
                    nextIndex = tabs.length - 1;
                }

                if (nextIndex !== null) {
                    event.preventDefault();
                    activate(tabs[nextIndex], true);
                }
            });
        });
    }

    function init() {
        initNavigation();
        initRail();
        initTabs();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

/**
 * VAP UNIVERSITY - STUDENT PORTAL SCRIPTS
 * Centralized Navigation Transitions, Branded Loader, Form/Button Loading States,
 * Skeleton Utilities, Responsive Shell, Notification Drawer & SignalR Integration
 */

(function () {
    'use strict';

    // =========================================================================
    // 1. DOM REFERENCES
    // =========================================================================
    const sidebar = document.getElementById('spSidebar');
    const sidebarBackdrop = document.getElementById('spSidebarBackdrop');
    const menuToggle = document.getElementById('spMenuToggle');

    const drawer = document.getElementById('spNotificationDrawer');
    const drawerBackdrop = document.getElementById('spDrawerBackdrop');
    const drawerOpenBtn = document.getElementById('spDrawerOpenBtn');
    const drawerCloseBtn = document.getElementById('spDrawerCloseBtn');
    const unreadCountBadges = document.querySelectorAll('.sp-unread-counter');

    const pageContent = document.getElementById('spPageContent');
    const navProgress = document.getElementById('spNavProgress');
    const navLoader = document.getElementById('spNavLoader');
    const navLoaderText = document.getElementById('spNavLoaderText');

    // =========================================================================
    // 2. ACCESSIBILITY & UTILITY HELPERS
    // =========================================================================
    const DEFAULT_LOADER_TEXT = 'Loading your campus experience...';
    const DOWNLOAD_OR_PRINT_PATTERN = /\/(download|export|print|pdf)(\/|$)/i;
    const FILE_EXT_PATTERN = /\.(pdf|csv|xlsx?|docx?|zip|png|jpe?g|gif|webp|svg)$/i;

    function prefersReducedMotion() {
        return typeof window.matchMedia === 'function' &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function escapeHtml(text) {
        if (!text) return '';
        return String(text).replace(/[&<>"']/g, function (m) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#039;'
            }[m];
        });
    }

    // =========================================================================
    // 3. CENTRALIZED NAVIGATION TRANSITION & UNIVERSITY-BRANDED LOADER
    // =========================================================================
    let isNavigating = false;
    let loaderDelayTimer = null;
    let loaderSafetyTimer = null;
    let navExecuteTimer = null;

    function clearNavTimers() {
        if (loaderDelayTimer) {
            window.clearTimeout(loaderDelayTimer);
            loaderDelayTimer = null;
        }
        if (loaderSafetyTimer) {
            window.clearTimeout(loaderSafetyTimer);
            loaderSafetyTimer = null;
        }
        if (navExecuteTimer) {
            window.clearTimeout(navExecuteTimer);
            navExecuteTimer = null;
        }
    }

    function showLoader(message, options) {
        const opts = options || {};
        const showPill = opts.showPill !== false;
        const delayMs = typeof opts.delayMs === 'number' ? opts.delayMs : 90;

        clearNavTimers();

        if (navProgress) {
            navProgress.classList.add('is-active');
        }

        if (showPill && navLoader) {
            if (navLoaderText) {
                navLoaderText.textContent = message || DEFAULT_LOADER_TEXT;
            }
            const reveal = function () {
                navLoader.classList.add('is-visible');
                navLoader.setAttribute('aria-hidden', 'false');
            };
            if (delayMs <= 0 || prefersReducedMotion()) {
                reveal();
            } else {
                loaderDelayTimer = window.setTimeout(reveal, delayMs);
            }
        }

        // Safety timeout: never allow the loader to stay stuck permanently
        loaderSafetyTimer = window.setTimeout(function () {
            hideLoader();
        }, 10000);
    }

    function hideLoader() {
        clearNavTimers();
        isNavigating = false;

        if (navProgress) {
            navProgress.classList.remove('is-active');
        }
        if (navLoader) {
            navLoader.classList.remove('is-visible');
            navLoader.setAttribute('aria-hidden', 'true');
        }
        if (pageContent) {
            pageContent.classList.remove('sp-page-leaving');
            pageContent.classList.remove('sp-region-loading');
        }
        document.querySelectorAll('.sp-region-loading').forEach(function (el) {
            el.classList.remove('sp-region-loading');
        });
    }

    function startPageTransition(url, message) {
        if (!url || isNavigating) return;
        isNavigating = true;

        // Close mobile overlays cleanly before transition
        if (sidebar && sidebar.classList.contains('show')) {
            toggleSidebar(false);
        }
        if (drawer && drawer.classList.contains('show')) {
            toggleDrawer(false);
        }

        showLoader(message || DEFAULT_LOADER_TEXT, { delayMs: 85 });

        if (prefersReducedMotion() || !pageContent) {
            window.location.assign(url);
            return;
        }

        pageContent.classList.add('sp-page-leaving');

        // Crisp 140ms fade-out before triggering browser navigation
        navExecuteTimer = window.setTimeout(function () {
            window.location.assign(url);
        }, 140);
    }

    function isEligibleNavigationLink(link, event) {
        if (!link || !event) return false;
        if (event.defaultPrevented) return false;
        if (event.button !== 0) return false;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
        if (link.hasAttribute('download') || link.hasAttribute('data-no-transition')) return false;

        const target = (link.getAttribute('target') || '').trim().toLowerCase();
        if (target && target !== '_self') return false;

        if (link.hasAttribute('data-bs-toggle') || link.getAttribute('role') === 'button') {
            return false;
        }

        const rawHref = (link.getAttribute('href') || '').trim();
        if (!rawHref || rawHref === '#' || rawHref.startsWith('#')) return false;
        if (/^(javascript|mailto|tel|blob|data):/i.test(rawHref)) return false;

        let parsedUrl;
        try {
            parsedUrl = new URL(link.href, window.location.href);
        } catch (e) {
            return false;
        }

        if (parsedUrl.protocol !== 'http:' && parsedUrl.protocol !== 'https:') return false;
        if (parsedUrl.origin !== window.location.origin) return false;

        // Skip in-page anchor links on the current URL
        if (parsedUrl.pathname === window.location.pathname &&
            parsedUrl.search === window.location.search &&
            parsedUrl.hash) {
            return false;
        }

        // Skip file download & print routes so browser handles attachments directly
        if (DOWNLOAD_OR_PRINT_PATTERN.test(parsedUrl.pathname) ||
            FILE_EXT_PATTERN.test(parsedUrl.pathname)) {
            return false;
        }

        return true;
    }

    function getNavigationMessage(link) {
        const custom = link.getAttribute('data-loading-text');
        if (custom) return custom;

        const href = (link.getAttribute('href') || '').toLowerCase();
        if (href.includes('/events')) return 'Loading campus events...';
        if (href.includes('/registrations')) return 'Loading your registrations...';
        if (href.includes('/tickets')) return 'Loading admission passes...';
        if (href.includes('/certificates')) return 'Loading academic credentials...';
        if (href.includes('/payments')) return 'Loading payment records...';
        if (href.includes('/feedback')) return 'Loading feedback...';
        if (href.includes('/notifications')) return 'Loading notifications...';
        if (href.includes('/announcements')) return 'Loading announcements...';
        if (href.includes('/dashboard')) return 'Loading student dashboard...';
        return DEFAULT_LOADER_TEXT;
    }

    document.addEventListener('click', function (event) {
        const link = event.target.closest('a[href]');
        if (!link) return;

        if (!isEligibleNavigationLink(link, event)) return;

        event.preventDefault();
        startPageTransition(link.href, getNavigationMessage(link));
    });

    // =========================================================================
    // 4. CENTRALIZED BUTTON & FORM LOADING STATES
    // =========================================================================
    const activeForms = new Set();
    const formResetTimers = new WeakMap();
    const lastClickedSubmitter = new WeakMap();

    function deriveLoadingLabel(button, form) {
        if (button) {
            const explicit = button.getAttribute('data-loading-text');
            if (explicit) return explicit;
        }

        const btnText = (button ? (button.textContent || button.value || '') : '').trim().toLowerCase();
        const action = (form ? (form.getAttribute('action') || '') : '').toLowerCase();

        if (action.includes('logout') || btnText.includes('sign out') || btnText.includes('log out')) {
            return 'Signing out...';
        }
        if (action.includes('cancel') || btnText.includes('cancel')) {
            return 'Cancelling...';
        }
        if (action.includes('register') || btnText.includes('register') || btnText.includes('enroll')) {
            return 'Registering...';
        }
        if (action.includes('claim') || btnText.includes('claim')) {
            return 'Claiming...';
        }
        if (action.includes('verify') || action.includes('payment') || btnText.includes('pay ')) {
            return 'Processing payment...';
        }
        if (btnText.includes('search') || btnText.includes('filter') || btnText.includes('apply')) {
            return 'Searching...';
        }
        if (action.includes('markall') || action.includes('markasread') || btnText.includes('mark')) {
            return 'Updating...';
        }
        if (btnText.includes('save') || btnText.includes('update')) {
            return 'Saving...';
        }
        if (btnText.includes('submit')) {
            return 'Submitting...';
        }
        return 'Processing...';
    }

    function setButtonLoading(button, isLoading, loadingText) {
        if (!button) return;

        if (isLoading) {
            if (button.classList.contains('sp-btn-loading')) return;

            const isInput = button.tagName === 'INPUT';
            if (isInput) {
                button.dataset.spOriginalValue = button.value;
                button.value = loadingText || 'Processing...';
            } else {
                button.dataset.spOriginalHtml = button.innerHTML;
                const currentWidth = button.getBoundingClientRect().width;
                if (currentWidth > 0) {
                    button.dataset.spOriginalMinWidth = button.style.minWidth || '';
                    button.style.minWidth = Math.ceil(currentWidth) + 'px';
                }

                const visibleText = (button.textContent || '').trim();
                const isIconOnly = button.dataset.loadingMode === 'icon' || visibleText.length === 0;

                if (isIconOnly) {
                    button.innerHTML =
                        '<span class="sp-btn-spinner" aria-hidden="true"></span>' +
                        '<span class="visually-hidden">' + escapeHtml(loadingText || 'Processing...') + '</span>';
                } else {
                    button.innerHTML =
                        '<span class="sp-btn-spinner" aria-hidden="true"></span>' +
                        '<span class="ms-1">' + escapeHtml(loadingText || 'Processing...') + '</span>';
                }
            }

            button.classList.add('sp-btn-loading');
            button.setAttribute('aria-busy', 'true');
        } else {
            if (!button.classList.contains('sp-btn-loading')) return;

            const isInput = button.tagName === 'INPUT';
            if (isInput && typeof button.dataset.spOriginalValue !== 'undefined') {
                button.value = button.dataset.spOriginalValue;
                delete button.dataset.spOriginalValue;
            } else if (!isInput && typeof button.dataset.spOriginalHtml !== 'undefined') {
                button.innerHTML = button.dataset.spOriginalHtml;
                delete button.dataset.spOriginalHtml;
                if (typeof button.dataset.spOriginalMinWidth !== 'undefined') {
                    button.style.minWidth = button.dataset.spOriginalMinWidth;
                    delete button.dataset.spOriginalMinWidth;
                }
            }

            button.classList.remove('sp-btn-loading');
            button.removeAttribute('aria-busy');
            button.disabled = false;
        }
    }

    function resetFormState(form) {
        if (!form) return;

        const timer = formResetTimers.get(form);
        if (timer) {
            window.clearTimeout(timer);
            formResetTimers.delete(form);
        }

        delete form.dataset.spSubmitting;
        form.removeAttribute('aria-busy');
        activeForms.delete(form);

        form.querySelectorAll('button, input[type="submit"]').forEach(function (btn) {
            if (btn.classList.contains('sp-btn-loading')) {
                setButtonLoading(btn, false);
            } else if (btn.dataset.spTempDisabled === 'true') {
                btn.disabled = false;
                delete btn.dataset.spTempDisabled;
            }
        });

        const drawerItem = form.closest('.sp-drawer-item');
        if (drawerItem) {
            drawerItem.classList.remove('sp-item-loading');
        }
    }

    function resetAllForms() {
        activeForms.forEach(function (form) {
            resetFormState(form);
        });
    }

    // Track clicked submit button for forms where event.submitter may be unavailable
    document.addEventListener('click', function (event) {
        const submitter = event.target.closest('button[type="submit"], button:not([type]), input[type="submit"]');
        if (!submitter) return;
        const form = submitter.form || submitter.closest('form');
        if (form) {
            lastClickedSubmitter.set(form, submitter);
        }
    }, true);

    // Bubble-phase submit handler: runs AFTER inline onsubmit="return confirm(...)" and jQuery Validate
    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (!form || form.tagName !== 'FORM') return;
        if (form.hasAttribute('data-no-loading')) return;

        // Respect cancelled submissions (e.g., confirm() == false or validation prevented default)
        if (event.defaultPrevented) return;

        // Prevent duplicate submissions while request is in progress
        if (form.dataset.spSubmitting === 'true') {
            event.preventDefault();
            return;
        }

        // Preserve HTML5 validation
        if (!form.noValidate && typeof form.checkValidity === 'function' && !form.checkValidity()) {
            return;
        }

        // Preserve jQuery Unobtrusive Validation when present
        if (window.jQuery && typeof window.jQuery.fn.valid === 'function') {
            const $form = window.jQuery(form);
            if ($form.data('validator') && !$form.valid()) {
                return;
            }
        }

        const submitBtn = event.submitter ||
            lastClickedSubmitter.get(form) ||
            form.querySelector('button[type="submit"]:not(:disabled), input[type="submit"]:not(:disabled)');

        const loadingLabel = deriveLoadingLabel(submitBtn, form);
        const isDrawerForm = Boolean(form.closest('#spNotificationDrawer'));
        const isGetForm = (form.getAttribute('method') || 'get').toLowerCase() === 'get';

        form.dataset.spSubmitting = 'true';
        form.setAttribute('aria-busy', 'true');
        activeForms.add(form);

        if (submitBtn) {
            setButtonLoading(submitBtn, true, loadingLabel);
        }

        // Defer disabling submit buttons by one tick so named button values still serialize in POST body
        window.setTimeout(function () {
            if (form.dataset.spSubmitting !== 'true') return;
            form.querySelectorAll('button[type="submit"], button:not([type]), input[type="submit"]').forEach(function (btn) {
                if (!btn.disabled) {
                    btn.disabled = true;
                    btn.dataset.spTempDisabled = 'true';
                }
            });
        }, 0);

        // Local vs page-level loading indicator
        if (isDrawerForm) {
            const drawerItem = form.closest('.sp-drawer-item');
            if (drawerItem) {
                drawerItem.classList.add('sp-item-loading');
            }
            showLoader(loadingLabel, { showPill: false });
        } else if (!form.hasAttribute('data-no-loader')) {
            const loaderMsg = isGetForm ? 'Updating results...' : loadingLabel;
            showLoader(loaderMsg, { delayMs: 90 });
        }

        // Safety recovery: never permanently lock a button if request fails or stalls
        const safetyTimer = window.setTimeout(function () {
            resetFormState(form);
        }, 15000);
        formResetTimers.set(form, safetyTimer);
    }, false);

    // Handle auto-submitting filter dropdowns/checkboxes (onchange="this.form.submit()")
    document.addEventListener('change', function (event) {
        const control = event.target;
        if (!control) return;
        const onchangeAttr = control.getAttribute('onchange') || '';
        if (!onchangeAttr.includes('submit')) return;

        const form = control.form || control.closest('form');
        if (!form) return;

        showLoader('Updating results...', { delayMs: 60 });
        if (pageContent) {
            pageContent.classList.add('sp-region-loading');
        }
    });

    // =========================================================================
    // 5. BROWSER BACK/FORWARD (BFCACHE) & VISIBILITY RECOVERY
    // =========================================================================
    window.addEventListener('pageshow', function () {
        hideLoader();
        resetAllForms();
    });

    window.addEventListener('pagehide', function () {
        clearNavTimers();
    });

    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible' && isNavigating) {
            hideLoader();
        }
    });

    // =========================================================================
    // 6. REUSABLE SKELETON & LOCAL ASYNC LOADING UTILITIES
    // =========================================================================
    function buildSkeletonHtml(variant, count) {
        const n = Math.max(1, parseInt(count, 10) || 3);
        let html = '<div class="sp-skeleton-group" role="status" aria-live="polite">' +
            '<span class="visually-hidden">Loading content...</span>';

        if (variant === 'event-card') {
            html += '<div class="row g-4">';
            for (let i = 0; i < n; i++) {
                html +=
                    '<div class="col-md-6 col-xl-4">' +
                        '<div class="sp-skeleton-card">' +
                            '<span class="sp-skeleton sp-skeleton-event-banner"></span>' +
                            '<span class="sp-skeleton sp-skeleton-line is-short"></span>' +
                            '<span class="sp-skeleton sp-skeleton-line is-title"></span>' +
                            '<span class="sp-skeleton sp-skeleton-line"></span>' +
                            '<span class="sp-skeleton sp-skeleton-line is-medium mb-0"></span>' +
                        '</div>' +
                    '</div>';
            }
            html += '</div>';
        } else if (variant === 'notification') {
            html += '<div class="d-flex flex-column gap-2">';
            for (let i = 0; i < n; i++) {
                html +=
                    '<div class="sp-skeleton-notif-item">' +
                        '<span class="sp-skeleton sp-skeleton-line is-title" style="height: 14px; width: 62%;"></span>' +
                        '<span class="sp-skeleton sp-skeleton-line"></span>' +
                        '<span class="sp-skeleton sp-skeleton-line is-short mb-0"></span>' +
                    '</div>';
            }
            html += '</div>';
        } else if (variant === 'dashboard-kpi') {
            html += '<div class="row g-3 g-xl-4">';
            for (let i = 0; i < n; i++) {
                html +=
                    '<div class="col-sm-6 col-xl-3">' +
                        '<div class="sp-skeleton-kpi">' +
                            '<span class="sp-skeleton sp-skeleton-circle"></span>' +
                            '<div class="flex-grow-1">' +
                                '<span class="sp-skeleton sp-skeleton-line is-short"></span>' +
                                '<span class="sp-skeleton sp-skeleton-line is-title mb-0" style="width: 45%;"></span>' +
                            '</div>' +
                        '</div>' +
                    '</div>';
            }
            html += '</div>';
        } else {
            html += '<div>';
            for (let i = 0; i < n; i++) {
                html +=
                    '<div class="sp-skeleton-row">' +
                        '<span class="sp-skeleton sp-skeleton-line is-medium mb-0 flex-grow-1"></span>' +
                        '<span class="sp-skeleton sp-skeleton-line is-short mb-0" style="width: 90px;"></span>' +
                    '</div>';
            }
            html += '</div>';
        }

        html += '</div>';
        return html;
    }

    function renderSkeleton(container, variant, count) {
        if (!container) return;
        container.setAttribute('aria-busy', 'true');
        container.innerHTML = buildSkeletonHtml(variant || 'table-row', count || 3);
    }

    function clearSkeleton(container) {
        if (!container) return;
        container.removeAttribute('aria-busy');
        const group = container.querySelector('.sp-skeleton-group');
        if (group) {
            group.remove();
        }
    }

    async function withLocalLoader(container, asyncFn, options) {
        const opts = options || {};
        if (!container || typeof asyncFn !== 'function') return null;

        const previousHtml = container.innerHTML;
        renderSkeleton(container, opts.variant || 'table-row', opts.count || 3);

        try {
            const result = await asyncFn();
            container.removeAttribute('aria-busy');
            return result;
        } catch (err) {
            container.removeAttribute('aria-busy');
            if (opts.restoreOnError !== false) {
                container.innerHTML = previousHtml;
            }
            throw err;
        }
    }

    // Export centralized reusable API
    window.StudentPortalUI = {
        showLoader: showLoader,
        hideLoader: hideLoader,
        startPageTransition: startPageTransition,
        setButtonLoading: setButtonLoading,
        resetFormState: resetFormState,
        renderSkeleton: renderSkeleton,
        clearSkeleton: clearSkeleton,
        withLocalLoader: withLocalLoader
    };

    // =========================================================================
    // 7. RESPONSIVE SIDEBAR & NOTIFICATION DRAWER
    // =========================================================================
    function toggleSidebar(show) {
        if (!sidebar) return;
        const isOpen = typeof show === 'boolean' ? show : !sidebar.classList.contains('show');
        sidebar.classList.toggle('show', isOpen);
        if (sidebarBackdrop) {
            sidebarBackdrop.classList.toggle('show', isOpen);
        }
        document.body.style.overflow = isOpen && window.innerWidth < 992 ? 'hidden' : '';
    }

    if (menuToggle) {
        menuToggle.addEventListener('click', function (e) {
            e.preventDefault();
            toggleSidebar();
        });
    }

    if (sidebarBackdrop) {
        sidebarBackdrop.addEventListener('click', function () {
            toggleSidebar(false);
        });
    }

    function toggleDrawer(show) {
        if (!drawer) return;
        const isOpen = typeof show === 'boolean' ? show : !drawer.classList.contains('show');
        drawer.classList.toggle('show', isOpen);
        if (drawerBackdrop) {
            drawerBackdrop.classList.toggle('show', isOpen);
        }
        document.body.style.overflow = isOpen ? 'hidden' : '';
    }

    if (drawerOpenBtn) {
        drawerOpenBtn.addEventListener('click', function (e) {
            e.preventDefault();
            toggleDrawer(true);
        });
    }

    if (drawerCloseBtn) {
        drawerCloseBtn.addEventListener('click', function (e) {
            e.preventDefault();
            toggleDrawer(false);
        });
    }

    if (drawerBackdrop) {
        drawerBackdrop.addEventListener('click', function () {
            toggleDrawer(false);
        });
    }

    // Keyboard ESC listener
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            if (drawer && drawer.classList.contains('show')) {
                toggleDrawer(false);
            }
            if (sidebar && sidebar.classList.contains('show')) {
                toggleSidebar(false);
            }
        }
    });

    // Drawer Tabs Filtering (dynamic query so SignalR-added items are included)
    const drawerTabBtns = document.querySelectorAll('.sp-drawer-tab-btn');
    const emptyState = document.getElementById('spDrawerEmptyState');
    const emptyStateText = document.getElementById('spDrawerEmptyStateText');

    drawerTabBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            drawerTabBtns.forEach(function (b) { b.classList.remove('active'); });
            btn.classList.add('active');

            const category = btn.getAttribute('data-tab');
            const currentDrawerItems = document.querySelectorAll('.sp-drawer-item');
            let visibleCount = 0;

            currentDrawerItems.forEach(function (item) {
                const itemCategory = item.getAttribute('data-category');
                if (category === 'all' || itemCategory === category) {
                    item.style.display = 'flex';
                    visibleCount++;
                } else {
                    item.style.display = 'none';
                }
            });

            if (emptyState) {
                if (visibleCount === 0) {
                    emptyState.style.display = 'block';
                    if (emptyStateText) {
                        const tabLabel = btn.textContent.trim();
                        emptyStateText.textContent = 'No ' + tabLabel.toLowerCase() + ' notifications found.';
                    }
                } else {
                    emptyState.style.display = 'none';
                }
            }
        });
    });

    // =========================================================================
    // 8. REAL-TIME NOTIFICATIONS & SIGNALR INTEGRATION
    // =========================================================================
    window.updateNotificationBadges = function (count) {
        const parsed = parseInt(count, 10) || 0;
        unreadCountBadges.forEach(function (badge) {
            if (parsed > 0) {
                badge.textContent = parsed > 99 ? '99+' : parsed;
                badge.style.display = 'flex';
            } else {
                badge.style.display = 'none';
            }
        });
    };

    window.addNotificationToDrawer = function (notif) {
        if (!notif) return;
        const list = document.getElementById('spDrawerList');
        if (!list) return;

        if (emptyState) {
            emptyState.style.display = 'none';
        }

        let category = 'all';
        const typeStr = (notif.type || notif.TypeName || '').toString();
        if (typeStr.includes('Announcement')) {
            category = 'announcements';
        } else if (typeStr.includes('Registration') || typeStr.includes('Payment') || typeStr.includes('Ticket')) {
            category = 'registrations';
        } else if (typeStr.includes('Attendance') || typeStr.includes('Certificate')) {
            category = 'approvals';
        }

        const item = document.createElement('div');
        item.className = 'sp-drawer-item unread';
        item.setAttribute('data-category', category);
        item.innerHTML =
            '<div class="sp-drawer-item-top">' +
                '<h6 class="sp-drawer-item-title">' + escapeHtml(notif.title || 'New Notification') + '</h6>' +
                '<span class="sp-drawer-item-time">Just now</span>' +
            '</div>' +
            '<p class="sp-drawer-item-msg">' + escapeHtml(notif.message || '') + '</p>' +
            '<div class="sp-drawer-item-bottom">' +
                (notif.actionUrl ? '<a href="' + escapeHtml(notif.actionUrl) + '" class="small text-primary text-decoration-none fw-semibold">View &rarr;</a>' : '<span></span>') +
                '<span class="badge bg-primary-subtle text-primary border" style="font-size: 0.68rem;">NEW</span>' +
            '</div>';

        list.prepend(item);
    };

    if (typeof signalR !== 'undefined') {
        try {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/notifications')
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.None)
                .build();

            connection.on('UpdateUnreadCount', function (count) {
                window.updateNotificationBadges(count);
            });

            connection.on('ReceiveNotification', function (notification, unreadCount) {
                window.addNotificationToDrawer(notification);
                if (typeof unreadCount === 'number') {
                    window.updateNotificationBadges(unreadCount);
                }
            });

            connection.start().catch(function () {
                // Graceful fallback if unauthenticated or offline
            });
        } catch (e) {
            // SignalR script present but connection setup bypassed
        }
    }
})();

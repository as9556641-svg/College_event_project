/**
 * VAP UNIVERSITY - STUDENT PORTAL SCRIPTS
 * Phase 16.1: Responsive Shell, Notification Drawer & SignalR Integration
 */

(function () {
    'use strict';

    // DOM Elements
    const sidebar = document.getElementById('spSidebar');
    const sidebarBackdrop = document.getElementById('spSidebarBackdrop');
    const menuToggle = document.getElementById('spMenuToggle');

    const drawer = document.getElementById('spNotificationDrawer');
    const drawerBackdrop = document.getElementById('spDrawerBackdrop');
    const drawerOpenBtn = document.getElementById('spDrawerOpenBtn');
    const drawerCloseBtn = document.getElementById('spDrawerCloseBtn');
    const unreadCountBadges = document.querySelectorAll('.sp-unread-counter');

    // Sidebar Toggle
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

    // Notification Drawer Toggle
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

    // Drawer Tabs Filtering
    const drawerTabBtns = document.querySelectorAll('.sp-drawer-tab-btn');
    const drawerItems = document.querySelectorAll('.sp-drawer-item');
    const emptyState = document.getElementById('spDrawerEmptyState');
    const emptyStateText = document.getElementById('spDrawerEmptyStateText');

    drawerTabBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            drawerTabBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const category = btn.getAttribute('data-tab');
            let visibleCount = 0;

            drawerItems.forEach(function (item) {
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
                        emptyStateText.textContent = `No ${tabLabel.toLowerCase()} notifications found.`;
                    }
                } else {
                    emptyState.style.display = 'none';
                }
            }
        });
    });

    // Dynamic Badge Counter Updater
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

    // Prepend Real-time Notification
    window.addNotificationToDrawer = function (notif) {
        if (!notif) return;
        const list = document.getElementById('spDrawerList');
        if (!list) return;

        // Hide empty state if visible
        if (emptyState) {
            emptyState.style.display = 'none';
        }

        // Determine category
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
        item.innerHTML = `
            <div class="sp-drawer-item-top">
                <h6 class="sp-drawer-item-title">${escapeHtml(notif.title || 'New Notification')}</h6>
                <span class="sp-drawer-item-time">Just now</span>
            </div>
            <p class="sp-drawer-item-msg">${escapeHtml(notif.message || '')}</p>
            <div class="sp-drawer-item-bottom">
                ${notif.actionUrl ? `<a href="${escapeHtml(notif.actionUrl)}" class="small text-primary text-decoration-none fw-semibold">View &rarr;</a>` : '<span></span>'}
                <span class="badge bg-primary-subtle text-primary border" style="font-size: 0.68rem;">NEW</span>
            </div>
        `;

        list.prepend(item);
    };

    function escapeHtml(text) {
        if (!text) return '';
        return text.replace(/[&<>"']/g, function (m) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#039;'
            }[m];
        });
    }

    // SignalR Real-time Connection
    if (typeof signalR !== 'undefined') {
        try {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/notifications")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.None)
                .build();

            connection.on("UpdateUnreadCount", function (count) {
                window.updateNotificationBadges(count);
            });

            connection.on("ReceiveNotification", function (notification, unreadCount) {
                window.addNotificationToDrawer(notification);
                if (typeof unreadCount === 'number') {
                    window.updateNotificationBadges(unreadCount);
                }
            });

            connection.start().catch(function (err) {
                // Graceful fallback if unauthenticated or offline
            });
        } catch (e) {
            // SignalR script present but connection setup bypassed
        }
    }
})();

/**
 * College Event & Workshop Registration System
 * Dashboard Shell & Navigation Interactions
 */
document.addEventListener('DOMContentLoaded', function () {
    const sidebar = document.querySelector('.dash-sidebar');
    const toggleBtn = document.querySelector('.dash-sidebar-toggle');
    const backdrop = document.querySelector('.dash-sidebar-backdrop');

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('is-open');
            if (backdrop) {
                backdrop.classList.toggle('is-open');
            }
        });
    }

    if (backdrop && sidebar) {
        backdrop.addEventListener('click', function () {
            sidebar.classList.remove('is-open');
            backdrop.classList.remove('is-open');
        });
    }
});

/**
 * College Event & Workshop Registration System
 * Authentication UI Controller & Interaction Handlers
 */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // 1. Password Visibility Toggle
    const passwordToggles = document.querySelectorAll('.auth-password-toggle');
    passwordToggles.forEach(function (toggle) {
        toggle.addEventListener('click', function () {
            const targetId = this.getAttribute('data-target');
            const targetInput = document.getElementById(targetId);
            if (!targetInput) return;

            const isPassword = targetInput.getAttribute('type') === 'password';
            targetInput.setAttribute('type', isPassword ? 'text' : 'password');

            // Update ARIA
            this.setAttribute('aria-label', isPassword ? 'Hide password' : 'Show password');
            this.setAttribute('title', isPassword ? 'Hide password' : 'Show password');

            // Swap icon SVGs
            const eyeOpen = this.querySelector('.icon-eye-open');
            const eyeClosed = this.querySelector('.icon-eye-closed');
            if (eyeOpen && eyeClosed) {
                if (isPassword) {
                    eyeOpen.style.display = 'none';
                    eyeClosed.style.display = 'block';
                } else {
                    eyeOpen.style.display = 'block';
                    eyeClosed.style.display = 'none';
                }
            }
        });
    });

    const resetTimers = new WeakMap();

    function resetAuthForm(form) {
        if (!form) return;
        const timer = resetTimers.get(form);
        if (timer) {
            window.clearTimeout(timer);
            resetTimers.delete(form);
        }

        delete form.dataset.submitting;
        form.removeAttribute('aria-busy');

        const submitBtn = form.querySelector('.btn-auth-primary, .btn-auth-vap');
        if (submitBtn) {
            submitBtn.classList.remove('is-loading');
            submitBtn.removeAttribute('aria-busy');
            submitBtn.disabled = false;
            const btnText = submitBtn.querySelector('.btn-text');
            if (btnText && typeof submitBtn.dataset.originalText !== 'undefined') {
                btnText.textContent = submitBtn.dataset.originalText;
            }
        }
    }

    // 2. Form submission loading state (validation-aware & duplicate-safe)
    const authForms = document.querySelectorAll('.auth-form');
    authForms.forEach(function (form) {
        form.addEventListener('submit', function (event) {
            if (event.defaultPrevented) {
                return;
            }

            // Prevent duplicate submissions
            if (this.dataset.submitting === 'true') {
                event.preventDefault();
                return;
            }

            // Check jQuery Unobtrusive Validation if active on this form
            if (window.jQuery && typeof window.jQuery.fn.valid === 'function') {
                const $form = window.jQuery(this);
                if ($form.data('validator') && !$form.valid()) {
                    return;
                }
            }

            // Check HTML5 constraint validation when form does not opt out via novalidate
            if (!this.noValidate && typeof this.checkValidity === 'function' && !this.checkValidity()) {
                return;
            }

            const submitBtn = this.querySelector('.btn-auth-primary, .btn-auth-vap');
            this.dataset.submitting = 'true';
            this.setAttribute('aria-busy', 'true');

            if (submitBtn) {
                const btnText = submitBtn.querySelector('.btn-text');
                if (btnText) {
                    if (typeof submitBtn.dataset.originalText === 'undefined') {
                        submitBtn.dataset.originalText = btnText.textContent.trim();
                    }
                    const loadingText = submitBtn.getAttribute('data-loading-text') || 'Signing in...';
                    btnText.textContent = loadingText;
                }
                submitBtn.classList.add('is-loading');
                submitBtn.setAttribute('aria-busy', 'true');

                // Defer disabled state so form submission payload is never interrupted
                window.setTimeout(function () {
                    if (form.dataset.submitting === 'true') {
                        submitBtn.disabled = true;
                    }
                }, 0);
            }

            // Safety recovery if request stalls or fails without page unload
            const timer = window.setTimeout(function () {
                resetAuthForm(form);
            }, 15000);
            resetTimers.set(form, timer);
        });
    });

    // 3. Google Sign-In button submission feedback (preserves provider POST value)
    const googleBtn = document.getElementById('btnGoogleSignIn');
    function resetGoogleBtn() {
        if (!googleBtn) return;
        const googleForm = googleBtn.closest('form');
        if (googleForm) {
            delete googleForm.dataset.submitting;
            googleForm.removeAttribute('aria-busy');
        }
        googleBtn.style.opacity = '';
        googleBtn.removeAttribute('aria-busy');
        googleBtn.disabled = false;
        const span = googleBtn.querySelector('span');
        if (span && typeof googleBtn.dataset.originalText !== 'undefined') {
            span.textContent = googleBtn.dataset.originalText;
        }
    }

    if (googleBtn) {
        const googleForm = googleBtn.closest('form');
        if (googleForm) {
            googleForm.addEventListener('submit', function (event) {
                if (event.defaultPrevented) return;
                if (googleForm.dataset.submitting === 'true') {
                    event.preventDefault();
                    return;
                }

                googleForm.dataset.submitting = 'true';
                googleForm.setAttribute('aria-busy', 'true');
                googleBtn.style.opacity = '0.75';
                googleBtn.setAttribute('aria-busy', 'true');

                const span = googleBtn.querySelector('span');
                if (span) {
                    if (typeof googleBtn.dataset.originalText === 'undefined') {
                        googleBtn.dataset.originalText = span.textContent.trim();
                    }
                    span.textContent = 'Redirecting to Google...';
                }

                // Defer disable so button's name="provider" value="Google" is submitted intact
                window.setTimeout(function () {
                    if (googleForm.dataset.submitting === 'true') {
                        googleBtn.disabled = true;
                    }
                }, 0);

                window.setTimeout(resetGoogleBtn, 15000);
            });
        }
    }

    // 4. Restore buttons cleanly when navigating back via browser history (bfcache)
    window.addEventListener('pageshow', function () {
        authForms.forEach(resetAuthForm);
        resetGoogleBtn();
    });
});

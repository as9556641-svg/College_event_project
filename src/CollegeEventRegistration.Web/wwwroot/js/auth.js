/**
 * College Event & Workshop Registration System
 * Authentication UI Controller & Interaction Handlers
 */
document.addEventListener('DOMContentLoaded', function () {
    // 1. Password Visibility Toggle
    const passwordToggles = document.querySelectorAll('.auth-password-toggle');
    passwordToggles.forEach(toggle => {
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

    // 2. Form submission loading state
    const authForms = document.querySelectorAll('.auth-form');
    authForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            // Check HTML5 validity
            if (!this.checkValidity()) {
                return;
            }

            const submitBtn = this.querySelector('.btn-auth-primary');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.classList.add('is-loading');
                const btnText = submitBtn.querySelector('.btn-text');
                if (btnText) {
                    btnText.setAttribute('data-original-text', btnText.innerText);
                    btnText.innerText = 'Verifying...';
                }
            }
        });
    });

    // 3. Ensure floating labels reflect browser autofilled values
    setTimeout(() => {
        const inputs = document.querySelectorAll('.auth-floating-input');
        inputs.forEach(input => {
            if (input.value && input.value.trim().length > 0) {
                input.dispatchEvent(new Event('input', { bubbles: true }));
            }
        });
    }, 150);
});

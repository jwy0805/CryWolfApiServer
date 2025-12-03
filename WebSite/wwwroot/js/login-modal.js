// wwwroot/js/login-modal.js

(function () {
    function createLoginModal() {
        const loginUi = document.querySelector(".login-ui");
        const modal = document.getElementById("loginModal");
        const modalEmail = document.getElementById("loginModalEmail");
        const modalForm = modal?.querySelector(".login-modal__form");
        const modalSubmit = modal?.querySelector(".modal-submit");
        const modalPassword = modal?.querySelector(".modal-password");
        const modalStatus = document.getElementById("loginModalStatus");
        const closeEls = modal ? modal.querySelectorAll("[data-close-login-modal]") : [];
        
        if (!loginUi || !modal) {
            console.warn("Login modal elements not found.");
            return null;
        }

        const shake = () => {
            loginUi.classList.remove("shake");
            void loginUi.offsetWidth;
            loginUi.classList.add("shake");
        };

        const openModal = (email) => {
            modal.classList.add("is-open");
            modal.setAttribute("aria-hidden", "false");
            modalEmail.textContent = email;
            modalStatus.textContent = "Password entry shown";
            setTimeout(() => modalPassword?.focus(), 50);
        };

        const closeModal = () => {
            modal.classList.remove("is-open");
            modal.setAttribute("aria-hidden", "true");
            modalStatus.textContent = "";
            if (modalPassword) modalPassword.value = "";
        };

        closeEls.forEach((el) => el.addEventListener("click", closeModal));

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && modal.classList.contains("is-open")) {
                closeModal();
            }
        });

        const getEmail = () => modalEmail?.textContent ?? "";
        const getPassword = () => modalPassword?.value ?? "";
        const setStatus = (text) => {
            if (modalStatus) modalStatus.textContent = text;
        };

        return {
            modalForm,
            shake,
            openModal,
            closeModal,
            getEmail,
            getPassword,
            setStatus,
            modalSubmit
        };
    }

    window.CryWolfLoginModal = {
        createLoginModal
    }
})();

// wwwroot/js/login.js
/**
 * @typedef {Object} LoginResponse
 * @property {boolean} success
 * @property {string} userName
 * @property {string} userTag
 * @property {number} userRole
 */

/** @type {LoginResponse} */

document.addEventListener("DOMContentLoaded", () => {
    const {API_BASE_URL, callApiWithRefresh} = window.CryWolfConfig || {};
    const modalApi = window.CryWolfLoginModal?.createLoginModal();

    const loginUi = document.querySelector("[data-login-ui]");
    const loginPanel = loginUi?.querySelector("[data-login-panel]");
    const userPanel = loginUi?.querySelector("[data-user-panel]");
    const nicknameEl = loginUi?.querySelector("[data-user-nickname]");
    const adminLink = loginUi?.querySelector("[data-admin-link]");
    const logoutButton = loginUi?.querySelector("[data-logout-button]");
    const emailInput = loginUi?.querySelector(".login-input");
    const loginButton = loginUi?.querySelector(".btn-login");
    
    const UserRole = Object.freeze({
        User: 0,
        Admin: 1
    })

    if (!loginUi || !loginPanel || !userPanel || !emailInput || !loginButton || !modalApi) {
        console.warn("Login init skipped: missing elements or modalApi.");
        return;
    }

    const {modalForm, modalSubmit, shake, openModal, getEmail, getPassword, setStatus, closeModal} = modalApi;

    const parseUser = (raw) => {
        const userName = raw.userName;
        const userTag = raw.userTag;
        const userRole = raw.userRole;

        return {userName, userTag, userRole};
    };

    const setLoggedInUi = (rawUser) => {
        const {userName, userTag, userRole} = parseUser(rawUser);
        console.log(`userRole ${userRole} detected`);
        if (!loginPanel || !userPanel || !nicknameEl) return;
        loginPanel.hidden = true;
        userPanel.hidden = false;
        nicknameEl.textContent = `${userName} #${userTag}`;
        if (adminLink) adminLink.hidden = userRole !== UserRole.Admin;
        loginUi.classList.remove("shake");
        
        if (userRole === UserRole.Admin) {
            console.log(`${userRole} is logged in`);
        }
    };

    const setLoggedOutUi = () => {
        if (!loginPanel || !userPanel) return;
        userPanel.hidden = true;
        loginPanel.hidden = false;
        if (adminLink) adminLink.hidden = true;
        if (nicknameEl) nicknameEl.textContent = "";
    };

    const logout = async () => {
        if (logoutButton) logoutButton.disabled = true;
        try {
            if (API_BASE_URL) {
                await fetch(`${API_BASE_URL}/api/UserAccount/LogoutFromWeb`, {
                    method: "POST",
                    credentials: "include"
                });
            }
        } catch (error) {
            console.error("Logout error:", error);
        } finally {
            if (logoutButton) logoutButton.disabled = false;
            setLoggedOutUi();
            if (window.location.pathname.toLowerCase() === "/admin") {
                window.location.href = "/";
            }
        }
    };

    const restoreSession = async () => {
        if (!callApiWithRefresh || !API_BASE_URL) return;
        try {
            const response = await callApiWithRefresh(`${API_BASE_URL}/api/UserAccount/KeepInfoFromWeb`);
            if (!response.ok) return;
            const result = await response.json();
            setLoggedInUi(result?.data ?? result);
        } catch (error) {
            console.debug("Session restore skipped:", error);
        }
    };

    loginButton.addEventListener("click", () => {
        const email = emailInput.value.trim();
        const emailRegex = /^(admin|.+@.+\..+)$/;

        if (!email || !emailRegex.test(email)) {
            shake();
            return;
        }

        openModal(email);
    });
    
    emailInput.addEventListener("keydown", (event) => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        loginButton.click();
    });

    const performLogin = async () => {
        const email = getEmail();
        const password = getPassword();

        if (!email || !password) {
            shake();
            return;
        }

        try {
            setStatus("Logging in...");
            loginButton.disabled = true;
            loginButton.classList.add("is-loading");

            const response = await fetch(`${API_BASE_URL}/api/UserAccount/LoginFromWeb`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                credentials: "include",
                body: JSON.stringify({
                    UserAccount: email,
                    Password: password
                })
            });

            if (!response.ok) {
                if (response.status === 401 || response.status === 400) {
                    setStatus("Invalid email or password.");
                } else if (response.status >= 500) {
                    setStatus("Server error. Please try again later.");
                } else {
                    setStatus("Failed to login. Please try again.");
                }
                shake();
                return;
            }
            
            const result = await response.json();
            if (!result.success) {
                setStatus("Failed to login.");
                shake();
                return;
            }

            setStatus("Login successful");
            setLoggedInUi(result?.data ?? result);
            closeModal?.();
        } catch (error) {
            console.error("Network error:", error);
            setStatus("Cannot connect to server (Network error).");
            shake();
        } finally {
            loginButton.disabled = false;
            loginButton.classList.remove("is-loading");
        }

        console.log("Login attempt", {email, password});
    };

    modalForm?.addEventListener("submit", async (event) => {
        event.preventDefault();
        await performLogin();
    });

    modalSubmit?.addEventListener("click", async (event) => {
        event.preventDefault();
        await performLogin();
    });

    logoutButton?.addEventListener("click", async (event) => {
        event.preventDefault();
        await logout();
    });

    window.CryWolfAuthUi = {
        setLoggedIn: (user) => setLoggedInUi(user),
        onSocialLoginSuccess: (user) => setLoggedInUi(user),
        logout,
        reset: setLoggedOutUi
    };

    setLoggedOutUi();
    void restoreSession();
});

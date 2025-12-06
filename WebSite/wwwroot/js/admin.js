// wwwroot/js/admin.js
document.addEventListener("DOMContentLoaded", () => {
    const {API_BASE_URL, callApiWithRefresh} = window.CryWolfConfig || {};
    const messageToggle = document.querySelector("[data-message-toggle]");
    const targetToggle = document.querySelector("[data-target-toggle]");
    const rangeBlock = document.querySelector("[data-range-block]");
    const rangeStart = document.querySelector("[data-range-start]");
    const rangeEnd = document.querySelector("[data-range-end]");
    const addRangeBtn = document.querySelector("[data-add-range]");
    const selectionSummary = document.querySelector("[data-selection-summary]");
    const langToggle = document.querySelector("[data-lang-toggle]");
    const langForms = document.querySelector("[data-lang-forms]");
    const startAtInput = document.querySelector("[data-start-at]");
    const endAtInput = document.querySelector("[data-end-at]");
    const noticeSchedule = document.querySelector("[data-notice-schedule]");
    const sendBtn = document.querySelector("[data-send]");
    const sendStatus = document.querySelector("[data-send-status]");

    const UserRole = Object.freeze({
        User: 0,
        Admin: 1
    });

    const NoticeType = Object.freeze({
        None: 0,
        Notice: 1,
        Event: 2,
        Emergency: 3
    });

    const ensureAdmin = async () => {
        if (!callApiWithRefresh || !API_BASE_URL) {
            window.location.href = "/";
            return;
        }
        try {
            const res = await callApiWithRefresh(`${API_BASE_URL}/api/UserAccount/KeepInfoFromWeb`);
            if (!res.ok) {
                window.location.href = "/";
                return;
            }
            const data = await res.json();
            const role = data?.userRole ?? data?.data?.userRole ?? data?.role ?? data?.Role;
            const isAdmin = role === UserRole.Admin || String(role).toLowerCase() === "admin";
            if (!isAdmin) {
                window.location.href = "/";
            }
        } catch (error) {
            console.error("Admin check failed", error);
            window.location.href = "/";
        }
    };

    if (!messageToggle || !targetToggle || !langToggle || !langForms) return;

    let messageType = "message";
    let targetType = "all";
    const ranges = [];
    const supportedLangs = [
        {code: "ko", label: "한국어"},
        {code: "en", label: "English"},
        {code: "ja", label: "日本語"},
        {code: "vi", label: "Tiếng Việt"}
    ];
    const activeLangs = new Set();

    const setActiveButton = (group, targetAttr, value) => {
        [...group.querySelectorAll(".toggle-btn")].forEach((btn) => {
            const match = btn.getAttribute(targetAttr) === value;
            btn.classList.toggle("is-active", match);
        });
    };

    const renderSummary = () => {
        if (!selectionSummary) return;
        if (targetType === "all") {
            selectionSummary.textContent = "모든 유저에게 전송됩니다.";
            return;
        }

        if (!ranges.length) {
            selectionSummary.textContent = "유저 ID 범위를 추가하세요.";
            return;
        }

        const parts = ranges.map(({start, end}) => `${start} ~ ${end}`);
        selectionSummary.textContent = `${parts.join(", ")} 유저에게 전송됩니다.`;
    };

    const toggleRangeBlock = () => {
        if (!rangeBlock) return;
        rangeBlock.hidden = targetType !== "select";
    };

    const toggleNoticeSchedule = () => {
        if (!noticeSchedule) return;
        noticeSchedule.hidden = messageType !== "notice";
    };

    const createLangToggleButtons = () => {
        langToggle.innerHTML = "";
        supportedLangs.forEach(({code, label}) => {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "toggle-btn";
            btn.textContent = label;
            btn.setAttribute("data-lang", code);
            langToggle.appendChild(btn);
        });
    };

    const ensureLangForm = (code, label) => {
        const existing = langForms.querySelector(`[data-lang-form="${code}"]`);
        if (existing) return existing;

        const wrapper = document.createElement("div");
        wrapper.className = "lang-form";
        wrapper.setAttribute("data-lang-form", code);

        const header = document.createElement("div");
        header.className = "lang-form__header";
        header.innerHTML = `<span>${label}</span><span class="lang-code-pill">${code}</span>`;

        const titleInput = document.createElement("input");
        titleInput.type = "text";
        titleInput.className = "admin-input";
        titleInput.placeholder = `${label} 제목을 입력하세요`;
        titleInput.setAttribute("data-lang-title", code);

        const bodyInput = document.createElement("textarea");
        bodyInput.className = "admin-textarea";
        bodyInput.rows = 4;
        bodyInput.placeholder = `${label} 내용을 입력하세요`;
        bodyInput.setAttribute("data-lang-body", code);

        wrapper.appendChild(header);
        wrapper.appendChild(titleInput);
        wrapper.appendChild(bodyInput);

        langForms.appendChild(wrapper);
        return wrapper;
    };

    const removeLangForm = (code) => {
        const el = langForms.querySelector(`[data-lang-form="${code}"]`);
        if (el) el.remove();
    };

    const toggleLang = (code, label) => {
        if (activeLangs.has(code)) {
            activeLangs.delete(code);
            removeLangForm(code);
        } else {
            activeLangs.add(code);
            ensureLangForm(code, label);
        }

        [...langToggle.querySelectorAll("[data-lang]")].forEach((btn) => {
            const isActive = activeLangs.has(btn.getAttribute("data-lang"));
            btn.classList.toggle("is-active", isActive);
        });
    };

    messageToggle.addEventListener("click", (event) => {
        const btn = event.target.closest("[data-message-type]");
        if (!btn) return;
        const type = btn.getAttribute("data-message-type");
        if (!type) return;
        messageType = type;
        setActiveButton(messageToggle, "data-message-type", type);
        toggleNoticeSchedule();
    });

    targetToggle.addEventListener("click", (event) => {
        const btn = event.target.closest("[data-target-type]");
        if (!btn) return;
        const type = btn.getAttribute("data-target-type");
        if (!type) return;
        targetType = type;
        setActiveButton(targetToggle, "data-target-type", type);
        toggleRangeBlock();
        renderSummary();
    });

    langToggle.addEventListener("click", (event) => {
        const btn = event.target.closest("[data-lang]");
        if (!btn) return;
        const code = btn.getAttribute("data-lang");
        const lang = supportedLangs.find((l) => l.code === code);
        if (!lang) return;
        toggleLang(lang.code, lang.label);
    });

    addRangeBtn?.addEventListener("click", () => {
        const startVal = parseInt(rangeStart?.value, 10);
        const endVal = parseInt(rangeEnd?.value, 10);
        if (Number.isNaN(startVal) || Number.isNaN(endVal)) {
            rangeStart?.focus();
            return;
        }
        const start = Math.min(startVal, endVal);
        const end = Math.max(startVal, endVal);
        ranges.push({start, end});
        if (rangeStart) rangeStart.value = "";
        if (rangeEnd) rangeEnd.value = "";
        rangeStart?.focus();
        renderSummary();
    });

    sendBtn?.addEventListener("click", async () => {
        if (activeLangs.size === 0) {
            sendStatus.textContent = "언어를 선택하세요.";
            return;
        }

        const contents = [];
        for (const code of activeLangs) {
            const titleInput = langForms.querySelector(`[data-lang-title="${code}"]`);
            const bodyInput = langForms.querySelector(`[data-lang-body="${code}"]`);
            const title = titleInput?.value?.trim() || "";
            const body = bodyInput?.value?.trim() || "";
            if (!title) {
                sendStatus.textContent = `${code} 제목을 입력하세요.`;
                titleInput?.focus();
                return;
            }
            if (!body) {
                sendStatus.textContent = `${code} 내용을 입력하세요.`;
                bodyInput?.focus();
                return;
            }
            contents.push({lang: code, title, body});
        }

        if (!API_BASE_URL || !callApiWithRefresh) {
            if (sendStatus) {
                sendStatus.textContent = "API 설정이 올바르지 않습니다.";
            }
            return;
        }

        const targetSummary = targetType === "all"
            ? "모든 유저"
            : ranges.length
                ? ranges.map(({start, end}) => `${start}~${end}`).join(", ")
                : "none";

        const label = messageType === "notice" ? "notice" : "message";
        const schedule = {
            startAt: startAtInput?.value?.trim() || null,
            endAt: endAtInput?.value?.trim() || null
        };
        if (messageType === "notice" && (!schedule.startAt || !schedule.endAt)) {
            sendStatus.textContent = "공지 기간을 설정하세요 (Start At / End At).";
            if (!schedule.startAt) startAtInput?.focus();
            else endAtInput?.focus();
            return;
        }

        try {
            sendBtn.disabled = true;
            if (sendStatus) {
                const scheduleText = messageType === "notice" ? ` (${schedule.startAt} ~ ${schedule.endAt})` : "";
                sendStatus.textContent = `[${label}] ${targetSummary}${scheduleText}에게 전송 중...`;
            }

            let url;
            let payload;

            if (messageType === "notice") {
                url = `${API_BASE_URL}/api/Admin/CreateNotice`;
                const localizations = contents.map(({ lang, title, body }) => ({
                    LanguageCode: lang,
                    Title: title,
                    Content: body
                }));
                
                payload = {
                    NoticeType: NoticeType.Notice,
                    IsPinned: false,
                    StartAt: schedule.startAt,
                    EndAt: schedule.endAt,
                    Localizations: localizations,
                };
            } else {
                url = `${API_BASE_URL}/api/Admin/SendMail`;
                payload = {
                    contents
                };
            }

            const res = await callApiWithRefresh(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                const text = await res.text().catch(() => "");
                console.error("Admin send failed:", res.status, text);
                if (sendStatus) {
                    sendStatus.textContent = `[${label}] 전송 실패 (HTTP ${res.status})`;
                }
                return;
            }

            const result = await res.json().catch(() => ({}));

            if (result.success === false) {
                if (sendStatus) {
                    sendStatus.textContent = `[${label}] 전송 실패: ${result.message || "알 수 없는 오류"}`;
                }
                return;
            }

            if (sendStatus) {
                const langsPart = `langs: ${contents.map((c) => c.lang).join(", ")}`;
                const schedulePart = messageType === "notice" ? ` (${schedule.startAt} ~ ${schedule.endAt})` : "";
                sendStatus.textContent =
                    `[${label}] ${langsPart} ${targetSummary}${schedulePart} 에게 전송 완료`;
            }

            // 성공 후 폼 초기화
            contents.forEach(({lang}) => {
                const titleInput = langForms.querySelector(`[data-lang-title="${lang}"]`);
                const bodyInput = langForms.querySelector(`[data-lang-body="${lang}"]`);
                if (titleInput) titleInput.value = "";
                if (bodyInput) bodyInput.value = "";
            });
            if (startAtInput) startAtInput.value = "";
            if (endAtInput) endAtInput.value = "";
            ranges.length = 0;
            renderSummary();
        } catch (error) {
            console.error("Admin send error:", error);
            if (sendStatus) {
                sendStatus.textContent = `[${label}] 전송 중 오류 발생`;
            }
        } finally {
            sendBtn.disabled = false;
        }
    });

    renderSummary();
    toggleRangeBlock();
    toggleNoticeSchedule();
    createLangToggleButtons();
    const defaultLang = supportedLangs.find((l) => l.code === "ko");
    if (defaultLang) toggleLang(defaultLang.code, defaultLang.label);
    void ensureAdmin();
});

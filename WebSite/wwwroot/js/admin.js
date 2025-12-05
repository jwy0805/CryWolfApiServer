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
    const sendBtn = document.querySelector("[data-send]");
    const sendStatus = document.querySelector("[data-send-status]");
    const messageBody = document.querySelector("[data-message-body]");

    const UserRole = Object.freeze({
        User: 0,
        Admin: 1
    });

    const ensureAdmin = async () => {
        if (!callApiWithRefresh || !API_BASE_URL) {
            window.location.href = "/";
            return;
        }
        try {
            const res = await callApiWithRefresh(`${API_BASE_URL}/api/UserAccount/MeFromWeb`);
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

    if (!messageToggle || !targetToggle) return;

    let messageType = "message";
    let targetType = "all";
    const ranges = [];

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

    messageToggle.addEventListener("click", (event) => {
        const btn = event.target.closest("[data-message-type]");
        if (!btn) return;
        const type = btn.getAttribute("data-message-type");
        if (!type) return;
        messageType = type;
        setActiveButton(messageToggle, "data-message-type", type);
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

    sendBtn?.addEventListener("click", () => {
        if (!messageBody) return;
        const body = messageBody.value.trim();
        if (!body) {
            sendStatus.textContent = "내용을 입력하세요.";
            return;
        }

        const targetSummary = targetType === "all"
            ? "모든 유저"
            : ranges.length
                ? ranges.map(({start, end}) => `${start}~${end}`).join(", ")
                : "대상 미지정";

        sendStatus.textContent = `[${messageType === "notice" ? "공지" : "메시지"}] ${targetSummary} 에게 전송 준비됨`;
        // 실제 API 연동 시 이 부분에서 POST 호출
    });

    renderSummary();
    toggleRangeBlock();
    void ensureAdmin();
});

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
    const messageTitle = document.querySelector("[data-message-title]");
    const startAtInput = document.querySelector("[data-start-at]");
    const endAtInput = document.querySelector("[data-end-at]");
    const noticeSchedule = document.querySelector("[data-notice-schedule]");
    const sendBtn = document.querySelector("[data-send]");
    const sendStatus = document.querySelector("[data-send-status]");
    const messageBody = document.querySelector("[data-message-body]");

    const UserRole = Object.freeze({
        User: 0,
        Admin: 1
    });
    
    const NoticeType = Object.freeze( {
        None : 0,
        Notice : 1,
        Event : 2,
        Emergency : 3
    })

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

    const toggleNoticeSchedule = () => {
        if (!noticeSchedule) return;
        noticeSchedule.hidden = messageType !== "notice";
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
        if (!messageBody) return;
        
        const title = messageTitle ? messageTitle.value.trim() : "";
        const body = messageBody.value.trim();
        if (!title) {
            sendStatus.textContent = "제목을 입력하세요.";
            messageTitle?.focus();
            return;
        }
        
        if (!body) {
            sendStatus.textContent = "내용을 입력하세요.";
            return;
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
        
        try {
            sendBtn.disabled = true;
            if (sendStatus) {
                sendStatus.textContent = `[${label}] ${targetSummary}${messageType === "notice" ? ` (${schedule.startAt} ~ ${schedule.endAt})` : ""}에게 전송 중...`;
            }
            
            let url;
            let payload;
            
            if (messageType === "notice") {
                url = `${API_BASE_URL}/api/Admin/CreateNotice`;
                payload = {
                    noticeType: NoticeType.Notice,
                    title: title,
                    content: body,
                    isPinned: false,
                    startAt: schedule.startAt,
                    endAt: schedule.endAt
                }
            } else {
                url = `${API_BASE_URL}/api/Admin/SendMail`;
                payload = {
                    
                }
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
            
            const result = await res.json().catch(() => ({}))
            
            if (result.success === false) {
                if (sendStatus) {
                    sendStatus.textContent = `[${label}] 전송 실패: ${result.message || "알 수 없는 오류"}`;
                }
            }

            if (sendStatus) {
                const titlePart = title ? `제목: ${title} · ` : "";
                const schedulePart = messageType === "notice" ? ` (${schedule.startAt} ~ ${schedule.endAt})` : "";
                sendStatus.textContent =
                    `[${label}] ${titlePart}${targetSummary}${schedulePart} 에게 전송 완료`;
            }
            
            // 성공 후 폼 초기화
            messageBody.value = "";
            if (messageTitle) messageTitle.value = "";
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
    void ensureAdmin();
});

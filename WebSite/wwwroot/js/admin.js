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
    const eventCard = document.querySelector("[data-event-card]");
    const eventKeyInput = document.querySelector("[data-event-key]");
    const eventTierList = document.querySelector("[data-event-tier-list]");
    const addTierBtn = document.querySelector("[data-add-tier]");

    const UserRole = Object.freeze({
        User: 0,
        Admin: 1
    });

    const setAdminUiEnabled = (enabled) => {
        const disabled = !enabled;

        // Buttons
        if (sendBtn) sendBtn.disabled = disabled;
        if (addTierBtn) addTierBtn.disabled = disabled;
        if (addRangeBtn) addRangeBtn.disabled = disabled;

        // Prevent interaction on the rest of the UI until admin check completes
        if (messageToggle) messageToggle.style.pointerEvents = enabled ? "" : "none";
        if (targetToggle) targetToggle.style.pointerEvents = enabled ? "" : "none";
        if (langToggle) langToggle.style.pointerEvents = enabled ? "" : "none";
        if (langForms) langForms.style.pointerEvents = enabled ? "" : "none";
        if (rangeBlock) rangeBlock.style.pointerEvents = enabled ? "" : "none";
        if (noticeSchedule) noticeSchedule.style.pointerEvents = enabled ? "" : "none";
        if (eventCard) eventCard.style.pointerEvents = enabled ? "" : "none";

        if (sendStatus && !enabled) sendStatus.textContent = "관리자 확인 중...";
    };

    function toUtcIsoOrNull(datetimeLocalValue) {
        const v = (datetimeLocalValue || "").trim();
        if (!v) return null;

        // "YYYY-MM-DDTHH:mm" 또는 "YYYY-MM-DDTHH:mm:ss"
        const m = v.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/);
        if (!m) return null;

        const year = Number(m[1]);
        const month = Number(m[2]); // 1~12
        const day = Number(m[3]);
        const hour = Number(m[4]);
        const minute = Number(m[5]);
        const second = m[6] ? Number(m[6]) : 0;

        // 로컬 타임존 기준 Date 생성
        const d = new Date(year, month - 1, day, hour, minute, second, 0);
        if (Number.isNaN(d.getTime())) return null;

        return d.toISOString();
    }
    
    const ensureAdmin = async () => {
        setAdminUiEnabled(false);
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
            } else {
                setAdminUiEnabled(true);
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
    const counterKeys = ["friendly_match", "single_play_win", "first_purchase"];

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
        noticeSchedule.hidden = messageType === "message";
    };

    const toggleEventCard = () => {
        if (!eventCard) return;
        eventCard.hidden = messageType !== "event";
    };

    const createRewardRow = () => {
        const row = document.createElement("div");
        row.className = "reward-row";
        row.setAttribute("data-reward-row", "true");
        row.innerHTML = `
            <label class="reward-field">
                <span>Product ID</span>
                <input type="number" class="admin-input" placeholder="예: 12001" min="1" data-reward-product-id />
            </label>
            <label class="reward-field">
                <span>타입</span>
                <select class="admin-input" data-reward-product-type>
                    <option value="">선택</option>
                    <option value="0">Container</option>
                    <option value="1">Unit</option>
                    <option value="2">Material</option>
                    <option value="3">Enchant</option>
                    <option value="4">Sheep</option>
                    <option value="5">Character</option>
                    <option value="6">Gold</option>
                    <option value="7">Spinel</option>
                    <option value="8">Exp</option>
                    <option value="9">Subscription</option>
                </select>
            </label>
            <label class="reward-field">
                <span>수량</span>
                <input type="number" class="admin-input" placeholder="예: 1" min="1" data-reward-count />
            </label>
            <button type="button" class="btn-remove-reward" data-remove-reward>삭제</button>
        `;
        return row;
    };

    const createEventTierRow = (tierNumber) => {
        const tier = document.createElement("div");
        tier.className = "event-tier";
        tier.setAttribute("data-event-tier", "true");

        const header = document.createElement("div");
        header.className = "event-tier__header";
        header.innerHTML = `
            <span>Tier</span>
            <input type="number" class="admin-input" min="1" value="${tierNumber}" data-tier-number />
            <button type="button" class="btn-remove-tier" data-remove-tier>삭제</button>
        `;

        const body = document.createElement("div");
        body.className = "event-tier__body";
        body.innerHTML = `
            <label class="reward-field">
                <span>조건 종류</span>
                <select class="admin-input" data-condition-type>
                    <option value="counter">카운터 목표</option>
                </select>
            </label>
            <label class="reward-field">
                <span>counterKey</span>
                <select class="admin-input" data-counter-key>
                    <option value="">선택</option>
                    ${counterKeys.map((key) => `<option value="${key}">${key}</option>`).join("")}
                    <option value="__custom__">직접 입력</option>
                </select>
            </label>
            <label class="reward-field" data-counter-key-custom-wrap hidden>
                <span>counterKey 직접 입력</span>
                <input type="text" class="admin-input" placeholder="예: custom_counter_key" data-counter-key-custom />
            </label>
            <label class="reward-field">
                <span>목표값 (value)</span>
                <input type="number" class="admin-input" min="1" placeholder="예: 2" data-condition-value />
            </label>
        `;

        const rewards = document.createElement("div");
        rewards.className = "event-tier__rewards";

        const list = document.createElement("div");
        list.className = "reward-list";
        list.setAttribute("data-tier-reward-list", "true");
        list.appendChild(createRewardRow());

        const addReward = document.createElement("button");
        addReward.type = "button";
        addReward.className = "btn-add-reward";
        addReward.textContent = "보상 추가";
        addReward.setAttribute("data-add-tier-reward", "true");

        rewards.appendChild(list);
        rewards.appendChild(addReward);

        tier.appendChild(header);
        tier.appendChild(body);
        tier.appendChild(rewards);
        return tier;
    };

    const resetEventTiers = () => {
        if (!eventTierList) return;
        eventTierList.innerHTML = "";
        eventTierList.appendChild(createEventTierRow(1));
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
        toggleEventCard();
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

    eventTierList?.addEventListener("click", (event) => {
        const addTierRewardBtn = event.target.closest("[data-add-tier-reward]");
        if (addTierRewardBtn) {
            const tier = addTierRewardBtn.closest("[data-event-tier]");
            const list = tier?.querySelector("[data-tier-reward-list]");
            if (list) list.appendChild(createRewardRow());
            return;
        }

        const removeRewardBtn = event.target.closest("[data-remove-reward]");
        if (removeRewardBtn) {
            const row = removeRewardBtn.closest("[data-reward-row]");
            const list = removeRewardBtn.closest("[data-tier-reward-list]");
            const rows = list?.querySelectorAll("[data-reward-row]") || [];
            if (rows.length <= 1) {
                row?.querySelectorAll("input, select").forEach((el) => {
                    if (el instanceof HTMLInputElement || el instanceof HTMLSelectElement) el.value = "";
                });
                return;
            }
            row?.remove();
            return;
        }

        const removeTierBtn = event.target.closest("[data-remove-tier]");
        if (removeTierBtn) {
            const tiers = eventTierList.querySelectorAll("[data-event-tier]");
            const tier = removeTierBtn.closest("[data-event-tier]");
            if (tiers.length <= 1) {
                resetEventTiers();
                return;
            }
            tier?.remove();
        }
    });

    eventTierList?.addEventListener("change", (event) => {
        const select = event.target.closest("[data-counter-key]");
        if (!select) return;
        const tier = select.closest("[data-event-tier]");
        const wrap = tier?.querySelector("[data-counter-key-custom-wrap]");
        if (!wrap) return;
        wrap.hidden = select.value !== "__custom__";
        if (select.value !== "__custom__") {
            const input = wrap.querySelector("[data-counter-key-custom]");
            if (input instanceof HTMLInputElement) input.value = "";
        }
    });

    addTierBtn?.addEventListener("click", () => {
        if (!eventTierList) return;
        const tiers = eventTierList.querySelectorAll("[data-event-tier]");
        const nextTierNumber = tiers.length + 1;
        eventTierList.appendChild(createEventTierRow(nextTierNumber));
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

    const saveEvent = async () => {
        if (!eventKeyInput || !startAtInput || !endAtInput || !eventTierList) return;

        const eventKey = eventKeyInput.value.trim();
        if (!eventKey) {
            if (sendStatus) sendStatus.textContent = "Event Key를 입력하세요.";
            eventKeyInput.focus();
            return;
        }

        // 이벤트 공지(Localizations)도 같이 발행하므로, 언어/텍스트 필수
        if (activeLangs.size === 0) {
            if (sendStatus) sendStatus.textContent = "언어를 선택하세요.";
            return;
        }

        const localizations = [];
        for (const code of activeLangs) {
            const titleInput = langForms?.querySelector(`[data-lang-title="${code}"]`);
            const bodyInput = langForms?.querySelector(`[data-lang-body="${code}"]`);
            const title = titleInput?.value?.trim() || "";
            const body = bodyInput?.value?.trim() || "";
            if (!title) {
                if (sendStatus) sendStatus.textContent = `${code} 제목을 입력하세요.`;
                titleInput?.focus();
                return;
            }
            if (!body) {
                if (sendStatus) sendStatus.textContent = `${code} 내용을 입력하세요.`;
                bodyInput?.focus();
                return;
            }
            localizations.push({
                LanguageCode: code,
                Title: title,
                Content: body
            });
        }

        const startAt = toUtcIsoOrNull(startAtInput.value);
        const endAt = toUtcIsoOrNull(endAtInput.value);
        if (!startAt || !endAt) {
            if (sendStatus) sendStatus.textContent = "기간(Start/End)을 입력하세요.";
            if (!startAt) startAtInput.focus();
            else endAtInput.focus();
            return;
        }
        if (new Date(endAt).getTime() <= new Date(startAt).getTime()) {
            if (sendStatus) sendStatus.textContent = "End At은 Start At 이후여야 합니다.";
            endAtInput.focus();
            return;
        }

        const tiers = [];
        const seenTierNumbers = new Set();
        const tierNodes = eventTierList.querySelectorAll("[data-event-tier]");
        for (const tier of tierNodes) {
            const tierNumberInput = tier.querySelector("[data-tier-number]");
            const conditionTypeSelect = tier.querySelector("[data-condition-type]");
            const counterKeySelect = tier.querySelector("[data-counter-key]");
            const counterKeyCustomInput = tier.querySelector("[data-counter-key-custom]");
            const valueInput = tier.querySelector("[data-condition-value]");

            const tierNumber = parseInt(tierNumberInput?.value, 10);
            const conditionType = conditionTypeSelect?.value || "";
            const selectedKey = counterKeySelect?.value || "";
            const customKey = counterKeyCustomInput?.value?.trim() || "";
            const counterKey = selectedKey === "__custom__" ? customKey : selectedKey;
            const value = parseInt(valueInput?.value, 10);

            if (Number.isNaN(tierNumber) || tierNumber <= 0) {
                if (sendStatus) sendStatus.textContent = "Tier 번호를 입력하세요.";
                tierNumberInput?.focus();
                return;
            }
            if (seenTierNumbers.has(tierNumber)) {
                if (sendStatus) sendStatus.textContent = `Tier 번호가 중복되었습니다: ${tierNumber}`;
                tierNumberInput?.focus();
                return;
            }
            seenTierNumbers.add(tierNumber);
            if (!conditionType) {
                if (sendStatus) sendStatus.textContent = "조건 종류를 선택하세요.";
                conditionTypeSelect?.focus();
                return;
            }
            if (!counterKey) {
                if (sendStatus) sendStatus.textContent = "counterKey를 선택/입력하세요.";
                if (selectedKey === "__custom__") counterKeyCustomInput?.focus();
                else counterKeySelect?.focus();
                return;
            }
            if (Number.isNaN(value) || value <= 0) {
                if (sendStatus) sendStatus.textContent = "목표값(value)을 입력하세요.";
                valueInput?.focus();
                return;
            }

            const rewardRows = tier.querySelectorAll("[data-reward-row]");
            const rewards = [];
            for (const row of rewardRows) {
                const productIdInput = row.querySelector("[data-reward-product-id]");
                const productTypeSelect = row.querySelector("[data-reward-product-type]");
                const countInput = row.querySelector("[data-reward-count]");
                const productId = parseInt(productIdInput?.value, 10);
                const productType = parseInt(productTypeSelect?.value, 10);
                const count = parseInt(countInput?.value, 10);
                if (Number.isNaN(productId) || productId <= 0) {
                    if (sendStatus) sendStatus.textContent = "보상 Product ID를 입력하세요.";
                    productIdInput?.focus();
                    return;
                }
                if (Number.isNaN(productType)) {
                    if (sendStatus) sendStatus.textContent = "보상 타입을 선택하세요.";
                    productTypeSelect?.focus();
                    return;
                }
                if (Number.isNaN(count) || count <= 0) {
                    if (sendStatus) sendStatus.textContent = "보상 수량을 입력하세요.";
                    countInput?.focus();
                    return;
                }
                rewards.push({
                    ItemId: productId,
                    ProductType: productType,
                    Count: count
                });
            }

            const conditionJson = JSON.stringify({
                type: conditionType,
                counterKey,
                value
            });
            const rewardJson = JSON.stringify(rewards); // 문자열화

            tiers.push({
                Tier: tierNumber,
                ConditionJson: conditionJson,
                RewardJson: rewardJson, // RewardJson 필드 사용
                MinEventVersion: 1,
                MaxEventVersion: null
            });
        }

        if (!API_BASE_URL || !callApiWithRefresh) {
            if (sendStatus) sendStatus.textContent = "API 설정이 올바르지 않습니다.";
            return;
        }

        try {
            if (sendBtn) sendBtn.disabled = true;
            if (sendStatus) sendStatus.textContent = `[event] ${eventKey} 발행 중...`;

            const res = await callApiWithRefresh(`${API_BASE_URL}/api/Admin/PublishEvent`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    EventKey: eventKey,
                    StartAt: startAt,
                    EndAt: endAt,
                    RepeatType: 0,
                    RepeatTimeZone: "UTC",
                    Version: 1,
                    Tiers: tiers,
                    IsPinned: false,
                    Localizations: localizations
                })
            });

            if (!res.ok) {
                const text = await res.text().catch(() => "");
                console.error("Event publish failed:", res.status, text);
                if (sendStatus) sendStatus.textContent = `[event] 발행 실패 (HTTP ${res.status})`;
                return;
            }

            const result = await res.json().catch(() => ({}));
            if (result.success === false) {
                if (sendStatus) sendStatus.textContent = `[event] 발행 실패: ${result.message || "알 수 없는 오류"}`;
                return;
            }

            if (sendStatus) sendStatus.textContent = `[event] ${eventKey} 발행 완료`;

            // 성공 후 폼 초기화(이벤트도 동일)
            for (const code of activeLangs) {
                const titleInput = langForms?.querySelector(`[data-lang-title="${code}"]`);
                const bodyInput = langForms?.querySelector(`[data-lang-body="${code}"]`);
                if (titleInput) titleInput.value = "";
                if (bodyInput) bodyInput.value = "";
            }
            eventKeyInput.value = "";
            startAtInput.value = "";
            endAtInput.value = "";
            resetEventTiers();
        } catch (error) {
            console.error("Event publish error:", error);
            if (sendStatus) sendStatus.textContent = "[event] 발행 중 오류 발생";
        } finally {
            if (sendBtn) sendBtn.disabled = false;
        }
    };

    sendBtn?.addEventListener("click", async () => {
        if (messageType === "event") {
            await saveEvent();
            return;
        }

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
            startAt: toUtcIsoOrNull(startAtInput?.value),
            endAt: toUtcIsoOrNull(endAtInput?.value)
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
                url = `${API_BASE_URL}/api/Admin/PublishNotice`;
                const localizations = contents.map(({ lang, title, body }) => ({
                    LanguageCode: lang,
                    Title: title,
                    Content: body
                }));
                payload = { IsPinned: false, StartAt: schedule.startAt, EndAt: schedule.endAt, Localizations: localizations };
            }
            else {
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
    toggleEventCard();
    resetEventTiers();
    createLangToggleButtons();
    const defaultLang = supportedLangs.find((l) => l.code === "ko");
    if (defaultLang) toggleLang(defaultLang.code, defaultLang.label);
    setAdminUiEnabled(false);
    void ensureAdmin();
});

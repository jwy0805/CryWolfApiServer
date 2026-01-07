// wwwroot/js/config.js

(function () {
    const IS_LOCAL =
        window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1";

    const API_BASE_URL = IS_LOCAL
        ? "https://localhost:7270"
        : "https://www.hamonstudio.net";
    
    async function callApiWithRefresh(url, options = {}) {
        const opts = {
            ...options,
            credentials: "include"
        };

        let response = await fetch(url, opts);

        if (response.status === 401) {
            // access_token 만료 가능성 → refresh 시도
            const refreshRes = await fetch(`${API_BASE_URL}/api/UserAccount/RefreshFromWeb`, {
                method: "POST",
                credentials: "include"
            });

            if (!refreshRes.ok) {
                // refresh 실패 → 로그인 화면으로 보내는 등
                return response;
            }

            // refresh 성공 → 원래 요청 재시도
            response = await fetch(url, opts);
        }

        return response;
    }

    window.CryWolfConfig = {
        IS_LOCAL,
        API_BASE_URL,
        callApiWithRefresh
    };
})();
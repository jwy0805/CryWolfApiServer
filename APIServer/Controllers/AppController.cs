using System;
using ApiServer.DB;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppController : ControllerBase
{
    private const string LatestVersion = "1.0.2";
    private const string MinSupportedAndroid = "1.0.2";
    private const string MinSupportedIos = "1.0.2";

    private const string AndroidStoreUrl = "https://play.google.com/store/apps/details?id=com.hamonstudio.crywolf";
    private const string IosStoreUrl = "https://apps.apple.com/app/id6745862935";

    [HttpGet("check")]
    public IActionResult CheckUpdate([FromQuery] string platform, [FromQuery] string current)
    {
        platform = NormalizePlatform(platform);
        current = (current ?? string.Empty).Trim();

        var minVersion = platform switch
        {
            "android" => MinSupportedAndroid,
            "ios" => MinSupportedIos,
            _ => null
        };

        if (minVersion is null)
        {
            return BadRequest(new
            {
                code = "INVALID_PLATFORM",
                message = "platform must be 'android' or 'ios'"
            });
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return BadRequest(new
            {
                code = "INVALID_CURRENT_VERSION",
                message = "current is required (e.g. 1.0.1)"
            });
        }

        var force = CompareVersion(current, minVersion) < 0;
        var needUpdate = CompareVersion(current, LatestVersion) < 0;
        var lang2 = GetLang2();

        return Ok(new AppCheckResponse
        {
            CurrentVersion = current,
            MinVersion = minVersion,
            LatestVersion = LatestVersion,
            NeedUpdate = needUpdate,
            Force = force,
            StoreUrl = platform == "android" ? AndroidStoreUrl : IosStoreUrl,
            Message = GetMessage(force, needUpdate, lang2)
        });
    }

    private static string NormalizePlatform(string platform)
    {
        var p = (platform ?? string.Empty).Trim().ToLowerInvariant();

        if (p is "android" or "aos") return "android";
        if (p.Contains("iphone") || p.Contains("ios") || p.Contains("ipados")) return "ios";

        return p;
    }

    private string GetLang2()
    {
        var raw = Request.Headers["Accept-Language"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
            raw = Request.Headers["X-Language"].ToString();

        raw = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw)) return "en";

        // "ko-KR,ko;q=0.9,en-US;q=0.8" -> first tag -> "ko-kr" -> "ko"
        var first = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        var tag = first.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        var lang2 = tag.Split('-', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        return lang2 switch
        {
            "ko" => "ko",
            "en" => "en",
            "ja" => "ja",
            "vi" => "vi",
            _ => "en"
        };
    }

    private static string GetMessage(bool force, bool needUpdate, string lang2)
    {
        return lang2 switch
        {
            "ko" => force
                ? "업데이트가 필요합니다. 업데이트 후 이용해주세요."
                : "새 버전이 있습니다. 업데이트를 권장합니다.",

            "en" => force
                ? "An update is required. Please update to continue."
                : "A new version is available. We recommend updating.",

            "ja" => force
                ? "アップデートが必要です。更新してからご利用ください。"
                : "新しいバージョンがあります。アップデートを推奨します。",

            "vi" => force
                ? "Bạn cần cập nhật. Vui lòng cập nhật để tiếp tục."
                : "Có phiên bản mới. Chúng tôi khuyến nghị bạn cập nhật.",

            _ => force
                ? "An update is required. Please update to continue."
                : "A new version is available. We recommend updating.",
        };
    }

    // SemVer-ish 비교
    private static int CompareVersion(string a, string b)
    {
        int[] A = ParseVersion(a);
        int[] B = ParseVersion(b);

        int n = Math.Max(A.Length, B.Length);
        for (int i = 0; i < n; i++)
        {
            int av = i < A.Length ? A[i] : 0;
            int bv = i < B.Length ? B[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }

    private static int[] ParseVersion(string v)
    {
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var arr = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            int end = 0;
            while (end < p.Length && char.IsDigit(p[end])) end++;
            arr[i] = int.TryParse(end == 0 ? "0" : p[..end], out var num) ? num : 0;
        }
        return arr;
    }
}
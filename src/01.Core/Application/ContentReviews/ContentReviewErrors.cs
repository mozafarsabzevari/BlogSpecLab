using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>Messages never include post content or model output.</summary>
internal static class ContentReviewErrors
{
    public static Error NotAvailable(PostStatus status) =>
        new(
            ErrorCode.ReviewNotAvailable,
            "بررسی هوشمند فقط برای پیش‌نویس و پست بازگشت‌خورده در دسترس است.",
            status.ToString());

    public static Error OutputInvalid() =>
        new(ErrorCode.ReviewOutputInvalid, "سرویس بررسی هوشمند نتیجهٔ معتبری برنگرداند. دوباره تلاش کنید.");

    public static Error Refused() =>
        new(ErrorCode.ReviewRefused, "سرویس بررسی هوشمند این پست را بررسی نکرد.");

    public static Error ServiceUnavailable() =>
        new(ErrorCode.ReviewServiceUnavailable, "سرویس بررسی هوشمند در دسترس نیست. بعداً دوباره تلاش کنید.");

    public static Error TimedOut() =>
        new(ErrorCode.ReviewTimedOut, "سرویس بررسی هوشمند در زمان مقرر پاسخ نداد. بعداً دوباره تلاش کنید.");
}

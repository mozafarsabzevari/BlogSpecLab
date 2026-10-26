<div dir="rtl">

# قرارداد HTTP API: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `001-blog-post-management` |
| تاریخ | 2026-10-19 |
| طرح | [plan.md](../plan.md) |

## قواعد کلی

- قالب داده JSON است و نام فیلدها به سبک camelCase نوشته می‌شود.
- زمان‌ها با قالب ISO 8601 و به وقت UTC هستند.
- مقادیر وضعیت: `Draft`، `InReview`، `Returned`، `Approved` و `Published`
  ([data-model.md](../data-model.md)).

## ورود به سامانه

هویت کاربر از اطلاعات ورود در ASP.NET Core خوانده می‌شود (research.md، تصمیم R-06). در محیط‌های
توسعه و آزمون، این سرآیندهای درخواست هویت کاربر را می‌سازند:

| سرآیند | مثال | توضیح |
|--------|------|-------|
| `X-Dev-User-Id` | `u-author-1` | شناسهٔ کاربر؛ اگر نباشد، کاربر وارد نشده است |
| `X-Dev-User-Name` | `Sara` | نام نمایشی |
| `X-Dev-User-Roles` | `Author,Editor` | نقش‌ها، جداشده با ویرگول |

## قالب پاسخ خطا

همهٔ خطاها با نوع محتوای `application/problem+json` برگردانده می‌شوند. نمونه:

</div>

```json
{
  "type": "https://blogspeclab/errors/invalid-status-transition",
  "title": "انتقال وضعیت مجاز نیست.",
  "status": 409,
  "detail": "پست در وضعیت «تأییدشده» نیست و قابل انتشار نیست.",
  "code": "InvalidStatusTransition"
}
```

<div dir="rtl">

| کد HTTP | مقدار `code` | زمان استفاده |
|---------|--------------|--------------|
| 400 | `InvalidTitle`، `InvalidBody`، `ReturnReasonRequired` | ورودی نامعتبر |
| 401 | `Unauthenticated` | کاربر وارد نشده است |
| 403 | `Forbidden`، `SelfApprovalNotAllowed` | نقش ناکافی، یا تأیید پست توسط نویسندهٔ خودش |
| 404 | `NotFound` | پست وجود ندارد یا برای این کاربر قابل مشاهده نیست (R-13) |
| 409 | `InvalidStatusTransition`، `DeleteNotAllowed`، `ConfirmationRequired`، `ConcurrencyConflict` | تعارض وضعیت یا تغییر هم‌زمان |

## مدل‌های پاسخ

| مدل | فیلدها |
|-----|--------|
| `AuthorPostSummary` (خلاصهٔ پست برای نویسنده) | `id`، `title`، `status`، `lastReturnReason` (اختیاری)، `lastModifiedAt`، `lastPublishedAt` (اختیاری)، `version` |
| `AuthorPostDetail` (جزئیات پست برای نویسنده) | فیلدهای خلاصه به‌علاوهٔ `body` و `createdAt` |
| `EditorPostSummary` (خلاصهٔ پست برای ویراستار) | `id`، `title`، `authorDisplayName`، `status`، `lastModifiedAt`، `lastPublishedAt` (اختیاری)، `version` |
| `EditorPostDetail` (جزئیات پست برای ویراستار) | فیلدهای خلاصه به‌علاوهٔ `body`، `authorId`، `createdAt` و `history` |
| `PublicPostSummary` (خلاصهٔ پست برای خواننده) | `id`، `title`، `authorDisplayName`، `publishedAt` |
| `PublicPostDetail` (جزئیات پست برای خواننده) | فیلدهای خلاصه به‌علاوهٔ `body` |

هر مورد در `history` شامل این فیلدهاست: `actorId`، `actorDisplayName`، `occurredAt`، `fromStatus`،
`toStatus` و `reason`. موارد به ترتیب زمان مرتب‌اند.

در پاسخ‌های خواننده، `publishedAt` همان زمان آخرین انتشار پست است.

---

## مسیرهای نویسنده

پیشوند مسیرها: `/api/author/posts`

همهٔ این مسیرها به نقش نویسنده نیاز دارند (در غیر این صورت 401 یا 403) و فقط روی پست‌های خود همان
نویسنده کار می‌کنند. درخواست برای پست دیگران همیشه 404 می‌گیرد.

| اقدام | متد و مسیر | بدنهٔ درخواست | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|---------------|-----------|-------------|------|
| ایجاد پست | `POST /api/author/posts` | `title`، `body` | 201 و `AuthorPostDetail` | 400 | FR-002 |
| فهرست پست‌های خود | `GET /api/author/posts` | — | 200 و فهرست `AuthorPostSummary`، جدیدترین تغییر اول | — | FR-005، SC-006 |
| مشاهدهٔ یک پست | `GET /api/author/posts/{id}` | — | 200 و `AuthorPostDetail` | 404 | FR-004 |
| ویرایش | `PUT /api/author/posts/{id}` | `title`، `body`، `confirmRevertToDraft` (اختیاری) | 200 و `AuthorPostDetail` | 400؛ 404؛ 409 با `InvalidStatusTransition` برای پست در انتظار بررسی؛ 409 با `ConfirmationRequired` | FR-003، FR-012 |
| حذف | `DELETE /api/author/posts/{id}` | — | 204 | 404؛ 409 با `InvalidStatusTransition` یا `DeleteNotAllowed` | FR-003 |
| ارسال برای بررسی | `POST /api/author/posts/{id}/submit` | — | 200 و `AuthorPostDetail` | 400، 404، 409 | FR-006 |
| پس گرفتن ارسال | `POST /api/author/posts/{id}/withdraw` | — | 200 و `AuthorPostDetail` | 404، 409 | FR-006 |

## مسیرهای ویراستار

پیشوند مسیرها: `/api/editor/posts`

همهٔ این مسیرها به نقش ویراستار نیاز دارند. پست‌های «پیش‌نویس» و «بازگشت برای اصلاح» از این مسیرها
404 می‌گیرند. در اقدام‌ها، ویراستار شمارهٔ نسخهٔ دیده‌شده (`version`) را می‌فرستد (R-11).

| اقدام | متد و مسیر | بدنهٔ درخواست | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|---------------|-----------|-------------|------|
| فهرست براساس وضعیت | `GET /api/editor/posts?status=...` | — | 200 و فهرست `EditorPostSummary` | 400 برای وضعیت نامعتبر | FR-007، FR-015 |
| مشاهدهٔ پست و سابقه | `GET /api/editor/posts/{id}` | — | 200 و `EditorPostDetail` | 404 | FR-011 |
| تأیید | `POST /api/editor/posts/{id}/approve` | `version` | 200 و `EditorPostDetail` | 403 با `SelfApprovalNotAllowed`؛ 404؛ 409 | FR-007، FR-013، FR-014 |
| بازگرداندن | `POST /api/editor/posts/{id}/return` | `version`، `reason` | 200 و `EditorPostDetail` | 400 با `ReturnReasonRequired`؛ 404؛ 409 | FR-007، FR-014 |
| انتشار | `POST /api/editor/posts/{id}/publish` | `version` | 200 و `EditorPostDetail` | 404، 409 | FR-008، FR-013 |
| خارج‌کردن از انتشار | `POST /api/editor/posts/{id}/unpublish` | `version` | 200 و `EditorPostDetail` | 404، 409 | FR-009 |

مقدارهای مجاز `status` در فهرست ویراستار: `InReview`، `Approved` و `Published`.

## مسیرهای عمومی (خوانندگان)

پیشوند مسیرها: `/api/posts`

این مسیرها به ورود نیاز ندارند و فقط پست‌های منتشرشده را برمی‌گردانند.

| اقدام | متد و مسیر | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|-----------|-------------|------|
| فهرست پست‌های منتشرشده | `GET /api/posts` | 200 و فهرست `PublicPostSummary`، جدیدترین انتشار اول | — | FR-010 |
| مشاهدهٔ یک پست | `GET /api/posts/{id}` | 200 و `PublicPostDetail` | 404 برای پست منتشرنشده | FR-010، SC-003 |

</div>

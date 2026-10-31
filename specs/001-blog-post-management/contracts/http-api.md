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

کاربر Token خود را در سرآیند استاندارد `Authorization` می‌فرستد (research.md، R-18):

</div>

```text
Authorization: Bearer <token>
```

<div dir="rtl">

- Token با دستور راه‌اندازی محلی صادر می‌شود ([quickstart.md](../quickstart.md)). API مسیری برای
  ورود یا صدور Token ندارد.
- Token فقط مشخص می‌کند کاربر کیست. نقش‌ها (نویسنده و ویراستار) هنگام هر عملیات از پایگاه داده
  خوانده می‌شوند (R-06).
- هیچ سرآیند یا فیلد دیگری نقش، مالکیت یا مجوز ایجاد نمی‌کند و در صورت ارسال نادیده گرفته می‌شود.
- اگر Token ارسال نشود، مسیرهای نیازمند ورود پاسخ 401 می‌دهند. اگر Token نامعتبر، منقضی یا متعلق به
  کاربر غیرفعال باشد، حتی در مسیرهای عمومی پاسخ 401 است.

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
| 400 | `InvalidTitle`، `InvalidBody`، `ReturnReasonRequired`، `InvalidIfMatch` | ورودی نامعتبر |
| 401 | `Unauthenticated` | Token ارسال نشده، نامعتبر یا منقضی است، یا کاربر غیرفعال است |
| 403 | `Forbidden`، `SelfApprovalNotAllowed` | نقش ذخیره‌شدهٔ کاربر کافی نیست، یا تأیید پست توسط نویسندهٔ خودش |
| 404 | `NotFound` | پست وجود ندارد یا برای این کاربر قابل مشاهده نیست (R-13) |
| 409 | `InvalidStatusTransition`، `DeleteNotAllowed`، `ConfirmationRequired` | وضعیت پست با عملیات سازگار نیست |
| 412 | `StaleVersion` | نسخهٔ ارسال‌شده در `If-Match` قدیمی است یا پست هم‌زمان تغییر کرده است |
| 428 | `PreconditionRequired` | سرآیند `If-Match` برای این فرمان الزامی است و ارسال نشده |

پاسخ‌های 409 و 412 فیلدهای اضافهٔ `currentStatus` و `currentVersion` را دارند، اما فقط وقتی کاربر به
آن پست دسترسی دارد. محتوای پست هرگز در این پاسخ‌ها قرار نمی‌گیرد.

## نسخه و فرمان‌های شرطی (ETag و If-Match)

جزئیات در research.md، تصمیم R-11.

- هر پاسخ موفق مسیرهای نویسنده و ویراستار که یک پست را برمی‌گرداند، نسخهٔ فعلی آن را در سرآیند
  `ETag` دارد. نمونه:

</div>

```text
ETag: "6f1c2b0e-3d4a-4f7e-9a51-2c8d0b7e4a11"
```

<div dir="rtl">

- همهٔ فرمان‌های تغییر وضعیت (ارسال، پس گرفتن، حذف، تأیید، بازگرداندن، انتشار و خارج‌کردن از
  انتشار) باید همان مقدار را در سرآیند `If-Match` بفرستند:

</div>

```text
If-Match: "6f1c2b0e-3d4a-4f7e-9a51-2c8d0b7e4a11"
```

<div dir="rtl">

- ویرایش (`PUT`) به `If-Match` نیاز ندارد، چون طبق FR-012 ویرایش نویسنده همیشه اعمال می‌شود.
- مسیرهای عمومی هیچ `ETag` یا نسخه‌ای برنمی‌گردانند.
- در فهرست‌های نویسنده و ویراستار، نسخهٔ هر پست در فیلد `version` آمده است تا بدون درخواست جداگانه
  قابل استفاده باشد.

## مدل‌های پاسخ

| مدل | فیلدها |
|-----|--------|
| `AuthorPostSummary` (خلاصهٔ پست برای نویسنده) | `id`، `title`، `status`، `lastReturnReason` (اختیاری)، `lastModifiedAt`، `lastPublishedAt` (اختیاری)، `version` |
| `AuthorPostDetail` (جزئیات پست برای نویسنده) | فیلدهای خلاصه به‌علاوهٔ `body` و `createdAt` |
| `EditorPostSummary` (خلاصهٔ پست برای ویراستار) | `id`، `title`، `authorDisplayName`، `status`، `lastModifiedAt`، `lastPublishedAt` (اختیاری)، `version` |
| `EditorPostDetail` (جزئیات پست برای ویراستار) | فیلدهای خلاصه به‌علاوهٔ `body`، `authorId`، `createdAt` و `history` |
| `EditorPostHistory` (سابقهٔ پست برای ویراستار) | `id`، `status` و `history`؛ بدون عنوان و متن پست |
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
| حذف | `DELETE /api/author/posts/{id}` + `If-Match` | — | 204 | 404؛ 409 با `InvalidStatusTransition` یا `DeleteNotAllowed`؛ 412؛ 428 | FR-003 |
| ارسال برای بررسی | `POST /api/author/posts/{id}/submit` + `If-Match` | — | 200 و `AuthorPostDetail` | 400، 404، 409، 412، 428 | FR-006 |
| پس گرفتن ارسال | `POST /api/author/posts/{id}/withdraw` + `If-Match` | — | 200 و `AuthorPostDetail` | 404، 409، 412، 428 | FR-006 |

## مسیرهای ویراستار

پیشوند مسیرها: `/api/editor/posts`

همهٔ این مسیرها به نقش ویراستار نیاز دارند. پست‌های «پیش‌نویس» و «بازگشت برای اصلاح» از این مسیرها
404 می‌گیرند؛ تنها استثنا مسیر سابقه است که سابقهٔ وضعیت پست را در هر وضعیتی، بدون عنوان و متن آن،
برمی‌گرداند (FR-011). همهٔ اقدام‌ها سرآیند `If-Match` با نسخهٔ دیده‌شده را الزامی دارند (R-11)؛ از دو تصمیم
هم‌زمان، دومی 412 می‌گیرد (FR-014).

| اقدام | متد و مسیر | بدنهٔ درخواست | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|---------------|-----------|-------------|------|
| فهرست براساس وضعیت | `GET /api/editor/posts?status=...` | — | 200 و فهرست `EditorPostSummary` | 400 برای وضعیت نامعتبر | FR-007، FR-015 |
| مشاهدهٔ پست و سابقه | `GET /api/editor/posts/{id}` | — | 200 و `EditorPostDetail` | 404 | FR-011 |
| مشاهدهٔ سابقهٔ پست در هر وضعیت | `GET /api/editor/posts/{id}/history` | — | 200 و `EditorPostHistory` | 404 فقط برای پست ناموجود | FR-011 |
| تأیید | `POST /api/editor/posts/{id}/approve` + `If-Match` | — | 200 و `EditorPostDetail` | 403 با `SelfApprovalNotAllowed`؛ 404؛ 409؛ 412؛ 428 | FR-007، FR-013، FR-014 |
| بازگرداندن | `POST /api/editor/posts/{id}/return` + `If-Match` | `reason` | 200 و `EditorPostDetail` | 400 با `ReturnReasonRequired`؛ 404؛ 409؛ 412؛ 428 | FR-007، FR-014 |
| انتشار | `POST /api/editor/posts/{id}/publish` + `If-Match` | — | 200 و `EditorPostDetail` | 404؛ 409؛ 412؛ 428 | FR-008، FR-013 |
| خارج‌کردن از انتشار | `POST /api/editor/posts/{id}/unpublish` + `If-Match` | — | 200 و `EditorPostDetail` | 404؛ 409؛ 412؛ 428 | FR-009 |

مقدارهای مجاز `status` در فهرست ویراستار: `InReview`، `Approved` و `Published`.

## مسیرهای عمومی (خوانندگان)

پیشوند مسیرها: `/api/posts`

این مسیرها به ورود نیاز ندارند و فقط پست‌های منتشرشده را برمی‌گردانند.

| اقدام | متد و مسیر | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|-----------|-------------|------|
| فهرست پست‌های منتشرشده | `GET /api/posts` | 200 و فهرست `PublicPostSummary`، جدیدترین انتشار اول | — | FR-010 |
| مشاهدهٔ یک پست | `GET /api/posts/{id}` | 200 و `PublicPostDetail` | 404 برای پست منتشرنشده | FR-010، SC-003 |

</div>

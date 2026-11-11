<div dir="rtl">

# قرارداد HTTP API: بررسی هوشمند محتوای پست

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `002-ai-content-review` |
| تاریخ | 2026-11-11 |
| طرح | [plan.md](../plan.md) |

قواعد کلی (JSON با camelCase، زمان UTC با ISO 8601، ورود با `Authorization: Bearer <token>`، قالب خطای
ProblemDetails با `code`) همان قواعد
[قرارداد Feature قبلی](../../001-blog-post-management/contracts/http-api.md) هستند.

## مسیرها

پیشوند: `/api/author/posts/{postId}/content-reviews`

هر دو مسیر به نقش نویسنده نیاز دارند و فقط روی پست‌های خود همان نویسنده کار می‌کنند. درخواست برای پست
دیگران یا پست ناموجود همیشه 404 می‌گیرد. هیچ‌کدام به `If-Match` نیاز ندارند (research.md، R-15).

| اقدام | متد و مسیر | بدنه | پاسخ موفق | خطاهای خاص | مرجع |
|-------|------------|------|-----------|-------------|------|
| درخواست بررسی | `POST /api/author/posts/{postId}/content-reviews` | — | 201 و `ContentReviewDetail` | 401، 403، 404، 409، 502، 503، 504 | FR-001 تا FR-012 |
| فهرست نتیجه‌ها | `GET /api/author/posts/{postId}/content-reviews` | — | 200 و فهرست `ContentReviewDetail`، جدیدترین اول | 401، 403، 404 | FR-013 |

فهرست نتیجه‌ها در هر وضعیتی از پست قابل مشاهده است؛ محدودیت وضعیت فقط برای درخواست بررسی جدید است.

## مدل پاسخ

| مدل | فیلدها |
|-----|--------|
| `ContentReviewDetail` | `id`، `postId`، `outcome`، `reviewedVersion`، `reviewedPostModifiedAt`، `isCurrentVersion`، `createdAt`، `issues`، `missingInformation` |
| `ContentIssue` | `category`، `severity`، `location`، `excerpt`، `explanation`، `suggestion` (اختیاری) |
| `MissingInformationItem` | `description` |

- مقادیر `outcome`: `IssuesFound`، `NoIssuesFound`، `NeedsMoreInformation`.
- مقادیر `category`: `SpellingAndWriting`، `Grammar`، `Clarity`، `StructureAndCoherence`،
  `TitleBodyMismatch`، `ClaimNeedsSource`.
- مقادیر `severity`: `High`، `Medium`، `Low`؛ مشکلات به همین ترتیب مرتب‌اند.
- مقادیر `location`: `Title`، `Body`.
- `isCurrentVersion` هنگام خواندن محاسبه می‌شود و نشان می‌دهد نسخهٔ بررسی‌شده هنوز نسخهٔ فعلی پست است
  (research.md، R-11).
- نام Model، نسخهٔ Prompt و نسخهٔ Schema در پاسخ نیستند و فقط برای Evaluation ذخیره می‌شوند.

نمونهٔ پاسخ:

</div>

```json
{
  "id": "0b8e6a4f-2b7c-4b0e-9d3a-7f1e2c4d5a60",
  "postId": "6f1c2b0e-3d4a-4f7e-9a51-2c8d0b7e4a11",
  "outcome": "IssuesFound",
  "reviewedVersion": "a2d4c6e8-1b3f-4a5c-8e7d-9f0a1b2c3d4e",
  "reviewedPostModifiedAt": "2026-11-11T08:30:00Z",
  "isCurrentVersion": true,
  "createdAt": "2026-11-11T08:31:12Z",
  "issues": [
    {
      "category": "SpellingAndWriting",
      "severity": "Medium",
      "location": "Body",
      "excerpt": "میخواهیم",
      "explanation": "«می» باید با نیم‌فاصله به فعل بچسبد.",
      "suggestion": "می‌خواهیم"
    }
  ],
  "missingInformation": []
}
```

<div dir="rtl">

## خطاها

خطاهای 401، 403 و 404 همان معنای Feature قبلی را دارند. کدهای جدید:

| کد HTTP | مقدار `code` | زمان استفاده | مرجع |
|---------|--------------|--------------|------|
| 409 | `ReviewNotAvailable` | پست در وضعیت «در انتظار بررسی»، «تأییدشده» یا «منتشرشده» است؛ همراه `currentStatus` | FR-002 |
| 502 | `ReviewOutputInvalid` | خروجی مدل ناقص، نامعتبر، ناسازگار با Schema یا رد‌شده توسط Validator است | FR-008 |
| 502 | `ReviewRefused` | مدل از انجام بررسی خودداری کرده است | research.md، R-08 |
| 503 | `ReviewServiceUnavailable` | سرویس بررسی در دسترس نیست یا پیکربندی نشده است | FR-011 |
| 504 | `ReviewTimedOut` | پاسخی در حداکثر ۶۰ ثانیه نرسیده است | FR-011 |

در هیچ‌کدام از این خطاها نتیجه‌ای ذخیره نمی‌شود، محتوای پست یا خروجی مدل در پاسخ نیست، و پست و سابقهٔ آن
تغییر نمی‌کنند (FR-009، SC-001).

</div>

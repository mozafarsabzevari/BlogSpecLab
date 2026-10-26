# HTTP API Contract: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

**Feature**: `001-blog-post-management` | **Date**: 2026-10-19 | **Plan**: [plan.md](../plan.md)

قالب داده JSON است و نام فیلدها camelCase. زمان‌ها ISO 8601 با منطقهٔ زمانی UTC هستند. مقادیر وضعیت:
`Draft`، `InReview`، `Returned`، `Approved`، `Published` ([data-model.md](../data-model.md)).

## احراز هویت

هویت از `ClaimsPrincipal` خوانده می‌شود (research.md، R-06). در محیط‌های `Development` و `Testing`
Headerهای زیر هویت را می‌سازند:

| Header | مثال | توضیح |
|--------|------|-------|
| `X-Dev-User-Id` | `u-author-1` | شناسهٔ کاربر؛ نبود آن یعنی کاربر وارد نشده |
| `X-Dev-User-Name` | `Sara` | نام نمایشی |
| `X-Dev-User-Roles` | `Author,Editor` | نقش‌ها، جداشده با ویرگول |

## قالب خطا

همهٔ خطاها `application/problem+json` هستند:

```json
{
  "type": "https://blogspeclab/errors/invalid-status-transition",
  "title": "انتقال وضعیت مجاز نیست.",
  "status": 409,
  "detail": "پست در وضعیت «تأییدشده» نیست و قابل انتشار نیست.",
  "code": "InvalidStatusTransition"
}
```

| کد HTTP | `code` | زمان استفاده |
|---------|--------|--------------|
| 400 | `InvalidTitle`، `InvalidBody`، `ReturnReasonRequired` | اعتبارسنجی ورودی |
| 401 | `Unauthenticated` | کاربر وارد نشده در مسیرهای نیازمند ورود |
| 403 | `Forbidden`، `SelfApprovalNotAllowed` | نقش ناکافی؛ تأیید پست خود |
| 404 | `NotFound` | پست وجود ندارد یا برای این کاربر قابل مشاهده نیست (R-13) |
| 409 | `InvalidStatusTransition`، `DeleteNotAllowed`، `ConfirmationRequired`، `ConcurrencyConflict` | تعارض وضعیت یا هم‌زمانی |

## مدل‌های پاسخ

**AuthorPostSummary**: `id`، `title`، `status`، `lastReturnReason` (nullable)، `lastModifiedAt`،
`lastPublishedAt` (nullable)، `version`

**AuthorPostDetail**: همهٔ فیلدهای AuthorPostSummary به‌علاوهٔ `body`، `createdAt`

**EditorPostSummary**: `id`، `title`، `authorDisplayName`، `status`، `lastModifiedAt`،
`lastPublishedAt` (nullable)، `version`

**EditorPostDetail**: همهٔ فیلدهای EditorPostSummary به‌علاوهٔ `body`، `authorId`، `createdAt`،
`history` (فهرست `{ actorId, actorDisplayName, occurredAt, fromStatus, toStatus, reason }` به ترتیب
زمانی)

**PublicPostSummary**: `id`، `title`، `authorDisplayName`، `publishedAt`

**PublicPostDetail**: همهٔ فیلدهای PublicPostSummary به‌علاوهٔ `body`

---

## مسیرهای نویسنده — `/api/author/posts`

همهٔ مسیرها نقش `Author` را لازم دارند (در غیر این صورت 401 یا 403) و فقط روی پست‌های همان نویسنده
کار می‌کنند؛ پست دیگران همیشه 404 است.

| متد و مسیر | بدنهٔ درخواست | پاسخ موفق | خطاهای ویژه | مرجع |
|------------|---------------|-----------|--------------|------|
| `POST /api/author/posts` | `{ title, body }` | 201 + AuthorPostDetail | 400 | FR-002 |
| `GET /api/author/posts` | — | 200 + `AuthorPostSummary[]` (آخرین تغییر، نزولی) | — | FR-005، SC-006 |
| `GET /api/author/posts/{id}` | — | 200 + AuthorPostDetail | 404 | FR-004 |
| `PUT /api/author/posts/{id}` | `{ title, body, confirmRevertToDraft? }` | 200 + AuthorPostDetail | 400، 404، 409 `InvalidStatusTransition` (InReview)، 409 `ConfirmationRequired` | FR-003، FR-012 |
| `DELETE /api/author/posts/{id}` | — | 204 | 404، 409 `InvalidStatusTransition`، 409 `DeleteNotAllowed` | FR-003 |
| `POST /api/author/posts/{id}/submit` | — | 200 + AuthorPostDetail | 400، 404، 409 | FR-006 |
| `POST /api/author/posts/{id}/withdraw` | — | 200 + AuthorPostDetail | 404، 409 | FR-006 |

## مسیرهای ویراستار — `/api/editor/posts`

همهٔ مسیرها نقش `Editor` را لازم دارند. پست‌های `Draft` و `Returned` از این مسیرها 404 هستند.
اقدام‌ها `version` دیده‌شده را می‌فرستند (R-11).

| متد و مسیر | بدنهٔ درخواست | پاسخ موفق | خطاهای ویژه | مرجع |
|------------|---------------|-----------|--------------|------|
| `GET /api/editor/posts?status={InReview\|Approved\|Published}` | — | 200 + `EditorPostSummary[]` | 400 برای وضعیت نامعتبر | FR-007، FR-015 |
| `GET /api/editor/posts/{id}` | — | 200 + EditorPostDetail | 404 | FR-011 |
| `POST /api/editor/posts/{id}/approve` | `{ version }` | 200 + EditorPostDetail | 403 `SelfApprovalNotAllowed`، 404، 409 | FR-007، FR-013، FR-014 |
| `POST /api/editor/posts/{id}/return` | `{ version, reason }` | 200 + EditorPostDetail | 400 `ReturnReasonRequired`، 404، 409 | FR-007، FR-014 |
| `POST /api/editor/posts/{id}/publish` | `{ version }` | 200 + EditorPostDetail | 404، 409 | FR-008، FR-013 |
| `POST /api/editor/posts/{id}/unpublish` | `{ version }` | 200 + EditorPostDetail | 404، 409 | FR-009 |

## مسیرهای عمومی — `/api/posts`

بدون نیاز به ورود. فقط پست‌های `Published`.

| متد و مسیر | پاسخ موفق | خطاهای ویژه | مرجع |
|------------|-----------|--------------|------|
| `GET /api/posts` | 200 + `PublicPostSummary[]` به ترتیب `publishedAt` نزولی | — | FR-010 |
| `GET /api/posts/{id}` | 200 + PublicPostDetail | 404 برای پست غیرمنتشرشده | FR-010، SC-003 |

`publishedAt` در پاسخ عمومی همان `LastPublishedAt` پست است.

# Data Model: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

**Feature**: `001-blog-post-management` | **Date**: 2026-10-19 | **Plan**: [plan.md](./plan.md)

## موجودیت‌ها

### Post (ریشهٔ Aggregate در `Core.Domain`)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `Id` | `Guid` | شناسهٔ پست (R-08) |
| `Title` | `string` | الزامی، پس از Trim غیرخالی، حداکثر ۲۰۰ نویسه |
| `Body` | `string` | الزامی، پس از Trim غیرخالی، حداکثر ۵۰٬۰۰۰ نویسه |
| `AuthorId` | `string` | شناسهٔ نویسنده از `ICurrentUser` |
| `AuthorDisplayName` | `string` | نام نمایشی نویسنده هنگام ایجاد (R-14) |
| `Status` | `PostStatus` | وضعیت فعلی |
| `CreatedAt` | `DateTimeOffset` | زمان ایجاد (UTC) |
| `LastModifiedAt` | `DateTimeOffset` | زمان آخرین تغییر محتوا یا وضعیت |
| `LastPublishedAt` | `DateTimeOffset?` | زمان آخرین انتشار؛ پس از خروج از انتشار پاک نمی‌شود (R-17) |
| `LastReturnReason` | `string?` | آخرین دلیل بازگشت (FR-005) |
| `Version` | `int` | Concurrency Token؛ با هر تغییر یک واحد افزایش می‌یابد (R-11) |
| `StatusChanges` | مجموعهٔ `StatusChange` | سابقهٔ تغییر وضعیت |

ویژگی محاسبه‌شده: `HasEverBeenPublished = LastPublishedAt != null`

### PostStatus (Enum در `Core.Domain`)

| مقدار | معادل Specification |
|-------|---------------------|
| `Draft` | پیش‌نویس |
| `InReview` | در انتظار بررسی |
| `Returned` | بازگشت برای اصلاح |
| `Approved` | تأییدشده |
| `Published` | منتشرشده |

### StatusChange (موجودیت فرزند Post)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `Id` | `Guid` | شناسه |
| `PostId` | `Guid` | پست مربوط |
| `ActorId` | `string` | شناسهٔ کاربر انجام‌دهنده |
| `ActorDisplayName` | `string` | نام کاربر انجام‌دهنده (R-14) |
| `OccurredAt` | `DateTimeOffset` | زمان تغییر (UTC) |
| `FromStatus` | `PostStatus?` | وضعیت قبلی؛ برای ایجاد پست `null` |
| `ToStatus` | `PostStatus` | وضعیت جدید |
| `Reason` | `string?` | برای بازگشت الزامی؛ در سایر موارد خالی |

سابقه فقط افزودنی است؛ هیچ `StatusChange` ویرایش یا حذف نمی‌شود، مگر همراه با حذف مجاز یک پست
هرگز منتشرنشده (FR-003).

### CurrentUser (قرارداد `ICurrentUser` در `Core.Application`، بدون ذخیره‌سازی)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `IsAuthenticated` | `bool` | آیا کاربر وارد شده است |
| `UserId` | `string` | شناسهٔ پایدار کاربر |
| `DisplayName` | `string` | نام نمایشی |
| `IsAuthor` | `bool` | نقش نویسنده |
| `IsEditor` | `bool` | نقش ویراستار |

کاربر ممکن است هر دو نقش را داشته باشد (Key Entities در Specification).

## روابط

- هر `Post` دقیقاً یک نویسنده دارد (`AuthorId`).
- هر `Post` صفر یا چند `StatusChange` دارد (یک به چند، حذف آبشاری فقط برای حذف مجاز).

## ماشین وضعیت

```text
(Create)          ──► Draft
Draft             ──Submit──►    InReview
Returned          ──Submit──►    InReview
InReview          ──Withdraw──►  Draft
InReview          ──Approve──►   Approved      (ویراستار غیر از نویسنده)
InReview          ──Return──►    Returned      (ویراستار، دلیل الزامی)
Approved          ──Publish──►   Published     (هر ویراستار)
Published         ──Unpublish──► Approved      (ویراستار)
Approved/Published ──Edit──►     Draft         (نویسنده، با تأیید پیامد)
```

### جدول انتقال‌ها

| انتقال | از | به | انجام‌دهنده | پیش‌شرط‌ها | مرجع |
|--------|----|----|-------------|-------------|------|
| Create | — | `Draft` | نویسنده | عنوان و متن معتبر | FR-002 |
| Edit | `Draft`، `Returned` | همان وضعیت | نویسندهٔ پست | عنوان و متن معتبر | FR-003 |
| Edit | `Approved`، `Published` | `Draft` | نویسندهٔ پست | `confirmRevertToDraft = true`؛ بدون نیاز به `version` | FR-012، R-11، R-12 |
| Delete | `Draft`، `Returned` | (حذف) | نویسندهٔ پست | `HasEverBeenPublished = false` | FR-003 |
| Submit | `Draft`، `Returned` | `InReview` | نویسندهٔ پست | عنوان و متن معتبر | FR-006 |
| Withdraw | `InReview` | `Draft` | نویسندهٔ پست | — | FR-006 |
| Approve | `InReview` | `Approved` | ویراستار | ویراستار ≠ نویسنده؛ `version` برابر | FR-007، FR-013، FR-014 |
| Return | `InReview` | `Returned` | ویراستار | دلیل غیرخالی؛ `version` برابر؛ `LastReturnReason` به‌روز می‌شود | FR-007، FR-014 |
| Publish | `Approved` | `Published` | هر ویراستار (از جمله نویسنده) | `version` برابر؛ `LastPublishedAt` به‌روز می‌شود | FR-008، FR-013 |
| Unpublish | `Published` | `Approved` | ویراستار | `version` برابر | FR-009 |

هر انتقال دیگری با کد `InvalidStatusTransition` رد می‌شود و وضعیت تغییر نمی‌کند. بازگرداندن
پست ویراستار-نویسنده توسط خودش مجاز است؛ فقط تأیید آن ممنوع است (FR-013).

## قواعد اعتبارسنجی

| قاعده | محل اعمال | کد خطا |
|-------|-----------|--------|
| عنوان خالی یا بیش از ۲۰۰ نویسه | Domain | `InvalidTitle` |
| متن خالی یا بیش از ۵۰٬۰۰۰ نویسه | Domain | `InvalidBody` |
| دلیل بازگشت خالی | Domain | `ReturnReasonRequired` |
| انتقال غیرمجاز | Domain | `InvalidStatusTransition` |
| تأیید توسط نویسندهٔ همان پست | Domain | `SelfApprovalNotAllowed` |
| حذف پستی که قبلاً منتشر شده | Domain | `DeleteNotAllowed` |
| ویرایش پست تأییدشده/منتشرشده بدون تأیید نویسنده | Application | `ConfirmationRequired` |
| `version` متفاوت یا تعارض هم‌زمانی در اقدام ویراستار | Application | `ConcurrencyConflict` |
| نقش ناکافی | Application | `Forbidden` |
| پست دیگران یا غیرقابل مشاهده | Application | `NotFound` |

## قواعد دسترسی به داده (Visibility)

| مصرف‌کننده | پست‌های قابل مشاهده |
|------------|---------------------|
| خواننده (حتی بدون ورود) | فقط `Published` |
| نویسنده | همهٔ پست‌های خودش در هر وضعیت، به‌علاوهٔ پست‌های `Published` دیگران از مسیر عمومی |
| ویراستار | پست‌های همهٔ نویسندگان در وضعیت‌های `InReview`، `Approved`، `Published` همراه با سابقه (FR-011، FR-015) |

## نکات ذخیره‌سازی

- جدول‌ها: `Posts` و `PostStatusChanges`.
- Index روی `Posts(Status, LastPublishedAt)` برای فهرست خوانندگان و ویراستار، و روی
  `Posts(AuthorId)` برای فهرست نویسنده.
- `PostStatus` به‌صورت رشته ذخیره می‌شود تا داده خوانا بماند.
- `Version` با `IsConcurrencyToken()` پیکربندی می‌شود.

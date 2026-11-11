<div dir="rtl">

# مدل داده: بررسی هوشمند محتوای پست

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `002-ai-content-review` |
| تاریخ | 2026-11-11 |
| طرح | [plan.md](./plan.md) |

موجودیت `Post` و قواعد آن از Feature قبلی
([001-blog-post-management/data-model.md](../001-blog-post-management/data-model.md)) بدون تغییر استفاده
می‌شوند. این قابلیت پست را فقط می‌خواند.

## موجودیت‌ها

### نتیجهٔ بررسی (`ContentReview`)

موجودیت جدید در `Core.Domain/ContentReviews`. پس از ایجاد تغییر نمی‌کند.

| فیلد | نوع | توضیح |
|------|-----|-------|
| `Id` | `Guid` | شناسهٔ نتیجه |
| `PostId` | `Guid` | پست بررسی‌شده |
| `AuthorId` | `Guid` | نویسندهٔ پست در زمان بررسی؛ برای کنترل دسترسی |
| `ReviewedPostVersion` | `Guid` | نسخهٔ پست در لحظهٔ Snapshot (FR-003) |
| `ReviewedPostModifiedAt` | `DateTimeOffset` | زمان آخرین تغییر پست در لحظهٔ Snapshot؛ برای نمایش به نویسنده |
| `CreatedAt` | `DateTimeOffset` | زمان ثبت نتیجه (UTC، از `TimeProvider`) |
| `Outcome` | `ContentReviewOutcome` | وضعیت نتیجه |
| `Issues` | فهرست `ContentIssue` | مشکلات؛ مرتب‌شده براساس شدت |
| `MissingInformation` | فهرست `MissingInformationItem` | موارد اطلاعات ناکافی |
| `ModelName` | `string` | نام Model که Provider در پاسخ برگردانده است (research.md، R-01) |
| `PromptVersion` | `string` | مثلاً `content-review-prompt-v1` |
| `SchemaVersion` | `string` | مثلاً `content_review_v1` |

### وضعیت نتیجه (`ContentReviewOutcome`)

| مقدار در کد | نام در Specification | مقدار `outcome` در خروجی مدل |
|-------------|----------------------|------------------------------|
| `IssuesFound` | مشکلاتی یافت شد | `completed` با فهرست غیرخالی |
| `NoIssuesFound` | مشکلی یافت نشد | `completed` با فهرست خالی |
| `NeedsMoreInformation` | نیاز به اطلاعات بیشتر | `needs_input` |

### مشکل محتوایی (`ContentIssue`)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `Category` | `ContentIssueCategory` | دستهٔ مشکل |
| `Severity` | `ContentIssueSeverity` | `High`، `Medium` یا `Low` (زیاد، متوسط، کم) |
| `Location` | `ContentLocation` | `Title` یا `Body` |
| `Excerpt` | `string` | بخش عین متن از عنوان یا متن پست؛ ۱ تا ۳۰۰ نویسه |
| `Explanation` | `string` | توضیح مشکل؛ ۱ تا ۱۰۰۰ نویسه |
| `Suggestion` | `string?` | پیشنهاد اصلاح؛ حداکثر ۱۰۰۰ نویسه؛ هرگز در پست اعمال نمی‌شود (FR-009) |

### دسته‌های مشکل (`ContentIssueCategory`)

| مقدار در کد | مقدار در خروجی مدل | نام در Specification |
|-------------|--------------------|----------------------|
| `SpellingAndWriting` | `spelling_and_writing` | املا و نگارش |
| `Grammar` | `grammar` | دستور زبان |
| `Clarity` | `clarity` | وضوح و خوانایی |
| `StructureAndCoherence` | `structure_and_coherence` | ساختار و انسجام |
| `TitleBodyMismatch` | `title_body_mismatch` | ناهماهنگی عنوان با متن |
| `ClaimNeedsSource` | `claim_needs_source` | ادعای نیازمند منبع |

### مورد اطلاعات ناکافی (`MissingInformationItem`)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `Description` | `string` | توضیح اطلاعات کم؛ ۱ تا ۱۰۰۰ نویسه |

## قواعد Domain

این قواعد در سازندهٔ `ContentReview` اعمال می‌شوند و نقض آن‌ها Exception دامنه است (FR-006):

| قاعده | مرجع |
|-------|------|
| `IssuesFound` دست‌کم یک مشکل و هیچ مورد اطلاعات ناکافی ندارد | FR-004، FR-006 |
| `NoIssuesFound` هیچ مشکل و هیچ مورد اطلاعات ناکافی ندارد | FR-006 |
| `NeedsMoreInformation` هیچ مشکل ندارد و دست‌کم یک مورد اطلاعات ناکافی دارد | FR-006، FR-007 |
| متن‌های الزامی غیرخالی و در سقف طول هستند | research.md، R-03 |

قاعدهٔ «`excerpt` در متن پست وجود دارد» به Snapshot پست نیاز دارد و در Validator لایهٔ Application اجرا
می‌شود، پیش از ساخت `ContentReview` (research.md، R-07).

## قراردادهای Application

این قراردادها ذخیره نمی‌شوند. جزئیات در [contracts/application-ai.md](./contracts/application-ai.md).

| قرارداد | محل | توضیح |
|---------|-----|-------|
| `IContentReviewModel` | `Core.Application` | درخواست بررسی به مدل؛ پیاده‌سازی در `Infra.AI` |
| `ContentReviewInput` | `Core.Application` | Snapshot ارسالی: فقط `Title` و `Body` (research.md، R-05) |
| `ContentReviewModelOutcome` | `Core.Application` | یکی از `Candidate`، `Refused`، `ProviderUnavailable`، `InvalidOutput`، `UnexpectedOutputForm` |
| `ContentReviewCandidate` | `Core.Application` | خروجی Deserialize‌شده و هنوز اعتبارسنجی‌نشدهٔ مدل، به‌همراه `ModelName` |
| `IContentReviewRepository` | `Core.Application` | افزودن نتیجه و فهرست نتیجه‌های یک پست؛ پیاده‌سازی در `Infra.DataAccess` |

## روابط

- هر پست صفر یا چند نتیجهٔ بررسی دارد.
- با حذف مجاز پست، همهٔ نتیجه‌های آن هم حذف می‌شوند (حذف آبشاری).
- نتیجه به سابقهٔ وضعیت پست (`StatusChange`) هیچ رکوردی اضافه نمی‌کند (FR-009).

## چه کسی چه نتیجه‌هایی را می‌بیند

| کاربر | دسترسی |
|-------|--------|
| نویسندهٔ پست | همهٔ نتیجه‌های پست‌های خودش، از جدیدترین به قدیمی‌ترین (FR-013) |
| نویسندهٔ دیگر | هیچ؛ پاسخ 404 |
| ویراستاری که نویسندهٔ پست نیست | هیچ؛ اگر نقش نویسنده ندارد پاسخ 403، و گرنه 404 |
| کاربر واردنشده | هیچ؛ پاسخ 401 |

## نکات ذخیره‌سازی

- جدول `ContentReviews` با کلید خارجی `PostId` به `Posts` و حذف آبشاری.
- Index روی `(PostId, CreatedAt)` برای فهرست نتیجه‌های یک پست.
- `Issues` و `MissingInformation` به‌صورت مجموعهٔ Owned با نگاشت JSON در همان سطر (research.md، R-14).
- مقادیر Enum به‌صورت متن ذخیره می‌شوند، مانند وضعیت پست.
- یک Migration جدید در `Infra.DataAccess`؛ Migrationها فقط با دستور صریح اعمال می‌شوند.

</div>

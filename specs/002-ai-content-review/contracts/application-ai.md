<div dir="rtl">

# قرارداد مرز Application و `Infra.AI`

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `002-ai-content-review` |
| تاریخ | 2026-11-11 |
| طرح | [plan.md](../plan.md) |

این قرارداد در `Core.Application` تعریف و در `Infra.AI` پیاده‌سازی می‌شود. هیچ نوع، Exception یا Client
مربوط به SDK در این قرارداد وجود ندارد (research.md، R-04).

## قرارداد `IContentReviewModel`

</div>

```text
IContentReviewModel
  ReviewAsync(ContentReviewInput input, CancellationToken cancellationToken)
    -> Task<ContentReviewModelOutcome>

ContentReviewInput
  Title : string
  Body  : string

ContentReviewModelOutcome   (exactly one of)
  Candidate(ContentReviewCandidate candidate)
  Refused
  ProviderUnavailable
  InvalidOutput
  UnexpectedOutputForm

ContentReviewCandidate
  Outcome            : string            // "completed" | "needs_input" (unvalidated)
  Issues             : list of CandidateIssue
  MissingInformation : list of string
  ModelName          : string            // model reported by the provider
  PromptVersion      : string            // "content-review-prompt-v1"
  SchemaVersion      : string            // "content_review_v1"

CandidateIssue
  Category    : string
  Severity    : string
  Location    : string
  Excerpt     : string
  Explanation : string
  Suggestion  : string?
```

<div dir="rtl">

## تعهدهای `Infra.AI`

- فقط `Title` و `Body` را به Provider می‌فرستد؛ `store=false`؛ بدون Tool؛ غیر Streaming؛ بدون Retry خودکار
  (research.md، R-05 و R-09).
- Schema [model-output.schema.json](./model-output.schema.json) را با نام `content_review_v1` و در حالت
  strict در سطح Provider اعمال می‌کند. نسخهٔ جاسازی‌شده در کد باید با این فایل برابر باشد و آزمون خودکار
  این برابری را بررسی می‌کند.
- پاسخ Provider را به یکی از Outcomeها تبدیل می‌کند:

| وضعیت پاسخ Provider | Outcome |
|---------------------|---------|
| `Completed` با یک پیام متنی که JSON آن با Schema منطبق است | `Candidate` |
| پیام دارای بخش Refusal | `Refused` |
| خطای شبکه، پاسخ HTTP 5xx، 429، 401 یا 403 از Provider، یا نبود `ApiKey` | `ProviderUnavailable` |
| `Incomplete` (هر دلیلی)، `Failed`، `Cancelled`، JSON نامعتبر یا ناسازگار با Schema | `InvalidOutput` |
| خروجی غیر از دقیقاً یک پیام متنی (مثلاً فراخوانی Tool) | `UnexpectedOutputForm` |

- اگر `cancellationToken` لغو شود، `OperationCanceledException` را بدون تبدیل منتشر می‌کند تا Application
  میان Deadline و قطع درخواست Client تمایز بگذارد.
- عنوان، متن، Prompt و خروجی مدل را در Log ثبت نمی‌کند (research.md، R-05).
- هیچ وابستگی‌ای به `Infra.DataAccess` ندارد و هیچ وضعیتی را تغییر نمی‌دهد.

## تعهدهای Application

- پیش از فراخوانی: ورود، نقش نویسنده، مالکیت و وضعیت «پیش‌نویس» یا «بازگشت برای اصلاح» را بررسی می‌کند.
- Snapshot عنوان، متن، `Version` و `LastModifiedAt` پست را می‌گیرد.
- Deadline را اعمال می‌کند (research.md، R-09).
- `Candidate` را با `ContentReviewValidator` اعتبارسنجی می‌کند (research.md، R-07).
- پیش از ذخیره، وجود پست و مالکیت را دوباره بررسی می‌کند (research.md، R-11).
- فقط نتیجهٔ معتبر را ذخیره می‌کند؛ هیچ تغییری در پست یا سابقهٔ آن ایجاد نمی‌کند.

</div>

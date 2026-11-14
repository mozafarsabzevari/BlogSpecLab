<div dir="rtl">

# شواهد اعتبارسنجی: بررسی هوشمند محتوای پست

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `002-ai-content-review` |
| تاریخ | 2026-11-14 |
| وظایف | T046 در [tasks.md](./tasks.md)؛ پیش‌نیاز T029، T033 و T039 |
| Model | `grok-4.7` |

نتایج زیر از اجرای واقعی روی Repository در همین تاریخ است. اجرای Build و آزمون را کاربر روی دستگاه خود
انجام داده و خلاصهٔ خروجی را گزارش کرده است. هیچ کلید API، Token یا متن پست کاربر در این سند ثبت نشده است.

## ۱. Build و Test بدون کلید API (T046)

</div>

```powershell
dotnet --version              # 10.0.403
dotnet build BlogSpecLab.slnx # Build succeeded
dotnet test BlogSpecLab.slnx  # total: 281، succeeded: 279، failed: 0، skipped: 2، duration: 25.3s
```

<div dir="rtl">

- اجرای قبلی Build فقط هشدار `NU1900` داشت: NuGet نتوانست برای بررسی آسیب‌پذیری بسته‌ها به
  `api.nuget.org` وصل شود. این هشدار به کد مربوط نیست.
- دو آزمون Skip شده همان آزمون‌های Provider واقعی‌اند که بدون `BLOGSPECLAB_PROVIDER_TESTS=1` اجرا
  نمی‌شوند: `ContentReviewProviderSmokeTests` (`Category=ProviderSmoke`) و `ContentReviewEvaluationTests`
  (`Category=Evaluation`).
- ۲۷۹ آزمون موفق شامل همهٔ آزمون‌های Feature قبلی (`001-blog-post-management`) و همهٔ آزمون‌های این قابلیت
  در Domain، Application، `Infra.AI` و HTTP است؛ بنابراین رگرسیون Feature قبلی هم دیده نشد.
- آزمون‌های عادی از مدل جعلی (`FakeContentReviewModel`) و Transport جعلی HTTP (`StubProviderHandler`)
  استفاده می‌کنند و به کلید API یا اینترنت نیاز ندارند.

## ۲. نگاشت سناریوهای quickstart به آزمون‌ها

| سناریو | شرح کوتاه | آزمون‌های پوشش‌دهنده |
|--------|-----------|----------------------|
| ۱ | `IssuesFound` و ترتیب براساس شدت | `Api/ContentReviewTests.cs`، `Application/ContentReviewValidatorTests.cs` |
| ۲ | `NoIssuesFound` | `Api/ContentReviewTests.cs` |
| ۳ | پست `Returned` | `Api/ContentReviewTests.cs`، `Application/ContentReviewServiceTests.cs` |
| ۴ | نسخهٔ بررسی‌شده و `isCurrentVersion` پس از ویرایش | `Api/ContentReviewTests.cs`، `Api/ContentReviewNeedsInputTests.cs` |
| ۵ | `NeedsMoreInformation` | `Api/ContentReviewNeedsInputTests.cs`، `Application/ContentReviewNeedsInputTests.cs` |
| ۶ | `needs_input` ناسازگار → 502 | `Api/ContentReviewNeedsInputTests.cs`، `Application/ContentReviewNeedsInputTests.cs` |
| ۷ | حذف مشکل بدون ارجاع واقعی | `Api/ContentReviewTests.cs`، `Application/ContentReviewValidatorTests.cs` |
| ۸ | حذف همهٔ مشکلات → 502 | `Api/ContentReviewTests.cs`، `Application/ContentReviewValidatorTests.cs` |
| ۹ | پست و سابقه بدون تغییر | `Api/ContentReviewNonMutationTests.cs` |
| ۱۰ | ارسال بدون بررسی | `Api/ContentReviewNonMutationTests.cs` |
| ۱۱ | نویسندهٔ دیگر → 404 | `Api/ContentReviewTests.cs`، `Application/ContentReviewServiceTests.cs` |
| ۱۲ | ویراستار ۲ → 403، ویراستار ۱ → 404 | `Api/ContentReviewTests.cs` |
| ۱۳ | بدون Token → 401 | `Api/ContentReviewTests.cs` |
| ۱۴ | وضعیت غیرمجاز → 409 | `Api/ContentReviewTests.cs` |
| ۱۵ | رسیدن Deadline → 504 | `Api/ContentReviewTimeoutTests.cs`، `Application/ContentReviewDeadlineTests.cs` |
| ۱۶ | Provider در دسترس نیست → 503 | `Api/ContentReviewTests.cs`، `Infra/AI/ResponsesContentReviewModelTests.cs` |
| ۱۷ | `Refused` و شکل غیرمنتظره → 502 | `Api/ContentReviewTests.cs`، `Infra/AI/ResponsesContentReviewModelTests.cs` |
| ۱۸ | دستور داخل متن پست بی‌اثر | `Api/ContentReviewNonMutationTests.cs` |
| ۱۹ | سه نتیجه، جدیدترین اول | `Api/ContentReviewTests.cs` |
| ۲۰ | حذف نتیجه‌ها با حذف پست | `Api/ContentReviewNonMutationTests.cs` |
| ۲۱ | حذف پست هنگام بررسی → 404 | `Api/ContentReviewTests.cs`، `Application/ContentReviewConcurrencyTests.cs` |

بررسی‌های جداگانهٔ `Infra.AI` (شکل درخواست، `store=false`، Schema strict، نبود Tool، ارسال فقط عنوان و متن،
نگاشت پاسخ‌ها و برابری Schema) در `Infra/AI/ResponsesContentReviewModelTests.cs` و
`Infra/AI/ContentReviewSchemaContractTests.cs` قرار دارند. نبود محتوای پست، خروجی Model و کلید در Log در
`Infra/AI/ContentReviewLoggingTests.cs` آزموده شده است.

## ۳. شواهد Success Criteria

| معیار | آزمون‌های پوشش‌دهنده | نتیجه |
|-------|----------------------|-------|
| SC-001: پست و سابقه پس از هر نتیجه یا خطا بدون تغییر | `Api/ContentReviewNonMutationTests.cs` (هشت Outcome و خطا)، `Api/ContentReviewTimeoutTests.cs`، `Application/ContentReviewConcurrencyTests.cs` | موفق |
| SC-002: نتیجه‌ها با ساختار سازگار؛ خروجی ناسازگار به خطا | `Application/ContentReviewValidatorTests.cs`، `Application/ContentReviewNeedsInputTests.cs`، `Infra/AI/ResponsesContentReviewModelTests.cs`، `Api/ContentReviewTests.cs` | موفق |
| SC-004: هر مشکل به بخش واقعی پست ارجاع دهد | `Application/ContentReviewValidatorTests.cs` (یافتن `excerpt` پس از NFC در محل درست)، سناریوهای ۷ و ۸ | موفق |
| SC-005: رد درخواست‌های غیرمجاز | سناریوهای ۱۱، ۱۳ و ۱۴؛ `Application/ContentReviewServiceTests.cs` | موفق |
| SC-007: ارسال پست بدون بررسی هوشمند | `Api/ContentReviewNonMutationTests.cs` (ارسال بدون بررسی و پس از بررسی با مشکل شدید) | موفق |
| SC-008: نتیجه‌ها فقط برای نویسندهٔ پست | سناریوهای ۱۱ و ۱۲ | موفق |

همهٔ این آزمون‌ها در اجرای بخش ۱ جزو ۲۷۹ آزمون موفق بودند.

## ۴. اجرای Evaluation با Provider واقعی (خارج از T046)

در همین تاریخ، Evaluation به‌طور ناخواسته همراه `dotnet test` اجرا شد، چون متغیر
`BLOGSPECLAB_PROVIDER_TESTS=1` هنوز در Terminal تنظیم بود. این اجرا با کلید واقعی xAI انجام شد و شواهد
کامل آن در
[`evaluation/results/content-review-eval-20261114-133655Z.json`](./evaluation/results/content-review-eval-20261114-133655Z.json)
ثبت شده است.

| معیار | نتیجه | آستانهٔ پیشنهادی |
|-------|-------|------------------|
| نمونه‌ها | ۱۴ | — |
| Model بازگشتی Provider | `grok-4.7` | — |
| نرخ `needs_input` (SC-003) | ۱۰۰٪ | ۱۰۰٪ |
| نرخ عبور تزریقی | ۱۰۰٪ | ۱۰۰٪ |
| بازیابی اشکال کاشته‌شده | ۱۰۰٪ | دست‌کم ۸۰٪ |
| پست سالم با شدت «زیاد» | ۰٪ | حداکثر ۲۰٪ |
| خروجی نامعتبر | ۰٪ | کمتر از ۵٪ |
| پاسخ زیر ۳۰ ثانیه (SC-006) | **۷۱٪** (۱۰ از ۱۴) | دست‌کم ۹۰٪ |

- چهار نمونهٔ کند بین ۳۶ و ۴۲ ثانیه طول کشیدند؛ هیچ درخواستی به Deadline شصت‌ثانیه‌ای نرسید.
- آستانه‌ها هنوز **تأیید انسانی نشده‌اند** (`approved: false`) و برچسب‌های Corpus پیش‌نویس‌اند. بنابراین
  این اجرا شاهد است، نه پذیرش، و T048 باز می‌ماند.
- SC-006 در این اجرا برآورده نشد. تصمیم دربارهٔ آن (پذیرش، بهبود سرعت یا بازنگری Model یا Prompt) با
  کاربر است و Fallback خودکار ایجاد نمی‌شود.

## ۵. کارهای باقی‌مانده

- **T047:** اجرای آگاهانهٔ آزمون Smoke با کلید xAI و افزودن نتیجه و نام Model بازگشتی به همین سند.
- **T048:** تأیید انسانی آستانه‌ها و تصمیم دربارهٔ SC-006، سپس پذیرش یا تکرار Evaluation.
- **T049:** ارزیابی انسانی خروجی‌ها براساس Rubric در `evaluation/protocol.md`.
- **T050:** پذیرش نهایی و ثبت ریسک نگهداری داده در xAI (R-05) در `acceptance.md`.
- اجرای دستی quickstart با HTTP (`dotnet ef database update` و درخواست واقعی در Postman) هنوز ثبت نشده
  است.

</div>

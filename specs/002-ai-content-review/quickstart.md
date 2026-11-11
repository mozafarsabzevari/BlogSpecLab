<div dir="rtl">

# راهنمای اعتبارسنجی: بررسی هوشمند محتوای پست

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `002-ai-content-review` |
| تاریخ | 2026-11-11 |
| طرح | [plan.md](./plan.md) |

> کد و آزمون‌ها نوشته شده‌اند، اما هیچ‌یک از دستورهای این راهنما (Build، آزمون، Migration، Smoke و
> Evaluation) هنوز اجرا و بررسی نشده است. نتیجهٔ اجرا در `validation.md` ثبت خواهد شد.

## پیش‌نیازها

- پیش‌نیازهای [راهنمای Feature قبلی](../001-blog-post-management/quickstart.md): .NET 10 SDK و ابزار
  `dotnet-ef` نسخهٔ 10.
- فقط برای اجرای واقعی بررسی، آزمون Smoke و Evaluation: کلید API معتبر xAI با دسترسی به Model
  `grok-4.7`. آزمون‌های عادی به کلید نیازی ندارند.

## ساخت و آزمون قطعی

</div>

```powershell
dotnet build BlogSpecLab.slnx
dotnet test BlogSpecLab.slnx
```

<div dir="rtl">

**نتیجهٔ مورد انتظار:** همهٔ آزمون‌ها بدون کلید API و بدون دسترسی به اینترنت موفق باشند. آزمون‌های Smoke
و Evaluation در این اجرا رد (Skip) می‌شوند (research.md، R-12).

## پیکربندی کلید API

کلید در Repository یا `appsettings` قرار نمی‌گیرد (research.md، R-13). در محیط توسعه با User Secrets:

</div>

```powershell
dotnet user-secrets set "ContentReview:ApiKey" "<xai-api-key>" --project src/03.Endpoints/Api
```

<div dir="rtl">

یا با متغیر محیطی `ContentReview__ApiKey`. سایر تنظیمات (`Endpoint` برابر `https://api.x.ai/v1`،
`Model` برابر `grok-4.7` و `TimeoutSeconds` برابر ۶۰) در `appsettings.json` پیش‌فرض دارند. بدون کلید،
برنامه اجرا می‌شود و فقط درخواست بررسی پاسخ 503 می‌گیرد.

تنظیمات عملیاتی بخش `ContentReview` (پروژهٔ `Infra.AI`):

| کلید | پیش‌فرض | توضیح |
|------|---------|-------|
| `Endpoint` | `https://api.x.ai/v1` | آدرس پایهٔ Responses API در xAI |
| `Model` | `grok-4.7` | تنها Model مجاز این قابلیت |
| `ApiKey` | ندارد | فقط User Secrets (شناسهٔ `blogspeclab-endpoints-api`) یا `ContentReview__ApiKey` |
| `TimeoutSeconds` | `60` | Deadline برنامه؛ مقدار خارج از ۱ تا ۶۰ هنگام شروع برنامه خطا می‌دهد |
| `MaxOutputTokenCount` | `8000` | سقف Token خروجی هر درخواست |

Retry خودکار SDK صفر است، `store` همیشه `false` است و Log فقط فرادادهٔ مجاز (شناسهٔ پست، Outcome، مدت،
نام Model، شمار Token و شناسهٔ پاسخ Provider) را ثبت می‌کند.

## اجرای برنامه

</div>

```powershell
dotnet ef migrations add AddContentReviews --project src/02.Infra/DataAccess --startup-project src/03.Endpoints/Api
dotnet ef database update --project src/02.Infra/DataAccess --startup-project src/03.Endpoints/Api
dotnet run --project src/03.Endpoints/Api
```

<div dir="rtl">

Migration `AddContentReviews` هنوز ساخته نشده است؛ دستور اول فقط یک بار و پیش از `database update` لازم است.
آزمون‌ها به Migration وابسته نیستند و پایگاه داده را با `EnsureCreated` در حافظه می‌سازند.

کاربران نمونه همان کاربران راهنمای Feature قبلی هستند.

## سناریوهای اعتبارسنجی

هر سناریو با آزمون پذیرش خودکار و مدل جعلی پوشش داده می‌شود (اصل IV). جزئیات در
[contracts/http-api.md](./contracts/http-api.md) و [data-model.md](./data-model.md).

| ردیف | سناریو | نتیجهٔ مورد انتظار | مرجع |
|------|--------|-------------------|------|
| ۱ | نویسندهٔ الف برای پیش‌نویس خود بررسی می‌خواهد و مدل مشکلاتی برمی‌گرداند | 201 با `IssuesFound`؛ هر مشکل دسته، شدت، محل، بخش متن و توضیح دارد؛ مرتب براساس شدت | US1، FR-004، FR-005 |
| ۲ | مدل `completed` بدون مشکل برمی‌گرداند | 201 با `NoIssuesFound` | FR-004 |
| ۳ | همان درخواست برای پستی در وضعیت «بازگشت برای اصلاح» | 201 | US1، FR-001 |
| ۴ | پاسخ شامل نسخه و زمان آخرین تغییر پست بررسی‌شده است؛ پس از ویرایش پست، فهرست نتیجه‌ها `isCurrentVersion` برابر `false` نشان می‌دهد | مطابق شرح | FR-003 |
| ۵ | مدل `needs_input` با یک مورد اطلاعات ناکافی برمی‌گرداند | 201 با `NeedsMoreInformation`؛ فهرست مشکلات خالی | US2، FR-007، SC-003 |
| ۶ | مدل `needs_input` همراه مشکل، یا بدون مورد اطلاعات ناکافی برمی‌گرداند | 502 با `ReviewOutputInvalid`؛ چیزی ذخیره نمی‌شود | FR-006، FR-008 |
| ۷ | یکی از دو مشکل به بخشی اشاره می‌کند که در پست نیست | 201 فقط با مشکل معتبر | FR-008، SC-004 |
| ۸ | همهٔ مشکلات به بخش‌هایی اشاره می‌کنند که در پست نیستند | 502 با `ReviewOutputInvalid` (نه `NoIssuesFound`) | FR-008، research.md R-07 |
| ۹ | عنوان، متن، وضعیت، `Version` و سابقهٔ پست پیش و پس از هر سناریوی بالا و پایین مقایسه می‌شوند | بدون تغییر | US3، FR-009، SC-001 |
| ۱۰ | نویسنده بدون هیچ بررسی پست را برای ویراستار ارسال می‌کند | ارسال موفق | FR-010، SC-007 |
| ۱۱ | نویسندهٔ ب برای پست نویسندهٔ الف بررسی می‌خواهد یا نتیجه‌های آن را می‌بیند | 404 | FR-002، SC-005، SC-008 |
| ۱۲ | ویراستار ۲ (بدون نقش نویسنده) و ویراستار ۱ (نویسنده و ویراستار، اما نه نویسندهٔ پست) نتیجه‌ها را می‌خواهند | به‌ترتیب 403 و 404 | FR-013، SC-008 |
| ۱۳ | درخواست بدون Token | 401 | FR-002، SC-005 |
| ۱۴ | درخواست بررسی برای پستی در وضعیت «در انتظار بررسی»، «تأییدشده» یا «منتشرشده» | 409 با `ReviewNotAvailable` | FR-002، SC-005 |
| ۱۵ | مدل جعلی تا پس از Deadline پاسخ نمی‌دهد | 504 با `ReviewTimedOut`؛ چیزی ذخیره نمی‌شود | FR-011 |
| ۱۶ | مدل جعلی `ProviderUnavailable` برمی‌گرداند، یا `ApiKey` تنظیم نشده است | 503 با `ReviewServiceUnavailable`؛ سایر مسیرهای پست کار می‌کنند | FR-011، R-13 |
| ۱۷ | مدل جعلی `Refused` یا `UnexpectedOutputForm` برمی‌گرداند | 502 با `ReviewRefused` یا `ReviewOutputInvalid` | research.md R-08 |
| ۱۸ | متن پست شامل دستوری برای تأیید یا انتشار پست است | وضعیت پست تغییر نمی‌کند؛ ساختار پاسخ مطابق قرارداد است | FR-012 |
| ۱۹ | نویسنده سه بار بررسی می‌خواهد و فهرست نتیجه‌ها را می‌بیند | سه نتیجه، جدیدترین اول | US1، FR-013 |
| ۲۰ | نویسنده پست هرگز منتشرنشده را حذف می‌کند | نتیجه‌های بررسی آن هم حذف می‌شوند | مورد مرزی حذف |
| ۲۱ | پست هنگام بررسی حذف می‌شود | 404؛ نتیجه ذخیره نمی‌شود | research.md R-11 |

آزمون‌های `Infra.AI` با Transport جعلی HTTP جداگانه بررسی می‌کنند که درخواست فقط `title` و `body` دارد،
`store` برابر `false` است، Schema در حالت strict ارسال می‌شود، هیچ Tool وجود ندارد و هر وضعیت پاسخ
Provider به Outcome درست تبدیل می‌شود ([contracts/application-ai.md](./contracts/application-ai.md)).

## آزمون Smoke و Evaluation با Provider واقعی

این دو فقط با اقدام صریح و کلید معتبر اجرا می‌شوند و جزو `dotnet test` عادی نیستند:

</div>

```powershell
$env:BLOGSPECLAB_PROVIDER_TESTS = "1"
$env:ContentReview__ApiKey = "<xai-api-key>"
dotnet test BlogSpecLab.slnx --filter "Category=ProviderSmoke"
dotnet test BlogSpecLab.slnx --filter "Category=Evaluation"
```

<div dir="rtl">

- **Smoke** (`Category=ProviderSmoke`، فایل `tests/BlogSpecLab.Tests/Evaluation/ContentReviewProviderSmokeTests.cs`):
  یک پست کوتاه نمونه بررسی می‌شود و فقط شکل و قابل پردازش بودن پاسخ بررسی می‌شود، نه کیفیت آن. نام Model
  بازگشتی در خروجی آزمون نوشته می‌شود.
- **Evaluation** (`Category=Evaluation`، فایل `tests/BlogSpecLab.Tests/Evaluation/ContentReviewEvaluationTests.cs`):
  Corpus `tests/BlogSpecLab.Tests/Evaluation/Corpus/content_review_corpus_v1.json` اجرا و شواهد در
  `specs/002-ai-content-review/evaluation/results/content-review-eval-<زمان>Z.json` ثبت می‌شوند. معیارها،
  Rubric و آستانه‌ها در [evaluation/protocol.md](./evaluation/protocol.md) آمده‌اند؛ آستانه‌ها پیشنهادی‌اند،
  از بخش `ContentReviewEvaluation` قابل تنظیم‌اند و پیش از پذیرش نتیجه تأیید انسانی می‌خواهند.
- بدون `BLOGSPECLAB_PROVIDER_TESTS=1` یا بدون کلید، هر دو آزمون Skip می‌شوند.
- این دو اجرا هنوز انجام نشده‌اند.

## معیار تکمیل

- همهٔ سناریوهای جدول بالا در `dotnet test` به‌صورت خودکار و بدون کلید API موفق باشند.
- آزمون Smoke با کلید واقعی موفق باشد.
- شواهد Evaluation ثبت و آستانه‌های تأییدشده پاس شده باشند؛ در غیر این صورت انتخاب Model یا Prompt دوباره
  بررسی شود (research.md، R-01).

</div>

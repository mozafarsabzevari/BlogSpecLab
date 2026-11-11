<div dir="rtl">

# طرح پیاده‌سازی: بررسی هوشمند محتوای پست پیش از ارسال برای ویراستار

| مشخصه | مقدار |
|-------|-------|
| شاخه | `main` |
| تاریخ | 2026-11-11 |
| مشخصات | [spec.md](./spec.md) |
| ورودی | مشخصات قابلیت در `specs/002-ai-content-review/spec.md` (تأییدشده در 2026-11-11) |

> این سند با دستور `/speckit-plan` و براساس قالب Plan در Spec Kit تهیه شده است.

## خلاصه

نویسنده برای پست خودش در وضعیت «پیش‌نویس» یا «بازگشت برای اصلاح» بررسی هوشمند درخواست می‌کند. سامانه
عنوان و متن آخرین نسخهٔ ذخیره‌شده را به مدل زبانی می‌فرستد، خروجی ساخت‌یافته را مستقل اعتبارسنجی می‌کند و
نتیجهٔ معتبر را با وضعیت «مشکلاتی یافت شد»، «مشکلی یافت نشد» یا «نیاز به اطلاعات بیشتر» ذخیره می‌کند. فقط
نویسندهٔ پست نتیجه‌ها را می‌بیند. بررسی هیچ تغییری در پست، وضعیت یا سابقهٔ آن ایجاد نمی‌کند و پیش‌شرط یا
جایگزین تأیید ویراستار نیست.

رویکرد فنی:

- پروژهٔ جدید `Infra.AI` با Package رسمی `OpenAI` نسخهٔ `2.14.0` و Responses API، متصل به Provider xAI
  و Model `grok-4.7` (research.md، R-01 و R-02).
- Structured Output با Schema نسخه‌دار `content_review_v1` در حالت strict
  ([contracts/model-output.schema.json](./contracts/model-output.schema.json)).
- Application مالک مجوز، Snapshot، Deadline ۶۰ ثانیه‌ای، اعتبارسنجی مستقل، بررسی دوباره و ذخیره است؛
  `Infra.AI` فقط درخواست را می‌فرستد و پاسخ را به Outcome مستقل از SDK تبدیل می‌کند
  ([contracts/application-ai.md](./contracts/application-ai.md)).
- نتیجهٔ معتبر، Refusal و Failure فنی کاملاً جدا هستند و هیچ Failure به‌صورت «مشکلی یافت نشد» نمایش
  داده نمی‌شود (research.md، R-08).
- فقط عنوان و متن پست ارسال می‌شوند، با `store=false` و بدون ثبت محتوا در Log (research.md، R-05).
- آزمون‌های قطعی با مدل جعلی در CI، و Smoke و Evaluation کیفیت با Provider واقعی فقط با اقدام صریح
  (research.md، R-12).

جزئیات و دلیل هر تصمیم در [research.md](./research.md) آمده است.

## زمینهٔ فنی

| موضوع | تصمیم |
|-------|-------|
| زبان و نسخه | C# روی .NET 10 و ASP.NET Core (طبق Constitution) |
| وابستگی‌های اصلی | موارد موجود Feature قبلی، به‌علاوهٔ Package رسمی `OpenAI` نسخهٔ `2.14.0` فقط در `Infra.AI` (R-01، R-02) |
| Provider و Model | xAI، Endpoint `https://api.x.ai/v1`، Model اولیهٔ `grok-4.7`، Responses API، غیر Streaming، بدون Tool و بدون Fallback (R-01) |
| Structured Output | JSON Schema `content_review_v1` در حالت strict از طریق `ResponseTextFormat.CreateJsonSchemaFormat` (R-02، R-03) |
| ذخیره‌سازی | SQLite و EF Core موجود؛ جدول جدید `ContentReviews` با مشکلات به‌صورت JSON و یک Migration جدید (R-14، [data-model.md](./data-model.md)) |
| ورود و مجوز | همان روش Feature قبلی؛ فقط نقش نویسنده و مالک پست (FR-001، FR-002) |
| آزمون | xUnit و WebApplicationFactory با `IContentReviewModel` جعلی؛ Transport جعلی HTTP برای `Infra.AI`؛ Smoke و Eval با متغیر محیطی (R-12) |
| بستر اجرا | همان Feature قبلی؛ دسترسی خروجی به `api.x.ai` فقط برای بررسی واقعی |
| نوع پروژه | سرویس وب (HTTP API)؛ دو مسیر جدید زیر مسیرهای نویسنده |
| هدف کارایی | دست‌کم ۹۰٪ پاسخ‌ها کمتر از ۳۰ ثانیه (SC-006)، سنجیده در Eval؛ Deadline سخت ۶۰ ثانیه (FR-011) |
| محدودیت‌ها | برنامه بدون کلید API اجرا می‌شود (اصل V.3)؛ Retry خودکار SDK خاموش؛ سقف ۸۰۰۰ Token خروجی؛ هیچ محتوایی در Log (R-05، R-09، R-13) |
| مقیاس | آموزشی؛ هر درخواست یک فراخوانی مدل؛ متن پست تا ۵۰٬۰۰۰ نویسه؛ بدون محدودیت تعداد درخواست (R-15) |

## بررسی انطباق با Constitution

این بررسی پیش از پژوهش و دوباره پس از طراحی انجام شد. نتیجهٔ هر دو بار یکسان است.

| اصل | شرح بررسی | نتیجه |
|-----|-----------|-------|
| I. توسعهٔ مبتنی بر مشخصات | Specification تأییدشده (2026-11-11) و شفاف‌سازی FR-013 مبنای طرح است؛ هر نیازمندی در مدل داده، قرارداد یا سناریوی quickstart نگاشت دارد. | ✅ |
| II. معماری و وابستگی‌ها | `Infra.AI` اکنون نیاز واقعی دارد و طبق ساختار هدف Constitution ساخته می‌شود؛ فقط به `Core.Application` وابسته است و به `Infra.DataAccess` وابستگی ندارد؛ `Infra.Qdrant` ساخته نمی‌شود. | ✅ |
| II.6 عدم تکرار منطق | قواعد سازگاری نتیجه در Domain و اعتبارسنجی خروجی در Application است؛ `Infra.AI` و Endpoint هیچ قاعدهٔ کسب‌وکاری ندارند (R-04، R-07). | ✅ |
| II.8 انتزاع فقط با نیاز | `IContentReviewModel` به‌خاطر قاعدهٔ ۲ وابستگی و `IContentReviewRepository` مانند Repository پست لازم‌اند؛ Microsoft.Extensions.AI یا لایهٔ انتزاعی Provider اضافه نمی‌شود (R-01). | ✅ |
| III.3 خروجی غیرقابل اعتماد | Schema در سطح Provider و Validator مستقل در Application؛ مشکلات بدون ارجاع واقعی حذف می‌شوند (R-03، R-07). | ✅ |
| III.4 بدون تغییر وضعیت توسط مدل | مدل هیچ Tool ندارد؛ فقط Application نتیجه را ذخیره می‌کند و پست هرگز تغییر نمی‌کند (FR-009، R-04). | ✅ |
| III.6 اطلاعات محرمانه | کلید API فقط در User Secrets یا متغیر محیطی؛ محتوا و خروجی مدل در Log نیستند (R-05، R-13). | ✅ |
| IV. کیفیت و آزمون‌پذیری | آزمون‌های قطعی برای Domain، Application، `Infra.AI` و HTTP بدون مدل واقعی؛ Eval جدا برای کیفیت غیرقطعی (R-12). | ✅ |
| V.3 اجرا بدون مدل زبانی | بدون کلید، برنامه اجرا می‌شود و فقط این مسیر 503 می‌دهد (R-13). | ✅ |
| V.4 و V.8 ورود مرحله‌ای AI | انتخاب Provider و Model در این قابلیت و با بررسی محدودیت‌ها و پیامدها (R-01، R-05) انجام شد. | ✅ |
| V.5 Tool Calling و MCP | استفاده نمی‌شوند. | ✅ |
| VI. حاکمیت | چهار تصمیم سراسری پس از بازبینی انسانی در Technical Baseline نسخهٔ 1.1.0 ثبت شدند؛ Constitution تغییر نمی‌کند. | ✅ |

نتیجه: هیچ نقضی وجود ندارد.

## ساختار پروژه

### مستندات این قابلیت

</div>

```text
specs/002-ai-content-review/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── http-api.md
│   ├── application-ai.md
│   └── model-output.schema.json
├── checklists/
│   └── requirements.md
├── evaluation/      (later, Eval evidence)
└── tasks.md         (later, by /speckit-tasks)
```

<div dir="rtl">

### کد منبع

فقط بخش‌های جدید یا تغییرکرده نشان داده شده‌اند.

</div>

```text
BlogSpecLab.slnx                          (add Infra.AI)
src/
├── 01.Core/
│   ├── Domain/
│   │   └── ContentReviews/               (new)
│   └── Application/
│       └── ContentReviews/               (new)
├── 02.Infra/
│   ├── DataAccess/
│   │   ├── ContentReviews/               (new)
│   │   ├── BlogSpecLabDbContext.cs       (add ContentReviews)
│   │   └── Migrations/                   (new migration)
│   └── AI/                               (new project)
│       ├── BlogSpecLab.Infra.AI.csproj
│       ├── DependencyInjection.cs
│       └── ContentReviews/
└── 03.Endpoints/
    └── Api/
        ├── BlogSpecLab.Endpoints.Api.csproj   (reference Infra.AI)
        ├── Program.cs                         (register services)
        ├── appsettings.json                   (ContentReview section, no key)
        └── ContentReviews/                    (new)
tests/
└── BlogSpecLab.Tests/
    ├── BlogSpecLab.Tests.csproj           (reference Infra.AI)
    ├── Domain/                            (ContentReview rules)
    ├── Application/                       (service and validator)
    ├── Infra/AI/                          (fake HTTP transport)
    ├── Api/                               (acceptance with fake model)
    └── Evaluation/                        (corpus, smoke, eval runner)
```

<div dir="rtl">

محتوای پوشه‌ها:

| پوشه | محتوا |
|------|-------|
| `Domain/ContentReviews` | موجودیت `ContentReview`، `ContentIssue`، `MissingInformationItem`، Enumها و قواعد سازگاری (FR-006) |
| `Application/ContentReviews` | `ContentReviewService`، `ContentReviewValidator`، قراردادهای `IContentReviewModel` و `IContentReviewRepository`، مدل‌های ورودی و خروجی، تنظیمات Deadline و خطاها |
| `DataAccess/ContentReviews` | پیکربندی EF Core و پیاده‌سازی `ContentReviewRepository` |
| `Infra.AI/ContentReviews` | پیاده‌سازی `IContentReviewModel` با `ResponsesClient`، Prompt نسخه‌دار، Schema جاسازی‌شده، تبدیل پاسخ به Outcome |
| `Api/ContentReviews` | دو مسیر نویسنده و تبدیل کدهای خطای جدید به ProblemDetails |
| `Tests/Infra/AI` | آزمون درخواست و پاسخ با Transport جعلی و برابری Schema با فایل `contracts` |
| `Tests/Evaluation` | Corpus برچسب‌دار، آزمون Smoke و اجراکنندهٔ Eval؛ فقط با `BLOGSPECLAB_PROVIDER_TESTS=1` |

جهت وابستگی پروژه‌ها:

</div>

```text
Core.Application   → Core.Domain
Infra.DataAccess   → Core.Application, Core.Domain
Infra.AI           → Core.Application
Endpoints.Api      → Core.Application, Infra.DataAccess, Infra.AI
Tests              → Core.Domain, Core.Application, Infra.AI, Endpoints.Api
```

<div dir="rtl">

### تصمیم دربارهٔ ساختار

ساختار هدف Constitution بدون تغییر استفاده می‌شود و فقط پروژهٔ `Infra.AI` که اکنون نیاز واقعی دارد اضافه
می‌شود. `Infra.AI` به `Infra.DataAccess` وابسته نیست (قاعدهٔ ۵ اصل II). همهٔ آزمون‌ها، از جمله Smoke و
Eval، در همان پروژهٔ `BlogSpecLab.Tests` با پوشه‌های جدا قرار می‌گیرند.

## پیگیری پیچیدگی

هیچ نقضی از Constitution وجود ندارد؛ بنابراین موردی برای توجیه ثبت نمی‌شود.

## تصمیم‌های ثبت‌شده در Technical Baseline

تصمیم‌های زیر اثر سراسری بر همهٔ Integrationهای هوش مصنوعی آینده دارند. پس از بازبینی انسانی در
2026-11-11 در بخش ۹ (AI Integration) نسخهٔ 1.1.0
[Technical Baseline](../../docs/architecture/technical-baseline.md) ثبت شدند.

| شناسه | تصمیم |
|-------|-------|
| AI-01 | قرارداد Structured Output صریح و نسخه‌دار، اعمال Schema در سطح Provider، و Validator مستقل در Application |
| AI-02 | تفکیک نتیجهٔ معتبر، Refusal و Failure فنی؛ هیچ Failure به‌صورت پاسخ موفق یا فهرست خالی نمایش داده نمی‌شود |
| AI-03 | Data Minimization و ثبت نکردن محتوای کاربر و خروجی مدل در Log به‌صورت پیش‌فرض |
| AI-04 | تفکیک آزمون قطعی نرم‌افزار در CI از Evaluation نسخه‌دار کیفیت مدل |

تصمیم‌های زیر عمداً محدود به این قابلیت هستند و وارد Technical Baseline نشدند: Provider xAI و Endpoint
`https://api.x.ai/v1`، Model `grok-4.7`، Responses API، Package `OpenAI 2.14.0`، ریسک نگهداری داده در
xAI، Schema `content_review_v1` و دسته‌های آن، Prompt، فیلدهای ارسالی `title` و `body`، Deadline ۶۰
ثانیه‌ای، تنظیم Reasoning، ذخیرهٔ نتیجه فقط برای نویسندهٔ پست (FR-013)، Dataset و آستانه‌های Eval.

## نکات باز پیش از Tasks

- آستانه‌های پیشنهادی Eval (research.md، R-12) تأیید انسانی می‌خواهند.
- ریسک نگهداری داده در xAI با وجود `store=false` (research.md، R-05) باید پذیرفته یا رد شود.
- امضای دقیق سازندهٔ `ResponsesClient` در نسخهٔ 2.14.0 هنگام پیاده‌سازی بررسی می‌شود (research.md، R-02).

</div>

<div dir="rtl">

# طرح پیاده‌سازی: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

| مشخصه | مقدار |
|-------|-------|
| شاخه | `main` |
| تاریخ | 2026-10-19 |
| مشخصات | [spec.md](./spec.md) |
| ورودی | مشخصات قابلیت در `specs/001-blog-post-management/spec.md` |

> این سند با دستور `/speckit-plan` و براساس قالب Plan در Spec Kit تهیه شده است.

## خلاصه

نویسنده‌ها پست‌های خود را ایجاد، ویرایش، ارسال و حذف می‌کنند. ویراستارها پست‌ها را تأیید یا با
ذکر دلیل بازمی‌گردانند و فقط پست‌های تأییدشده را منتشر می‌کنند. خوانندگان فقط پست‌های منتشرشده را
می‌بینند. هیچ پستی بدون تأیید ویراستاری غیر از نویسندهٔ آن منتشر نمی‌شود.

رویکرد فنی:

- یک سرویس وب (HTTP API) با ASP.NET Core و سبک Minimal APIs روی .NET 10.
- قواعد وضعیت پست در موجودیت `Post` در پروژهٔ `Core.Domain` متمرکز است.
- موارد کاربرد و کنترل مجوز در پروژهٔ `Core.Application` قرار دارد.
- ذخیره‌سازی با EF Core و SQLite در پروژهٔ `Infra.DataAccess` انجام می‌شود.
- هم‌زمانی با `Version` از نوع `Guid`، سرآیند `ETag` و فرمان‌های شرطی با `If-Match` کنترل می‌شود؛
  نسخهٔ قدیمی 412 و وضعیت ناسازگار 409 می‌گیرد. ویرایش نویسنده طبق FR-012 از این شرط مستثناست (R-11).
- ورود با Token مبهم (Opaque Bearer Token) انجام می‌شود که فقط هویت کاربر را مشخص می‌کند؛ نقش‌ها
  هنگام هر عملیات از پایگاه داده خوانده می‌شوند و Client نمی‌تواند نقش یا مجوز ایجاد کند.

جزئیات و دلیل هر تصمیم در [research.md](./research.md) آمده است.

## زمینهٔ فنی

| موضوع | تصمیم |
|-------|-------|
| زبان و نسخه | C# روی .NET 10 و ASP.NET Core (طبق Constitution) |
| وابستگی‌های اصلی | Minimal APIs (R-01)، EF Core با درایور SQLite (R-03)، ابزار Migrations (R-04) |
| ذخیره‌سازی | SQLite (R-02)، با جدول‌های پست‌ها، سابقهٔ وضعیت، کاربران، نقش‌ها و Tokenها ([data-model.md](./data-model.md)) |
| ورود و مجوز | Token مبهم در سرآیند `Authorization` (R-18)؛ ذخیرهٔ فقط Hash آن (R-19)؛ نقش‌ها از پایگاه داده (R-06)؛ ساخت کاربران و Tokenها با دستور راه‌اندازی محلی (R-20) |
| آزمون | xUnit و WebApplicationFactory با SQLite درون‌حافظه‌ای (R-05) |
| بستر اجرا | سرور چندسکویی؛ Windows برای توسعه، Windows یا Linux برای اجرا |
| نوع پروژه | سرویس وب (HTTP API)؛ رابط کاربری خارج از این قابلیت است |
| هدف کارایی | Specification هدف عددی ندارد؛ SC-004 و SC-005 به زمان کار کاربر مربوط‌اند |
| محدودیت‌ها | اجرا بدون مدل زبانی، MCP، Agent یا پایگاه دادهٔ برداری (اصل V.3)؛ بدون بستهٔ اضافه؛ Application هویت را فقط از `ICurrentUser` و نقش‌ها را فقط از `IUserAccessReader` می‌گیرد (R-06)؛ اجرای عادی برنامه هیچ Migration یا دادهٔ ضمنی ایجاد نمی‌کند (R-20) |
| مقیاس | آموزشی: ده‌ها کاربر و حداکثر چند هزار پست؛ ۱۵ مسیر API در سه گروه؛ بدون صفحه‌بندی (R-16) |

## بررسی انطباق با Constitution

این بررسی پیش از پژوهش و دوباره پس از طراحی انجام شد. نتیجهٔ هر دو بار یکسان است.

| اصل | شرح بررسی | نتیجه |
|-----|-----------|-------|
| I. توسعهٔ مبتنی بر مشخصات | Specification تأییدشده (2026-10-24) و شفاف‌سازی‌های آن مبنای طرح است؛ هر نیازمندی در مدل داده یا قرارداد API نگاشت دارد. | ✅ |
| II. معماری و وابستگی‌ها | فقط پروژه‌های Domain، Application، DataAccess، Api و Tests ساخته می‌شوند؛ پروژه‌های AI و Qdrant ساخته نمی‌شوند؛ جهت وابستگی‌ها مطابق قواعد است. | ✅ |
| II.6 عدم تکرار منطق | انتقال‌های وضعیت فقط در `Post` انجام می‌شوند (R-10)؛ مسیرهای API فقط درخواست را به Application می‌سپارند. | ✅ |
| II.8 انتزاع فقط با نیاز | `IPostRepository`، `IUserAccessReader` و `IAccessTokenReader` به‌خاطر قاعدهٔ ۲ وابستگی پروژه‌ها و `ICurrentUser` برای جدا ماندن Application از روش ورود لازم‌اند؛ دستور راه‌اندازی محلی در همان پروژهٔ Api است و پروژهٔ جدیدی ساخته نمی‌شود؛ کتابخانهٔ Mediator یا State Machine استفاده نمی‌شود (R-09، R-10، R-20). | ✅ |
| III. امنیت | قواعد وضعیت در Domain و مجوزها در Application؛ احراز هویت فقط هویت را مشخص می‌کند و نقش‌ها از منبع سمت سرور خوانده می‌شوند (R-06)؛ فقط Hash هر Token ذخیره می‌شود و Token در Log ثبت نمی‌شود (R-19)؛ پاسخ 404 برای پست‌های غیرقابل دسترس (R-13)؛ بدون اطلاعات محرمانه در کد. | ✅ |
| IV. کیفیت و آزمون‌پذیری | آزمون واحد برای Domain و Application و آزمون پذیرش HTTP برای همهٔ سناریوهای quickstart، از جمله دسترسی غیرمجاز و هم‌زمانی؛ زمان در آزمون‌ها قابل کنترل است (R-08). | ✅ |
| V. سادگی | Minimal APIs، سرویس‌های ساده، SQLite، بدون صفحه‌بندی و بدون قابلیت هوش مصنوعی. | ✅ |
| VI. حاکمیت | تصمیم‌های سراسری با برچسب «کاندید Technical Baseline پروژه» برای بازبینی انسانی مشخص شده‌اند؛ Constitution تغییر نمی‌کند. | ✅ |

نتیجه: هیچ نقضی وجود ندارد.

## ساختار پروژه

### مستندات این قابلیت

</div>

```text
specs/001-blog-post-management/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── http-api.md
├── checklists/
│   └── requirements.md
└── tasks.md        (later, by /speckit-tasks)
```

<div dir="rtl">

### کد منبع

</div>

```text
BlogSpecLab.slnx
src/
├── 01.Core/
│   ├── Domain/
│   │   ├── BlogSpecLab.Core.Domain.csproj
│   │   └── Posts/
│   └── Application/
│       ├── BlogSpecLab.Core.Application.csproj
│       ├── Common/
│       └── Posts/
├── 02.Infra/
│   └── DataAccess/
│       ├── BlogSpecLab.Infra.DataAccess.csproj
│       ├── BlogSpecLabDbContext.cs
│       ├── Posts/
│       ├── Identity/
│       └── Migrations/
└── 03.Endpoints/
    └── Api/
        ├── BlogSpecLab.Endpoints.Api.csproj
        ├── Program.cs
        ├── Authentication/
        ├── Provisioning/
        └── Posts/
tests/
└── BlogSpecLab.Tests/
    ├── BlogSpecLab.Tests.csproj
    ├── Domain/
    ├── Application/
    └── Api/
```

<div dir="rtl">

محتوای پوشه‌ها:

| پوشه | محتوا |
|------|-------|
| `Domain/Posts` | موجودیت `Post`، وضعیت‌ها (`PostStatus`)، سابقهٔ وضعیت (`StatusChange`) و خطاهای دامنه |
| `Application/Common` | قراردادهای `ICurrentUser`، `IUserAccessReader` و `IAccessTokenReader`، نوع `Role`، و نتایج و خطاهای Application |
| `Application/Posts` | سرویس‌های `AuthorPostService`، `EditorPostService`، `PublicPostQueries`، قرارداد `IPostRepository` و مدل‌های ورودی و خروجی |
| `DataAccess/Posts` | پیکربندی EF Core و پیاده‌سازی `PostRepository` |
| `DataAccess/Identity` | جدول‌های کاربران، نقش‌ها و Tokenها؛ پیاده‌سازی `IUserAccessReader` و `IAccessTokenReader`؛ منطق ساخت کاربر و صدور Token |
| `DataAccess/Migrations` | فایل‌های ساخت و تغییر ساختار پایگاه داده |
| `Api/Program.cs` | پیکربندی برنامه و تزریق وابستگی‌ها |
| `Api/Authentication` | AuthenticationHandler مربوط به Token مبهم و پیاده‌سازی `ICurrentUser` |
| `Api/Provisioning` | خواندن آرگومان‌های دستورهای `provision-user` و `issue-token` (فقط در محیط Development) |
| `Api/Posts` | مسیرهای نویسنده، ویراستار و عمومی، و تبدیل خطاها به ProblemDetails |
| `Tests/Domain` | آزمون قواعد `Post` و انتقال‌های وضعیت |
| `Tests/Application` | آزمون مجوزها و موارد کاربرد |
| `Tests/Api` | آزمون‌های پذیرش HTTP براساس سناریوهای quickstart |

جهت وابستگی پروژه‌ها:

</div>

```text
Core.Application   → Core.Domain
Infra.DataAccess   → Core.Application, Core.Domain
Endpoints.Api      → Core.Application, Infra.DataAccess
Tests              → Core.Domain, Core.Application, Endpoints.Api
```

<div dir="rtl">

### تصمیم دربارهٔ ساختار

ساختار هدف Constitution بدون تغییر استفاده می‌شود و فقط پروژه‌های موردنیاز این قابلیت ساخته
می‌شوند. پروژه‌های `Infra.AI` و `Infra.Qdrant` ساخته نمی‌شوند، چون این قابلیت هوش مصنوعی ندارد
(اصل V.3). همهٔ آزمون‌ها در یک پروژهٔ `BlogSpecLab.Tests` با پوشه‌های جداگانه قرار می‌گیرند.

## پیگیری پیچیدگی

هیچ نقضی از Constitution وجود ندارد؛ بنابراین موردی برای توجیه ثبت نمی‌شود.

## کاندیدهای Technical Baseline پروژه

تصمیم‌های زیر اثر سراسری دارند و باید پیش از پذیرش، توسط انسان بازبینی شوند. جزئیات هر کدام در
[research.md](./research.md) آمده است.

| شناسه | تصمیم |
|-------|-------|
| R-01 | سبک API: Minimal APIs |
| R-02 | پایگاه داده: SQLite |
| R-03 | دسترسی به داده: EF Core فقط در پروژهٔ DataAccess |
| R-04 | مدیریت ساختار پایگاه داده: EF Core Migrations |
| R-05 | آزمون: xUnit و WebApplicationFactory با SQLite درون‌حافظه‌ای |
| R-06 | جداسازی احراز هویت از مجوز: احراز هویت فقط شناسهٔ کاربر را مشخص می‌کند و نقش‌ها هنگام هر عملیات حساس از منبع سمت سرور خوانده می‌شوند؛ ورودی Client هرگز نقش یا مجوز ایجاد نمی‌کند |
| R-07 | قالب خطا: ProblemDetails همراه با فیلد `code` |
| R-08 | شناسه‌ها از نوع `Guid`؛ زمان به UTC و از طریق `TimeProvider` |
| R-09 | موارد کاربرد به‌صورت سرویس‌های ساده و بدون Mediator |

روش مشخص ورود در این قابلیت، یعنی Token مبهم (R-18)، نگهداری Hash آن (R-19) و دستور راه‌اندازی
محلی (R-20)، عمداً محدود به این قابلیت است و کاندید Technical Baseline نیست.

</div>

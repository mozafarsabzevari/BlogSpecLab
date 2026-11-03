<div dir="rtl">

# شواهد اعتبارسنجی: مدیریت و انتشار پست

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `001-blog-post-management` |
| تاریخ | 2026-11-03 |
| وظایف | T058، T059، T060 (بخش خودکار)، T061 در [tasks.md](./tasks.md) |

همهٔ نتایج زیر از اجرای واقعی روی Repository در همین تاریخ است. هیچ Token، Credential یا متن خصوصی
در این سند ثبت نشده است.

## ۱. Restore، Build و Test

</div>

```powershell
dotnet --version                 # 10.0.403
dotnet restore BlogSpecLab.slnx  # موفق، هر پنج پروژه
dotnet build BlogSpecLab.slnx    # Build succeeded، 0 Warning، 0 Error
dotnet test BlogSpecLab.slnx     # Passed: 142، Failed: 0، Skipped: 0
```

<div dir="rtl">

## ۲. Project Referenceها، بسته‌ها و مرزها (T058)

| بررسی | نتیجه |
|-------|-------|
| Project Referenceها | Domain بدون رفرنس؛ Application → Domain؛ DataAccess → Application و Domain؛ Api → Application و DataAccess؛ Tests → Domain، Application و Api. مطابق plan.md و اصل II. |
| بسته‌ها | DataAccess: `Microsoft.EntityFrameworkCore.Sqlite`؛ Api: `Microsoft.EntityFrameworkCore.Design` (فقط زمان طراحی)؛ Tests: `Microsoft.AspNetCore.Mvc.Testing`، `Microsoft.NET.Test.Sdk`، `xunit`، `xunit.runner.visualstudio`. همه نسخهٔ 10.0.14 یا ابزار آزمون. |
| AI، MCP، RAG، Qdrant، Swagger/OpenAPI | هیچ بسته یا ارجاعی در `src/` و `tests/` نیست. |
| استقلال Core | Domain و Application هیچ `using` از EF Core، ASP.NET Core یا HTTP و هیچ `DbContext`/`IQueryable` ندارند. |
| Business Logic در Api | Endpointها فقط Binding، خواندن `If-Match`، فراخوانی سرویس و نگاشت نتیجه را انجام می‌دهند. تنها کنترل محلی، اعتبار پارامتر `status` فهرست ویراستار است (بخش ۷). |
| Business Logic در DataAccess | `PostRepository` فقط خواندن، فیلتر وضعیت، مرتب‌سازی و ذخیره است و تعارض EF را به `ConcurrencyConflictException` تبدیل می‌کند. |
| تطبیق با `contracts/http-api.md` | مسیرها، متدها، مدل‌های پاسخ (`AuthorPostSummary/Detail`، `EditorPostSummary/Detail/History`، `PublicPostSummary/Detail`)، `ETag`/`If-Match` (و نبود آن در `PUT` و مسیرهای عمومی)، status codeها و `code`ها با کد و اجرای واقعی (بخش ۴) سازگارند. کد `InvalidStatus` که در قرارداد نبود، اضافه شد. OpenAPI عمداً ایجاد نشده است. |

## ۳. Secret و Credential (T059)

- در کد، تنظیمات، مستندات و آزمون‌ها هیچ Token، Secret، API Key یا Connection String حساسی نیست؛
  Connection String فقط مسیر فایل محلی (`Data Source=blogspeclab.db`) است. واژهٔ «Secret» فقط به‌عنوان
  متن ساختگی پست در آزمون‌های عدم افشا به کار رفته است.
- `LocalProvisioner` فقط Hash SHA-256 Token را ذخیره می‌کند؛ `ProvisioningCommands` Token خام را فقط یک
  بار در خروجی دستور چاپ می‌کند و فقط در Development اجرا می‌شود.
- `OpaqueTokenAuthenticationHandler` و `AccessTokenReader` Token را Log نمی‌کنند؛ در اجرای T060، هیچ
  یک از Tokenها در Log برنامه پیدا نشد.
- `*.db`، `*.db-shm` و `*.db-wal` در `.gitignore` مستثنا هستند.

## ۴. اجرای quickstart (T060، بخش خودکار)

</div>

```powershell
dotnet ef --version   # 10.0.14 (ابزار سراسری)
dotnet ef database update --project src/02.Infra/DataAccess --startup-project src/03.Endpoints/Api
dotnet run --project src/03.Endpoints/Api -- provision-user --name "Author A" --roles Author
dotnet run --project src/03.Endpoints/Api -- issue-token --user-id <guid>
dotnet run --project src/03.Endpoints/Api --launch-profile http
```

<div dir="rtl">

- پایگاه دادهٔ تازه: هر دو Migration (`InitialIdentity` و `AddPosts`) اعمال شدند. فایل در
  `src/03.Endpoints/Api/blogspeclab.db` ایجاد شد و `dotnet run` از ریشهٔ Repository همان فایل را به کار برد.
- چهار کاربر نمونهٔ quickstart با `provision-user` ساخته شدند؛ `issue-token` برای یک کاربر موجود Token
  دوم و متفاوت صادر کرد.

مسیر اصلی (درخواست HTTP به `http://localhost:5059`):

| گام | پاسخ |
|-----|------|
| نویسندهٔ الف: ایجاد | 201، `Draft`، با `ETag` |
| ویرایش بدون `If-Match` | 200، `Draft` |
| ارسال | 200، `InReview` |
| ویراستار ۲: بازگرداندن با دلیل | 200، `Returned` |
| فهرست نویسنده | `Returned` همراه آخرین دلیل بازگشت و `version` |
| ویرایش و ارسال دوباره | 200، `Returned` و سپس `InReview` |
| ویراستار ۲: تأیید | 200، `Approved` |
| ویراستار ۱: انتشار | 200، `Published` |
| خوانندهٔ ناشناس: فهرست و جزئیات | 200؛ فقط `id`، `title`، `authorDisplayName`، `publishedAt` (و `body` در جزئیات)؛ بدون `ETag` |
| ویراستار ۲: سابقه | ارسال، بازگرداندن، تأیید (ویراستار ۲) و انتشار (ویراستار ۱) با نام انجام‌دهنده |

شکست‌ها:

| مورد | status | `code` |
|------|--------|--------|
| بدون Token در مسیر نویسنده | 401 | `Unauthenticated` |
| Token نامعتبر در مسیر عمومی | 401 | `Unauthenticated` |
| ویراستار ۱ پست خودش را تأیید می‌کند | 403 | `SelfApprovalNotAllowed` |
| نویسندهٔ بدون نقش ویراستار تأیید می‌کند | 403 | `Forbidden` |
| تأیید با `If-Match` قدیمی | 412 | `StaleVersion` همراه `currentStatus` و `currentVersion`، بدون متن پست |
| انتشار بدون `If-Match` | 428 | `PreconditionRequired` |
| انتشار پست `InReview` | 409 | `InvalidStatusTransition` |
| ویرایش پست `Published` بدون تأیید پیامد | 409 | `ConfirmationRequired` |
| حذف پست `Published` | 409 | `InvalidStatusTransition` |
| حذف پیش‌نویسِ قبلاً منتشرشده | 409 | `DeleteNotAllowed` |
| نویسندهٔ ب پست نویسندهٔ الف را می‌خواند | 404 | `NotFound` |
| مسیر عمومی برای پست منتشرنشده و پست ناموجود | 404 | `NotFound`؛ بدنهٔ یکسان |
| `status` نامعتبر در فهرست ویراستار | 400 | `InvalidStatus` |

همچنین خارج‌کردن از انتشار و انتشار دوباره بدون تأیید مجدد، ویرایش با تأیید پیامد (بازگشت به `Draft` و
حذف از فهرست عمومی)، پس گرفتن ارسال (سناریوی ۴) و هر دو ترتیب سناریوی ۱۶ اجرا شدند: تأیید سپس
بازگرداندن با همان `If-Match` → 412؛ بازگرداندن سپس تأیید → 404.

## ۵. شواهد Success Criteria

| معیار | سناریوهای quickstart | آزمون‌های پوشش‌دهنده | نتیجه |
|-------|----------------------|----------------------|-------|
| SC-001: انتشار فقط با تأیید ثبت‌شده؛ تأییدکننده و منتشرکننده در سابقه | ۸، ۱۰، ۱۸، ۲۶ | `Api/PublicationTests.cs`، `Domain/PostPublicationTests.cs` | موفق؛ در اجرای HTTP، سابقه تأیید ویراستار ۲ و انتشار ویراستار ۱ را نشان داد و انتشار پست `InReview` 409 گرفت. |
| SC-002: رد ۱۰۰٪ تلاش‌های غیرمجاز | ۲، ۷، ۹، ۱۰، ۲۱، ۲۲ | `Api/AuthorDraftsTests.cs`، `Api/ReviewWorkflowTests.cs`، `Api/PublicationTests.cs`، `Domain/PostReviewTests.cs` | موفق؛ در اجرای HTTP خودتأییدی 403، تأیید بدون نقش 403، دسترسی نویسندهٔ دیگر 404 و انتشار تأییدنشده 409 بودند. |
| SC-003: هیچ پست منتشرنشده‌ای برای خواننده دیده نشود | ۱۱، ۱۴، ۱۷، ۲۷ | `Api/PublicReadTests.cs`، `Api/PublicationTests.cs` | موفق؛ در اجرای HTTP پست منتشرنشده 404 با بدنهٔ یکسان پست ناموجود گرفت و پس از بازگشت به `Draft` از فهرست عمومی حذف شد. |
| SC-006: وضعیت درست و آخرین دلیل بازگشت در فهرست نویسنده | ۱، ۵ | `Api/ReviewWorkflowTests.cs` (سناریوی ۵ و SC-006)، `Api/AuthorDraftsTests.cs` | موفق؛ در اجرای HTTP فهرست نویسنده `Returned` و دلیل بازگشت را نشان داد. |

همهٔ این آزمون‌ها در اجرای بخش ۱ جزو ۱۴۲ آزمون موفق بودند.

**SC-004 و SC-005:** این دو معیار زمان انجام کار توسط انسان را می‌سنجند (کمتر از ۵ دقیقه برای ایجاد و
ارسال پست؛ کمتر از ۱ دقیقه برای ثبت تصمیم ویراستار). سبز بودن آزمون‌ها جایگزین آن‌ها نیست. سناریوهای
۱، ۴ و ۵ با HTTP اجرا شدند، اما **سنجش انسانی هنوز انجام نشده است** و به همین دلیل T060 باز است.

## ۶. محدودیت‌ها

- اجرا فقط محلی و با SQLite فایل‌محور است؛ Migrationها فقط روی پایگاه دادهٔ محلی توسعه اعمال شدند.
- `dotnet-ef` ابزار سراسری است و Repository فایل Tool Manifest ندارد.
- تعارض هم‌زمان هنگام ذخیره فقط با Repository جعلی (`Application/ConcurrencyConflictTests.cs`) آزموده شده
  است، نه با دو اتصال واقعی SQLite.

## ۷. مشکلات باقی‌مانده

- فهرست ویراستار پارامتر `status` را پیش از بررسی نقش کنترل می‌کند؛ کاربر بدون نقش ویراستار با `status`
  نامعتبر 400 `InvalidStatus` و با `status` معتبر 403 می‌گیرد. قرارداد ترتیب این دو را مشخص نکرده است.
- جدول خطاهای `data-model.md` کد `InvalidStatus` را ندارد؛ فقط `contracts/http-api.md` اصلاح شد.
- یافتهٔ I1 اصلاح نشد: R-01 تا R-09 همه در `docs/architecture/technical-baseline.md` ثبت نشده‌اند (R-01
  و R-02 صریحاً مخصوص این قابلیت‌اند و `Guid` شناسه‌ها در R-08 و R-09 در آن نیامده‌اند).

</div>

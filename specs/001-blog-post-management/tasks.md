<div dir="rtl">

# Tasks: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

| مشخصه | مقدار |
|-------|-------|
| قابلیت | `001-blog-post-management` |
| شاخه | `main` |
| تاریخ | 2026-10-29 |
| ورودی‌ها | [spec.md](./spec.md)، [plan.md](./plan.md)، [research.md](./research.md)، [data-model.md](./data-model.md)، [contracts/http-api.md](./contracts/http-api.md)، [quickstart.md](./quickstart.md) |
| مراجع سراسری | [Constitution](../../.specify/memory/constitution.md)، [Technical Baseline](../../docs/architecture/technical-baseline.md) |

**آزمون‌ها:** الزامی هستند. اصل IV در Constitution آزمون خودکار قواعد Domain و موارد کاربرد را
می‌خواهد و [quickstart.md](./quickstart.md) پوشش هر سناریو با آزمون پذیرش را الزامی کرده است. در هر
داستان کاربر، آزمون‌ها پیش از پیاده‌سازی نوشته می‌شوند و باید ابتدا شکست بخورند.

**سازمان‌دهی:** وظایف براساس داستان‌های کاربر Specification گروه‌بندی شده‌اند تا هر داستان مستقل
پیاده‌سازی و آزموده شود.

## قالب وظایف

- `[P]`: قابل اجرای موازی (فایل متفاوت و بدون وابستگی به وظیفهٔ ناتمام)
- `[US1]`، `[US2]`، `[US3]`: داستان کاربر مربوط در spec.md
- نام فایل‌ها و مسیرها مطابق ساختار plan.md هستند.

ریشهٔ مسیرها:

| مخفف | مسیر |
|------|------|
| Domain | `src/01.Core/Domain` |
| Application | `src/01.Core/Application` |
| DataAccess | `src/02.Infra/DataAccess` |
| Api | `src/03.Endpoints/Api` |
| Tests | `tests/BlogSpecLab.Tests` |

در متن وظایف، مسیر کامل نوشته شده است.

---

## فاز ۱: Setup (راه‌اندازی پروژه)

**هدف:** ایجاد Solution و پروژه‌ها مطابق Constitution و plan.md.

- [ ] T001 ایجاد `BlogSpecLab.slnx` در ریشه و پنج پروژه با هدف `net10.0`: کتابخانهٔ `src/01.Core/Domain/BlogSpecLab.Core.Domain.csproj`، کتابخانهٔ `src/01.Core/Application/BlogSpecLab.Core.Application.csproj`، کتابخانهٔ `src/02.Infra/DataAccess/BlogSpecLab.Infra.DataAccess.csproj`، وب `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` و آزمون xUnit `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj`؛ همه به Solution اضافه شوند
- [ ] T002 افزودن Project Referenceها دقیقاً مطابق plan.md: Application → Domain؛ DataAccess → Application و Domain؛ Api → Application و DataAccess؛ Tests → Domain، Application و Api (در فایل‌های `.csproj` همان پروژه‌ها)
- [ ] T003 [P] افزودن بسته‌ها: `Microsoft.EntityFrameworkCore.Sqlite` به `src/02.Infra/DataAccess/BlogSpecLab.Infra.DataAccess.csproj`؛ `Microsoft.EntityFrameworkCore.Design` به `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj`؛ `Microsoft.AspNetCore.Mvc.Testing` به `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj`؛ هیچ بستهٔ دیگری اضافه نشود
- [ ] T004 [P] ایجاد `Directory.Build.props` در ریشه با `Nullable` و `ImplicitUsings` فعال برای همهٔ پروژه‌ها
- [ ] T005 [P] ایجاد یا تکمیل `.gitignore` در ریشه برای `bin/`، `obj/` و فایل‌های پایگاه دادهٔ محلی `*.db`
- [ ] T006 اجرای `dotnet build BlogSpecLab.slnx` و `dotnet test BlogSpecLab.slnx` و پس از موفقیت، افزودن همین دو دستور به بخش ۷ فایل `AGENTS.md`

**نقطهٔ بررسی:** Solution بدون خطا ساخته می‌شود و جهت وابستگی‌ها با Constitution (اصل II) منطبق است.

---

## فاز ۲: Foundational (زیرساخت پایهٔ مسدودکننده)

**هدف:** هویت، مجوز سمت سرور، پایگاه داده، احراز هویت با Token مبهم، نگاشت خطا و زیرساخت آزمون.
تا پایان این فاز هیچ داستان کاربری آغاز نمی‌شود.

### قراردادهای Application

- [ ] T007 [P] تعریف `ICurrentUser` با `IsAuthenticated` و `UserId` (بدون نقش) در `src/01.Core/Application/Common/ICurrentUser.cs` (R-06)
- [ ] T008 [P] تعریف `Role` (`Author`، `Editor`)، رکورد `UserAccess` و قرارداد `IUserAccessReader` در `src/01.Core/Application/Common/UserAccess.cs` (data-model.md)
- [ ] T009 [P] تعریف قرارداد `IAccessTokenReader` که Token خام را به شناسهٔ کاربر فعال تبدیل می‌کند در `src/01.Core/Application/Common/IAccessTokenReader.cs` (R-18، R-19)
- [ ] T010 [P] تعریف نوع نتیجهٔ Application مستقل از HTTP و فهرست ثابت کدهای خطا (`NotFound`، `Forbidden`، `Unauthenticated`، `StaleVersion`، `ConfirmationRequired`، `InvalidStatusTransition`، `DeleteNotAllowed`، `SelfApprovalNotAllowed`، `InvalidTitle`، `InvalidBody`، `ReturnReasonRequired`) همراه با `currentStatus` و `currentVersion` اختیاری در `src/01.Core/Application/Common/Result.cs`
- [ ] T011 [P] تعریف `ConcurrencyConflictException` در `src/01.Core/Application/Common/ConcurrencyConflictException.cs` تا Repositoryها خطای هم‌زمانی EF Core را بدون نشت جزئیات EF به Application گزارش کنند

### DataAccess

- [ ] T012 [P] ایجاد مدل‌های ذخیره‌سازی کاربر، نقش و Token و پیکربندی EF Core آن‌ها (کلید ترکیبی نقش، Index یکتا روی `TokenHash`، نقش به‌صورت متن) در `src/02.Infra/DataAccess/Identity/IdentityRecords.cs` و `src/02.Infra/DataAccess/Identity/IdentityConfiguration.cs`
- [ ] T013 ایجاد `BlogSpecLabDbContext` با جدول‌های `Users`، `UserRoles` و `AccessTokens` در `src/02.Infra/DataAccess/BlogSpecLabDbContext.cs` (وابسته به T012)
- [ ] T014 [P] پیاده‌سازی `UserAccessReader` که نام، فعال بودن و نقش‌های فعلی را در هر فراخوانی از پایگاه داده می‌خواند در `src/02.Infra/DataAccess/Identity/UserAccessReader.cs`
- [ ] T015 [P] پیاده‌سازی `AccessTokenReader`: محاسبهٔ SHA-256، جست‌وجو، بررسی انقضا با `TimeProvider` و فعال بودن کاربر، بدون ثبت Token در Log در `src/02.Infra/DataAccess/Identity/AccessTokenReader.cs`
- [ ] T016 [P] پیاده‌سازی `LocalProvisioner` برای ساخت کاربر با نقش‌ها و صدور Token (۳۲ بایت تصادفی با `RandomNumberGenerator`، کدگذاری base64url، ذخیرهٔ فقط Hash، انقضای اختیاری، برگرداندن Token خام فقط یک بار) در `src/02.Infra/DataAccess/Identity/LocalProvisioner.cs` (R-20)
- [ ] T017 افزودن متد ثبت سرویس‌های DataAccess (DbContext با Connection String، `UserAccessReader`، `AccessTokenReader`، `LocalProvisioner`) در `src/02.Infra/DataAccess/DependencyInjection.cs`
- [ ] T018 ایجاد Migration اولیهٔ جدول‌های هویت با نام `InitialIdentity` در `src/02.Infra/DataAccess/Migrations/`

### Api

- [ ] T019 [P] پیاده‌سازی `OpaqueTokenAuthenticationHandler`: خواندن `Authorization: Bearer`، فراخوانی `IAccessTokenReader`، تولید فقط Claim شناسهٔ کاربر، و شکست احراز هویت برای Token نامعتبر یا منقضی حتی در مسیرهای عمومی در `src/03.Endpoints/Api/Authentication/OpaqueTokenAuthenticationHandler.cs`
- [ ] T020 [P] پیاده‌سازی `HttpCurrentUser` برای `ICurrentUser` از کاربر احرازشدهٔ درخواست در `src/03.Endpoints/Api/Authentication/HttpCurrentUser.cs`
- [ ] T021 [P] پیاده‌سازی نگاشت نتیجهٔ Application به `ProblemDetails` با فیلد `code`، کدهای HTTP مطابق contracts/http-api.md، فیلدهای `currentStatus` و `currentVersion` برای 409 و 412، بدون محتوای پست یا جزئیات داخلی، و پاسخ 401 با `Unauthenticated` در `src/03.Endpoints/Api/Posts/ProblemDetailsMapping.cs`
- [ ] T022 [P] پیاده‌سازی دستورهای خط فرمان `provision-user` و `issue-token` که فقط در محیط Development اجرا می‌شوند، Migration انجام نمی‌دهند و Token را فقط یک بار در خروجی چاپ می‌کنند در `src/03.Endpoints/Api/Provisioning/ProvisioningCommands.cs`
- [ ] T023 تکمیل `src/03.Endpoints/Api/Program.cs`: ثبت `TimeProvider.System`، سرویس‌های DataAccess، Scheme احراز هویت پیش‌فرض، Authorization، ProblemDetails و اجرای دستورهای T022 به‌جای راه‌اندازی وب در صورت وجود آرگومان؛ بدون Migration یا ساخت داده هنگام Startup؛ افزودن `public partial class Program` برای آزمون (وابسته به T017 و T019 تا T022)
- [ ] T024 [P] افزودن Connection String محلی `ConnectionStrings:BlogSpecLab` با مقدار `Data Source=blogspeclab.db` در `src/03.Endpoints/Api/appsettings.json` و تنظیمات توسعه در `src/03.Endpoints/Api/appsettings.Development.json`

### زیرساخت آزمون

- [ ] T025 [P] پیاده‌سازی `ManualTimeProvider` قابل تنظیم برای آزمون‌های وابسته به زمان در `tests/BlogSpecLab.Tests/Api/Infrastructure/ManualTimeProvider.cs`
- [ ] T026 پیاده‌سازی `BlogSpecLabApiFactory` بر پایهٔ `WebApplicationFactory<Program>` با محیط `Testing`، SQLite درون‌حافظه‌ای با اتصال باز، `EnsureCreated` و جایگزینی `TimeProvider`؛ به‌همراه `TestUsers` برای ساخت مستقیم کاربران، نقش‌ها و Tokenهای نمونه (نویسندهٔ الف، نویسندهٔ ب، ویراستار ۱، ویراستار ۲) و ساخت `HttpClient` با Token هر کاربر در `tests/BlogSpecLab.Tests/Api/Infrastructure/BlogSpecLabApiFactory.cs` و `tests/BlogSpecLab.Tests/Api/Infrastructure/TestUsers.cs`

**نقطهٔ بررسی:** برنامه بالا می‌آید، دستور `provision-user` روی پایگاه دادهٔ Migrateشده کار می‌کند و
آزمون‌ها می‌توانند کاربر و Token بسازند.

---

## فاز ۳: User Story 1 — مدیریت پیش‌نویس‌ها توسط نویسنده (Priority: P1) 🎯 MVP

**هدف:** نویسنده پیش‌نویس می‌سازد، ویرایش و حذف می‌کند و فهرست پست‌های خود را با وضعیت می‌بیند.

**آزمون مستقل:** با Token نویسندهٔ الف، ایجاد، ویرایش، مشاهده و حذف یک پیش‌نویس کامل انجام می‌شود و
نویسندهٔ ب به آن دسترسی ندارد (quickstart.md، سناریوهای ۱، ۲، ۳، ۲۰ و ۲۳).

### آزمون‌های User Story 1

- [ ] T027 [P] [US1] آزمون‌های Domain برای ایجاد پست (وضعیت `Draft`، ثبت سابقه با وضعیت قبلی خالی، `Version` جدید)، اعتبارسنجی عنوان و متن (Trim، خالی، ۲۰۰/۲۰۱ و ۵۰٬۰۰۰/۵۰٬۰۰۱ نویسه)، ویرایش در `Draft` و `Returned` با `Version` تازه، و اجازهٔ حذف فقط برای پست هرگز منتشرنشده در `tests/BlogSpecLab.Tests/Domain/PostDraftTests.cs`
- [ ] T028 [P] [US1] آزمون‌های پذیرش HTTP برای سناریوهای ۱، ۲، ۳، ۲۰ و ۲۳ در quickstart.md، به‌علاوهٔ: حذف پیش‌نویس هرگز منتشرنشده (204 و حذف از فهرست)، حذف بدون `If-Match` (428)، حذف با نسخهٔ قدیمی (412)، وجود `ETag` در پاسخ یک پست، و 403 برای کاربری که نقش نویسنده ندارد در `tests/BlogSpecLab.Tests/Api/AuthorDraftsTests.cs`

### پیاده‌سازی User Story 1

- [ ] T029 [P] [US1] ایجاد `PostStatus` با پنج مقدار data-model.md در `src/01.Core/Domain/Posts/PostStatus.cs`
- [ ] T030 [P] [US1] ایجاد موجودیت `StatusChange` (فقط‌افزودنی) در `src/01.Core/Domain/Posts/StatusChange.cs`
- [ ] T031 [P] [US1] ایجاد `PostDomainException` با کد خطای ثابت در `src/01.Core/Domain/Posts/PostDomainException.cs`
- [ ] T032 [US1] پیاده‌سازی Aggregate `Post` با `Create`، `Edit` برای `Draft` و `Returned` (رد در `InReview`)، `EnsureCanDelete`، `HasEverBeenPublished`، به‌روزرسانی `LastModifiedAt` و `Version` تازه در هر تغییر؛ زمان از بیرون به متدها داده می‌شود در `src/01.Core/Domain/Posts/Post.cs` (وابسته به T029 تا T031)
- [ ] T033 [P] [US1] تعریف `IPostRepository` (خواندن با شناسه، فهرست نویسنده، فهرست براساس وضعیت، فهرست منتشرشده‌ها، افزودن، حذف و ذخیره با گزارش `ConcurrencyConflictException`) در `src/01.Core/Application/Posts/IPostRepository.cs`
- [ ] T034 [P] [US1] تعریف مدل‌های `AuthorPostSummary` و `AuthorPostDetail` مطابق contracts/http-api.md در `src/01.Core/Application/Posts/AuthorPostModels.cs`
- [ ] T035 [US1] پیاده‌سازی `AuthorPostService` برای ایجاد، فهرست، مشاهده، ویرایش (`Draft` و `Returned`) و حذف با نسخهٔ مورد انتظار؛ بررسی نقش نویسنده از `IUserAccessReader` در هر فراخوانی؛ پست دیگران `NotFound`؛ ذخیرهٔ نام نویسنده از جدول کاربران؛ زمان از `TimeProvider` در `src/01.Core/Application/Posts/AuthorPostService.cs`
- [ ] T036 [P] [US1] پیکربندی EF Core برای `Post` و `StatusChange` (وضعیت به‌صورت متن، `Version` به‌عنوان Concurrency Token، Indexهای data-model.md) در `src/02.Infra/DataAccess/Posts/PostConfiguration.cs` و افزودن جدول‌ها به `src/02.Infra/DataAccess/BlogSpecLabDbContext.cs`
- [ ] T037 [US1] پیاده‌سازی `PostRepository` و ثبت آن در `src/02.Infra/DataAccess/DependencyInjection.cs`، در فایل `src/02.Infra/DataAccess/Posts/PostRepository.cs`
- [ ] T038 [US1] ایجاد Migration با نام `AddPosts` در `src/02.Infra/DataAccess/Migrations/`
- [ ] T039 [P] [US1] پیاده‌سازی کمک‌کنندهٔ `ETag` و `If-Match`: نوشتن `ETag`، خواندن Guid داخل گیومه، 428 برای نبود سرآیند و 400 با `InvalidIfMatch` برای قالب نادرست در `src/03.Endpoints/Api/Posts/ETagHeaders.cs`
- [ ] T040 [US1] پیاده‌سازی مسیرهای `POST`، `GET` فهرست، `GET {id}`، `PUT {id}` و `DELETE {id}` زیر `/api/author/posts` و ثبت آن‌ها در `Program.cs`، در فایل `src/03.Endpoints/Api/Posts/AuthorEndpoints.cs`

**نقطهٔ بررسی:** User Story 1 به‌تنهایی کار می‌کند و آزمون‌های T027 و T028 موفق‌اند.

---

## فاز ۴: User Story 2 — ارسال برای بررسی و تصمیم ویراستار (Priority: P2)

**هدف:** نویسنده پست را ارسال یا پس می‌گیرد؛ ویراستار صف بررسی را می‌بیند و تأیید یا با دلیل
بازمی‌گرداند.

**آزمون مستقل:** با یک نویسنده و یک ویراستار، ارسال، دیده‌شدن در صف، تأیید و در سناریوی جداگانه
بازگرداندن با دلیل، بدون نیاز به انتشار (quickstart.md، سناریوهای ۴، ۵، ۶، ۷، ۹، ۱۶، ۲۱، ۲۲، ۲۵ و ۲۸).

### آزمون‌های User Story 2

- [ ] T041 [P] [US2] آزمون‌های Domain برای `Submit` (از `Draft` و `Returned`، رد محتوای نامعتبر)، `Withdraw`، `Approve` (رد تأیید توسط نویسندهٔ خودش)، `Return` (دلیل الزامی و به‌روزرسانی `LastReturnReason`)، ثبت انجام‌دهنده در سابقه و رد انتقال‌های غیرمجاز در `tests/BlogSpecLab.Tests/Domain/PostReviewTests.cs`
- [ ] T042 [P] [US2] آزمون‌های پذیرش HTTP برای سناریوهای ۴، ۵، ۶، ۷، ۹، ۱۶، ۲۱، ۲۲، ۲۵ و ۲۸ در quickstart.md، به‌علاوهٔ: ویرایش پست `InReview` (409)، صف `InReview` ویراستار، 404 برای پست `Draft` از مسیر جزئیات ویراستار، 200 بدون عنوان و متن برای همان پست از مسیر سابقه، 404 مسیر سابقه برای پست ناموجود، 403 مسیر سابقه برای کاربر بدون نقش ویراستار، و نمایش آخرین دلیل بازگشت در فهرست نویسنده (SC-006) در `tests/BlogSpecLab.Tests/Api/ReviewWorkflowTests.cs`

### پیاده‌سازی User Story 2

- [ ] T043 [US2] افزودن متدهای `Submit`، `Withdraw`، `Approve` و `Return` به `src/01.Core/Domain/Posts/Post.cs`
- [ ] T044 [US2] افزودن ارسال و پس گرفتن با نسخهٔ مورد انتظار به `src/01.Core/Application/Posts/AuthorPostService.cs`
- [ ] T045 [P] [US2] تعریف مدل‌های `EditorPostSummary`، `EditorPostDetail` همراه با `history` و `EditorPostHistory` (فقط `id`، `status` و `history`) در `src/01.Core/Application/Posts/EditorPostModels.cs`
- [ ] T046 [US2] پیاده‌سازی `EditorPostService` برای فهرست براساس وضعیت (فقط `InReview`، `Approved` و `Published`)، مشاهده با سابقه (`Draft` و `Returned` برابر `NotFound`)، مشاهدهٔ فقط سابقه برای پست در هر وضعیت بدون عنوان و متن (FR-011)، تأیید و بازگرداندن؛ بررسی نقش ویراستار از `IUserAccessReader` در هر فراخوانی؛ نتیجهٔ `StaleVersion` با وضعیت و نسخهٔ فعلی در `src/01.Core/Application/Posts/EditorPostService.cs`
- [ ] T047 [US2] افزودن مسیرهای `POST {id}/submit` و `POST {id}/withdraw` با `If-Match` به `src/03.Endpoints/Api/Posts/AuthorEndpoints.cs`
- [ ] T048 [US2] پیاده‌سازی مسیرهای `GET ?status=`، `GET {id}`، `GET {id}/history`، `POST {id}/approve` و `POST {id}/return` زیر `/api/editor/posts` و ثبت آن‌ها در `Program.cs`، در فایل `src/03.Endpoints/Api/Posts/EditorEndpoints.cs`

**نقطهٔ بررسی:** User Stories 1 و 2 هر دو مستقل کار می‌کنند.

---

## فاز ۵: User Story 3 — انتشار پست تأییدشده و مشاهده توسط خوانندگان (Priority: P3)

**هدف:** ویراستار پست تأییدشده را منتشر یا از انتشار خارج می‌کند؛ خوانندگان فقط پست‌های منتشرشده
را می‌بینند؛ ویرایش نویسنده روی پست تأییدشده یا منتشرشده با تأیید پیامد آن را به پیش‌نویس برمی‌گرداند.

**آزمون مستقل:** با یک پست تأییدشده، انتشار، دیده‌شدن برای خواننده و دیده‌نشدن پست‌های منتشرنشده
(quickstart.md، سناریوهای ۸، ۱۰ تا ۱۵، ۱۷ تا ۱۹، ۲۴، ۲۶ و ۲۷).

### آزمون‌های User Story 3

- [ ] T049 [P] [US3] آزمون‌های Domain برای `Publish` فقط از `Approved` (به‌روزرسانی `LastPublishedAt`)، `Unpublish` به `Approved` (حفظ `LastPublishedAt`)، ویرایش `Approved` و `Published` فقط با تأیید پیامد و بازگشت به `Draft`، و رد حذف پس از اولین انتشار در `tests/BlogSpecLab.Tests/Domain/PostPublicationTests.cs`
- [ ] T050 [P] [US3] آزمون‌های پذیرش HTTP برای سناریوهای ۸، ۱۰، ۱۲، ۱۳، ۱۴، ۱۵، ۱۷ (هر دو ترتیب انتشار و ویرایش به‌صورت قطعی؛ در ترتیب «اول انتشار»، نبود پست در فهرست `Published` ویراستار و ثبت تغییر «منتشرشده» به «پیش‌نویس» توسط نویسنده در مسیر سابقه نیز بررسی شود)، ۱۸، ۱۹، ۲۴ و ۲۶ در quickstart.md، در `tests/BlogSpecLab.Tests/Api/PublicationTests.cs`
- [ ] T051 [P] [US3] آزمون‌های پذیرش مسیرهای عمومی برای سناریوهای ۱۱ و ۲۷، ترتیب جدیدترین انتشار با `ManualTimeProvider`، پاسخ 404 یکسان برای پست ناموجود و منتشرنشده، دسترسی بدون Token، و 401 برای Token نامعتبر در `tests/BlogSpecLab.Tests/Api/PublicReadTests.cs`

### پیاده‌سازی User Story 3

- [ ] T052 [US3] افزودن `Publish`، `Unpublish` و ویرایش `Approved` و `Published` با تأیید پیامد به `src/01.Core/Domain/Posts/Post.cs`
- [ ] T053 [US3] تکمیل ویرایش در `src/01.Core/Application/Posts/AuthorPostService.cs`: نتیجهٔ `ConfirmationRequired` بدون `confirmRevertToDraft`، و در صورت `ConcurrencyConflictException` یک بار خواندن دوبارهٔ پست و اجرای مجدد قاعدهٔ دامنه (R-11، FR-012)
- [ ] T054 [US3] افزودن انتشار و خارج‌کردن از انتشار با نسخهٔ مورد انتظار به `src/01.Core/Application/Posts/EditorPostService.cs`
- [ ] T055 [P] [US3] تعریف `PublicPostSummary` و `PublicPostDetail` و پیاده‌سازی `PublicPostQueries` (فقط `Published`، مرتب براساس `LastPublishedAt` نزولی، بدون نسخه) در `src/01.Core/Application/Posts/PublicPostModels.cs` و `src/01.Core/Application/Posts/PublicPostQueries.cs`
- [ ] T056 [US3] افزودن مسیرهای `POST {id}/publish` و `POST {id}/unpublish` با `If-Match` به `src/03.Endpoints/Api/Posts/EditorEndpoints.cs`
- [ ] T057 [US3] پیاده‌سازی مسیرهای عمومی `GET /api/posts` و `GET /api/posts/{id}` بدون نیاز به ورود و بدون `ETag`، و ثبت آن‌ها در `Program.cs`، در فایل `src/03.Endpoints/Api/Posts/PublicEndpoints.cs`

**نقطهٔ بررسی:** هر سه داستان کاربر مستقل کار می‌کنند و همهٔ سناریوهای quickstart.md آزمون خودکار
دارند.

---

## فاز ۶: Polish و بررسی‌های نهایی

- [ ] T058 بررسی Project Referenceها و بسته‌های همهٔ فایل‌های `.csproj` در برابر اصل II در Constitution و نبود هیچ وابستگی AI، MCP، RAG یا Qdrant
- [ ] T059 [P] بررسی اینکه هیچ Token، Secret یا Connection String حساسی در کد، Log یا Repository ثبت نشده است، به‌ویژه در `src/03.Endpoints/Api/Provisioning/ProvisioningCommands.cs` و `src/03.Endpoints/Api/Authentication/OpaqueTokenAuthenticationHandler.cs`
- [ ] T060 اجرای کامل [quickstart.md](./quickstart.md): Build، Test، `dotnet ef database update`، ساخت چهار کاربر نمونه با `provision-user` و اجرای دستی سناریوهای ۱، ۴ و ۵ برای سنجش SC-004 و SC-005
- [ ] T061 [P] اصلاح `specs/001-blog-post-management/quickstart.md` در صورتی که دستورها یا خروجی‌های واقعی با متن آن تفاوت داشته باشند

---

## وابستگی‌ها و ترتیب اجرا

### وابستگی فازها

- **فاز ۱ (Setup):** بدون وابستگی.
- **فاز ۲ (Foundational):** پس از فاز ۱؛ همهٔ داستان‌های کاربر را مسدود می‌کند.
- **فاز ۳ (US1):** پس از فاز ۲.
- **فاز ۴ (US2):** پس از فاز ۳، چون ارسال و بررسی روی پستی کار می‌کنند که US1 آن را می‌سازد و ذخیره
  می‌کند.
- **فاز ۵ (US3):** پس از فاز ۴، چون فقط پست تأییدشده قابل انتشار است.
- **فاز ۶ (Polish):** پس از همهٔ داستان‌ها.

### وابستگی داستان‌های کاربر

- **US1:** مستقل؛ کوچک‌ترین بخش ارزشمند (MVP).
- **US2:** به موجودیت، Repository و مسیرهای US1 وابسته است، اما با آزمون‌های خودش مستقل بررسی
  می‌شود.
- **US3:** به US1 و US2 وابسته است، چون انتشار به پست تأییدشده نیاز دارد.

### ترتیب داخل هر داستان

1. آزمون‌ها نوشته شوند و ابتدا شکست بخورند.
2. Domain، سپس Application، سپس DataAccess، سپس Api.
3. پس از موفقیت همهٔ آزمون‌های داستان، داستان بعدی آغاز شود.

## فرصت‌های اجرای موازی

- **فاز ۱:** T003، T004 و T005 پس از T002.
- **فاز ۲:** T007 تا T012 با هم؛ T014، T015 و T016 پس از T013؛ T019 تا T022 و T024 و T025 با هم.
- **US1:** T027 و T028 با هم؛ T029، T030 و T031 با هم؛ T033، T034، T036 و T039 با هم.
- **US2:** T041 و T042 با هم؛ T045 هم‌زمان با T043.
- **US3:** T049، T050 و T051 با هم؛ T055 هم‌زمان با T052.

### نمونهٔ اجرای موازی: User Story 1

</div>

```text
Task: "T027 Domain tests in tests/BlogSpecLab.Tests/Domain/PostDraftTests.cs"
Task: "T028 HTTP acceptance tests in tests/BlogSpecLab.Tests/Api/AuthorDraftsTests.cs"

Task: "T029 PostStatus in src/01.Core/Domain/Posts/PostStatus.cs"
Task: "T030 StatusChange in src/01.Core/Domain/Posts/StatusChange.cs"
Task: "T031 PostDomainException in src/01.Core/Domain/Posts/PostDomainException.cs"
```

<div dir="rtl">

## راهبرد پیاده‌سازی

### ابتدا MVP (فقط User Story 1)

1. فاز ۱ و فاز ۲ کامل شوند.
2. فاز ۳ (US1) کامل شود.
3. **توقف و اعتبارسنجی:** آزمون‌های US1 و سناریوهای ۱، ۲، ۳، ۲۰ و ۲۳ بررسی شوند.

### تحویل تدریجی

1. Setup و Foundational: زیرساخت آماده است.
2. US1: نویسنده پیش‌نویس‌ها را مدیریت می‌کند (MVP).
3. US2: چرخهٔ ارسال و بررسی اضافه می‌شود.
4. US3: انتشار و مشاهدهٔ عمومی اضافه می‌شود.
5. هر مرحله بدون خراب کردن مراحل قبل، با آزمون‌های خودش تأیید می‌شود.

## یادداشت‌ها

- هر وظیفه یا گروه منطقی وظایف پس از بررسی Commit شود.
- وظیفه فقط پس از تکمیل و موفقیت آزمون‌های مربوط علامت `[X]` می‌گیرد.
- منطق کسب‌وکار فقط در Domain و Application است؛ DataAccess و Api آن را تکرار نمی‌کنند.
- Migration و ساخت داده فقط با دستور صریح انجام می‌شوند، نه هنگام Startup.

</div>

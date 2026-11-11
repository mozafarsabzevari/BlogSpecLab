<div dir="rtl">

# وظایف: بررسی هوشمند محتوای پست پیش از ارسال برای ویراستار

**ورودی**: اسناد `specs/002-ai-content-review/`: `spec.md`، `plan.md`، `research.md`، `data-model.md`، `contracts/http-api.md`، `contracts/application-ai.md`، `contracts/model-output.schema.json` و `quickstart.md`؛ همچنین Constitution و `docs/architecture/technical-baseline.md` (نسخهٔ 1.1.0، بخش ۹).

**وضعیت**: هیچ وظیفه‌ای اجرا نشده است. وظایف دارای برچسب «(اجرای واقعی)» به اجرای واقعی آزمون، Provider، Evaluation یا پذیرش انسانی وابسته‌اند و نتیجهٔ آن‌ها نباید ساختگی ثبت شود.

**آزمون‌ها**: الزامی هستند (اصل IV در Constitution و `quickstart.md`). آزمون‌های هر داستان پیش از پیاده‌سازی رفتار نوشته شوند و شکست مرتبط با رفتار دیده شود؛ خطای کامپایل به‌تنهایی شاهد شکست رفتاری نیست. آزمون‌های عادی به Model واقعی، کلید API یا اینترنت وابسته نیستند (Technical Baseline، بخش ۹).

**قالب**: `- [ ] Tnnn [P?] [USn?] شرح همراه مسیر`. `[P]` فقط برای فایل‌های مستقل در گروه‌های مشخص‌شده است؛ پیش‌نیاز گروه باید کامل باشد. مسیرها نسبت به ریشهٔ مخزن‌اند. شناسه‌های کد انگلیسی و مستندات و پیام‌های انسانی فارسی باشند.

## فاز ۱: آماده‌سازی مشترک

**هدف**: ایجاد فقط پروژهٔ `Infra.AI` مطابق Plan و آماده‌سازی تنظیمات؛ بدون پروژهٔ Qdrant و بدون بستهٔ اضافه.

- [ ] T001 ایجاد کتابخانهٔ `src/02.Infra/AI/BlogSpecLab.Infra.AI.csproj` با target برابر net10.0 و افزودن آن به `BlogSpecLab.slnx`؛ رفرنس فقط به `src/01.Core/Application/BlogSpecLab.Core.Application.csproj`؛ افزودن رفرنس `Infra.AI` به `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` و `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj`؛ هیچ رفرنسی به `Infra.DataAccess` ایجاد نشود (research.md، R-04).
- [ ] T002 افزودن package `OpenAI` نسخهٔ دقیق `2.14.0` فقط به `src/02.Infra/AI/BlogSpecLab.Infra.AI.csproj` و غیرفعال کردن هشدار `OPENAI001` فقط در همین پروژه؛ هیچ package دیگری (از جمله Microsoft.Extensions.AI یا کتابخانهٔ غیررسمی Grok) اضافه نشود (R-01، R-02).
- [ ] T003 [P] افزودن بخش `ContentReview` با `Endpoint` برابر `https://api.x.ai/v1`، `Model` برابر `grok-4.7` و `TimeoutSeconds` برابر ۶۰، بدون `ApiKey`، به `src/03.Endpoints/Api/appsettings.json`؛ افزودن `UserSecretsId` به `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` تا کلید از User Secrets یا متغیر محیطی `ContentReview__ApiKey` خوانده شود (R-13).
- [ ] T004 [P] افزودن نسخهٔ جاسازی‌شدهٔ Schema به‌عنوان `EmbeddedResource` در `src/02.Infra/AI/ContentReviews/content_review_v1.schema.json`، عیناً برابر `specs/002-ai-content-review/contracts/model-output.schema.json` (Source of Truth همان فایل `contracts` است؛ برابری در T021 آزموده می‌شود).

## فاز ۲: زیرساخت پایهٔ مسدودکننده

**هدف**: موجودیت نتیجه، قراردادهای Application، ذخیره‌سازی، نگاشت خطا و مدل جعلی آزمون؛ پیش از داستان‌ها کامل شود.

### Domain

- [ ] T005 [P] تعریف `ContentReviewOutcome`، `ContentIssueCategory`، `ContentIssueSeverity` و `ContentLocation` مطابق `data-model.md` در `src/01.Core/Domain/ContentReviews/ContentReviewEnums.cs`.
- [ ] T006 [P] تعریف `ContentIssue`، `MissingInformationItem` و `ContentReviewDomainException` با کد خطای ثابت در `src/01.Core/Domain/ContentReviews/ContentIssue.cs`، `src/01.Core/Domain/ContentReviews/MissingInformationItem.cs` و `src/01.Core/Domain/ContentReviews/ContentReviewDomainException.cs`؛ سقف‌های طول R-03 (`Excerpt` ۱ تا ۳۰۰، `Explanation` و `Description` ۱ تا ۱۰۰۰، `Suggestion` حداکثر ۱۰۰۰ یا null).
- [ ] T007 [P] نوشتن آزمون‌های Domain در `tests/BlogSpecLab.Tests/Domain/ContentReviewTests.cs` برای قواعد سازگاری FR-006 (`IssuesFound` با دست‌کم یک مشکل و بدون مورد ناکافی، `NoIssuesFound` بدون هر دو، `NeedsMoreInformation` بدون مشکل و با دست‌کم یک مورد ناکافی)، سقف‌ها و متن‌های خالی، و ثبت `ReviewedPostVersion`، `ReviewedPostModifiedAt`، `ModelName`، `PromptVersion` و `SchemaVersion`.
- [ ] T008 ایجاد موجودیت تغییرناپذیر `ContentReview` با کارخانهٔ `Create` که قواعد T007 را اعمال می‌کند در `src/01.Core/Domain/ContentReviews/ContentReview.cs`؛ زمان از بیرون داده می‌شود (وابسته به T005 و T006).

### قراردادهای Application

- [ ] T009 [P] تعریف `IContentReviewModel`، `ContentReviewInput` (فقط `Title` و `Body`)، `ContentReviewModelOutcome` (دقیقاً یکی از `Candidate`، `Refused`، `ProviderUnavailable`، `InvalidOutput`، `UnexpectedOutputForm`)، `ContentReviewCandidate` و `CandidateIssue` مطابق `contracts/application-ai.md` در `src/01.Core/Application/ContentReviews/IContentReviewModel.cs` و `src/01.Core/Application/ContentReviews/ContentReviewModelOutcome.cs`؛ بدون هیچ نوع SDK.
- [ ] T010 [P] تعریف `IContentReviewRepository` (افزودن، فهرست نتیجه‌های یک پست از جدید به قدیم، ذخیره) در `src/01.Core/Application/ContentReviews/IContentReviewRepository.cs`.
- [ ] T011 [P] افزودن کدهای `ReviewNotAvailable`، `ReviewOutputInvalid`، `ReviewRefused`، `ReviewServiceUnavailable` و `ReviewTimedOut` به `ErrorCode` در `src/01.Core/Application/Common/Result.cs` و پیام‌های فارسی بدون جزئیات داخلی در `src/01.Core/Application/ContentReviews/ContentReviewErrors.cs`.
- [ ] T012 [P] تعریف `ContentReviewDeadlineOptions` با `TimeoutSeconds` (پیش‌فرض ۶۰، معتبر بین ۱ و ۶۰) و اعتبارسنجی هنگام شروع برنامه در `src/01.Core/Application/ContentReviews/ContentReviewDeadlineOptions.cs` (R-09).

### DataAccess

- [ ] T013 پیکربندی EF Core برای `ContentReview` در `src/02.Infra/DataAccess/ContentReviews/ContentReviewConfiguration.cs` و افزودن `ContentReviews` به `src/02.Infra/DataAccess/BlogSpecLabDbContext.cs`؛ کلید خارجی `PostId` به `Posts` با حذف آبشاری، Index روی `(PostId, CreatedAt)`، Enumها به‌صورت متن، `Issues` و `MissingInformation` به‌صورت مجموعهٔ Owned با نگاشت JSON (R-14).
- [ ] T014 پیاده‌سازی `ContentReviewRepository` در `src/02.Infra/DataAccess/ContentReviews/ContentReviewRepository.cs` و ثبت آن در `src/02.Infra/DataAccess/DependencyInjection.cs` (وابسته به T013).
- [ ] T015 ایجاد Migration با نام `AddContentReviews` در `src/02.Infra/DataAccess/Migrations/`؛ فقط ایجاد، بدون اعمال روی پایگاه داده.

### Api و زیرساخت آزمون

- [ ] T016 [P] افزودن نگاشت کدهای T011 به ProblemDetails در `src/03.Endpoints/Api/Posts/ProblemDetailsMapping.cs`: 409 برای `ReviewNotAvailable` همراه `currentStatus`، 502 برای `ReviewOutputInvalid` و `ReviewRefused`، 503 برای `ReviewServiceUnavailable` و 504 برای `ReviewTimedOut`؛ بدون محتوای پست یا خروجی مدل (`contracts/http-api.md`).
- [ ] T017 [P] پیاده‌سازی `FakeContentReviewModel` قابل برنامه‌ریزی (هر Outcome، تأخیر وابسته به `CancellationToken`، ثبت ورودی دریافتی) در `tests/BlogSpecLab.Tests/Api/Infrastructure/FakeContentReviewModel.cs` و جایگزینی `IContentReviewModel` با آن در `tests/BlogSpecLab.Tests/Api/Infrastructure/BlogSpecLabApiFactory.cs`؛ هیچ آزمون عادی به Provider واقعی دسترسی نداشته باشد.

**نقطهٔ کنترل**: موجودیت، قراردادها، جدول و مدل جعلی آماده‌اند و آزمون T007 موفق است. مسیر HTTP هنوز وجود ندارد.

## فاز ۳: داستان ۱ — درخواست بررسی هوشمند و دریافت گزارش مشکلات (P1) 🎯 MVP

**هدف**: نویسنده برای پست «پیش‌نویس» یا «بازگشت برای اصلاح» خود بررسی می‌خواهد، نتیجهٔ معتبر ذخیره و فقط به خودش نمایش داده می‌شود؛ FR-001 تا FR-005، FR-008، FR-011، FR-013 و SC-002، SC-004، SC-005، SC-008.

**آزمون مستقل**: با `FakeContentReviewModel` و دو نویسنده؛ درخواست بررسی، دریافت `IssuesFound` یا `NoIssuesFound`، حذف مشکل بدون ارجاع واقعی، فهرست نتیجه‌ها با `isCurrentVersion` و رد دسترسی دیگران (`quickstart.md`، سناریوهای ۱ تا ۴، ۷، ۸، ۱۱ تا ۱۴، ۱۶، ۱۷، ۱۹ و ۲۱).

### آزمون‌ها

- [ ] T018 [P] [US1] نوشتن آزمون‌های Validator در `tests/BlogSpecLab.Tests/Application/ContentReviewValidatorTests.cs` برای قواعد ۱، ۴، ۵، ۶ و ۷ در R-07: مقادیر مجاز، سقف‌ها، یافتن `excerpt` به‌صورت Ordinal پس از Normalization Form C در `location` درست، حذف مشکل بدون ارجاع، نامعتبر شدن کل خروجی وقتی همهٔ مشکلات حذف شوند، و ترتیب براساس شدت.
- [ ] T019 [P] [US1] نوشتن آزمون‌های Application در `tests/BlogSpecLab.Tests/Application/ContentReviewServiceTests.cs`: ورود، نقش نویسنده، مالکیت (پست دیگران `NotFound`)، وضعیت‌های مجاز و `ReviewNotAvailable`، ارسال فقط عنوان و متن، ذخیرهٔ نتیجهٔ معتبر با نسخه و زمان Snapshot، نگاشت `Refused`، `ProviderUnavailable`، `InvalidOutput` و `UnexpectedOutputForm` به کدهای T011 بدون ذخیره، بررسی دوبارهٔ وجود و مالکیت پیش از ذخیره، و `isCurrentVersion` در فهرست؛ با `ManualTimeProvider` و جایگزین‌های بدون سرویس بیرونی.
- [ ] T020 [P] [US1] نوشتن آزمون‌های `Infra.AI` با Transport جعلی HTTP در `tests/BlogSpecLab.Tests/Infra/AI/ResponsesContentReviewModelTests.cs`: درخواست به `/responses` با Model تنظیم‌شده، `store` برابر `false`، `text.format` از نوع `json_schema` با نام `content_review_v1` و `strict` برابر `true`، نبود هیچ Tool، غیر Streaming، `max_output_tokens` برابر ۸۰۰۰ و ورودی فقط شامل `title` و `body`؛ و نگاشت `Completed` معتبر، Refusal، `Incomplete` (هر دلیل)، `Failed`، JSON نامعتبر یا ناسازگار با Schema، خروجی غیر از یک پیام متنی، پاسخ 5xx، 429، 401، خطای شبکه و نبود `ApiKey` به Outcome درست؛ انتشار `OperationCanceledException` بدون تبدیل.
- [ ] T021 [P] [US1] نوشتن آزمون برابری Schema جاسازی‌شده در `src/02.Infra/AI/ContentReviews/content_review_v1.schema.json` با `specs/002-ai-content-review/contracts/model-output.schema.json` در `tests/BlogSpecLab.Tests/Infra/AI/ContentReviewSchemaContractTests.cs`.
- [ ] T022 [P] [US1] نوشتن آزمون‌های پذیرش HTTP در `tests/BlogSpecLab.Tests/Api/ContentReviewTests.cs` برای سناریوهای ۱ تا ۴، ۷، ۸، ۱۱ تا ۱۴، ۱۶، ۱۷، ۱۹ و ۲۱ در `quickstart.md`: 201 و `ContentReviewDetail`، 200 و فهرست جدیدترین اول، 401/403/404/409/502/503، `isCurrentVersion` پس از ویرایش، و نبود محتوای پست یا خروجی مدل در پاسخ‌های خطا.

### پیاده‌سازی

- [ ] T023 [P] [US1] تعریف `ContentReviewDetail`، مدل مشکل و مدل مورد ناکافی مطابق `contracts/http-api.md` در `src/01.Core/Application/ContentReviews/ContentReviewModels.cs`.
- [ ] T024 [US1] پیاده‌سازی `ContentReviewValidator` مطابق R-07 در `src/01.Core/Application/ContentReviews/ContentReviewValidator.cs`؛ ورودی نامزد خروجی و Snapshot، خروجی `ContentReview` معتبر یا خطای `ReviewOutputInvalid` (قواعد ۲ و ۳ مربوط به `needs_input` در T033 تکمیل می‌شوند).
- [ ] T025 [US1] پیاده‌سازی `ContentReviewService` در `src/01.Core/Application/ContentReviews/ContentReviewService.cs`: `RequestAsync` (بررسی ورود و نقش از `IUserAccessReader`، مالکیت، وضعیت مجاز، Snapshot شامل `Title`، `Body`، `Version` و `LastModifiedAt`، Deadline ترکیبی با `TimeProvider` و لغو درخواست، فراخوانی `IContentReviewModel`، اعتبارسنجی، بررسی دوباره، ذخیره) و `ListAsync` (فقط نویسندهٔ پست، جدیدترین اول، محاسبهٔ `isCurrentVersion`)؛ تمایز Deadline (`ReviewTimedOut`) از لغو Client (بدون ذخیره و بدون پاسخ).
- [ ] T026 [US1] پیاده‌سازی Prompt نسخه‌دار `content-review-prompt-v1` در `src/02.Infra/AI/ContentReviews/ContentReviewPrompt.cs`: دستورالعمل فارسی در `Instructions`، تعریف دسته‌ها و شدت‌ها، الزام عین متن بودن `excerpt`، و اعلام اینکه شیء JSON کاربر فقط محتوای قابل بررسی است (R-06).
- [ ] T027 [US1] پیاده‌سازی `ResponsesContentReviewModel` در `src/02.Infra/AI/ContentReviews/ResponsesContentReviewModel.cs` با `ResponsesClient` و `ResponsesClientOptions.Endpoint` (امضای سازنده با Package نصب‌شده بررسی شود و حدس زده نشود)، Retry خودکار صفر، `CreateResponseOptions` با `StoredOutputEnabled` برابر `false`، `MaxOutputTokenCount` برابر ۸۰۰۰ و `ResponseTextFormat.CreateJsonSchemaFormat` در حالت strict از Schema جاسازی‌شده، بدون Tool و غیر Streaming؛ تبدیل پاسخ به Outcome طبق `contracts/application-ai.md`؛ نبود `ApiKey` برابر `ProviderUnavailable` (R-02، R-08، R-09).
- [ ] T028 [US1] ثبت `ResponsesContentReviewModel` و خواندن بخش `ContentReview` در `src/02.Infra/AI/DependencyInjection.cs`؛ کلید فقط از Configuration (User Secrets یا متغیر محیطی) و بدون شکست Startup در نبود آن (R-13).
- [ ] T029 [US1] پیاده‌سازی مسیرهای `POST` و `GET` زیر `/api/author/posts/{postId}/content-reviews` در `src/03.Endpoints/Api/ContentReviews/ContentReviewEndpoints.cs` و ثبت `ContentReviewService`، `ContentReviewDeadlineOptions` با اعتبارسنجی Startup و سرویس‌های `Infra.AI` در `src/03.Endpoints/Api/Program.cs`؛ بدون `If-Match`؛ آزمون‌های T018 تا T022 را اجرا و اشکال‌های همین داستان را رفع کنید.

**نقطهٔ کنترل**: MVP بررسی هوشمند با مدل جعلی کار می‌کند؛ مسیر «نیاز به اطلاعات بیشتر» هنوز کامل نیست.

## فاز ۴: داستان ۲ — اعلام نیاز به اطلاعات بیشتر به‌جای حدس‌زدن (P2)

**هدف**: خروجی `needs_input` به «نیاز به اطلاعات بیشتر» بدون مشکل حدسی تبدیل شود و خروجی ناسازگار رد شود؛ FR-006، FR-007 و SC-003.

**آزمون مستقل**: با `FakeContentReviewModel` که `needs_input` برمی‌گرداند؛ دریافت `NeedsMoreInformation`، رد خروجی ناسازگار و بررسی دوباره پس از تکمیل پست (`quickstart.md`، سناریوهای ۵ و ۶).

### آزمون‌ها

- [ ] T030 [P] [US2] افزودن آزمون‌های قواعد ۲ و ۳ در R-07 به `tests/BlogSpecLab.Tests/Application/ContentReviewNeedsInputTests.cs`: `needs_input` با مشکل یا بدون مورد ناکافی نامعتبر، `completed` با مورد ناکافی نامعتبر، و ذخیرهٔ `NeedsMoreInformation` معتبر.
- [ ] T031 [P] [US2] نوشتن آزمون‌های پذیرش HTTP برای سناریوهای ۵ و ۶ و بررسی دوباره پس از ویرایش پست (نتیجهٔ جدید با نسخهٔ جدید و نتیجهٔ قبلی با `isCurrentVersion` برابر `false`) در `tests/BlogSpecLab.Tests/Api/ContentReviewNeedsInputTests.cs`.

### پیاده‌سازی

- [ ] T032 [US2] تکمیل دستورالعمل Prompt در `src/02.Infra/AI/ContentReviews/ContentReviewPrompt.cs` برای برگرداندن `needs_input` همراه توضیح اطلاعات کم و منع گزارش مشکل حدسی (FR-007)؛ نسخهٔ Prompt فقط در صورت تغییر معنایی پس از ثبت شواهد Eval افزایش یابد.
- [ ] T033 [US2] تکمیل قواعد ۲ و ۳ در `src/01.Core/Application/ContentReviews/ContentReviewValidator.cs` و نگاشت `needs_input` به `NeedsMoreInformation` در `src/01.Core/Application/ContentReviews/ContentReviewService.cs`؛ آزمون‌های T030 و T031 و رگرسیون US1 اجرا شوند.

**نقطهٔ کنترل**: هر سه وضعیت نتیجه با مدل جعلی قابل اثبات‌اند.

## فاز ۵: داستان ۳ — نقش صرفاً کمکی بررسی و حفظ مسیر تأیید انسانی (P3)

**هدف**: اثبات اینکه بررسی هیچ اثری بر پست ندارد، پیش‌شرط ارسال نیست، متن پست نمی‌تواند رفتار را تغییر دهد، و Timeout، هم‌زمانی و Logging امن‌اند؛ FR-009 تا FR-012، SC-001، SC-007 و Technical Baseline بخش ۹.

**آزمون مستقل**: مقایسهٔ عنوان، متن، وضعیت، `Version` و سابقهٔ پست پیش و پس از هر Outcome؛ ارسال بدون بررسی؛ و سناریوهای Timeout، حذف هم‌زمان و Logging (`quickstart.md`، سناریوهای ۹، ۱۰، ۱۵، ۱۸، ۲۰ و ۲۱).

### آزمون‌ها

- [ ] T034 [P] [US3] نوشتن آزمون‌های عدم تغییر در `tests/BlogSpecLab.Tests/Api/ContentReviewNonMutationTests.cs` برای سناریوهای ۹، ۱۰، ۱۸ و ۲۰: پست و سابقه پس از هر Outcome و هر خطا بدون تغییر، ارسال پست برای ویراستار بدون بررسی، متن پست حاوی دستور تأیید یا انتشار بدون اثر، و حذف نتیجه‌ها با حذف پست هرگز منتشرنشده.
- [ ] T035 [P] [US3] نوشتن آزمون‌های Timeout و لغو در `tests/BlogSpecLab.Tests/Application/ContentReviewDeadlineTests.cs` و سناریوی ۱۵ در `tests/BlogSpecLab.Tests/Api/ContentReviewTimeoutTests.cs`: رسیدن Deadline با `ManualTimeProvider` به 504 و `ReviewTimedOut` بدون ذخیره؛ لغو درخواست Client بدون ذخیره؛ اعتبارسنجی `TimeoutSeconds` خارج از ۱ تا ۶۰ هنگام شروع؛ بدون `sleep`.
- [ ] T036 [P] [US3] نوشتن آزمون‌های حریم خصوصی و Logging در `tests/BlogSpecLab.Tests/Infra/AI/ContentReviewLoggingTests.cs` با Logger ضبط‌کننده: هیچ عنوان، متن، Prompt، خروجی مدل یا کلید API در Log مسیر Application و `Infra.AI` ثبت نشود و فقط فرادادهٔ مجاز R-05 دیده شود.
- [ ] T037 [P] [US3] نوشتن آزمون‌های هم‌زمانی در `tests/BlogSpecLab.Tests/Application/ContentReviewConcurrencyTests.cs`: دو درخواست هم‌زمان برای یک پست دو نتیجهٔ جدا می‌سازند (R-15)؛ حذف پست در فاصلهٔ فراخوانی مدل به `NotFound` بدون ذخیره می‌رسد (سناریوی ۲۱)؛ ویرایش پست در همین فاصله نتیجه را با نسخهٔ قبلی ذخیره می‌کند (R-11)؛ ترتیب با مدل جعلی کنترل شود و بدون `sleep` باشد.

### پیاده‌سازی و سخت‌کردن

- [ ] T038 [US3] افزودن Logging فقط با فرادادهٔ مجاز R-05 (شناسهٔ پست، Outcome، مدت، نام Model، شمار Token، شناسهٔ پاسخ Provider) در `src/01.Core/Application/ContentReviews/ContentReviewService.cs` و `src/02.Infra/AI/ContentReviews/ResponsesContentReviewModel.cs`؛ Log داخلی محتوای HTTP مربوط به SDK فعال نشود.
- [ ] T039 [US3] اجرای آزمون‌های T034 تا T037 و رفع هر نقض در `src/01.Core/Application/ContentReviews/ContentReviewService.cs`، `src/02.Infra/DataAccess/ContentReviews/ContentReviewRepository.cs` و `src/02.Infra/AI/ContentReviews/ResponsesContentReviewModel.cs`؛ ذخیرهٔ نتیجه هیچ تغییر ردیابی‌شده‌ای روی `Post` ذخیره نکند و همهٔ آزمون‌های US1، US2 و Feature قبلی نیز عبور کنند.

**نقطهٔ کنترل**: سه داستان با مدل جعلی و پاسخ HTTP و وضعیت واقعی DB قابل اثبات‌اند؛ کیفیت Model واقعی هنوز ارزیابی نشده است.

## فاز ۶: Evaluation، تکمیل و پذیرش

**هدف**: آماده‌سازی زیرساخت Smoke و Evaluation، بررسی سراسری، و ثبت شواهد واقعی. وظایف «(اجرای واقعی)» فقط با اقدام صریح، کلید معتبر یا مشارکت انسانی انجام می‌شوند.

- [ ] T040 [P] ایجاد Corpus نسخه‌دار `tests/BlogSpecLab.Tests/Evaluation/Corpus/content_review_corpus_v1.json` از پست‌های فارسی با برچسب انسانی: پست سالم، پست با اشکال‌های کاشته‌شده در هر دسته، پست با اطلاعات ناکافی و پست حاوی دستور تزریقی؛ هر نمونه شامل شناسه، عنوان، متن، Outcome مورد انتظار و اشکال‌های کاشته‌شده با دسته و `excerpt`.
- [ ] T041 [P] نوشتن پروتکل Evaluation در `specs/002-ai-content-review/evaluation/protocol.md`: Rubric، معیارها، آستانه‌های R-12 با برچسب «پیشنهادی؛ نیازمند تأیید انسانی»، و فیلدهای الزامی شواهد (تاریخ، Model واقعی، نسخهٔ Prompt و Schema، تنظیمات، نتیجهٔ هر نمونه، Failureها و زمان پاسخ).
- [ ] T042 [P] نوشتن آزمون Smoke با Provider واقعی در `tests/BlogSpecLab.Tests/Evaluation/ContentReviewProviderSmokeTests.cs` با `Category=ProviderSmoke`؛ فقط با `BLOGSPECLAB_PROVIDER_TESTS=1` و کلید معتبر اجرا و در غیر این صورت Skip شود؛ فقط شکل و قابل پردازش بودن پاسخ بررسی شود.
- [ ] T043 نوشتن اجراکنندهٔ Evaluation در `tests/BlogSpecLab.Tests/Evaluation/ContentReviewEvaluationTests.cs` با `Category=Evaluation` و همان شرط محیطی؛ Corpus T040 را اجرا و شواهد را در `specs/002-ai-content-review/evaluation/results/` ثبت کند؛ آستانه‌ها قابل تنظیم باشند و هیچ نتیجهٔ پیش‌فرض یا ساختگی نوشته نشود (وابسته به T040 و T041).
- [ ] T044 بررسی Project Referenceها و Packageهای همهٔ `.csproj` در برابر اصل II در Constitution: فقط `Infra.AI` به `OpenAI 2.14.0` وابسته باشد، `Infra.AI` به `Infra.DataAccess` وابسته نباشد، و هیچ وابستگی MCP، RAG، Qdrant یا Agent وجود نداشته باشد؛ نبود کلید API یا Secret در کد، `appsettings`، مستندات و Log بررسی شود.
- [ ] T045 [P] تکمیل `specs/002-ai-content-review/quickstart.md` با تنظیمات عملیاتی `Infra.AI`، نام دقیق Categoryهای آزمون و مسیر شواهد Eval؛ هر دستوری که هنوز اجرا نشده با همین وضعیت مشخص شود.
- [ ] T046 (اجرای واقعی) اجرای `dotnet build BlogSpecLab.slnx` و `dotnet test BlogSpecLab.slnx` بدون کلید API و ثبت نتیجه، نگاشت سناریوهای `quickstart.md` به آزمون‌ها، و شواهد SC-001، SC-002، SC-004، SC-005، SC-007 و SC-008 در `specs/002-ai-content-review/validation.md`.
- [ ] T047 (اجرای واقعی) اجرای آزمون Smoke با کلید معتبر xAI و ثبت نتیجه در `specs/002-ai-content-review/validation.md`؛ نام Model واقعی بازگشتی Provider ثبت شود و کلید یا محتوای پست ثبت نشود.
- [ ] T048 (اجرای واقعی) پس از تأیید انسانی آستانه‌ها، اجرای Evaluation با `grok-4.7` و ثبت شواهد کامل در `specs/002-ai-content-review/evaluation/results/`؛ SC-003 و SC-006 در برابر آستانه‌ها بررسی شوند؛ در صورت شکست، بازنگری Model یا Prompt به‌عنوان کار باقی‌مانده ثبت شود و Fallback خودکار ایجاد نشود.
- [ ] T049 (اجرای واقعی) ارزیابی انسانی نمونه‌ای از خروجی‌های Eval براساس Rubric T041 (درستی دسته و شدت، مفید بودن توضیح، نبود مشکل حدسی) و ثبت تعداد ارزیاب‌ها، نمونه‌ها و نتیجه در `specs/002-ai-content-review/evaluation/human-review.md`؛ نبود ارزیاب به‌عنوان کار باقی‌مانده ثبت شود و موفقیت جعل نشود.
- [ ] T050 (اجرای واقعی) پذیرش نهایی مطابق `quickstart.md` و ثبت نتیجه، ریسک پذیرفته‌شدهٔ نگهداری داده در xAI (R-05) و محدودیت‌های باقی‌مانده در `specs/002-ai-content-review/acceptance.md` (وابسته به T046 تا T049).

## وابستگی‌ها و ترتیب اجرا

</div>

```text
Phase 1 (T001–T004) → Phase 2 (T005–T017)
  → US1 (T018–T029) → US2 (T030–T033) → US3 (T034–T039)
  → Evaluation and completion (T040–T045) → real execution (T046–T050)
```

<div dir="rtl">

- T001 پیش از T002؛ T003 و T004 پس از T001 مستقل‌اند.
- در فاز ۲، T005، T006 و T007 مستقل‌اند و T008 پس از T005 و T006 می‌آید؛ T009 تا T012 مستقل‌اند؛ T013 پس از T008، سپس T014 و T015؛ T016 پس از T011؛ T017 پس از T009.
- آزمون‌های هر داستان پیش از رفتار نوشته می‌شوند؛ برای دیدن شکست رفتاری فقط اسکلت حداقلی نوع‌ها ساخته شود. T021 به T004 وابسته است.
- در US1، T024 پس از T023؛ T025 پس از T024؛ T026 پیش از T027؛ T028 پس از T027؛ T029 پس از T025 و T028.
- US2 و US3 از سرویس، Validator، Adapter و مسیرهای US1 استفاده می‌کنند؛ مستقل بودن آزمون به معنای مستقل بودن کد از US1 نیست. به‌دلیل فایل‌های مشترک Validator، Service و Prompt، US2 و US3 به‌صورت کامل موازی نیستند.
- T040، T041 و T042 پس از US1 قابل شروع‌اند؛ T043 پس از T040 و T041؛ T044 و T045 پس از همهٔ داستان‌ها.
- T046 پس از همهٔ وظایف کدنویسی؛ T047 پس از T046 و تنظیم کلید؛ T048 پس از T047 و تأیید انسانی آستانه‌ها؛ T049 پس از T048؛ T050 در پایان.

## فرصت‌ها و نمونه‌های اجرای موازی

این بخش امکان تقسیم کار در اجرای آینده را بیان می‌کند؛ مجوز اجرای خودکار عامل‌ها یا شروع Implement نیست.

| گروه | نمونهٔ وظایف قابل اجرای هم‌زمان | شرط آغاز |
| --- | --- | --- |
| آماده‌سازی | T003 و T004 | T001 کامل |
| Domain | T005، T006 و T007 | فاز ۱ کامل |
| قراردادها | T009، T010، T011 و T012 | فاز ۱ کامل |
| پایهٔ Api و آزمون | T016 و T017 | T009 و T011 کامل |
| US1 آزمون‌ها | T018، T019، T020، T021 و T022 | فاز ۲ کامل؛ فایل‌های آزمون مستقل |
| US2 آزمون‌ها | T030 و T031 | US1 کامل |
| US3 آزمون‌ها | T034، T035، T036 و T037 | US2 کامل؛ هر آزمون DB درون‌حافظه‌ای مستقل |
| Evaluation | T040، T041 و T042 | US1 کامل |

تغییر هم‌زمان `ContentReviewService`، `ContentReviewValidator`، `ContentReviewPrompt`، `ResponsesContentReviewModel`، `Program.cs` یا `BlogSpecLabApiFactory` در این گروه‌ها مجاز نیست؛ نیاز به تغییر فایل مشترک ابتدا به‌صورت ترتیبی حل شود.

</div>

```text
Task: "T018 Validator tests in tests/BlogSpecLab.Tests/Application/ContentReviewValidatorTests.cs"
Task: "T019 Service tests in tests/BlogSpecLab.Tests/Application/ContentReviewServiceTests.cs"
Task: "T020 Infra.AI tests in tests/BlogSpecLab.Tests/Infra/AI/ResponsesContentReviewModelTests.cs"
Task: "T021 Schema contract test in tests/BlogSpecLab.Tests/Infra/AI/ContentReviewSchemaContractTests.cs"
Task: "T022 HTTP acceptance tests in tests/BlogSpecLab.Tests/Api/ContentReviewTests.cs"
```

<div dir="rtl">

## راهبرد پیاده‌سازی و تحویل تدریجی

۱. آماده‌سازی و پایه، سپس US1؛ با مدل جعلی و دو نویسنده معیار مستقل را تأیید کنید و برای بازبینی توقف کنید. این **MVP بررسی هوشمند** است.

۲. US2 را اضافه کنید و مسیر `needs_input` و رد خروجی ناسازگار را بسنجید.

۳. US3 را اضافه کنید؛ چون نقش صرفاً کمکی و عدم تغییر پست نیاز اصلی Specification است، **حداقل نسخهٔ قابل پذیرش شامل هر سه داستان** است.

۴. زیرساخت Smoke و Evaluation را آماده کنید. عبور build و test به‌تنهایی اثبات کیفیت Model نیست و موفقیت Eval جایگزین آزمون نرم‌افزار نیست (Technical Baseline، بخش ۹). وظایف «(اجرای واقعی)» فقط با دستور صریح کاربر اجرا شوند و بدون دستور کاربر به قابلیت یا مرحلهٔ SDD بعدی نروید.

## کنترل پوشش

| نیازمندی | وظایف اصلی |
| --- | --- |
| FR-001، FR-002 | T019، T022، T025، T029 |
| FR-003 | T008، T019، T022، T025، T031 |
| FR-004، FR-005، FR-006 | T005–T008، T018، T023–T024، T030، T033 |
| FR-007 | T030–T033، T048 |
| FR-008 | T018، T020، T024، T027 |
| FR-009، FR-010 | T034، T039، T046 |
| FR-011 | T016، T019–T020، T025، T027، T035 |
| FR-012 | T026، T034، T040، T048 |
| FR-013 | T010، T013–T015، T019، T022، T025، T029 |
| SC-001، SC-007 | T034، T039، T046 |
| SC-002، SC-004 | T018، T020، T024، T046 |
| SC-003 | T030–T031، T048 |
| SC-005، SC-008 | T019، T022، T046 |
| SC-006 | T043، T048 |
| Technical Baseline بخش ۹ | T020، T021، T036، T038، T040–T043، T048–T049 |

**جمع**: ۵۰ وظیفه؛ آماده‌سازی ۴، پایه ۱۳، US1 تعداد ۱۲، US2 تعداد ۴، US3 تعداد ۶، Evaluation و تکمیل ۱۱ (شامل ۵ وظیفهٔ «اجرای واقعی»)؛ ۲۷ وظیفه دارای `[P]` در گروه‌های مشخص‌شده.

</div>

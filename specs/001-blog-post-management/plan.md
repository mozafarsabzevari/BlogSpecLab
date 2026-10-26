# Implementation Plan: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

**Branch**: `main` | **Date**: 2026-10-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-blog-post-management/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

نویسنده‌ها پست‌های خود را ایجاد، ویرایش، ارسال و حذف می‌کنند؛ ویراستارها پست‌ها را تأیید یا با دلیل
بازمی‌گردانند و فقط پست‌های تأییدشده را منتشر می‌کنند؛ خوانندگان فقط پست‌های منتشرشده را می‌بینند.
هیچ پستی بدون تأیید ویراستاری غیر از نویسنده منتشر نمی‌شود.

رویکرد فنی: یک Web API با ASP.NET Core Minimal APIs روی .NET 10. قواعد وضعیت در موجودیت `Post` در
`Core.Domain` متمرکزند، موارد کاربرد و کنترل مجوز در `Core.Application` و ذخیره‌سازی با EF Core و
SQLite در `Infra.DataAccess` انجام می‌شود. هم‌زمانی با Concurrency Token کنترل می‌شود. جزئیات
تصمیم‌ها در [research.md](./research.md) آمده است.

## Technical Context

**Language/Version**: C# روی .NET 10 (ASP.NET Core) — طبق Constitution

**Primary Dependencies**: ASP.NET Core Minimal APIs (R-01)؛ Entity Framework Core با Provider
SQLite (R-03)؛ `Microsoft.EntityFrameworkCore.Design` برای Migrations (R-04)

**Storage**: SQLite (R-02)؛ جدول‌های `Posts` و `PostStatusChanges` ([data-model.md](./data-model.md))

**Testing**: xUnit؛ `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) با SQLite In-Memory (R-05)

**Target Platform**: سرور Cross-platform (Windows برای توسعه، Linux یا Windows برای اجرا)

**Project Type**: Web service (HTTP API)؛ رابط کاربری خارج از این Feature است

**Performance Goals**: هدف عددی خاصی در Specification نیست؛ SC-004 و SC-005 به زمان کار کاربر
مربوط‌اند و با پاسخ‌گویی عادی API تأمین می‌شوند

**Constraints**: اجرا بدون مدل زبانی، MCP، Agent یا Vector Database (اصل V.3)؛ بدون Package
اضافه جز موارد بالا؛ هویت فقط از طریق `ICurrentUser` (R-06)

**Scale/Scope**: مقیاس آموزشی (ده‌ها کاربر، حداکثر چند هزار پست)؛ ۳ گروه مسیر و ۱۵ Endpoint
([contracts/http-api.md](./contracts/http-api.md))؛ بدون صفحه‌بندی (R-16)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| اصل | بررسی | وضعیت پیش از طراحی | وضعیت پس از طراحی |
|-----|-------|---------------------|---------------------|
| I. توسعهٔ مبتنی بر مشخصات | Specification تأییدشده (2026-10-24) و Clarifications ثبت‌شده مبنای Plan است؛ هر FR در data-model یا contracts نگاشت دارد | ✅ | ✅ |
| II. معماری و قواعد وابستگی | فقط پروژه‌های Domain، Application، Infra.DataAccess، Endpoints.Api و Tests؛ AI و Qdrant ساخته نمی‌شوند؛ Domain بدون وابستگی؛ Application فقط به Domain؛ بدون Modules یا ZaminX | ✅ | ✅ |
| II.6 عدم تکرار منطق | انتقال‌های وضعیت فقط در `Post` (R-10)؛ Endpointها فقط نگاشت HTTP | ✅ | ✅ |
| II.8 انتزاع فقط با نیاز | `IPostRepository` به دلیل قاعدهٔ ۲ Project Reference و `ICurrentUser` به دلیل جدا ماندن Application از سازوکار ورود لازم‌اند؛ Mediator و State Machine Library استفاده نمی‌شوند (R-09، R-10) | ✅ | ✅ |
| III. امنیت و مرز مسئولیت‌ها | قواعد وضعیت در Domain؛ مجوزها در Application؛ 404 برای پست‌های غیرقابل دسترس (R-13)؛ Scheme توسعه فقط در Development/Testing (R-06)؛ بدون Secret در کد | ✅ | ✅ |
| IV. کیفیت و آزمون‌پذیری | آزمون واحد Domain/Application و آزمون پذیرش HTTP برای همهٔ سناریوهای quickstart، شامل دسترسی غیرمجاز و هم‌زمانی؛ `TimeProvider` برای کنترل زمان (R-08) | ✅ | ✅ |
| V. سادگی و توسعهٔ تدریجی | Minimal APIs، سرویس‌های ساده، SQLite، بدون صفحه‌بندی؛ بدون قابلیت AI | ✅ | ✅ |
| VI. حاکمیت | انتخاب پایگاه داده و سایر تصمیم‌های سراسری به‌عنوان «کاندید Technical Baseline پروژه» برای بازبینی انسانی علامت خورده‌اند؛ Constitution تغییر نمی‌کند | ✅ | ✅ |

نتیجه: هیچ نقضی وجود ندارد؛ Complexity Tracking لازم نیست.

## Project Structure

### Documentation (this feature)

```text
specs/001-blog-post-management/
├── spec.md
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── http-api.md      # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
BlogSpecLab.slnx
src/
├── 01.Core/
│   ├── Domain/
│   │   ├── BlogSpecLab.Core.Domain.csproj
│   │   └── Posts/                 # Post، PostStatus، StatusChange، خطاهای دامنه
│   └── Application/
│       ├── BlogSpecLab.Core.Application.csproj
│       ├── Common/                # ICurrentUser، نتایج و خطاهای Application
│       └── Posts/                 # AuthorPostService، EditorPostService، PublicPostQueries، IPostRepository، DTOها
├── 02.Infra/
│   └── DataAccess/
│       ├── BlogSpecLab.Infra.DataAccess.csproj
│       ├── BlogSpecLabDbContext.cs
│       ├── Posts/                 # پیکربندی EF و PostRepository
│       └── Migrations/
└── 03.Endpoints/
    └── Api/
        ├── BlogSpecLab.Endpoints.Api.csproj
        ├── Program.cs             # Composition و DI
        ├── Authentication/        # Scheme توسعه و پیاده‌سازی ICurrentUser
        └── Posts/                 # AuthorEndpoints، EditorEndpoints، PublicEndpoints، نگاشت خطا به ProblemDetails
tests/
└── BlogSpecLab.Tests/
    ├── BlogSpecLab.Tests.csproj
    ├── Domain/                    # قواعد Post و ماشین وضعیت
    ├── Application/               # مجوزها و موارد کاربرد
    └── Api/                       # آزمون‌های پذیرش HTTP (سناریوهای quickstart)
```

جهت Project Referenceها:

```text
Core.Application   → Core.Domain
Infra.DataAccess   → Core.Application, Core.Domain
Endpoints.Api      → Core.Application, Infra.DataAccess
Tests              → Core.Domain, Core.Application, Endpoints.Api
```

**Structure Decision**: ساختار هدف Constitution بدون تغییر استفاده می‌شود. از آن فقط پروژه‌های موردنیاز
این Feature ساخته می‌شوند. `Infra.AI` و `Infra.Qdrant` ساخته نمی‌شوند، چون این Feature قابلیت هوش
مصنوعی ندارد (اصل V.3). همهٔ آزمون‌ها در یک پروژهٔ `BlogSpecLab.Tests` با پوشه‌های جدا قرار
می‌گیرند.

## Complexity Tracking

هیچ نقض Constitution وجود ندارد؛ این بخش خالی است.

## کاندیدهای Technical Baseline پروژه

تصمیم‌های زیر اثر سراسری دارند و باید پیش از پذیرش توسط انسان بازبینی شوند (جزئیات در
[research.md](./research.md)):

| شناسه | تصمیم |
|-------|-------|
| R-01 | سبک API: Minimal APIs |
| R-02 | پایگاه داده: SQLite |
| R-03 | دسترسی به داده: EF Core فقط در Infra.DataAccess |
| R-04 | مدیریت Schema: EF Core Migrations |
| R-05 | آزمون: xUnit و WebApplicationFactory با SQLite In-Memory |
| R-06 | هویت: `ICurrentUser` در Application؛ Scheme مبتنی بر Header فقط در Development/Testing؛ سازوکار ورود واقعی بعداً تعیین می‌شود |
| R-07 | قالب خطا: ProblemDetails با فیلد `code` |
| R-08 | شناسه‌ها `Guid`؛ زمان UTC با `TimeProvider` |
| R-09 | موارد کاربرد به‌صورت سرویس‌های ساده، بدون Mediator |

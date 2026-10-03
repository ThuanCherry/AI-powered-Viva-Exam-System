# 🎓 AssignmentPRN - AI-powered Viva Exam System (AIVES)

<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-Web%20MVC-blue?style=for-the-badge&logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-10.0-purple?style=for-the-badge&logo=nuget&logoColor=white)
![MySQL](<https://img.shields.io/badge/MySQL-8.4%20(Aiven%20TLS)-00758F?style=for-the-badge&logo=mysql&logoColor=white>)

**Hệ thống thi vấn đáp (Viva Exam) thông minh với kiến trúc 3-Layers chuẩn hoá**

</div>

---

## 🏛️ Sơ đồ Kiến trúc Hệ thống (System Architecture)

<p align="center">
  <img src="./docs/AssignmentPRN_3Layers_Architecture.drawio (1).svg" alt="AIVES System Architecture" width="100%"/>
</p>

---

## 🔍 Chi tiết các Tầng (Layer Breakdown)

### 1. 🌐 Presentation Layer (`AIVES.PresentationLayer`)

- **WebMVC Pattern**: Hỗ trợ đồng thời Razor Views để render giao diện Web và các Controller phục vụ RESTful API.
- **Controllers**:
  - `HomeController`: Điều hướng và render trang chủ qua Razor Views.
  - `CoursesController`: Quản lý khoá học (Course).
  - `ExamsController`: Quản lý đề thi vấn đáp (Exam).
  - `ExamSessionsController`: Quản lý phiên thi, nộp bài vấn đáp.
  - `UsersController`: Đăng ký, đăng nhập và thông tin tài khoản.
- **Razor Views**:
  - `_Layout.cshtml`: Khung giao diện chính (header, footer, stylesheet).
  - `Home/Index.cshtml`: Giao diện danh sách khoá học và đề thi.
  - `_ViewImports.cshtml`, `_ViewStart.cshtml`.
- **ViewModels**: Chịu trách nhiệm data binding và validation cho dữ liệu người dùng nhập (`ExamViewModels`, `UserViewModels`, v.v.).
- **Hạ tầng · khởi động**:
  - `Program.cs`: Cấu hình Dependency Injection và Middleware pipeline.
  - `appsettings.json`: Cấu hình ConnectionStrings (MySQL 8.4 Aiven TLS / SQL Server) và `MaterialStorage`.
  - `Constants/AppConstants.cs`: Các hằng số ứng dụng.
  - `Filters/ApiExceptionFilter.cs`: Bắt và xử lý ngoại lệ tập trung.
  - `wwwroot/`: Chứa file tĩnh (`site.css`, assets, scripts).

---

### 2. ⚡ Business Layer (`AIVES.BusinessLogicLayer`)

- **Interfaces**:
  - Nằm ở tầng ngoài cùng cấp với Services (`Interfaces/`).
  - Hợp đồng dịch vụ: `ICourseService`, `IExamService`, `IUserService`, `IExamSessionService`.
- **Services**:
  - `UserService`: Nghiệp vụ xác thực, đăng nhập, bảo mật.
  - `CourseService`: Nghiệp vụ quản lý học phần / khoá học.
  - `ExamService`: Tạo, duyệt và phân loại đề thi viva.
  - `ExamSessionService`: Điều khiển phiên thi, tích hợp trực tiếp thuật toán chấm điểm câu trả lời viva exam.
- **Common**:
  - `ServiceResult<T>`: Chuẩn hoá kết quả trả về của các dịch vụ.
- **Khối Nối Tầng**:
  - `ServiceCollectionExtensions.cs`: Extension method `AddAivesLayers()` đăng ký toàn bộ DI của hệ thống, giúp Presentation Layer kết nối sạch sẽ với các tầng dưới.

---

### 3. 💾 Data Access Layer (`AIVES.DataAccessLayer`)

- **Repositories & Unit of Work**:
  - `IGenericRepository<T>` & `GenericRepository<T>`: Hỗ trợ cả **Query** (Get, Find, Exists, Count) và **Command** (Add, Update, Remove).
  - `IUnitOfWork` & `UnitOfWork`: Quản lý transaction tập trung cho tất cả DbSets.
- **AivesDbContext**:
  - Kế thừa `DbContext` của Entity Framework Core.
  - Hỗ trợ kết nối cơ sở dữ liệu **MySQL 8.4 (Aiven Cloud với TLS)** hoặc **SQL Server**.
- **Entity Models**:
  - `User`, `Course`, `Exam`, `Question`, `ExamSession`, `StudentAnswer`.
- **File tài liệu (`LocalMaterialFileStore`)**:
  - Quản lý lưu trữ tài liệu môn học, file âm thanh câu trả lời viva tại thư mục `Storage/materials`.
- **Hạ tầng DAL**:
  - `Common/Contracts`: `IAuditableEntity`, `ISoftDeletable`.
  - `Common/Enums`: `UserRole`, `ExamStatus`, `SessionStatus`, `QuestionDifficulty`.

---

## 📂 Cấu trúc thư mục Source Code

```
AI-powered-Viva-Exam-System/
│
├── 📄 AIVES.slnx                                # Solution file (.NET 10)
├── 📄 .gitignore
├── 📄 README.md
│
└── 📂 src/
    │
    ├── 📂 AIVES.PresentationLayer/              # 🌐 TẦNG 1: PRESENTATION LAYER (WebMVC)
    │   ├── 📂 Controllers/
    │   │   ├── HomeController.cs                # MVC Controller render Razor Views
    │   │   ├── CoursesController.cs             # API Controller khoá học
    │   │   ├── ExamsController.cs               # API Controller đề thi
    │   │   ├── ExamSessionsController.cs        # API Controller phiên thi viva
    │   │   └── UsersController.cs               # API Controller người dùng & auth
    │   ├── 📂 Views/                            # Razor Views (.cshtml)
    │   │   ├── 📂 Home/
    │   │   │   └── Index.cshtml
    │   │   ├── 📂 Shared/
    │   │   │   └── _Layout.cshtml
    │   │   ├── _ViewImports.cshtml
    │   │   └── _ViewStart.cshtml
    │   ├── 📂 ViewModels/                       # Binding & Validation
    │   │   ├── CourseViewModels.cs
    │   │   ├── ExamViewModels.cs
    │   │   └── UserViewModels.cs
    │   ├── 📂 Constants/
    │   │   └── AppConstants.cs
    │   ├── 📂 Filters/
    │   │   └── ApiExceptionFilter.cs
    │   ├── 📂 wwwroot/                          # Static files (CSS, JS)
    │   │   └── 📂 css/
    │   │       └── site.css
    │   ├── Program.cs                           # DI & Middleware pipeline
    │   ├── appsettings.json                     # ConnectionString & MaterialStorage
    │   └── appsettings.Development.json
    │
    ├── 📂 AIVES.BusinessLogicLayer/             # ⚡ TẦNG 2: BUSINESS LAYER (SERVICES)
    │   ├── 📄 ServiceCollectionExtensions.cs    # 🔗 NỐI TẦNG: DI Extension Method
    │   ├── 📄 DataAccessMappings.cs             # 🔗 NỐI TẦNG: Entity <-> DTO Mapping
    │   ├── 📂 Services/
    │   │   ├── 📂 Interfaces/
    │   │   │   ├── ICourseService.cs
    │   │   │   ├── IExamService.cs
    │   │   │   ├── IExamSessionService.cs
    │   │   │   └── IUserService.cs
    │   │   └── 📂 Implementations/
    │   │       ├── CourseService.cs
    │   │       ├── ExamService.cs
    │   │       ├── ExamSessionService.cs
    │   │       └── UserService.cs
    │   ├── 📂 BusinessRules/                    # 📐 Quy tắc nghiệp vụ riêng biệt
    │   │   ├── ScoringRule.cs                   # Tính điểm viva tự động
    │   │   ├── QuestionPickingRule.cs           # Bốc thăm câu hỏi viva
    │   │   └── ExamSchedulingRule.cs            # Kiểm tra thời gian làm bài
    │   └── 📂 DTOs/
    │       └── AllDTOs.cs                       # Request / Response DTOs
    │
    └── 📂 AIVES.DataAccessLayer/                # 💾 TẦNG 3: DATA ACCESS LAYER
        ├── 📂 Data/
        │   └── AivesDbContext.cs                # EF Core DbContext (MySQL / SQL Server)
        ├── 📂 Models/                           # Entity Models
        │   ├── User.cs
        │   ├── Course.cs
        │   ├── Exam.cs
        │   ├── Question.cs
        │   ├── ExamSession.cs
        │   └── StudentAnswer.cs
        ├── 📂 Repositories/                     # Repository & Unit of Work
        │   ├── IGenericRepository.cs
        │   ├── GenericRepository.cs
        │   ├── IUnitOfWork.cs
        │   └── UnitOfWork.cs
        ├── 📂 Storage/                          # 💾 File tài liệu
        │   └── LocalMaterialFileStore.cs        # Lưu trữ Storage/materials
        └── 📂 Common/                           # 🛠️ Hạ tầng DAL
            ├── 📂 Contracts/
            │   └── IEntityContracts.cs
            └── 📂 Enums/
                └── AppEnums.cs
```

---

## 🚀 Hướng dẫn Cài đặt & Chạy Dự án

### 1. Yêu cầu môi trường

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- MySQL 8.4 (hoặc SQL Server LocalDB)

### 2. Cấu hình Connection String

Mở file `src/AIVES.PresentationLayer/appsettings.json` và cấu hình chuỗi kết nối:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AIVES_DB;Trusted_Connection=True;MultipleActiveResultSets=true",
    "MySqlConnection": "Server=mysql-aiven-host.aivencloud.com;Port=12345;Database=defaultdb;User=avnadmin;Password=secret;SslMode=Required;"
  },
  "MaterialStorage": {
    "BasePath": "Storage/materials",
    "MaxFileSizeMB": 50
  }
}
```

### 3. Build & Chạy Solution

```bash
# Clone source code
git clone https://github.com/your-username/AI-powered-Viva-Exam-System.git
cd AI-powered-Viva-Exam-System

# Restore & Build
dotnet restore
dotnet build AIVES.slnx

# Chạy ứng dụng WebMVC
dotnet run --project src/AIVES.PresentationLayer
```

Truy cập:

- 🌐 **Web MVC Interface**: `http://localhost:5000`
- 📡 **Swagger API Documentation**: `http://localhost:5000/openapi/v1.json`

---

## 📄 License

Phân phối theo giấy phép MIT License.

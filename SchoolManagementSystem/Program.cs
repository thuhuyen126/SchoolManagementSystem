using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Repositories;
using System.Text;
using Syncfusion.Licensing;

namespace SchoolManagementSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var testModeSetting = Environment.GetEnvironmentVariable("SCHOOL_TEST_MODE");
            if (testModeSetting is not null && !bool.TryParse(testModeSetting, out _))
            {
                throw new InvalidOperationException("SCHOOL_TEST_MODE must be set to true or false.");
            }

            var testMode = bool.TryParse(testModeSetting, out var testModeEnabled) && testModeEnabled;
            var testRecaptchaBypass = bool.TryParse(
                Environment.GetEnvironmentVariable("TEST_RECAPTCHA_BYPASS"),
                out var bypassEnabled) && bypassEnabled;
            var recaptchaBypass = testMode && testRecaptchaBypass;
            var seedTestDataRequested = args.Contains("--seed-test-data", StringComparer.Ordinal);
            if (seedTestDataRequested && !testMode)
            {
                throw new InvalidOperationException("--seed-test-data is only allowed when SCHOOL_TEST_MODE=true.");
            }

            if (testMode)
            {
                var testConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
                var allowedTestDataSource = Environment.GetEnvironmentVariable("SCHOOL_TEST_ALLOWED_DATA_SOURCE");
                var testDatabaseName = Environment.GetEnvironmentVariable("SCHOOL_TEST_DATABASE_NAME");
                if (string.IsNullOrWhiteSpace(testConnectionString))
                {
                    throw new InvalidOperationException(
                        "Test mode requires ConnectionStrings__DefaultConnection. No database initialization was performed.");
                }

                var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (!string.Equals(configuredConnectionString, testConnectionString, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The effective DefaultConnection does not match the explicit test connection. No database initialization was performed.");
                }

                Microsoft.Data.SqlClient.SqlConnectionStringBuilder parsedTestConnection;
                try
                {
                    parsedTestConnection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(testConnectionString);
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException)
                {
                    throw new InvalidOperationException(
                        "ConnectionStrings__DefaultConnection is invalid. No database initialization was performed.");
                }

                if (!string.Equals(parsedTestConnection.InitialCatalog, "SchoolManagementDb_Test", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(allowedTestDataSource) ||
                    !string.Equals(parsedTestConnection.DataSource, allowedTestDataSource, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(testDatabaseName, parsedTestConnection.InitialCatalog, StringComparison.OrdinalIgnoreCase) ||
                    !parsedTestConnection.IntegratedSecurity ||
                    !string.IsNullOrEmpty(parsedTestConnection.AttachDBFilename))
                {
                    throw new InvalidOperationException(
                        "Test mode requires explicit SCHOOL_TEST_ALLOWED_DATA_SOURCE and SCHOOL_TEST_DATABASE_NAME values matching the approved test target and Windows Integrated Authentication. No database initialization was performed.");
                }

                if (seedTestDataRequested && !string.Equals(
                    Environment.GetEnvironmentVariable("SCHOOL_TEST_DATABASE_CONFIRMATION"),
                    "I_CONFIRM_DISPOSABLE_TEST_DATABASE",
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Seeding requires SCHOOL_TEST_DATABASE_CONFIRMATION=I_CONFIRM_DISPOSABLE_TEST_DATABASE. No database initialization was performed.");
                }
            }

            ValidateRequiredConfiguration(builder.Configuration, testMode, recaptchaBypass);

            if (testMode)
            {
                var testDataProtectionPath = Path.Combine(
                    builder.Environment.ContentRootPath,
                    ".test-data-protection-keys");

                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(testDataProtectionPath));
            }

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddFile("Logs/schoolmanagementsystem-{Date}.txt");

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(10);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
            builder.Services.AddSingleton<OtpChallengeStore>();
            builder.Services.AddHttpClient("GoogleReCaptcha", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            // DbContext configuration
            builder.Services.AddDbContext<SchoolDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Identity Configuration
            builder.Services.AddIdentity<User, IdentityRole>(cfg =>
            {
                cfg.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
                cfg.SignIn.RequireConfirmedEmail = true;
                cfg.User.RequireUniqueEmail = true;
                cfg.Lockout.AllowedForNewUsers = true;
                cfg.Lockout.MaxFailedAccessAttempts = 5;
                cfg.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

                // THAY ĐỔI CỰC KỲ QUAN TRỌNG Ở ĐÂY: Đồng bộ quy tắc mật khẩu
                cfg.Password.RequireDigit = true;            // Bắt buộc có số
                cfg.Password.RequiredUniqueChars = 1;        // Có ít nhất 1 ký tự khác biệt
                cfg.Password.RequireUppercase = true;        // Bắt buộc có chữ in hoa
                cfg.Password.RequireLowercase = true;        // Bắt buộc có chữ thường
                cfg.Password.RequireNonAlphanumeric = true;  // Bắt buộc có ký tự đặc biệt (@, #, !, v.v.)
                cfg.Password.RequiredLength = 10;            // Độ dài tối thiểu 10 ký tự
            })
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<SchoolDbContext>();

            // Authentication services
            var jwtSigningKey = builder.Configuration["Tokens:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey))
            {
                throw new InvalidOperationException("Required configuration 'Tokens:Key' is missing.");
            }

            builder.Services.AddAuthentication()
                .AddCookie()
                .AddJwtBearer(cfg =>
                {
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = builder.Configuration["Tokens:Issuer"],
                        ValidAudience = builder.Configuration["Tokens:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSigningKey))
                    };
                });

            // Repositories
            builder.Services.AddScoped<IAlertRepository, AlertRepository>();
            builder.Services.AddScoped<ICourseRepository, CourseRepository>();
            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IGradeRepository, GradeRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<ISchoolClassRepository, SchoolClassRepository>();
            builder.Services.AddScoped<IStudentRepository, StudentRepository>();
            builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
            builder.Services.AddScoped<ITeacherRepository, TeacherRepository>();
            builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();

            // Generic repository
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Inject UserHelper, MailHelper, BlobHelper, and other helpers
            builder.Services.AddScoped<IUserHelper, UserHelper>();
            if (testMode)
            {
                builder.Services.AddTransient<IMailHelper, TestMailHelper>();
            }
            else
            {
                builder.Services.AddTransient<IMailHelper, MailHelper>();
            }
            builder.Services.AddScoped<IBlobHelper, BlobHelper>();
            builder.Services.AddScoped<IConverterHelper, ConverterHelper>();
            builder.Services.AddScoped<IActivityLogHelper, ActivityLogHelper>();

            // --- ĐÃ THÊM MỚI Ở ĐÂY: Đăng ký AlertHelper ---
            builder.Services.AddScoped<IAlertHelper, AlertHelper>();
            // ----------------------------------------------

            // Register SeedDb to seed the database with initial data
            builder.Services.AddTransient<SeedDb>();
            builder.Services.AddTransient<TestSeedDb>();

            var app = builder.Build();
            var syncfusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
            }

            if (seedTestDataRequested)
            {
                using var scope = app.Services.CreateScope();
                var testSeedDb = scope.ServiceProvider.GetRequiredService<TestSeedDb>();
                await testSeedDb.SeedAsync();
                return;
            }

            // Preserve the existing initialization behavior outside test mode.
            if (!testMode)
            {
                using (var scope = app.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    var seedDb = services.GetRequiredService<SeedDb>();
                    await seedDb.SeedAsync();

                    var userHelper = services.GetRequiredService<IUserHelper>();
                    await userHelper.CheckRoleAsync("Admin");
                    await userHelper.CheckRoleAsync("Employee");
                    await userHelper.CheckRoleAsync("Student");
                    await userHelper.CheckRoleAsync("Teacher");
                    await userHelper.CheckRoleAsync("Anonymous");
                    await userHelper.CheckRoleAsync("Pending");
                }
            }
            // Exception and error handling middleware
            if (!app.Environment.IsDevelopment())
            {
                // Redirects to general error page
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Configuration for specific HTTP status codes
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();

            app.UseAuthentication(); // Required for authentication
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }

        private static void ValidateRequiredConfiguration(
            IConfiguration configuration,
            bool testMode,
            bool recaptchaBypass)
        {
            Require(configuration.GetConnectionString("DefaultConnection"), "ConnectionStrings:DefaultConnection");
            Require(configuration["Tokens:Issuer"], "Tokens:Issuer");
            Require(configuration["Tokens:Audience"], "Tokens:Audience");

            var signingKey = configuration["Tokens:Key"];
            Require(signingKey, "Tokens:Key");
            if (System.Text.Encoding.UTF8.GetByteCount(signingKey!) < 32)
            {
                throw new InvalidOperationException("Configuration 'Tokens:Key' must contain at least 256 bits of key material.");
            }

            if (!testMode)
            {
                Require(configuration["Mail:NameFrom"], "Mail:NameFrom");
                Require(configuration["Mail:From"], "Mail:From");
                Require(configuration["Mail:Smtp"], "Mail:Smtp");
                Require(configuration["Mail:Password"], "Mail:Password");
                if (!int.TryParse(configuration["Mail:Port"], out var mailPort) || mailPort is < 1 or > 65535)
                {
                    throw new InvalidOperationException("Configuration 'Mail:Port' is missing or invalid.");
                }
            }
            else
            {
                var testEmailMode = Environment.GetEnvironmentVariable("TEST_EMAIL_MODE");
                if (!string.IsNullOrWhiteSpace(testEmailMode) &&
                    !string.Equals(testEmailMode, "local", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Test mode only permits local email delivery.");
                }
            }

            if (!recaptchaBypass)
            {
                Require(configuration["GoogleReCAPTCHA:SiteKey"], "GoogleReCAPTCHA:SiteKey");
                Require(configuration["GoogleReCAPTCHA:SecretKey"], "GoogleReCAPTCHA:SecretKey");
                var allowedHostnames = configuration.GetSection("GoogleReCAPTCHA:AllowedHostnames").Get<string[]>();
                if (allowedHostnames == null || allowedHostnames.Length == 0 ||
                    allowedHostnames.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException("Configuration 'GoogleReCAPTCHA:AllowedHostnames' is missing or invalid.");
                }
            }
        }

        private static void Require(string? value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Required configuration '{key}' is missing.");
            }
        }
    }
}

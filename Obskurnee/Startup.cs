using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Converters;
using Obskurnee.Hubs;
using Obskurnee.Models;
using Obskurnee.Services;
using Serilog;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using VueCliMiddleware;

namespace Obskurnee
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration, sectionName: "Logging")
                .CreateLogger();
        }

        public void ConfigureServices(
            IServiceCollection services)
        {
            Config.Current = new Config();
            Directory.CreateDirectory(Config.DataFolder);

            services.AddControllers(options =>
            {
                options.Filters.Add(new HttpResponseExceptionFilter());
            })
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling
                        = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                });

            services.AddSpaStaticFiles(configuration => { configuration.RootPath = Path.Combine("ClientApp", "dist"); });

            Configuration.Bind(Config.Current);
            Trace.Assert(
                Config.SupportedLanguages.Contains(Config.Current.DefaultCulture),
                "Unsupported Default Culture");

            ConfigureDI(services);
            ConfigureLocalization(services);

            services.AddSignalR()
                .AddNewtonsoftJsonProtocol(options =>
                {
                    options.PayloadSerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                    options.PayloadSerializerSettings.Converters.Add(new StringEnumConverter());
                });
            ConfigureAuthAndIdentity(services);
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime lifetime,
            UserServiceBase userService,
            ApplicationDbContext dbContext)
        {
            Log.Information("Updating database");
            dbContext.Database.Migrate();

            Log.Information("Setting up for environment {env}", env.EnvironmentName);
            app.UseSerilogRequestLogging();
            app.UseRequestLocalization();
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseCors(builder =>
            {
                builder.WithOrigins(Config.Current.BaseUrl, Config.Current.Urls)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            app.UseAuthentication();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseSpaStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<EventHub>("/hubs/events");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp/";
                if (env.IsDevelopment())
                {
                    spa.UseVueCli(npmScript: "serve");
                }
            });

            userService.LoadUsernameCache();
            lifetime.ApplicationStarted.Register(() =>
                Log.Information("Application started at {@base}",
                Config.Current.BaseUrl));
            lifetime.ApplicationStopping.Register(() => Log.Information("Application stopping"));
            lifetime.ApplicationStopped.Register(() => Log.Information("Application stopped"));
        }

        private static void ConfigureAuthAndIdentity(IServiceCollection services)
        {
            services.AddAuthentication(cfg =>
            {
                cfg.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                cfg.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        LifetimeValidator = (before, expires, token, param) =>
                        {
                            return expires > DateTime.UtcNow;
                        },
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateActor = false,
                        ValidateLifetime = true,
                        IssuerSigningKey = Config.Current.SecurityKey,
                    };
                });

            services.AddIdentityCore<Bookworm>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = Config.Current.DefaultPasswordMinLength;
                options.ClaimsIdentity.UserIdClaimType = BookclubClaims.UserId;
                options.User.AllowedUserNameCharacters = ""; // all
            })
               .AddEntityFrameworkStores<ApplicationDbContext>()
               .AddSignInManager<SignInManager<Bookworm>>()
               .AddDefaultTokenProviders();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ModOnly", policy => policy.RequireClaim(BookclubClaims.Moderator));
                options.AddPolicy("AdminOnly", policy => policy.RequireClaim(BookclubClaims.Admin));
            });
        }

        private void ConfigureDI(IServiceCollection services)
        {
            services.AddSingleton(Config.Current);
            services.AddTransient<GoodreadsScraper>();
            services.AddTransient<PollService>();
            services.AddTransient<BookService>();
            services.AddTransient<RoundManagerService>();
            services.AddTransient<DiscussionService>();
            services.AddTransient<SettingsService>();
            services.AddTransient<NewsletterService>();
            services.AddTransient<RecommendationService>();
            services.AddTransient<MatrixService>();
            services.AddTransient<ReviewService>();
            services.AddTransient<BackupService>();
            services.AddHostedService<FeedFetcherService>();

#if DEMOMODE
            services.AddTransient<UserServiceBase, DemoUserService>();
#else
            services.AddTransient<UserServiceBase, UserService>();
#endif

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(
                    Configuration.GetConnectionString("SqliteConnection")));

#if DEMOMODE
            services.AddTransient<IMailerService, FakeMailerService>();
#else
            switch (Config.Current.MailerType)
            {
                case "mailgun":
                    services.AddTransient<IMailerService, MailgunMailerService>();
                    break;
                case "log-only":
                    services.AddTransient<IMailerService, FakeMailerService>();
                    break;
                default:
                    throw new ConfigurationErrorsException($"Invalid mailer type: {Configuration["MailerType"]}");
            }

            if (Config.Current.EnablePeriodicBackup)
            {
                services.AddHostedService<PeriodicBackupService>();
            }
#endif
        }

        private static void ConfigureLocalization(IServiceCollection services)
        {
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services.Configure<RequestLocalizationOptions>(
                options =>
                {
                    var supportedCultures = Config.SupportedLanguages.Select(l => new CultureInfo(l)).ToList();

                    options.DefaultRequestCulture = new RequestCulture(
                        culture: Config.Current.DefaultCulture,
                        uiCulture: Config.Current.DefaultCulture);
                    options.SetDefaultCulture(Config.Current.DefaultCulture);
                    options.SupportedCultures = supportedCultures;
                    options.SupportedUICultures = supportedCultures;

                    options.AddInitialRequestCultureProvider(
                        new AcceptLanguageHeaderRequestCultureProvider());
                });
        }
    }
}
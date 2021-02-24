using AspNetCore.Identity.LiteDB;
using AspNetCore.Identity.LiteDB.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Obskurnee.Models;
using Obskurnee.Services;
using Serilog;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VueCliMiddleware;
using System.Text.Json.Serialization;
using System.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Obskurnee
{
    public class Startup
    {
        // Configs
        public static readonly SymmetricSecurityKey SecurityKey =
            new SymmetricSecurityKey(
                Encoding.Default.GetBytes("ghf345678oikjhgfde3456789ijbvcdsw6789opkjfdeuijknbvgfdre4567uij"));
        public const string DataFolder = "data";
        public const string ImageFolder = "images";
        public const string BaseUrl = "http://localhost:5000";
        public const int DefaultPasswordMinLength = 13;
        public const string PasswordGenerationChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        public const string GoodreadsRssBaseUrl = "https://www.goodreads.com/review/list_rss/";
        public const string GoodreadsProfielUrlPrevix = "https://www.goodreads.com/user/";
        public const string GlobalCulture = "sk";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(
            IServiceCollection services)
        {
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(Path.Combine(DataFolder, ImageFolder));

            services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp";
            });

            var databaseSingleton = new Database(Log.Logger.ForContext<Database>());

            services.AddSingleton<Database>(databaseSingleton);
            services.AddSingleton<ILiteDbContext>((ILiteDbContext)databaseSingleton);
            services.AddTransient<GoodreadsScraper>();
            services.AddTransient<PollService>();
            services.AddTransient<UserService>();
            services.AddTransient<BookService>();
            services.AddTransient<RoundManagerService>();
            services.AddTransient<DiscussionService>();
            services.AddTransient<SettingsService>();
            services.AddTransient<NewsletterService>();
            services.AddTransient<RecommendationService>();
            services.AddSingleton<MatrixService>();

            switch (Configuration["MailerType"])
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

            ConfigureAuthAndIdentity(services);
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            UserService userService)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseSpaStaticFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(env.ContentRootPath, DataFolder, ImageFolder)),
                RequestPath = "/images"
            });

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSerilogRequestLogging();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSpa(spa =>
            {
                if (env.IsDevelopment())
                {
                    spa.Options.SourcePath = "ClientApp/";
                }
                else
                {
                    spa.Options.SourcePath = "dist";
                }

                if (env.IsDevelopment())
                {
                    spa.UseVueCli(npmScript: "serve");
                }
            });

            userService.ReloadCache();
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
                        IssuerSigningKey = SecurityKey,
                    };
                    // The JwtBearer scheme knows how to extract the token from the Authorization header
                    // but we will need to manually extract it from the query string in the case of requests to the hub
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = ctx =>
                        {
                            if (ctx.Request.Query.ContainsKey("access_token"))
                            {
                                ctx.Token = ctx.Request.Query["access_token"];
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services.Configure<RequestLocalizationOptions>(
                options =>
                {
                    var supportedCultures = new[]
                    {
                        new CultureInfo(GlobalCulture)
                    };

                    options.DefaultRequestCulture = new RequestCulture(culture: GlobalCulture, uiCulture: GlobalCulture);
                    options.SupportedCultures = supportedCultures;
                    options.SupportedUICultures = supportedCultures;

                    options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(async context =>
                    {
                        return new ProviderCultureResult(GlobalCulture);
                    }));
                });

            services.AddIdentityCore<Bookworm>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 15;
                options.ClaimsIdentity.UserIdClaimType = BookclubClaims.UserId;
            })
               .AddUserStore<LiteDbUserStore<Bookworm>>()
               .AddSignInManager<SignInManager<Bookworm>>()
               .AddDefaultTokenProviders();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ModOnly", policy => policy.RequireClaim(BookclubClaims.Moderator));
                options.AddPolicy("AdminOnly", policy => policy.RequireClaim(BookclubClaims.Admin));
            });
        }
    }
}
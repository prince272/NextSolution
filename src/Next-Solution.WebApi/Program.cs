using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Settings.Configuration;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Next_Solution.WebApi.Data.Entities.Identity;
using Next_Solution.WebApi.Data;
using Next_Solution.WebApi.Providers.Messaging.MailKit;
using Next_Solution.WebApi.Middlewares;
using Next_Solution.WebApi.Extensions;
using Next_Solution.WebApi.Providers.SwaggerGen;
using Next_Solution.WebApi.Helpers;
using Next_Solution.WebApi.Providers.Messaging.Twilio;
using Next_Solution.WebApi.Providers.JwtBearer;
using Next_Solution.WebApi.Providers.RazorViewRender;
using Next_Solution.WebApi.Providers.ModelValidator;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Next_Solution.WebApi.Options;
using Microsoft.OpenApi.Models;
using System.Reflection;


#if (configureNgrok)
using Next_Solution.WebApi.Providers.Ngrok;
#endif
using Next_Solution.WebApi.Services;

try
{
    // Set the default culture info to "en-GH"
    var defaultCultureInfo = new CultureInfo("en-GH");
    CultureInfo.DefaultThreadCurrentCulture = defaultCultureInfo;
    CultureInfo.DefaultThreadCurrentUICulture = defaultCultureInfo;

    var appAssemblies = AssemblyHelper.GetAppAssemblies().ToArray();


    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog for logging
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration,
            new ConfigurationReaderOptions { SectionName = "Serilog" })
        .Enrich.FromLogContext()
        .CreateLogger();

    builder.Logging.ClearProviders();
    builder.Host.UseSerilog(Log.Logger);

    // Configure JSON serialization options
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        var serializerOptions = options.SerializerOptions;
        serializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        serializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        serializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        serializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

    // Configure API behavior options
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

    builder.Services.AddDataProtection()
        .UseCryptographicAlgorithms(new AuthenticatedEncryptorConfiguration
        {
            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
            ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
        });

    // Configure database context with SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("Application");
        options.UseSqlServer(connectionString, sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));
    });

    // Configure Identity services
    builder.Services.AddIdentity<User, Role>(options =>
    {
        // Password settings
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 0;
        options.Password.RequiredUniqueChars = 0;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.AllowedUserNameCharacters = string.Empty;
        options.User.RequireUniqueEmail = false;

        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        // Token providers
        options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
        options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
        options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;

        // Claim types
        options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
        options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Name;
        options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
        options.ClaimsIdentity.EmailClaimType = ClaimTypes.Email;
        options.ClaimsIdentity.SecurityStampClaimType = ClaimTypes.SerialNumber;
    })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<UserClaimsPrincipalFactory<User, Role>>();

    builder.Services.AddAutoMapper(appAssemblies);
    builder.Services.AddModelValidator(appAssemblies);
    builder.Services.AddRazorViewRenderer(appAssemblies);

    // Configure messaging services
    builder.Services.AddMailKitSender(options =>
    {
        builder.Configuration.GetRequiredSection("MailKit").Bind(options);
    });

    builder.Services.AddTwilioSender(options =>
    {
        builder.Configuration.GetRequiredSection("Twilio").Bind(options);
    });

    // Configure authentication schemes
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
        .AddJwtProvider(options =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins")?.Get<string[]>();
            if (allowedOrigins != null && allowedOrigins.Length != 0)
                options.Issuer = string.Join(";", allowedOrigins);
        })
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            builder.Configuration.GetRequiredSection("OAuth:Google").Bind(options);
        })
        .AddFacebook(FacebookDefaults.AuthenticationScheme, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            builder.Configuration.GetRequiredSection("OAuth:Facebook").Bind(options);
        });

    // Configure CORS policies
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy = policy
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition")
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
                .SetIsOriginAllowedToAllowWildcardSubdomains();


            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins")?.Get<string[]>();

            if (allowedOrigins != null)
                policy.WithOrigins(allowedOrigins);

            else policy.AllowAnyOrigin();
        });
    });

    // Configure routing and JSON options
    builder.Services.AddRouting(options =>
    {
        options.LowercaseUrls = true;
        options.LowercaseQueryStrings = false;
    })
        .AddControllers()
        .AddJsonOptions(options =>
        {
            var serializerOptions = options.JsonSerializerOptions;
            serializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            serializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            serializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            serializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

    // Configure Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        // Add the JWT security scheme
        var jwtSecurityScheme = new OpenApiSecurityScheme
        {
            BearerFormat = "JWT",
            Name = "JWT Authentication",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            Description = "Put **_ONLY_** your JWT Bearer token on the textbox below!",

            Reference = new OpenApiReference
            {
                Id = JwtBearerDefaults.AuthenticationScheme,
                Type = ReferenceType.SecurityScheme
            }
        };



        // .NET 7 introduce Typed Http Results, but Swashbuckle don't generate the Open Api Response from this types.
        // This filter will generate the Open Api Response for Typed Http Results.
        // source: https://github.com/vernou/Vernou.Swashbuckle.HttpResultsAdapter
        options.OperationFilter<HttpResultsOperationFilter>();

        options.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });

        // Include XML comments (optional)
        var xmlFilePath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlFilePath))
        {
            options.IncludeXmlComments(xmlFilePath);
        }
    });

    builder.Services.Configure<IdentityServiceOptions>(options =>
    {
        options.FormProtectorKey = Guid.NewGuid().ToString("N");
    });
    builder.Services.AddScoped<IIdentityService, IdentityService>();

#if (configureNgrok)
    // Configure Ngrok
    if (builder.Environment.IsDevelopment())
    {
        var startNgrokService = builder.Configuration.GetValue<bool>("Ngrok:StartNgrokService");

        if (startNgrokService)
        {
            Log.Information("Registering Ngrok hosted service as the application is running in development.");

            builder.Services.AddNgrokHostedService(options =>
            {
                builder.Configuration.GetRequiredSection("Ngrok").Bind(options);
            });
        }
        else
        {
            Log.Information("Skipping Ngrok hosted service registration as 'Ngrok:StartNgrokService' is not enabled.");
        }
    }
#endif

    var app = builder.Build();

    // Run database migrations
    await app.RunDbMigrationsAsync<ApplicationDbContext>();

    app.UseDbTransaction<ApplicationDbContext>();

    // Configure error handling and status code pages
    app.UseStatusCodePagesWithReExecute("/errors/{0}");
    app.UseExceptionHandler(new ExceptionHandlerOptions()
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = null,
        ExceptionHandlingPath = "/errors/500"
    });

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger(c =>
        {
            c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
            {
                string currentOrigin = $"{httpReq.Scheme}://{httpReq.Host}";
                swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer
            {
                Url = currentOrigin,
                Description = "Defines the server URL and configuration for API operations."
            }
        };
            });
        });

        app.UseSwaggerUI(swaggerUiOptions =>
        {
            var responseInterceptor = @"(res) => 
        {
            if (res.obj && res.obj.accessToken) 
            { 
                console.log(res.obj.accessToken);
                const token = res.obj.accessToken;
                localStorage.setItem('token', token);
            }; 
            return res; 
        }";
            var requestInterceptor = @"(req) => 
        { 
            const token = localStorage.getItem('token');
            if (token) {
                req.headers['Authorization'] = 'Bearer ' + token;
            }
            return req; 
        }";
            swaggerUiOptions.UseResponseInterceptor(Regex.Replace(responseInterceptor, @"\s+", " "));
            swaggerUiOptions.UseRequestInterceptor(Regex.Replace(requestInterceptor, @"\s+", " "));
        });

    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

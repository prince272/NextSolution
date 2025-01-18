﻿using DeviceId;
using Microsoft.Extensions.Options;
using Next_Solution.WebApi.Helpers;
using System.Reflection;

namespace Next_Solution.WebApi.Providers.JwtBearer
{
    public class ConfigureJwtProviderOptions : IConfigureOptions<JwtProviderOptions>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ConfigureJwtProviderOptions(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public void Configure(JwtProviderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.Secret ??= HashHelper.GenerateSHA256Hash(new DeviceIdBuilder()
                  .AddMachineName()
                  .AddOsVersion()
                  .AddUserName()
                  .AddFileToken(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"jwt-secret.txt")).ToString());

            var httpContext = (_httpContextAccessor?.HttpContext) ?? throw new InvalidOperationException("Unable to determine the current HttpContext.");
            string currentOrigin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}".ToLowerInvariant();

            options.Issuer = string.Join(";",
                (options.Issuer?.Split(';') ?? [])
                .Append(currentOrigin)
                .Where(issuer => !string.IsNullOrWhiteSpace(issuer)));

            options.Audience = string.Join(";",
                (options.Audience?.Split(';') ?? [])
                .Append(currentOrigin)
                .Where(audience => !string.IsNullOrWhiteSpace(audience)));
        }
    }
}

﻿using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using MvcCoreApiWithDocs.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace MvcCoreApiWithDocs
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var readPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).
                RequireAuthenticatedUser().
                RequireClaim("scope", "read").Build();

            var writePolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).
                RequireAuthenticatedUser().
                RequireClaim("scope", "write").Build();

            services.AddSingleton<IContactRepository, InMemoryContactRepository>();
            services.AddMvcCore(opt =>
                {
                    opt.Filters.Add(new AuthorizeFilter("ReadPolicy"));
                }).AddAuthorization(o =>
                {
                    o.AddPolicy("ReadPolicy", readPolicy);
                    o.AddPolicy("WritePolicy", writePolicy);
                }).AddDataAnnotations().
                AddJsonFormatters().
                AddApiExplorer();

            // set up embedded identity server
            services.AddIdentityServer().
                AddTestClients().
                AddTestResources().
                AddTemporarySigningCredential();

            services.AddSwaggerGen(options => {
                options.SwaggerDoc("v1", new Info
                {
                    Title = "Contacts API",
                    Version = "v1",
                    Description = "Used to exchange contact information"
                });

                options.AddSecurityDefinition("oauth2", new OAuth2Scheme
                {
                    Type = "oauth2",
                    Flow = "Client Credentials",
                    Scopes = new Dictionary<string, string> { { "read", "Read access"}, {"write", "Write access"} },
                    TokenUrl = "http://localhost:28134"
                });

                options.OperationFilter<ScopesDefinitionOperationFilter>(new Dictionary<string, string> { { "ReadPolicy", "read" }, { "WritePolicy", "write" } });

                var xmlDocs = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "MvcCoreApiWithDocs.xml");
                options.IncludeXmlComments(xmlDocs);
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));

            // use embedded identity server to issue tokens
            app.UseIdentityServer();

            // consume the JWT tokens in the API
            app.UseIdentityServerAuthentication(new IdentityServerAuthenticationOptions
            {
                Authority = "http://localhost:28134",
                RequireHttpsMetadata = false,
            });

            app.UseSwagger();
            app.UseSwaggerUi(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
            });

            app.UseMvc();
        }
    }
}

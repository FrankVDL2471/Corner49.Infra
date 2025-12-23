using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.Auth {

	internal class Auth0SchemeTransformer : IOpenApiDocumentTransformer {

		private readonly IAuthenticationSchemeProvider _schemeProvider;
		private readonly IOptions<AuthSettings> _config;

		public Auth0SchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider, IOptions<AuthSettings> config) {
			_schemeProvider = authenticationSchemeProvider;
			_config = config;
		}

		public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {
			var authenticationSchemes = await _schemeProvider.GetAllSchemesAsync();


			document.Components ??= new OpenApiComponents();
			//	document.Components.SecuritySchemes = requirements;

			document.Components.SecuritySchemes.Add("oath2", new OpenApiSecurityScheme {
				Name = "Authorization",
				Scheme = JwtBearerDefaults.AuthenticationScheme,
				Type = SecuritySchemeType.OAuth2,
				//BearerFormat = "JWT",
				In = ParameterLocation.Header,
				Flows = new OpenApiOAuthFlows {
					Implicit = new OpenApiOAuthFlow {
						TokenUrl = new Uri($"https://{_config.Value.Domain}/oauth/token"),
						AuthorizationUrl = new Uri($"https://{_config.Value.Domain}/authorize?audience={_config.Value.Audience}"),
						Scopes = new Dictionary<string, string>
							{
								{"openid", "Open Id"},
								{"email", "User Email"},
								{"sub", "User ID"},
								{"aud", "Audience"},
								{"offline_access", "Offline Access"}
							},
					}
				}
			});




			// Apply it as a requirement for all operations
			var scheme = new OpenApiSecurityScheme {
				Reference = new OpenApiReference {
					Type = ReferenceType.SecurityScheme,
					Id = JwtBearerDefaults.AuthenticationScheme
				},
				Scheme = "oauth2",
				Name = JwtBearerDefaults.AuthenticationScheme,
				In = ParameterLocation.Header
			};

			foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations)) {
				operation.Value.Security.Add(new OpenApiSecurityRequirement {
					[scheme] = new[] { "openid" }
				});
			}
		}
	}
}

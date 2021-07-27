using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace Function.CSharp.OBO
{
    public static class MiddleTierAPI
    {
        [FunctionName("MiddleTierAPI")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("MiddleTierAPI function processed a request.");

            string tenantId = Environment.GetEnvironmentVariable("TenantId");
            string clientId = Environment.GetEnvironmentVariable("ClientId");
            string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            string[] downstreamApiScopes = { "https://graph.microsoft.com/.default" };

            try
            {
                if (string.IsNullOrEmpty(tenantId) ||
                string.IsNullOrEmpty(tenantId) ||
                string.IsNullOrEmpty(tenantId))
                {
                    throw new Exception("Configuration values are missing.");
                }

                string authority = $"https://login.microsoftonline.com/{tenantId}";
                string issuer = $"https://sts.windows.net/{tenantId}/";
                string audience = $"api://{clientId}";

                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                   .WithAuthority(authority)
                   .WithClientSecret(clientSecret)
                   .Build();

                var headers = req.Headers;
                var token = string.Empty;
                if (headers.TryGetValue("Authorization", out var authHeader))
                {
                    if (authHeader[0].StartsWith("Bearer "))
                    {
                        token = authHeader[0].Substring(7, authHeader[0].Length - 7);
                    }
                    else
                    {
                        return new UnauthorizedResult();
                    }
                }


                var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    issuer + "/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever());

                bool validatedToken = await ValidateToken(token, issuer, audience, configurationManager);

                if (!validatedToken)
                {
                    throw new Exception("Token validation failed.");
                }

                UserAssertion userAssertion = new UserAssertion(token);
                AuthenticationResult result = await app.AcquireTokenOnBehalfOf(downstreamApiScopes, userAssertion).ExecuteAsync();

                string accessToken = result.AccessToken;
                if (accessToken == null)
                {
                    throw new Exception("Access Token could not be acquired.");
                }

                var myObj = new { access_token = accessToken };
                var jsonToReturn = JsonConvert.SerializeObject(myObj);
                return new OkObjectResult(jsonToReturn);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        private static async Task<bool> ValidateToken(
            string token,
            string issuer,
            string audience,
            IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException(nameof(issuer));

            var discoveryDocument = await configurationManager.GetConfigurationAsync(default(CancellationToken));
            var signingKeys = discoveryDocument.SigningKeys;

            var validationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            try
            {
                new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var rawValidatedToken);
                return true;
            }
            catch (SecurityTokenValidationException)
            {
                return false;
            }
        }
    }
}

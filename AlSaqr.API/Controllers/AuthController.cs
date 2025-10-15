using AlSaqr.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using System;
using AlSaqr.Data;
using System.Security.Claims;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {

        private readonly ILogger<AuthController> _logger;
        private readonly IDriver _driver;


        public AuthController(ILogger<AuthController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        [HttpGet("external-login/{provider}")]
        public IActionResult ExternalLogin([FromRoute] string provider, [FromQuery] string returnUrl = "/")
        {
            var redirectUrl = Url.Action(nameof(ExternalCallback), "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet("signin-google")]
        public IActionResult GoogleSignin()
        {

            return Ok();
        }

        [HttpGet("external-callback")]
        public async Task<IActionResult> ExternalCallback(string returnUrl = "/")
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
                return BadRequest("External authentication error");

            var claims = authenticateResult.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            // (Optional) register user or generate JWT here

            return Redirect($"http://localhost:3000{returnUrl}?email={email}&name={name}");
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                Name = User.Identity?.Name,
                Email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
            });
        }
    }
}

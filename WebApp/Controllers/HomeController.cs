using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using System.Text;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {

        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(ITokenAcquisition tokenAcquisition, IHttpClientFactory httpClientFactory)
        {
            _tokenAcquisition = tokenAcquisition;
            _httpClientFactory = httpClientFactory;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                var token = await _tokenAcquisition.GetAccessTokenForUserAsync(
                    new[] { "User.Read" },
                    user: HttpContext.User,
                    authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var resp = await client.GetAsync("https://graph.microsoft.com/v1.0/me");
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                var me = JsonSerializer.Deserialize<JsonElement>(json);

                HttpContext.Session.SetString("email", me.GetProperty("userPrincipalName").GetString() ?? "");
                HttpContext.Session.SetString("user", HttpContext.Session.GetString("email").Split('@')[0].ToLower());
                HttpContext.Session.SetString("email_contact", me.TryGetProperty("mail", out var mailProp)
                    ? mailProp.GetString() ?? HttpContext.Session.GetString("email")
                    : HttpContext.Session.GetString("email"));

                ViewBag.User = HttpContext.Session.GetString("user");
                ViewBag.Email = HttpContext.Session.GetString("email");
                ViewBag.EmailContact = HttpContext.Session.GetString("email_contact");

            }
            catch (MicrosoftIdentityWebChallengeUserException ex)
            {
                return Challenge(
                    new AuthenticationProperties { RedirectUri = "/" },
                    OpenIdConnectDefaults.AuthenticationScheme);
            }
            

            return View("Site"); // Renders site.cshtml
        }

        [HttpPost]
        [Authorize]
        [Route("api/transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferRequest req)
        {
            var email = req.Email ?? HttpContext.Session.GetString("email");
            var user = req.User ?? HttpContext.Session.GetString("user");
            var contact = req.Contact ?? HttpContext.Session.GetString("email_contact");

            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { error = "Unauthorized" });

            string targetUrl;
            string successMsg;

            if (req.Action == "ml_to_storage")
            {
                targetUrl = "PUT YOUR LOGIC APP WORKFLOW TRIGGER LINK HERE";
                successMsg = "ML → Public Storage request sent.";
            }
            else if (req.Action == "storage_to_ml")
            {
                targetUrl = "PUT YOUR LOGIC APP WORKFLOW TRIGGER LINK HERE";
                successMsg = "Public Storage → ML request sent.";
            }
            else
            {
                return BadRequest(new { error = "Invalid action" });
            }

            var payload = new { email, user, email_contact = contact };
            string jsonToPost = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonToPost, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            var resp = await client.PostAsync(targetUrl, content);

            if (resp.IsSuccessStatusCode)
                return Ok(new { message = successMsg });
            else
                return StatusCode(500, new { error = "Transfer failed" });
        }
    }
    public class TransferRequest
    {
        public string Action { get; set; }
        public string User { get; set; }
        public string Email { get; set; }
        public string Contact { get; set; }
    }

}

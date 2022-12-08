using Microsoft.AspNetCore.Mvc;
using demo.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Newtonsoft.Json;
using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Build.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace demo.Controllers;

[ApiController]
[Route("api")]
public class UserController : ControllerBase
{

    private readonly DemoSpecContext _DBContext;
    private readonly IConfiguration _configuration;

    public UserController(DemoSpecContext DBContext, IConfiguration configuration)
    {
        this._DBContext = DBContext;
        this._configuration = configuration;
    }

    [HttpGet]
    [Route("GetAll")]
    public IActionResult Get()
    {
        var users = this._DBContext.Users.ToList();
        return Ok(users);
    }

    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> Login([FromBody] UserLogin userLogin)
    {
        var user = this._DBContext.Users.FirstOrDefault(u => u.Username == userLogin.Username);
        // if (user.Status == 0) return BadRequest(new { description = "Email isn't activated yet"});
        if (user != null && user.Password==userLogin.Password)
        {
            if (user.Status == 1){
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

                var token = new JwtSecurityToken(
                    issuer: _configuration["JWT:Issuer"],
                    audience: _configuration["JWT:Audience"],
                    expires: DateTime.Now.AddHours(3),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                    );

                return Ok(new
                {
                    data = new {accessToken = new JwtSecurityTokenHandler().WriteToken(token)}
                });
            }
            else return BadRequest(new { description = "Email isn't activated yet"});
        }
        return Unauthorized();
    }

    [HttpPost]
    [Route("sign_up_validate")]
    public IActionResult SignUpValidate([FromBody] User _user){
        var userExists = this._DBContext.Users.FirstOrDefault(u => u.Email == _user.Email || u.Username == _user.Username);
        if (userExists != null)
            return Conflict();

        return NoContent();
    }

    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> Register([FromBody] User _user)
    {
        var codeActivation = CreateActivationKey();

        var userExists = this._DBContext.Users.FirstOrDefault(u => u.Email == _user.Email);
        if (userExists != null)
            return Conflict();

        _user.Code = codeActivation;
        _user.Status = 0;
        this._DBContext.Users.Add(_user);
        this._DBContext.SaveChanges();

        // send mail 
        var client = new SendGridClient(_configuration["apiKeyGrid"]);
        var msg = new SendGridMessage(){
            From = new EmailAddress("admin@demo_spec.com"),
            Subject = "Active Account for Login",
            PlainTextContent = codeActivation
        };
        msg.AddTo(new EmailAddress(_user.Email));
        await client.SendEmailAsync(msg);

        return Ok(new {
            data = new { email = _user.Email}
        });
    }
    private string CreateActivationKey()
    {
        var activationKey = Guid.NewGuid().ToString();

        var activationKeyAlreadyExists = this._DBContext.Users.FirstOrDefault(u => u.Code == activationKey);

        if (activationKeyAlreadyExists != null)
        {
            activationKey = CreateActivationKey();
        }

        return activationKey;
    }

    [HttpPut]
    [Route("sign_up_active")]
    public IActionResult SignUpActive(string code, string email)
    {
        var user = this._DBContext.Users.FirstOrDefault(u => u.Email == email);

        if (email == null || user == null) {
            return BadRequest(new {
                description = new { email = "email is invalid"}
            });
        }
        
        if (user.Code != code || code == null){
            return BadRequest(new {
                escription = new { tokenCode = "code is invalid"}
            });
        }

        user.Status = 1;
        this._DBContext.SaveChanges();

        var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            expires: DateTime.Now.AddHours(3),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

        return Ok(new
        {
            data = new {accessToken = new JwtSecurityTokenHandler().WriteToken(token)}
        });
    }

    [HttpPost]
    [Route("resend_code")]
    public IActionResult ResendCode(string email){
        var user = this._DBContext.Users.FirstOrDefault(u => u.Email == email);

        if (email == null || user == null) {
            return BadRequest(new {
                description = new { email = "email is invalid"}
            });
        }
        var client = new SendGridClient(_configuration["apiKeyGrid"]);
        var msg = new SendGridMessage(){
            From = new EmailAddress("admin@demo_spec.com"),
            Subject = "Resend Code Active Account for Login",
            PlainTextContent = user.Code
        };
        msg.AddTo(new EmailAddress(user.Email));
        client.SendEmailAsync(msg);

        return Ok(new {
            data = new { email = user.Email}
        });
    }

    [HttpPut]
    [Route("change_password")]
    public IActionResult ChangePassword(string password, string accessToken){
        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(accessToken);
        var username = jwtSecurityToken.Claims.First(claim => claim.Type == ClaimTypes.Name).Value;

        if (username == null) return BadRequest(new { description = "accessToken is invalid"});

        var user = this._DBContext.Users.FirstOrDefault(o => o.Username == username);
        user.Password = password;
        this._DBContext.SaveChanges();

        return Ok();
    }
}

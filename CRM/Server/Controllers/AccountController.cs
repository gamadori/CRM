using Azure;
using CRM.Client.Shared.Components;
using CRM.Server.Data;
using CRM.Shared;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
    
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly SignInManager<ApplicationUser> _signInManager;

        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
        }


        // GET api/<AccountController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<AccountController>

        //public async Task<LoginModel> Login(LoginModel request)
        //{

        //    //var user = _userManager.Users.FirstOrDefault(x=>x.UserName == request.UserName);


        //    //bool isValid = await _userManager.CheckPasswordAsync(user, request.Password);


        //    //if (user == null || isValid == false)
        //    //{
        //    //    return new LoginModel()
        //    //    {
        //    //        Token = "",
        //    //        UserName = null
        //    //    };
        //    //}

        //    ////if user was found generate JWT Token
        //    //var roles = await _userManager.GetRolesAsync(user);
        //    //var tokenHandler = new JwtSecurityTokenHandler();
        //    //var key = Encoding.ASCII.GetBytes(secretKey);

        //    //var tokenDescriptor = new SecurityTokenDescriptor
        //    //{
        //    //    Subject = new ClaimsIdentity(new Claim[]
        //    //    {
        //    //        new Claim(ClaimTypes.Name, user.UserName.ToString()),
        //    //        new Claim(ClaimTypes.Role, roles.FirstOrDefault())
        //    //    }),
        //    //    Expires = DateTime.UtcNow.AddDays(7),
        //    //    SigningCredentials = new(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        //    //};

        //    //var token = tokenHandler.CreateToken(tokenDescriptor);
        //    //LoginModel model = new LoginModel()
        //    //{
        //    //    Token = tokenHandler.WriteToken(token),
        //    //    UserName = user.UserName

        //    //};
        //    //return model;
        //    return new LoginModel();
        //}

        // PUT api/<AccountController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<AccountController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}

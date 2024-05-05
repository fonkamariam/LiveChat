using Microsoft.AspNetCore.Mvc;
using LiveChat.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic.CompilerServices;
using Supabase;
using Supabase.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace LiveChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class UsersController : ControllerBase

    {
        private readonly IConfiguration _configuration;
        private readonly Supabase.Client _supabaseClient;

        public UsersController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VertifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computeHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return computeHash.SequenceEqual(passwordHash);
            }
        }

        private bool SendEmail(string ToEmail, int Code)
        {
            var secretsConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
                .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
                .Build();
            string fromEmail = secretsConfig["Email:EmailAccount"];
            string password = secretsConfig["Email:EmailPassword"];

            MailMessage message = new MailMessage();

            message.From = new MailAddress(fromEmail);
            message.Subject = "Vertification Code";
            message.To.Add(new MailAddress(ToEmail));
            message.Body = $"Your Verticfication Code:{Code}.";


            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true,
            };
            try
            {
                smtpClient.Send(message);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string CreateToken(string emailPara)
        {
            var secretsConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
                .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
                .Build();
            var claims = new[]
            {
                new Claim("Email",emailPara)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig["AppSettings:Token"]));

            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,

                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: cred
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        private Refresh_Token GenerateRefreshToken()
        {
            var refreshToken = new Refresh_Token
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                Expires = DateTime.Now.AddDays(1),
                Created = DateTime.UtcNow
            };
            return refreshToken;
        }
        
        [HttpPost("refreshToken/{id}")]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] Refresh_Token refreshToken, long id)
        {
            // validating Refresh token with the id
            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.Id == id).Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Invalid Id");
                    }

                    if (hey.Refresh_Token != refreshToken.Token)
                    {
                        return Unauthorized("Invalid Refresh Token");
                    }
                    else if (hey.Token_Expiry < DateTime.UtcNow)
                    {
                        return Unauthorized("your Refresh token has expired, sign in again");
                    }

                    string newToken = CreateToken(hey.Email);

                    return Ok(newToken);

                }
                catch (Exception)
                {
                    return BadRequest("Invalid ID");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }


        }


        [HttpPost("login")]
        public async Task<IActionResult> Loginn([FromBody] UserLogin person)
        {
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == person.Email && n.Deleted == false).Get();

                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return StatusCode(10,"Phone Number Invalid");
                    }

                    if (!VertifyPasswordHash(person.Password, hey.PasswordHash, hey.PasswordSalt))
                    {
                        return StatusCode(20,"Wrong Password");
                    }

                    string token = CreateToken(hey.Email);
                    var refreshToken = GenerateRefreshToken();
                   

                    var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == person.Email)
                            .Single();
                    responseUpdate.Refresh_Token = refreshToken.Token;
                    responseUpdate.Token_Created = refreshToken.Created;
                    responseUpdate.Token_Expiry = refreshToken.Expires;
                    await responseUpdate.Update<Userdto>();


                var result = new
                    {
                        Id = hey.Id,
                        Token = token,
                        Refresh_Token = refreshToken
                    };

                    return Ok(result);
               
            }
            catch (Exception)
            {
                return StatusCode(30,"No Connection, Please Try again");
            }
        }

        // Post 
        [HttpPost("registerOne")]
        public async Task<IActionResult> RegisterPartOne([FromBody] string emailPara)
        {
            // First Let's check if the Email is Available in the database
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(a => a.Email == emailPara && a.Deleted == false)
                    .Get();
                var hey = response.Models.Count;
                if (hey == 1)
                {
                    return StatusCode(10,"Email Invalid,Email already in use");
                }
               
                Random random = new Random(); 
                int code = random.Next(10000, 100000);

                // send email 
                bool x = SendEmail(emailPara, code);
                if (x == false)
                {
                    Console.WriteLine("Invaild Email");
                    return StatusCode(20,"Couldn't Send email,Try again");
                }
                RegisterVertifyDto registerVertify = new RegisterVertifyDto
                        {
                            Email = emailPara,
                            VertficationNo = code,
                            VReg_Expiry = DateTime.UtcNow.AddMinutes(30) 
                        };
                var responseReg = await _supabaseClient.From<RegisterVertifyDto>()
                    .Where(a => a.Email == emailPara)
                    .Get();
                var rg = responseReg.Models.Count();
                Console.WriteLine(rg);
                if (rg == 1)
                {
                    var sin = await _supabaseClient.From<RegisterVertifyDto>()
                        .Where(a => a.Email == emailPara)
                        .Single();
                    sin.VertficationNo = code;
                    sin.VReg_Expiry=DateTime.UtcNow.AddMinutes(30);
                    await sin.Update<RegisterVertifyDto>();
                    
                    return Ok("Updated");

                }

                    
                var responseUpdate = await _supabaseClient.From<RegisterVertifyDto>().Insert(registerVertify);
                
                return Ok("Sent");

            }
            catch (Exception)
            {
                return StatusCode(50, "Connection Problem");
            }
        }

        [HttpPost("registerTwo")]
        public async Task<IActionResult> RegisterPartTwo([FromBody] User person)
        { 
            try
            {
                var verifyNo = await _supabaseClient.From<RegisterVertifyDto>()
                    .Where(n => n.Email == person.Email && n.VertficationNo == person.VertificationNo)
                    .Get();
                if (verifyNo.Models.Count == 0)
                {
                    return StatusCode(10,"Incorrect Verification Number");
                }

                var checkVerify = verifyNo.Models.First();
                if (checkVerify.VReg_Expiry < DateTime.UtcNow)
                {
                    
                    return StatusCode(20,"Verification Number Expired,Try again");
                }
                
                // Register the User now 

                CreatePasswordHash(person.Password, out byte[] passwordHash, out byte[] passwordSalt);
                
                var refreshToken = GenerateRefreshToken();
                var userdto = new Userdto   
                {
                    Email = person.Email,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Refresh_Token = refreshToken.Token,
                    Token_Expiry = refreshToken.Expires,
                    Token_Created = refreshToken.Created
                };
                
                var final=await _supabaseClient.From<Userdto>().Insert(userdto);
                
                var final1 = final.Models.First();

                string token = CreateToken(userdto.Email); 
                var result = new
                        {
                            Id=final1.Id,
                            Token = token,
                            RefreshToken = final1.Refresh_Token
                        };
                await _supabaseClient.From<RegisterVertifyDto>()
                    .Where(a => a.Email == person.Email)
                    .Delete();
                
                return Ok(result);
                
            }
            catch (Exception)
            {
                return StatusCode(30,"Can't connect to server");
            }
        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody]string email)
        {

            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.Email == email).Get();

                if (response.Models.Count == 0)
                {
                    return StatusCode(10, "Invalid Email, No Email found");
                }


                //Generate 5 code number
                Random random = new Random(); 
                int code = random.Next(10000, 100000);
                    // send email
                    
                bool x = SendEmail(email, code);
                if (x == false)
                {
                    return StatusCode(20,"Couldn't Send email,Try again");
                }

                // update vertification Database
                var responseUpdate = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email)
                    .Single();
                responseUpdate.V_Number_Value = code; 
                responseUpdate.V_Number_Created_At = DateTime.UtcNow;
                responseUpdate.V_Number_Expiry = DateTime.UtcNow.AddMinutes(10);
                await responseUpdate.Update<Userdto>();
                
                return Ok("Vertification Sent");
                
            }
            catch (Exception)
            {
                return StatusCode(30,"No Connection, Please Try again");
            }
        }

        [HttpPost("UpdatePasswordThroughCode")]
        public async Task<IActionResult> UpdatePasswordThroughCode( [FromBody] NewPassword newPassword)
        {
            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.Email == newPassword.Email).Get();

                if (response.Models.Count == 0)
                {
                    return StatusCode(10, "Invalid Email, No Email found");
                }

                var hey = response.Models.First();
                // update Vertification Database
                if (hey.V_Number_Value == newPassword.V_code && hey.V_Number_Expiry > DateTime.UtcNow)
                    {
                        CreatePasswordHash(newPassword.Password, out byte[] passwordHash, out byte[] passwordSalt);

                    var responseUpdate = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Email == newPassword.Email)
                        .Single();
                    responseUpdate.PasswordHash = passwordHash;
                    responseUpdate.PasswordSalt = passwordSalt;
                    responseUpdate.V_Number_Expiry =DateTime.UtcNow;
                    responseUpdate.V_Number_Value = 0;
                    responseUpdate.V_Number_Created_At = DateTime.UtcNow;
                    await responseUpdate.Update<Userdto>();
                    return Ok("Succssefully Updated!");
                        
                    }

                return StatusCode(20,"Invalid , either code exipred or lying!");

               
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        // Change Password after logged in
        [HttpPost("ChangePassword"), Authorize]
        public async Task<IActionResult> ChangePassword(ChangePassword changePassword)
        {
            var EmailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (EmailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == EmailClaim.ToString() && n.Deleted==false)
                    .Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Token Invalid");
                    }

                    // update Password
                    try
                    {
                        if (EmailClaim.ToString() != changePassword.OldPassword)
                        {
                            return BadRequest("Old password Invalid");
                        }

                        CreatePasswordHash(changePassword.NewPassword, out byte[] passwordHash,
                            out byte[] passwordSalt);


                        var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == EmailClaim.ToString())
                            .Set(u => u.PasswordHash, passwordHash)
                            .Set(u => u.PasswordSalt, passwordSalt)
                            .Update();
                        return Ok("Succssefully Updated Password!");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for updating the Password process");
                    }
                }
                catch (Exception)
                {
                    return BadRequest("Problem when querying the database");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpPost("ChangeEmail"), Authorize]
        public async Task<IActionResult> ChangePassword(ChangeEmail changeEmail)
        {
            var EmailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (EmailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == EmailClaim.ToString() && n.Deleted == false)
                    .Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Phone Number Taken");
                    }

                    // update Password
                    try
                    {
                        var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == EmailClaim.ToString())
                            .Set(u => u.Email, changeEmail.NewEmail)
                            .Update();
                        return Ok("Succssefully Updated Phone Number!");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for updating the Password process");
                    }
                }
                catch (Exception)
                {
                    return BadRequest("Problem when querying the database");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpPost("DeleteAccount"), Authorize]
        public async Task<IActionResult> DeleteAccount()
        {
            var EmailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (EmailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == EmailClaim.ToString() && n.Deleted == false)
                    .Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Invalid User");
                    }

                    // update Password
                    try
                    {
                        var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == EmailClaim.ToString())
                            .Set(u => u.Deleted, true)
                            .Update();
                        var deletedUser = responseUpdate.Models.First();

                        if (deletedUser == null)
                        {
                            return BadRequest("Problem when deleting the user");
                        }
                        
                        // Trigger
                        //UserProfile Table gets deleted
                        var userProfileTable = await _supabaseClient.From<UserProfiledto>()
                            .Where(n => n.Id == deletedUser.Id)
                            .Set(n=>n.Deleted,true)
                            .Update();
                        // Contact 


                        return Ok("Succssefully Deleted Account!");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for Deletion");
                    }
                }
                catch (Exception)
                {
                    return BadRequest("Problem when querying the database");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpGet("SearchUser"), Authorize]
        public async Task<IActionResult> SearchUser(Query query)
        {
            var EmailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (EmailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient
                    .From<UserProfiledto>()
                    .Where(n => (n.UserName.Contains(query.SerachQuery))&& n.Deleted==false)
                    .Get();

                try
                {
                    Array hey = response.Models.ToArray();

                    if (hey == null)
                    {
                        return BadRequest("No Search Results");
                    }

                    // update Password
                    return Ok(hey);

                }
                catch (Exception)
                {
                    return BadRequest("Problem when querying the database");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }

        }

        
    }
}
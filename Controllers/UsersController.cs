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
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

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
            try
            {
                Console.WriteLine("Starting to send email...");

                string fromEmail = "fonkagram@outlook.com"; // Replace with your Outlook email
                string password = "NandToTetris2023"; // Replace with your Outlook email password
                

                MailMessage message = new MailMessage();
                message.From = new MailAddress(fromEmail);
                message.Subject = "Fonkagram: Verification Code";
                message.To.Add(new MailAddress(ToEmail));
                message.Body = $"Your Verticfication Code:{Code}.";

                SmtpClient smtpClient = new SmtpClient("smtp.office365.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(fromEmail, password),
                    EnableSsl = true,
                };

                smtpClient.Send(message);

                Console.WriteLine("Message Sent");
                return true;
            }
            catch (SmtpException ex)
            {
                Console.WriteLine($"SMTP Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    return false;
                }
                Console.WriteLine("Please check your network connection, firewall settings, and email server settings.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error: {ex.Message}");
                return false;
            }

        }

        private string CreateToken(string emailPara,long idPara)
        {
            var secretsConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
                .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
                .Build();
            var claims = new[]
            {
                new Claim("Email",emailPara),
                new Claim("UserId",idPara.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig["AppSettings:Token"]));

            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,

                expires: DateTime.Now.AddMinutes(120),
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
                Expires = DateTime.Now.AddHours(3),
                Created = DateTime.UtcNow
            };
            return refreshToken;
        }
        
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] UserRefreshToken refreshToken)
        {
            
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                   .Where(n => n.Id == refreshToken.Id && n.Deleted == false)
                   .Get();


                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return StatusCode(10, "Invalid Id");
                }


                if (hey.Refresh_Token != refreshToken.Token)
                {
                    return StatusCode(20,"Invalid Refresh Token");
                }
                if (hey.Token_Expiry < DateTime.UtcNow)
                {
                    return StatusCode(30,"your Refresh token has expired, sign in again");
                }

                string newToken = CreateToken(hey.Email,hey.Id);

                var result = new
                {
                    Token = newToken
                };


                return Ok(result);
                

                
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
                        return BadRequest("Email Invalid");
                    }

                if (!VertifyPasswordHash(person.Password, hey.PasswordHash, hey.PasswordSalt))
                    {
                        return Unauthorized("Wrong Password");
                    }

                    string token = CreateToken(hey.Email,hey.Id);
                    var refreshToken = GenerateRefreshToken();
                var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == person.Email)
                            .Single();
                responseUpdate.Refresh_Token = refreshToken.Token;
                responseUpdate.Token_Created = refreshToken.Created;
                responseUpdate.MessagePayload = null;
                responseUpdate.ConvPayload = null;
                responseUpdate.UserPayload = null;
                responseUpdate.Token_Expiry = refreshToken.Expires;
                responseUpdate.Status = "true";
                responseUpdate.LastSeen = DateTime.UtcNow;
                responseUpdate.OnlinePayload = null;
                await responseUpdate.Update<Userdto>();
                var getProfile = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserId == hey.Id && n.Deleted==false)
                    .Get();
                if (getProfile == null) { return BadRequest("Invaild UserName"); }
                var hereProfile = getProfile.Models.FirstOrDefault();
                
                List<string> allProfilePic = hereProfile.ProfilePic != null
                    ? JsonConvert.DeserializeObject<List<string>>(hereProfile.ProfilePic)
                    : null;

                //allProfilePic.Reverse();
                //List<string> reversedProfilePic = allProfilePic.AsEnumerable().Reverse().ToList();
                if (allProfilePic != null)
                {
                    allProfilePic.Reverse();
                   // Console.WriteLine("REversed HHHH");
                }
                var result = new
                    {
                        Id = hey.Id,
                        Token = token,
                        RefreshToken = refreshToken.Token,
                        RefreshTokenExpiry = refreshToken.Expires,
                        Name = hereProfile.Name,
                        LastName=hereProfile.LastName,
                        Bio = hereProfile.Bio,
                        Dark = hey.Dark,
                        ProfilePicture = allProfilePic

                };
                


                return Ok(result);
               
            }
            catch (Exception ex)
            {
                return StatusCode(500,"No Connection, Please Try again");
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
                    return BadRequest("Email Invalid,Email already in use");
                }
               
                Random random = new Random(); 
                int code = random.Next(10000, 100000);

                // send email 
                bool x = SendEmail(emailPara, code);
                if (x == false)
                {
                    return Unauthorized("Couldn't Send email,Try again");
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
                return StatusCode(500, "Connection Problem");
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
                    return BadRequest("Incorrect Verification Number");
                }

                var checkVerify = verifyNo.Models.First();
                if (checkVerify.VReg_Expiry < DateTime.UtcNow)
                {
                    
                    return Unauthorized("Verification Number Expired,Try again");
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
                    Token_Created = refreshToken.Created,
                    Status = "true",
                    LastSeen = DateTime.UtcNow
                };
                


                var final =await _supabaseClient.From<Userdto>().Insert(userdto);
                
                var final1 = final.Models.First();

                string token = CreateToken(userdto.Email,final1.Id); 
                var result = new
                        {
                            Id=final1.Id,
                            Token = token,
                            RefreshToken = refreshToken.Token,
                            RefreshTokenExpiry= refreshToken.Expires,
                            Name = person.Name,
                            LastName = "",
                            Bio = "",
                            Dark = false,
                            ProfilePicture = ""

                };
                
                await _supabaseClient.From<RegisterVertifyDto>()
                    .Where(a => a.Email == person.Email)
                    .Delete();
                
                return Ok(result);
                
            }
            catch (Exception)
            {
                return StatusCode(500,"Can't connect to server");
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
                    return BadRequest("Invalid Email, No Email found");
                }


                //Generate 5 code number
                Random random = new Random(); 
                int code = random.Next(10000, 100000);
                    // send email
                    
                bool x = SendEmail(email, code);
                if (x == false)
                {
                    return Unauthorized("Couldn't Send email,Try again");
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
                return StatusCode(500,"No Connection, Please Try again");
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
                    return BadRequest("Invalid Email, No Email found");
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

                return Unauthorized("Invalid , either code exipred or lying!");

               
            }
            catch (Exception)
            {
                return StatusCode(500,"No Connection, Please Try again");
            }
        }

        // Change Password after logged in
        
        [HttpPost("ChangePassword"), Authorize]
        public async Task<IActionResult> ChangePassword( [FromBody] ChangePassword changePassword)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return Unauthorized("Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();


                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return StatusCode(10, "Invalid Token");
                }
                
                if (!VertifyPasswordHash(changePassword.OldPassword, hey.PasswordHash, hey.PasswordSalt))
                {
                    
                    return StatusCode(20, "Wrong Old Password");
                }
                
                CreatePasswordHash(changePassword.NewPassword, out byte[] passwordHash,out byte[] passwordSalt);


                var responseUpdate = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Email == email)
                 .Single();
                responseUpdate.PasswordHash = passwordHash;
                responseUpdate.PasswordSalt = passwordSalt;
 
                await responseUpdate.Update<Userdto>();
                return Ok("Succssefully Updated Password!");


            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpPost("ChangeEmail"), Authorize]
        public async Task<IActionResult> ChangePassword( [FromBody] ChangeEmail changeEmail)
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


        [HttpDelete("DeleteAccount"), Authorize]
        public async Task<IActionResult> DeleteAccount()
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return Unauthorized("Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try 
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();


                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return Unauthorized("Invalid Token");
                }

                var responseUpdate = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == hey.Id && n.Deleted == false)
                 .Single();
                
                responseUpdate.Email = responseUpdate.Email + "@" + responseUpdate.Id;
                
                responseUpdate.Deleted = true;
                responseUpdate.Status = false;
                

                await responseUpdate.Update<Userdto>();
                // Delete UserProfile
                var responseUpdateProfile = await _supabaseClient.From<UserProfiledto>()
                 .Where(n => n.UserId == hey.Id && n.Deleted == false)
                 .Single();
                responseUpdateProfile.Deleted = true;
                

                await responseUpdateProfile.Update<UserProfiledto>();

                return Ok("Succssefully Deleted User!");


            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }


        [HttpGet("SearchUser"), Authorize]
        public async Task<IActionResult> SearchUser([FromQuery] string query)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return Unauthorized("Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();


                var hey = response.Models.FirstOrDefault();
                if (hey == null)
                {
                    return Unauthorized("Invalid Token");
                }
                
                List<SearchEmail> allyouNeed = new List<SearchEmail>();

                if (query == "." || query == "@")
                {
                    return Ok(allyouNeed);
                }
                
                var emailPrefix = $"%{query}%";
                var queryResponse = await _supabaseClient
                    .From<Userdto>()
                    .Where(n=> n.Deleted == false)
                    .Filter("Email",Postgrest.Constants.Operator.ILike,emailPrefix)
                    .Get();
                

                var searchResult = queryResponse.Models.ToList();
                
                foreach (var user in searchResult)
                {
                    var getProfile = await _supabaseClient.From<UserProfiledto>()
                        .Where(n => n.UserId == user.Id && n.Deleted == false)
                        .Get();
                    var getProfile2 = getProfile.Models.FirstOrDefault();
                    List<string> allProfilePic = getProfile2.ProfilePic != null
                    ? JsonConvert.DeserializeObject<List<string>>(getProfile2.ProfilePic)
                    : null;
                    
                    //allProfilePic.Reverse();
                    //List<string> reversedProfilePic = allProfilePic.AsEnumerable().Reverse().ToList();
                    if (allProfilePic != null)
                    {
                        allProfilePic.Reverse();
                       // Console.WriteLine("REversed HHHH");
                    }

                    
                    SearchEmail xzz = new SearchEmail
                    {
                        Id = user.Id,
                        Name = getProfile2.Name,
                        LastName = getProfile2.LastName,
                        Email = user.Email,
                        ProfilePicSearch= allProfilePic,
                        Status = user.Status,
                        LastSeen = user.LastSeen,
                        Bio = getProfile2.Bio
                        
                    };

                    allyouNeed.Add(xzz);
                }
                return Ok (allyouNeed);

                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500,"No Connection, Please Try again");
            }

        }

        [HttpPut("LightDark"), Authorize]
        public async Task<IActionResult> LightDark([FromQuery] bool value)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return StatusCode(10, "Invalid Token");
                }


                hey.Dark = value;
                await hey.Update<Userdto>();



                return Ok("Apperance Changed successfully");

            }
            catch
            {
                return BadRequest("Connection Problem");
            }
        }

    }
}
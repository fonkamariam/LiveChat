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

        private bool SendEmail(string ToEmail,int Code)
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
        private string CreateToken(long userId)
        {
            var secretsConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
                .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
                .Build();
            var claims = new[]
            {
                new Claim("UserId", userId.ToString())
            };
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig["AppSettings:Token"]));

            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,

                expires: DateTime.Now.AddMinutes(30),
                signingCredentials:cred
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

                    string newToken = CreateToken(id);

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
        public async Task<IActionResult> Loginn(User person)
        {
            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.PhoneNo == person.PhoneNo).Get();
                
                try
                {
                    var hey = response.Models.FirstOrDefault();
                    
                    if (hey == null)
                    {
                        return BadRequest("Phone Number Invalid");
                    }
                    if (!VertifyPasswordHash(person.Password, hey.PasswordHash, hey.PasswordSalt))
                    {
                        return BadRequest("Wrong Password");
                    }
                    string token = CreateToken(hey.Id);
                    var refreshToken = GenerateRefreshToken();
                    try
                    {
                        
                        var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.PhoneNo == person.PhoneNo)
                            .Set(u => u.Refresh_Token, refreshToken.Token)
                            .Set(u => u.Token_Created, refreshToken.Created)
                            .Set(u => u.Token_Expiry, refreshToken.Expires)
                            .Update();
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for updating the refresh token");
                    }

                    
                    var result = new
                    {
                        Id = hey.Id,
                        Token = token,
                        Refresh_Token=refreshToken
                    };

                    return Ok(result);
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
        // Post 
        [HttpPost("register")]
        public async Task<IActionResult> CreateUserAsync ([FromBody] User person)
        {
            CreatePasswordHash(person.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var refreshToken = GenerateRefreshToken();
            var userdto = new Userdto
            {
                PhoneNo = person.PhoneNo,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Refresh_Token = refreshToken.Token,
                Token_Expiry = refreshToken.Expires,
                Token_Created = refreshToken.Created,
                Email = person.Email
                
            };
            
            try
            {
                var response = await _supabaseClient.From<Userdto>().Insert(userdto);
                try
                {
                    var hey = response.Models.First();

                    string token = CreateToken(hey.Id);
                    var result = new
                    {
                        Id = hey.Id,
                        Token = token,
                        RefreshToken=hey.Refresh_Token
                    };

                    return Ok(result);
                }
                catch (Exception)
                {
                    return BadRequest("Invalid phoneNo");
                }
            }
            catch (Exception)
            {
                return BadRequest("Can't connect to server");
            }

        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(Email email)
        {

            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.Email == email.EmailGet).Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Email Invalid");
                    }
                    //Generate 5 code number
                    Random random = new Random();
                    int code = random.Next(10000, 100000);
                    // send email 
                    try
                    {
                        var CheckEmail = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == email.EmailGet).Get();
                    }
                    catch (Exception)
                    {
                        return BadRequest("Invaild Email Address");
                    }
                    
                    bool x = SendEmail(email.EmailGet, code);
                    if (x == false)
                    {
                        return BadRequest("Couldn't Send email,Try again");
                    }
                    // update vertification Database
                    try
                    {

                        var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Email == email.EmailGet)
                            .Set(u => u.V_Number_Value, code)
                            .Set(u => u.V_Number_Created_At,DateTime.UtcNow )
                            .Set(u => u.V_Number_Expiry, DateTime.Now.AddMinutes(10))
                            .Update();

                        return Ok("Vertification Sent");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for updating the Vertification process");
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

        [HttpPost("UpdatePasswordThroughCode")]
        public async Task<IActionResult> UpdatePasswordThroughCode(NewPassword newPassword)
        {

            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n => n.Email == newPassword.Email).Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Email Invalid");
                    }
                    // update Vertification Database
                    if (hey.V_Number_Value != 0 && hey.V_Number_Expiry > DateTime.UtcNow)
                    {
                        CreatePasswordHash(newPassword.Password, out byte[] passwordHash, out byte[] passwordSalt);

                        try
                        {

                            var responseUpdate = await _supabaseClient.From<Userdto>()
                                .Where(n => n.Email == newPassword.Email)
                                .Set(u => u.PasswordHash, passwordHash)
                                .Set(u => u.PasswordSalt, passwordSalt)
                                .Set(u => u.V_Number_Expiry, DateTime.UtcNow)
                                .Set(u => u.V_Number_Value, 0)
                                .Set(u => u.V_Number_Created_At, DateTime.UtcNow)
                                .Update();

                            return Ok("Succssefully Updated!");
                        }
                        catch (Exception)
                        {
                            return BadRequest("No Connection for updating the Password process");
                        }
                    }

                    return BadRequest("Invalid , either code exipred or lying!");

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







        [HttpGet]
        public async Task<IActionResult> GetUser()
        {
           
                try
                {
                    var response = await _supabaseClient.From<Userdto>().Get();
                    try
                    {
                        var hey = response.Models.Count;
                        return Ok(hey);
                    }
                    catch (Exception)
                    {
                        return BadRequest(response);
                    }
                }
                catch (Exception)
                {
                    return BadRequest("First: No Response");
                }
               
               

               
           
        }
        //Get by id
        [HttpGet("{id}"),Authorize]
        public async Task<IActionResult> GetUser(long id)
        {

            try
            {
                var response = await _supabaseClient.From<Userdto>().Where(n=> n.Id == id).Get();
                Console.WriteLine("response found");
                try
                {
                    var hey = response.Models.FirstOrDefault();
                    if (hey == null)
                    {
                        return BadRequest("hey empty so, Invalid Id");
                    }

                    Console.WriteLine("hey found");

                    var x = new User
                    {
                        
                        PhoneNo = hey.PhoneNo,
                        Password = hey.PasswordHash.ToString()
                        
                    };
                    Console.WriteLine("x formed");
                    return Ok(x);
                }
                catch (Exception)
                {
                    Console.WriteLine("No model");
                    
                    return BadRequest(response);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("No Connection");
                return BadRequest("First: No Response");
            }





        }
        /*
        [HttpPost("login")]
        public IActionResult Loginn(User person)
        {

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT passwordHash,passwordSalt FROM Accounts WHERE phoneNo = @phoneNo";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@phoneNo", person.phoneNo);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            byte[] hashBytes = (byte[])reader[0];
                            byte[] saltBytes = (byte[])reader[1];
                            if (!VertifyPasswordHash(person.password, hashBytes, saltBytes))
                            {
                                return BadRequest("Wrong Password");
                            }

                            string token= CreateToken(person);
                            return Ok(token);

                        }
                        else
                        {
                            return BadRequest("Invalid Phone Number");
                        }
                    }
                }

            }


        }


        //Get all
        [HttpGet, Authorize(Roles = "Admin")]
        public IActionResult GetallUser()
        {
            var customClaimValue = User.FindFirstValue("phoneNo");
            if (customClaimValue == "0")
            {
               return Ok("i am just a demo");
            }

            if (customClaimValue == "977")
            {
                return Ok("I am fonka");
            }

            List<int> AllPersons = new List<int>();
            Console.WriteLine("GEt all");
            using (var connection = new SqlConnection(_connectionString))
            {


                connection.Open();
                string sql = "SELECT phoneNo FROM Accounts";
                using (var command = new SqlCommand(sql, connection))
                {
                    using(var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Userdto persondto = new Userdto();

                            persondto.phoneNo = reader.GetInt32(0);


                            AllPersons.Add(persondto.phoneNo);
                        }

                    }


                }
            }


            return Ok(AllPersons);
        }


        // Get by id
        [HttpGet("{id}")]
        public IActionResult GetUserById(int id)
        {
            Persondto persondto= new Persondto();
            using (var connection = new SqlConnection(connectionString))
            {


                connection.Open();
                string sql = "SELECT * FROM users WHERE id =@id";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            persondto.id=reader.GetInt32(0);
                            persondto.name=reader.GetString(1);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }

                }
            }


            return Ok(persondto);

        }


        //Update by id
        [HttpPut("{id}")]
        public IActionResult Update(int id,[FromBody] Person person)
        {

            Persondto persondto = new Persondto();
            using (var connection = new SqlConnection(connectionString))
            {


                connection.Open();
                string sql = "SELECT name FROM users WHERE id =@id";

                using (var command = new SqlCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            persondto.name = reader.GetString(0);
                    command.Parameters.AddWithValue("@id", id);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }

                }
                var oldname = persondto.name;


                string sql1 = "UPDATE users SET name = @Newname WHERE id = @id";


                using (var command = new SqlCommand(sql1, connection))
                {


                    command.Parameters.AddWithValue("@Newname", person.name);
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();

                }
                return Ok($"{oldname}->{person.name}");


            }
    }

        //Delete by id

        [HttpDelete("{id}")]
        public IActionResult DeleteUserById(int id)
        {
            Persondto persondto = new Persondto();
            using (var connection = new SqlConnection(connectionString))
            {


                connection.Open();
                string sql = "DELETE FROM users WHERE id =@id";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    int rowAffected= command.ExecuteNonQuery();
                    if (rowAffected > 0)
                    {
                        return Ok("Successfully Deleted");
                    }
                    else
                    {
                        return BadRequest();
                    }

                }
            }

        }
        */

    }
}
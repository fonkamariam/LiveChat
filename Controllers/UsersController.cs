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

        private string CreateToken(User person)
        {
            var secretsConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base path where secrets.json is located
                .AddJsonFile("Secret/secret.json", optional: true, reloadOnChange: true)
                .Build();
            var claims = new[]
            {
                new Claim("phoneNo", person.PhoneNo)
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

        [HttpPost("RefershToken")]
        public IActionResult RefreshToken([FromBody] User person)
        {
            
            // Generate new JWT token
            var newToken = CreateToken(person);

            return Ok(newToken);
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
                    var x = new UserCustomModel
                    {
                        Id = hey.Id,
                        PhoneNo = hey.PhoneNo,
                        PasswordHash = hey.PasswordHash,
                        PasswordSalt = hey.PasswordSalt,
                        Online = hey.Online,
                        Deleted = hey.Deleted
                    };


                    if (!VertifyPasswordHash(person.Password, x.PasswordHash, x.PasswordSalt))
                    {
                        return BadRequest("Wrong Password");
                    }

                    string token = CreateToken(person);
                    
                    return Ok(token);
                }
                catch (Exception)
                {
                    return BadRequest("No hey");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }
        // Post 
        [HttpPost]
        public async Task<IActionResult> CreateUserAsync ([FromBody] User person)
        {

            CreatePasswordHash(person.Password, out byte[] passwordHash, out byte[] passwordSalt);
            
            var userdto = new Userdto
            {
                PhoneNo = person.PhoneNo,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt
            };
            
            try
            {
                var response = await _supabaseClient.From<Userdto>().Insert(userdto);
                try
                {
                    var hey = response.Models.First();
                    return Ok("Successfully " + hey.Id +" inserted!");
                }
                catch (Exception)
                {
                    return BadRequest("Inserted but not giving back the inserted id");
                }
            }
            catch (Exception)
            {
                return BadRequest("Can't connect to server");
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
        [HttpGet("{id}")]
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
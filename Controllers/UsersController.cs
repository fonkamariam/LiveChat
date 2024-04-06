using Microsoft.AspNetCore.Mvc;
using LiveChat.Models;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic.CompilerServices;

namespace LiveChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class UsersController : ControllerBase

    {
        private readonly string _connectionString;

        public UsersController(IConfiguration configuration)
        {

            _connectionString = configuration["ConnectionStrings:Mydatabase"] ?? "";
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

        private string CreateToken(Person person)
        {
            string x = (person.phoneNo).ToString();
            string Role = "Default";

            if (person.phoneNo == 977)
            {
                Role = "Admin";
            }
            
            var claims = new[]
            {
                new Claim("phoneNo", x),
                new Claim(ClaimTypes.Role, Role)
            };
            
           
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("Planning outdoor recreational activities can be a fun and rewarding experience. Here are some steps to help you plan and organize outdoor recreational activities:\r\n1. Determine the Purpose: - Start by defining the purpose of the outdoor recreational activity. Is it for relaxation, exercise, team-building, or skill-building? Understanding the purpose will help you choose the right activities.\r\n2. Choose Suitable Activities: - Consider the preferences and abilities of the participants when selecting outdoor activities. Some popular outdoor recreational activities include hiking, camping, biking, kayaking, fishing, bird watching, and picnicking.\r\n3. Select a Location:- Choose a suitable location for the outdoor activity based on factors such as distance, accessibility, facilities, scenery, and safety. National parks, state parks, beaches, forests, and nature reserves are great options for outdoor activities"));

            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,

                expires: DateTime.Now.AddMinutes(1),
                signingCredentials:cred
                );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        [HttpPost("RefershToken")]
        public IActionResult RefreshToken([FromBody] Person person)
        {
            
            // Generate new JWT token
            var newToken = CreateToken(person);

            return Ok(newToken);
        }

        // Post 
        [HttpPost]
        public IActionResult CreateUser ( Person person)
        {

            CreatePasswordHash(person.password, out byte[] passwordHash, out byte[] passwordSalt);
           
            Console.WriteLine("Post");
                using (var connection = new SqlConnection(_connectionString)) 
                {


                    connection.Open();
                    string sql = "INSERT INTO Accounts " +
                        "(phoneNo,passwordHash,passwordSalt) VALUES" +
                        "(@phoneNo,@passwordHash,@passwordSalt)";
                    using (var command = new SqlCommand(sql, connection))
                    {
                    
                    command.Parameters.AddWithValue("@phoneNo", person.phoneNo);
                    command.Parameters.AddWithValue("@passwordHash", passwordHash);
                    command.Parameters.AddWithValue("@passwordSalt", passwordSalt);

                    command.ExecuteNonQuery();  
                    }
                }
           
           
            return Ok(person);
        }

        [HttpPost("login")]
        public IActionResult Loginn(Person person)
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
                            Persondto persondto = new Persondto();
                            
                            persondto.phoneNo = reader.GetInt32(0);
                         

                            AllPersons.Add(persondto.phoneNo);
                        }

                    }
                    
 
                }
            }


            return Ok(AllPersons);
        }
        
        /*
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
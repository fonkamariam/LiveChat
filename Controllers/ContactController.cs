using LiveChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase.Interfaces;
using Supabase;

namespace LiveChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public ContactController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        [HttpPost("AddContact"), Authorize]
        public async Task<IActionResult> AddContact_Email([FromBody] string emailPara)
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
                
                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == emailPara && n.Deleted == false)
                    .Get();
                    
                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey2 == null)
                    {
                        return NotFound("the email is invalid or It doesn't have a fonkagram account");
                    }
                    if(hey.Id == hey2.Id)
                {
                    return BadRequest("You can't add yourself as a contact");
                }
                    var response1 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey.Id && n.ContacteeId==hey2.Id)
                        .Get();

                    // Check if the user profile exists
                    if (response1.Models.Count != 0)
                    {
                        return NotFound("Already in Contact"); 
                    }
                    var response2 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey2.Id && n.ContacteeId == hey.Id)
                        .Get();
                    bool y = false;
                    if (response2.Models.Count!=0)
                    {
                        var HeyCheck = response2.Models.FirstOrDefault();

                        if (HeyCheck.Block == true)
                        {
                            y = true;
                            
                        }
                    }
                    

                    var contactDto = new ContactDto
                    {
                        ContacterId = hey.Id,
                        ContacteeId = hey2.Id,
                        Blocked = y,
                        Block = false,
                        created_at = DateTime.UtcNow
                    };

                    
                        var responseInserted = await _supabaseClient.From<ContactDto>().Insert(contactDto);

                        return Ok("Contact Added");
                    
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpDelete("RemoveContact"), Authorize] 
        public async Task<IActionResult> RemoveContact_Email([FromBody] string emailPara)
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

                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == emailPara && n.Deleted == false)
                    .Get();

                var hey2 = Contactee.Models.FirstOrDefault();

                if (hey2 == null)
                {
                    return NotFound("the email is invalid or It doesn't have a fonkagram account");
                }
                var response12 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey.Id && n.ContacteeId == hey2.Id)
                        .Get();

                // Check if the user profile exists
                if (response12.Models.Count == 0)
                {
                    return NotFound("Not in Contact");
                }

                await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey.Id && n.ContacteeId == hey2.Id)
                        .Delete();

                    return Ok("Deleted");
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }


        }

        [HttpGet("GetContacts"), Authorize]
        public async Task<IActionResult> GetContacts()
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
                Console.WriteLine("logged in");

                var responseGet = await _supabaseClient.From<ContactDto>()
                        .Where(n => n.ContacterId == hey.Id)
                        .Get();

                    var heyArray = responseGet.Models.ToList();
                
                List<GetContact> allyouNeed = new List<GetContact>();

                foreach (var user in heyArray)
                {
                    var getProfile = await _supabaseClient.From<UserProfiledto>()
                        .Where(n => n.UserId == user.ContacteeId)
                        .Get();

                    var getProfile2 = getProfile.Models.FirstOrDefault();
                    GetContact xzz = new GetContact
                    {
                        Id = user.ContacteeId,
                        Name = getProfile2.Name,
                        LastName = getProfile2.LastName,
                        Bio = getProfile2.Bio

                    };

                    allyouNeed.Add(xzz);
                }    


                return Ok(allyouNeed);
                
        }
        catch (Exception)
        {
            return BadRequest("No Connection, Please Try again");
        }
        }

        [HttpGet("GetBlockedContacts"), Authorize]
        public async Task<IActionResult> GetBlockedContacts()
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


                var responseGet = await _supabaseClient.From<ContactDto>()
                            .Where(n => n.ContacterId == hey.Id && n.Block ==true)
                            .Get();

                        var heyArray = responseGet.Models.ToList();

                        return Ok(heyArray);
                    
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }
        [HttpPut("BlockContact"), Authorize]
        public async Task<IActionResult> BlockContact([FromBody] long idPara)
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
                if (hey.Id == idPara)
                {
                    return BadRequest("YOu can't block yourself");
                }

                var Contactee = await _supabaseClient.From<ContactDto>()
                    .Where(n => n.ContacterId == hey.Id && n.ContacteeId == idPara)
                    .Get();

                                  
                                   
                    if (Contactee.Models.Count == 0)
                    {
                        return BadRequest("No such Contact in your Contact list with the provided id");
                    }

                var firstBlock = await _supabaseClient
                    .From<ContactDto>()
                    .Where(n => n.ContacterId == hey.Id && n.ContacteeId == idPara)
                    .Single();
                if (firstBlock.Block == true)
                {
                    return BadRequest("Already Blocked");
                }

                firstBlock.Block = true; 
                await firstBlock.Update<ContactDto>();

                var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey.Id && n.ContacterId == idPara)
                        .Get();
                Console.WriteLine(responseHey.Models.Count);
                Console.WriteLine(idPara);

                if (responseHey.Models.Count==0)
                    {
                        return Ok("Blocked");
                    }
                var secondBLock = await _supabaseClient
                    .From<ContactDto>()
                    .Where(n => n.ContacteeId == hey.Id && n.ContacterId == idPara)
                    .Single();
                
                secondBLock.Blocked = true;

                await secondBLock.Update<ContactDto>();
                        
                return Ok("Blocked");
                
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }
                        
        [HttpPut("UnBlockAcontact"), Authorize]
        public async Task<IActionResult> UnBlockAcontact_email([FromBody] long idPara)
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
                if(hey.Id == idPara)
                {
                    return BadRequest("YOu can't unblock yourself");
                }

                var Contactee = await _supabaseClient.From<ContactDto>()
                    .Where(n => n.ContacterId == hey.Id && n.ContacteeId == idPara)
                    .Get();

                if (Contactee.Models.Count == 0)
                {
                    return BadRequest("No such Contact in your Contact list with the provided id");
                }
                
                var firstBlock = await _supabaseClient
                    .From<ContactDto>()
                    .Where(n => n.ContacterId == hey.Id && n.ContacteeId == idPara)
                    .Single();
                if (firstBlock.Block == false)
                {
                    return BadRequest("Not blokced in the first place");
                }

                firstBlock.Block = false;
                await firstBlock.Update<ContactDto>();

                Console.WriteLine("two");

                var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey.Id && n.ContacterId == idPara)
                        .Get();
                
                if (responseHey.Models.Count == 0)
                    {
                        return Ok("UnBlocked");
                    }
                

                var secondBLock = await _supabaseClient
                    .From<ContactDto>()
                    .Where(n => n.ContacteeId == hey.Id && n.ContacterId == idPara)
                    .Single();
                secondBLock.Blocked = false;

                await secondBLock.Update<ContactDto>();

                

                return Ok("UnBlocked");
                
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }
        }

        [HttpGet("SearchContacts"), Authorize]
        public async Task<IActionResult> SearchUser([FromQuery] string query)
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
                Console.WriteLine("logged in");
                var nameQuery = $"%{query}%";
                // Retrieve user record(s) based on ContacteeId
                var userResponse = await _supabaseClient.From<UserProfiledto>()
                    .Filter("Name",Postgrest.Constants.Operator.ILike,nameQuery)
                    .Get();

                // Extract user names from the userResponse
                var userProfile = userResponse.Models.ToList();
                
                List<SearchContact> allyouNeed = new List<SearchContact>();

                foreach (var user in userProfile)
                {
                    var getProfile = await _supabaseClient.From<ContactDto>()
                        .Where(n=> n.ContacterId == hey.Id && n.ContacteeId == user.UserId)
                        .Get();
                    if (getProfile.Models.Count == 1)
                    {
                        var getEmail = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == user.UserId && n.Deleted == false)
                            .Get();
                        var getEmail1 = getEmail.Models.FirstOrDefault();
                        if (getEmail1 == null)
                        {
                            continue;
                        }

                    
                        SearchContact xzz = new SearchContact
                        {
                            Id = user.UserId,
                            Name = user.Name,
                            LastName = user.LastName,
                            Email = getEmail1.Email

                        };

                        allyouNeed.Add(xzz);
                    }

                    
                }
                return Ok(allyouNeed);


                
            }
            catch (Exception)
            {
                return BadRequest("No Connection, Please Try again");
            }

        }
    }
}
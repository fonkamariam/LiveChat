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

        [HttpPost("AddContact_PhoneNumber"), Authorize]
        public async Task<IActionResult> AddContact_PhoneNumber(string phoneNumber)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumber && n.Deleted == false).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    var response1 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId==hey2.Id)
                        .Get();

                    // Check if the user profile exists
                    if (response1.Models.Count != 0)
                    {
                        return NotFound("Already in Contact");
                    }
                    var response2 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey2.Id && n.ContacteeId == hey1.Id)
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
                        ContacterId = hey1.Id,
                        ContacteeId = hey2.Id,
                        Blocked = y,
                        Block = false,
                        created_at = DateTime.UtcNow
                    };

                    try
                    {
                        var responseInserted = await _supabaseClient.From<ContactDto>().Insert(contactDto);

                        return Ok("Contact Added");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for Inserting Contact");
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

        [HttpPost("AddContact_Username"), Authorize]
        public async Task<IActionResult> AddContact_Username(string UserName)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserName == UserName).Get();
                
                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid User Name");
                    }

                    var response1 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Get();

                    // Check if the user profile exists
                    if (response1.Models.Count != 0)
                    {
                        return NotFound("Already in Contact");
                    }
                    var response2 = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey2.Id && n.ContacteeId == hey1.Id)
                        .Get();
                    bool y = false;
                    if (response2.Models.Count != 0)
                    {
                        var HeyCheck = response2.Models.FirstOrDefault();

                        if (HeyCheck.Block == true)
                        {
                            y = true;
                        }
                    }

                    var contactDto = new ContactDto
                    {
                        ContacterId = hey1.Id,
                        ContacteeId = hey2.Id,
                        Blocked = y,
                        Block = false,
                        created_at = DateTime.UtcNow
                    };

                    try
                    {
                        var responseInserted = await _supabaseClient.From<ContactDto>().Insert(contactDto);

                        return Ok("Contact Added");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for Inserting Contact");
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

        [HttpDelete("RemoveContact_PhoneNumber"), Authorize]
        public async Task<IActionResult> RemoveContact_PhoneNumber(string phoneNumber)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumber).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Delete();

                    return Ok("Deleted");
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

        [HttpDelete("RemoveContact_Username"), Authorize]
        public async Task<IActionResult> RemoveContact_Username(string UserName)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserName == UserName).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid User Name");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Delete();

                    return Ok("Deleted");
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

        [HttpGet("GetContacts"), Authorize]
        public async Task<IActionResult> GetContacts()
        {
        var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
        {
            return BadRequest("Invalid Token");
        }

        try
        {
            var response = await _supabaseClient.From<Userdto>()
                .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

            try
            {
                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return BadRequest("Invalid Token");
                }

                try
                {
                    var responseGet = await _supabaseClient.From<ContactDto>()
                        .Where(n => n.ContacterId == hey.Id)
                        .Select("ContacteeId")
                        .Get();

                    Array heyArray = responseGet.Models.ToArray();

                    return Ok(heyArray);
                }
                catch (Exception)
                {
                    return BadRequest("No Connection for Getting Contacts");
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

        [HttpGet("GetBlockedContacts"), Authorize]
        public async Task<IActionResult> GetBlockedContacts()
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                try
                {
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    try
                    {
                        var responseGet = await _supabaseClient.From<ContactDto>()
                            .Where(n => n.ContacterId == hey.Id && n.Block ==true)
                            .Select("ContacteeId")
                            .Get();

                        Array heyArray = responseGet.Models.ToArray();

                        return Ok(heyArray);
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for Getting Contacts");
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
        [HttpPut("BlockAcontact_phoneNumber"), Authorize]
        public async Task<IActionResult> BlockAcontact_phoneNumber(string phoneNumber)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumber).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Set(u => u.Block, true)
                        .Update();
                    var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Get();
                    if (responseHey.Models.Count==0)
                    {
                        return Ok("Blocked");
                    }
                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Set(u => u.Blocked, true)
                        .Update();

                    return Ok("Blocked");
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

        [HttpPut("BlockAcontact_userName"), Authorize]
        public async Task<IActionResult> BlockAcontact_userName(string UserName)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserName == UserName).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Set(u => u.Block, true)
                        .Update();
                    var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Get();
                    if (responseHey.Models.Count == 0)
                    {
                        return Ok("Blocked");
                    }
                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Set(u => u.Blocked, true)
                        .Update();

                    return Ok("Blocked");
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

        [HttpPut("UnBlockAcontact_userName"), Authorize]
        public async Task<IActionResult> UnBlockAcontact_userName(string UserName)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserName == UserName).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Set(u => u.Block, false)
                        .Update();
                    var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Get();
                    if (responseHey.Models.Count == 0)
                    {
                        return Ok("Blocked");
                    }
                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Set(u => u.Blocked, false)
                        .Update();

                    return Ok("Blocked");
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

        [HttpPut("UnBlockAcontact_phoneNumber"), Authorize]
        public async Task<IActionResult> UnBlockAcontact_phoneNumber(string phoneNumber)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var Contacter = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var Contactee = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumber).Get();

                try
                {
                    var hey1 = Contacter.Models.FirstOrDefault();

                    var hey2 = Contactee.Models.FirstOrDefault();

                    if (hey1 == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    if (hey2 == null)
                    {
                        return BadRequest("Invalid Phone Number");
                    }

                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacterId == hey1.Id && n.ContacteeId == hey2.Id)
                        .Set(u => u.Block, false)
                        .Update();
                    var responseHey = await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Get();
                    if (responseHey.Models.Count == 0)
                    {
                        return Ok("Blocked");
                    }
                    await _supabaseClient
                        .From<ContactDto>()
                        .Where(n => n.ContacteeId == hey1.Id && n.ContacterId == hey2.Id)
                        .Set(u => u.Blocked, false)
                        .Update();

                    return Ok("Blocked");
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

        [HttpGet("SearchContacts"), Authorize]
        public async Task<IActionResult> SearchUser(Query query)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                // Retrieve user record(s) based on ContacteeId
                var userResponse = await _supabaseClient
                    .From<UserProfiledto>()
                    .Where(a => a.Name.Contains(query.SerachQuery))
                    .Select("UserId") // Select only the name column
                    .Get();

                // Extract user names from the userResponse
                var userIds = userResponse.Models.Select(b => b.UserId);

                // Now you can use userNames to filter contacts based on name
                var response = await _supabaseClient
                    .From<ContactDto>()
                    .Where(contact => userIds.Contains(contact.ContacteeId)) // Filter contacts based on user IDs
                    .Select("ContacteeId") // Select all columns from ContactDto
                    .Get();


                try
                {
                    Array hey = response.Models.ToArray();

                    if (hey == null)
                    {
                        return BadRequest("No Search Results");
                    }

                    // return the searched users Id.
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

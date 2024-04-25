using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LiveChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase.Interfaces;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Postgrest;
using System.Linq;

using Client = Supabase.Client;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LiveChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public SettingsController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        [HttpGet("GetNotification"), Authorize]
        public async Task<IActionResult> GetNotification()
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var UserToken = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var gotUser = UserToken.Models.FirstOrDefault();


                    if (gotUser == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    var response1 = await _supabaseClient
                        .From<SettingsDto>()
                        .Where(n => n.UserId == gotUser.Id)
                        .Get();

                    var notfication = response1.Models.FirstOrDefault();
                    if (notfication == null)
                    {
                        return BadRequest("No notification in the table for the user");
                    }

                    return Ok(notfication.Notification);

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

        [HttpPut("UpdateNotification"), Authorize]
        public async Task<IActionResult> UpdateNotification(bool value)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var UserToken = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var gotUser = UserToken.Models.FirstOrDefault();


                    if (gotUser == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    var response1 = await _supabaseClient
                        .From<SettingsDto>()
                        .Where(n => n.UserId == gotUser.Id)
                        .Set(n=>n.Notification,value)
                        .Update();

                    var notfication = response1.Models.FirstOrDefault();
                    if (notfication == null)
                    {
                        return BadRequest("No notification in the table for the user");
                    }

                    return Ok("Updated");

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

        [HttpGet("GetPresence"), Authorize]
        public async Task<IActionResult> GetPresence()
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var UserToken = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var gotUser = UserToken.Models.FirstOrDefault();


                    if (gotUser == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    var response1 = await _supabaseClient
                        .From<SettingsDto>()
                        .Where(n => n.UserId == gotUser.Id)
                        .Select("Presence")
                        .Get();

                    var presenceJson = response1.Models.FirstOrDefault()?.Presence;

                    if (presenceJson == null)
                    {
                        return BadRequest("No presence data found for the user");
                    }

                    // Deserialize the presenceJson into a C# object
                    var presenceObject = JsonConvert.DeserializeObject<PresenceUser>(presenceJson.ToString());

                    return Ok(presenceObject);


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

        [HttpPut("UpdateApperance"), Authorize]
        public async Task<IActionResult> UpdatePresence(ApperanceUser apperanceUser)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var UserToken = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var gotUser = UserToken.Models.FirstOrDefault();


                    if (gotUser == null)
                    {
                        return BadRequest("Invalid Token");
                    }
                    // Seralize the object parameter
                    var presenceSeralized = JsonConvert.SerializeObject(apperanceUser.ToString());
                    
                    var response1 = await _supabaseClient
                        .From<SettingsDto>()
                        .Where(n => n.UserId == gotUser.Id)
                        .Set(n=>n.Apperance , presenceSeralized)
                        .Update();

                    var apperanceJson = response1.Models.FirstOrDefault()?.Apperance;

                    if (apperanceJson == null)
                    {
                        return BadRequest("No apperance data found for the user");
                    }

                   
                    return Ok(apperanceJson);

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

        [HttpGet("GetApperance"), Authorize]
        public async Task<IActionResult> GetApperance()
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var UserToken = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var gotUser = UserToken.Models.FirstOrDefault();


                    if (gotUser == null)
                    {
                        return BadRequest("Invalid Token");
                    }
                    // Seralize the object parameter
                    
                    var response1 = await _supabaseClient
                        .From<SettingsDto>()
                        .Where(n => n.UserId == gotUser.Id)
                        .Select("Apperance")
                        .Get();

                    var apperanceJson = response1.Models.FirstOrDefault()?.Apperance;

                    if (apperanceJson == null)
                    {
                        return BadRequest("No apperance data found for the user");
                    }


                    return Ok(apperanceJson);

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

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
    public class UserProfile : ControllerBase
    {

        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public UserProfile(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        
        
        [HttpGet("GetUserProfile"),Authorize]
        public async Task<IActionResult> GetUserProfile()
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

                   

                    // update Password
                    try
                    {
                        var responseUpdate = await _supabaseClient.From<UserProfiledto>()
                            .Where(n => n.UserId == hey.Id)
                            .Single();

                        UserProfileCustom userProfileCustom = new UserProfileCustom
                        {
                            Name = responseUpdate.Name,
                            LastName = responseUpdate.LastName,
                            UserName = responseUpdate.UserName,
                            Avatar = responseUpdate.Avatar,
                            Bio = responseUpdate.Bio
                        };

                        return Ok(userProfileCustom);
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for Getting Userprofile");
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

        [HttpPut("UpdatUserProfile"),Authorize]
        public async Task<IActionResult> UpdateUserProfile(UserProfileCustom userProfileCustom)
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

                    var response1 = await _supabaseClient
                        .From<UserProfiledto>()
                        .Where(n => n.UserId == hey.Id)
                        .Single();

                    // Check if the user profile exists
                    if (response1 == null)
                    {
                        return NotFound("User profile not found");
                    }

                    UserProfiledto userProfiledto = response1 as UserProfiledto;

                    if (!string.IsNullOrEmpty(userProfileCustom.Name))
                    {
                        userProfiledto.Name = userProfileCustom.Name;
                    }
                    if (!string.IsNullOrEmpty(userProfileCustom.LastName))
                    {
                        userProfiledto.LastName = userProfileCustom.LastName;
                    }
                    if (!string.IsNullOrEmpty(userProfileCustom.Bio))
                    {
                        userProfiledto.Bio = userProfileCustom.Bio;
                    }
                    if (!string.IsNullOrEmpty(userProfileCustom.UserName))
                    {
                        userProfiledto.Bio = userProfileCustom.UserName;
                    }
                    if (!string.IsNullOrEmpty(userProfileCustom.Avatar))
                    {
                        userProfiledto.Bio = userProfileCustom.Avatar;
                    }
                    // update Profile
                    try
                    {

                        var response2 = await _supabaseClient
                            .From<UserProfiledto>()
                            .Where(n => n.UserId == hey.Id)
                            .Update(userProfiledto);
                        

                        return Ok("Updated UserProfile");
                    }
                    catch (Exception)
                    {
                        return BadRequest("No Connection for updating Userprofile");
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
    }
}
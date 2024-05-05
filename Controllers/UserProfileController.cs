﻿using LiveChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase.Interfaces;
using Supabase;

namespace LiveChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {

        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public UserProfileController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        
        
        [HttpGet("GetOwnUserProfile"),Authorize]
        public async Task<IActionResult> GetUserProfile()
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15,"Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                Console.WriteLine("0");
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();

               
                    var hey = response.Models.FirstOrDefault();

                    if (hey == null)
                    {
                        return StatusCode(10,"Invalid Token");
                    }
                    Console.WriteLine("1");


                var responseUpdate = await _supabaseClient.From<UserProfiledto>()
                            .Where(n => n.UserId == hey.Id && n.Deleted == false)
                            .Single();
                Console.WriteLine("2");

                UserProfileCustom userProfileCustom = new UserProfileCustom
                        {
                            Name = responseUpdate.Name,
                            LastName = responseUpdate.LastName,
                            Bio = responseUpdate.Bio
                        };
                Console.WriteLine("3");
                Console.WriteLine(userProfileCustom.Name);
                Console.WriteLine(userProfileCustom.Bio);
                Console.WriteLine(userProfileCustom.LastName);


                return Ok(userProfileCustom);
                    
            }
            catch (Exception)
            {
                return StatusCode(30,"No Connection, Please Try again");
            }
        }
        
        [HttpGet("GetUserProfile/{id}"), Authorize]
        public async Task<IActionResult> GetUserProfile(long id)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == emailClaim.ToString()&& n.Deleted ==false)
                    .Get();

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
                            .Where(n => n.UserId == id)
                            .Single();

                        if (responseUpdate.Deleted == true)
                        {
                            return BadRequest("User Not Found");
                        }
                        UserProfileCustom userProfileCustom = new UserProfileCustom
                        {
                            Name = responseUpdate.Name,
                            LastName = responseUpdate.LastName,
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
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15,"Invalid Token");
            }
            Console.WriteLine(emailClaim);

            var email = emailClaim.Value.Split(':')[0].Trim();
            Console.WriteLine(email);

            try
            {
                Console.WriteLine("1");
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted==false).Get();
                Console.WriteLine("2");


                var hey = response.Models.FirstOrDefault();
                Console.WriteLine("3");

                if (hey == null)
                    {
                        return StatusCode(10,"Internal Server Error");
                    }
                Console.WriteLine("4");

                var response1 = await _supabaseClient
                        .From<UserProfiledto>()
                        .Where(n => n.UserId == hey.Id && n.Deleted==false)
                        .Single();

                    // Create a User Profile
                    if (response1 == null)
                    {
                        Console.WriteLine("5");

                    UserProfiledto userProfiletoInsert = new UserProfiledto
                        {
                            UserId = hey.Id,
                            Name = userProfileCustom.Name,
                            Bio = userProfileCustom.Bio,
                            LastName= userProfileCustom.LastName
                        };

                    await _supabaseClient.From<UserProfiledto>().Insert(userProfiletoInsert);




                    return Ok ("User profile Created");
                    }
                    Console.WriteLine("6");

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
                Console.WriteLine("7");

                // update Profile
                var response2 = await _supabaseClient
                            .From<UserProfiledto>()
                            .Where(n => n.UserId == hey.Id && n.Deleted==false)
                            .Single();
                        response2.Name = userProfiledto.Name;
                        response2.LastName=userProfiledto.LastName;
                        response2.Bio = userProfiledto.Bio;

                        await response2.Update<UserProfiledto>();
                        Console.WriteLine("8");


                return Ok("Updated UserProfile");
                   
            }
            catch (Exception)
            {
                return StatusCode(30,"No Connection, Please Try again");
            }
        }
    }
}
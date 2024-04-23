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
namespace LiveChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public GroupController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        [HttpPost("CreateGroup")]
        async public Task<IActionResult> CreateGroup(GroupUser groupUser)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {

                var getSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var sender = getSender.Models.FirstOrDefault();


                if (sender == null)
                {
                    return BadRequest("Invalid Token");
                }
                
                try
                {
                    GroupDto grouptoInsert = new GroupDto
                    {
                        Created_at = DateTime.UtcNow,
                        Name = groupUser.Name,
                        Description = groupUser.Description,
                        CreatorId = sender.Id,

                    };

                    var createGroup = await _supabaseClient.From<GroupDto>().Insert(grouptoInsert);
                
                    var newGroupModel = createGroup.Models.FirstOrDefault();
                    if (newGroupModel== null)
                    {
                        return BadRequest("Problem Creating a new Group");
                    }

                    return Ok(newGroupModel);
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpPost("AddMember")]
        async public Task<IActionResult> AddMemberTGroup(long groupId , List<long> Members)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {

                var getSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var sender = getSender.Models.FirstOrDefault();


                if (sender == null)
                {
                    return BadRequest("Invalid Token");
                }

                try
                {


                    var checkGroupid = await _supabaseClient.From<GroupDto>()
                        .Where(n => n.GroupId == groupId)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null && vertifyingGroupid.CreatorId != sender.Id)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    // Add the members
                    List<GroupMemberDto> membersToAdd = new List<GroupMemberDto>();

                    // Add your GroupMemberDto objects to the list
                    foreach (var member  in Members)
                    {
                        membersToAdd.Add(new GroupMemberDto
                        {
                            JoinedTime = DateTime.UtcNow,
                            GroupId = groupId,
                            UserId = member,
                            Role = "member"
                        });

                    }

                    var addMembers = await _supabaseClient.From<GroupMemberDto>().Insert(membersToAdd);

                    var newGroupModel = addMembers.Models.FirstOrDefault();
                    if (newGroupModel == null)
                    {
                        return BadRequest("Problem Creating a new Group");
                    }

                    return Ok("Succesfully Added");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

    }
}

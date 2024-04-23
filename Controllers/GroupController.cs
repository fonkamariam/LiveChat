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
                    // First Create a "Welcome" Message for the group
                    GroupMessageDto groupMessageDto = new GroupMessageDto
                    {
                        Content = "Welcome",
                        Type = "Text",
                        MemberSenderId = sender.Id,
                        Created_at = DateTime.UtcNow
                        //RecGroupId is NULL temporarily
                    };
                    var getLastmessageId = await _supabaseClient.From<GroupMessageDto>().Insert(groupMessageDto);
                    var lastMessageId = getLastmessageId.Models.First();
                    if (lastMessageId== null)
                    {
                        return BadRequest("Problem when Inserting a Welcome message into the newly created Group");
                    }
                    // then create a Conversation for the group to be created
                    GroupConversationDto groupConversationDto = new GroupConversationDto
                    {
                        LastGroupMessage = lastMessageId.Id,
                        Created_at = DateTime.UtcNow,
                        Updated_time = DateTime.UtcNow
                    };
                    var createConvGroup = await _supabaseClient.From<GroupConversationDto>().Insert(groupConversationDto);
                    var newConversationId = createConvGroup.Models.First();
                   
                    if (newConversationId == null)
                    {
                        return BadRequest("Problem when creating a Conversation for the Group");
                    }
                    // Now I have a Conversation Id for the group

                    GroupDto grouptoInsert = new GroupDto
                    {
                        Created_at = DateTime.UtcNow,
                        Name = groupUser.Name,
                        Description = groupUser.Description,
                        CreatorId = sender.Id,
                        G_CoversationId = newConversationId.Id

                    };

                    var createGroup = await _supabaseClient.From<GroupDto>().Insert(grouptoInsert);
                
                    var newGroupModel = createGroup.Models.FirstOrDefault();
                    if (newGroupModel== null)
                    {
                        return BadRequest("Problem Creating a new Group");
                    }

                    var updateBackMessage = await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.Id == lastMessageId.Id)
                        .Set(n => n.RecGroupId, newGroupModel.GroupId)
                        .Update();
                    var updatedBackMessage = updateBackMessage.Models.First();
                    if (updatedBackMessage==null)
                    {
                        return BadRequest("Probelm when setting back what i borrowed in the GroupMessage");
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

        [HttpPost("RemoveMember")]
        async public Task<IActionResult> RemoveMemberGroup(long groupId, List<long> Members)
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

                    await _supabaseClient.From<GroupMemberDto>()
                        .Where(n=>n.GroupId == groupId && Members.Contains(n.UserId))
                        .Delete();


                    return Ok("Successfully Deleted");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when Deleting Members");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpPost("LeaveGroup")]
        async public Task<IActionResult> LeaveGroup(long groupId)
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
                    
                    if (vertifyingGroupid == null)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    if (vertifyingGroupid.CreatorId == sender.Id)
                    {
                        //Delete Group and Leave
                        await _supabaseClient.From<GroupMemberDto>()
                            .Where(n => n.GroupId == groupId)
                            .Delete();
                        await _supabaseClient.From<GroupDto>()
                            .Where(n => n.GroupId == groupId)
                            .Delete();
                        return Ok("Since you are the creator,group deleted")
                    }
                    // only Leave Group


                    await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupId && n.UserId==sender.Id)
                        .Delete();

                    return Ok("Successfully Left Group");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when Deleting Members");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpPost("SendGroupMessage")]
        async public Task<IActionResult> SendGroupMessage(GroupMessagePostModel groupMessagePostModel)
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
                        .Where(n => n.GroupId == groupMessagePostModel.GroupID)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    // I have the conversation Id
                    var checkSenderid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.UserId == sender.Id && n.GroupId == groupMessagePostModel.GroupID)
                        .Get();

                    var vertifySenderAMember = checkSenderid.Models.First();
                    if (vertifySenderAMember == null)
                    {
                        return BadRequest("You are not a member in the Group");
                    }
                    GroupMessageDto groupMessageDto = new GroupMessageDto
                    {
                        Content = groupMessagePostModel.Content,
                        Type = groupMessagePostModel.Type,
                        MemberSenderId = sender.Id,
                        Created_at = DateTime.UtcNow,
                        RecGroupId = groupMessagePostModel.GroupID
                    };
                    // Message Sent... now updating the conversation table
                    var messageToBeSent = await _supabaseClient.From<GroupMessageDto>().Insert(groupMessageDto)
                    var messageSent = messageToBeSent.Models.First();
                    if (messageSent == null)
                    {
                        return BadRequest("Problem sending message");
                    }
                    // I have the last message Id
                    var updateConversation = await _supabaseClient.From<GroupConversationDto>()
                        .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                        .Set(n => n.LastGroupMessage, messageSent.Id)
                        .Set(n => n.Updated_time, DateTime.UtcNow)
                        .Update();
                    
                    var updatedConv = updateConversation.Models.First();
                    if (updatedConv == null)
                    {
                        return BadRequest("Problem modifying Conversation table");
                    }

                    return Ok("Successfully Sent Message");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when Deleting Members");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }
        }

    }
}

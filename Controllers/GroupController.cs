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
        public async Task<IActionResult> CreateGroup(GroupUser groupUser)
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

                    GroupMemberDto groupMember = new GroupMemberDto
                    {
                        JoinedTime = DateTime.UtcNow,
                        GroupId = newGroupModel.GroupId,
                        UserId = sender.Id,
                        Role = "Admin"
                    };
                    var addMemberAdmin = await _supabaseClient.From<GroupMemberDto>().Insert(groupMember);
                    var addedMemberAdmin = addMemberAdmin.Models.First();
                    if (addedMemberAdmin == null)
                    {
                        return BadRequest("Probelm when adding the user to the groupmember table as admin");
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
        public async Task<IActionResult> AddMemberTGroup(long groupId , List<long> Members)
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


                    var checkGroupid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupId)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null || vertifyingGroupid.Role != "Admin")
                    {
                        return BadRequest("Invalid Group Id or you are not admin");
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
                            Role = "Member"
                        });

                    }

                    var addMembers = await _supabaseClient.From<GroupMemberDto>().Insert(membersToAdd);

                    var newGroupModel = addMembers.Models.FirstOrDefault();
                    if (newGroupModel == null)
                    {
                        return BadRequest("Problem adding a new member");
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
        public async Task<IActionResult> RemoveMemberGroup(long groupId, List<long> Members)
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


                    var checkGroupid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupId)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null || vertifyingGroupid.Role != "Admin")
                    {
                        return BadRequest("Invalid Group Id or You are not admin");
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
        public async Task<IActionResult> LeaveGroup(long groupId)
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
                    /*
                    if (vertifyingGroupid.CreatorId == sender.Id)
                    {
                        //Delete Group and Leave
                        await _supabaseClient.From<GroupMemberDto>()
                            .Where(n => n.GroupId == groupId)
                            .Delete();
                        await _supabaseClient.From<GroupDto>()
                            .Where(n => n.GroupId == groupId)
                            .Delete();
                        return Ok("Since you are the creator,group deleted");
                    }
                    */
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
        public async Task<IActionResult> SendGroupMessage(GroupMessagePostModel groupMessagePostModel)
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
                    var messageToBeSent = await _supabaseClient.From<GroupMessageDto>().Insert(groupMessageDto);
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

        [HttpGet("GetGroupMessages")]
        public async Task<IActionResult> GetGroupMessages(long groupID)
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
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    // I have the conversation Id
                    var checkSenderid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.UserId == sender.Id && n.GroupId == groupID)
                        .Get();

                    var vertifySenderAMember = checkSenderid.Models.First();
                    if (vertifySenderAMember == null)
                    {
                        return BadRequest("You are not a member in the Group");
                    }

                    var getEverything = await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.MemberSenderId == sender.Id && n.RecGroupId == groupID)
                        .Get();
                    var messageArray = getEverything.Models.ToArray();
                    return Ok(messageArray);
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
        [HttpPut("EditGroupMessage")]
        public async Task<IActionResult> EditGroupMessage(long groupId, long messageId, string newContent)
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
                    // I have the conversation Id
                    
                    var checkSenderid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.UserId == sender.Id && n.GroupId == groupId)
                        .Get();

                    var vertifySenderAMember = checkSenderid.Models.First();
                    if (vertifySenderAMember == null)
                    {
                        return BadRequest("You are not a member in the Group");
                    }

                    var checkMessageId = await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.MemberSenderId == sender.Id && n.Id == messageId)
                        .Get();
                    var checkingMessageId = checkMessageId.Models.First();
                    if (checkingMessageId==null)
                    {
                        return BadRequest("Invalid Message Id provided");
                    }

                    var updateMessageId = await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.Id == messageId)
                        .Set(n => n.Content, newContent)
                        .Update();

                    var updatedMessage = updateMessageId.Models.FirstOrDefault();
                    if (updatedMessage == null)
                    {
                        return BadRequest("Problem when updating the message");
                    }

                    var checkConv = await _supabaseClient.From<GroupConversationDto>()
                        .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                        .Get();
                    var checkingConv = checkConv.Models.First();

                    if (checkingConv.LastGroupMessage == messageId)
                    {
                        var updatingConv = await _supabaseClient.From<GroupConversationDto>()
                            .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                            .Set(n => n.LastGroupMessage, updatedMessage.Id)
                            .Update();
                    }
                    return Ok(updatedMessage);
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when editing message");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpDelete("DeleteGroupMessage")]
        public async Task<IActionResult> DeleteGroupMessage(long groupId, long messageId)
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
                    // I have the conversation Id

                    var checkSenderid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.UserId == sender.Id && n.GroupId == groupId)
                        .Get();

                    var vertifySenderAMember = checkSenderid.Models.First();
                    if (vertifySenderAMember == null)
                    {
                        return BadRequest("You are not a member in the Group");
                    }

                    var checkMessageId = await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.MemberSenderId == sender.Id && n.Id == messageId)
                        .Get();
                    var checkingMessageId = checkMessageId.Models.First();
                    if (checkingMessageId == null)
                    {
                        return BadRequest("Invalid Message Id provided");
                    }
                    // here my work begins
                    var checkConv = await _supabaseClient.From<GroupConversationDto>()
                        .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                        .Get();
                    var checkingConv = checkConv.Models.First();

                    if (checkingConv.LastGroupMessage == messageId)
                    {
                        //fetch the next last message
                        var fetchNextMessage = await _supabaseClient.From<GroupMessageDto>()
                            .Where(n => n.MemberSenderId == sender.Id && n.RecGroupId== groupId)
                            .Order(n=>n.Created_at,Constants.Ordering.Descending)
                            .Range(1,1)
                            .Get();
                        var fetchedMessage = fetchNextMessage.Models.FirstOrDefault();
                        
                        
                        if (fetchedMessage == null)
                        {
                            // the deleted message is the last message of the conversation
                            var updatingConv = await _supabaseClient.From<GroupConversationDto>()
                                .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                                .Set(n => n.LastGroupMessage, null)
                                .Set(n=>n.Updated_time,DateTime.UtcNow)
                                .Update();
                        }
                        else
                        {
                            var updatingConv = await _supabaseClient.From<GroupConversationDto>()
                                .Where(n => n.Id == vertifyingGroupid.G_CoversationId)
                                .Set(n => n.LastGroupMessage, fetchedMessage.Id)
                                .Set(n => n.Updated_time, DateTime.UtcNow)
                                .Update();
                        }



                    }

                    await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.Id == messageId)
                        .Delete();
                    
                    return Ok("Successfully Deleted");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when Deleting message");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpGet("GetGroupMembers")]
        public async Task<IActionResult> GetGroupMembers(long groupID)
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
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    // I have the conversation Id
                    var checkSenderid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.UserId == sender.Id && n.GroupId == groupID)
                        .Get();

                    var vertifySenderAMember = checkSenderid.Models.First();
                    if (vertifySenderAMember == null)
                    {
                        return BadRequest("You are not a member in the Group");
                    }

                    var getMembers = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var membersArray = getMembers.Models.ToArray();
                    return Ok(membersArray);
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when fetching Members");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }
        }

        [HttpGet("GetGroupInfo")]
        public async Task<IActionResult> GetGroupInfo(long groupID)
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
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null)
                    {
                        return BadRequest("Invalid Group Id");
                    }
                    // I have the conversation Id

                    var getGroupInfo = await _supabaseClient.From<GroupDto>()
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var groupInfo = getGroupInfo.Models.First();

                    return Ok(groupInfo);
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when fetching Members");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }
        }

        [HttpPost("SetGroupAdmin")]
        public async Task<IActionResult> SetGroupAdmin(long groupId, List<long> Admins)
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

                    var addAdminMembers = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => Admins.Contains(n.UserId))
                        .Set(n => n.Role, "Admin")
                        .Update();
                    var addedAdminNumbers = addAdminMembers.Models.Count;
                    if (addedAdminNumbers ==0)
                    {
                        return BadRequest("Problem Updating a new admin Member");
                    }

                    return Ok("Successfully updated their roles to admin");
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

        [HttpPut("UpdateGroupSettings")]
        public async Task<IActionResult> UpdateGroupSettings( UpdateGroupSettingModel updateGroupSetting)
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
                    // checking if the user is admin
                    var checkGroupid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == updateGroupSetting.GroupId && n.UserId == sender.Id)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null || vertifyingGroupid.Role != "Admin")
                    {
                        return BadRequest("Invalid Group Id or You are not Admin");
                    }
                    // let's update the setting
                    if (updateGroupSetting.Name!="")
                    {
                        var updateName = await _supabaseClient.From<GroupDto>()
                            .Where(n => n.GroupId == updateGroupSetting.GroupId)
                            .Set(n => n.Name, updateGroupSetting.Name)
                            .Get();

                        var updatedName = updateName.Models.First();
                        if (updatedName == null)
                        {
                            return BadRequest("Problem updating Name(Invalid Group Id)");
                        }

                    }

                    if (updateGroupSetting.Description!= "")
                    {
                        var updateDesc = await _supabaseClient.From<GroupDto>()
                            .Where(n => n.GroupId == updateGroupSetting.GroupId)
                            .Set(n => n.Description, updateGroupSetting.Description)
                            .Get();

                        var updatedDesc = updateDesc.Models.First();
                        if (updatedDesc == null)
                        {
                            return BadRequest("Problem updating Description (Invalid Group Id)");
                        }

                    }
                    return Ok("Successfully updated");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when editing message");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }

        }

        [HttpDelete("DeleteGroup")]
        public async Task<IActionResult> DeleteGroup(long groupID)
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
                    // checking if the user is admin
                    var checkGroupid = await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupID && n.UserId == sender.Id)
                        .Get();
                    var vertifyingGroupid = checkGroupid.Models.First();
                    if (vertifyingGroupid == null || vertifyingGroupid.Role != "Admin")
                    {
                        return BadRequest("Invalid Group Id or You are not Admin");
                    }
                    // getting the GroupConversation Id
                    var getGroupConv = await _supabaseClient.From<GroupDto>()
                        .Where(n => n.GroupId == groupID)
                        .Get();
                    var groupInfo = getGroupConv.Models.First();
                    if (groupInfo.GroupId != groupID)
                    {
                        return BadRequest("Invalid groupId given");
                    }
                    // i have GroupId and its conversation ID

                    // First let's delete the conversation row
                    await _supabaseClient.From<GroupConversationDto>()
                        .Where(n => n.Id == groupInfo.G_CoversationId)
                        .Delete();
                    // then Let's delete every message related to the group from the GroupMessage table
                    await _supabaseClient.From<GroupMessageDto>()
                        .Where(n => n.RecGroupId == groupInfo.GroupId)
                        .Delete();
                    // then remove all members from the Members table
                    await _supabaseClient.From<GroupMemberDto>()
                        .Where(n => n.GroupId == groupInfo.GroupId)
                        .Delete();

                    // finally Delete the group from the Group table
                    await _supabaseClient.From<GroupDto>()
                        .Where(n => n.GroupId == groupInfo.GroupId)
                        .Delete();

                    return Ok("Successfully Deleted everything about Group");
                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem when editing message");
                }
            }
            catch (Exception)
            {
                return BadRequest("Problem validation user");
            }
        }
    }
}

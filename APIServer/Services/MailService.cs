using ApiServer.DB;

namespace ApiServer.Services;

public class MailService
{
    public Mail WriteInvitationMail(int userId, string username)
    {
        return new Mail
        {
            UserId = userId,
            Type = MailType.Invite,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            Claimed = false,
            Message = $"{username} sent you a game invite.",
            Sender = username
        };
    }  
}
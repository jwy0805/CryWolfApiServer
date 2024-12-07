using System.Net;
using System.Net.Mail;
using System.Reflection;
using ApiServer.DB;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Services;

public class UserService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    
    public UserService(IConfiguration configuration, AppDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task SendVerificationEmail(string recipientEmail, string verificationLink)
    {
        // Load Html Template
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "ApiServer.Templates.EmailTemplate.html";
        
        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            var emailBody = await reader.ReadToEndAsync();
            emailBody = emailBody
                .Replace("{{UserName}}", recipientEmail)
                .Replace("{{VerificationLink}}", verificationLink);
            
            // Send Email
            var smtpClient = new SmtpClient
            {
                Host = _configuration["Email:Smtp:Server"] ?? "smtp.gmail.com",
                Port = int.Parse(Environment.GetEnvironmentVariable("GOOGLE_MAIL_PORT") ?? "587"),
                Credentials = new NetworkCredential(
                    _configuration["Email:Smtp:Username"],
                    Environment.GetEnvironmentVariable("GOOGLE_APP_PASSWORD")),
                EnableSsl = true,
            };
        
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_configuration["Email:Smtp:Username"] ?? string.Empty),
                Subject = "Cry Wolf Email Verification",
                Body = emailBody,
                IsBodyHtml = true,
            };
        
            mailMessage.To.Add(recipientEmail);
        
            await smtpClient.SendMailAsync(mailMessage);
        }
    }

    public async Task<bool> CreateAccount(string userAccount, string password)
    {
        var account = _context.User.AsNoTracking().FirstOrDefault(u => u.UserAccount == userAccount);
        if (account != null) return false;
        
        var newUser = new User()
        {
            UserAccount = userAccount,
            UserName = "",
            Password = password,
            Role = UserRole.User,
            State = UserState.Deactivate,
            CreatedAt = DateTime.UtcNow,
        };
        
        _context.User.Add(newUser);
        await _context.SaveChangesExtendedAsync(); // 이 때 UserId가 생성
        
        var newUserStat = new UserStats
        {
            UserId = newUser.UserId,
            UserLevel = 1,
            RankPoint = 500,
            Exp = 0,
            Gold = 0,
            Spinel = 0
        };
            
        var newUserMatch = new UserMatch
        {
            UserId = newUser.UserId,
            WinRankMatch = 0,
            LoseRankMatch = 0,
            WinFriendlyMatch = 0,
            LoseFriendlyMatch = 0,
        };
            
        _context.UserStats.Add(newUserStat);
        _context.UserMatch.Add(newUserMatch);
        newUser.UserName = $"Player{newUser.UserId}";
        
        // Create Initial Deck and Collection
        CreateInitDeckAndCollection(newUser.UserId, new [] {
            UnitId.Hare, UnitId.Toadstool, UnitId.FlowerPot, 
            UnitId.Blossom, UnitId.TrainingDummy, UnitId.SunfloraPixie
        }, Faction.Sheep);
            
        CreateInitDeckAndCollection(newUser.UserId, new [] {
            UnitId.DogBowwow, UnitId.MoleRatKing, UnitId.MosquitoStinger, 
            UnitId.Werewolf, UnitId.CactusBoss, UnitId.SnakeNaga
        }, Faction.Wolf);
            
        // Create Initial Sheep and Enchant
        CreateInitSheepAndEnchant(
            newUser.UserId, new [] { SheepId.PrimeSheepWhite }, new [] { EnchantId.Wind });
        CreateInitCharacter(newUser.UserId, new [] { CharacterId.PlayerCharacter });
        CreateInitBattleSetting(newUser.UserId);

        await _context.SaveChangesExtendedAsync();
        return true;
    }
    
    private void CreateInitDeckAndCollection(int userId, UnitId[] unitIds, Faction faction)
    {
        foreach (var unitId in unitIds)
        {
            _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = unitId, Count = 1});
        }

        for (int i = 0; i < 3; i++)
        {
            var deck = new Deck { UserId = userId, Faction = faction, DeckNumber = i + 1};
            _context.Deck.Add(deck);
            _context.SaveChangesExtended();
        
            foreach (var unitId in unitIds)
            {
                _context.DeckUnit.Add(new DeckUnit { DeckId = deck.DeckId, UnitId = unitId });
            }
        }
    }
    
    private void CreateInitSheepAndEnchant(int userId, SheepId[] sheepIds, EnchantId[] enchantIds)
    {
        foreach (var sheepId in sheepIds)
        {
            _context.UserSheep.Add(new UserSheep { UserId = userId, SheepId = sheepId, Count = 1});
        }
        
        foreach (var enchantId in enchantIds)
        {
            _context.UserEnchant.Add(new UserEnchant { UserId = userId, EnchantId = enchantId, Count = 1});
        }
    }
    
    private void CreateInitCharacter(int userId, CharacterId[] characterIds)
    {
        foreach (var characterId in characterIds)
        {
            _context.UserCharacter.Add(new UserCharacter { UserId = userId, CharacterId = characterId, Count = 1});
        }
    }

    private void CreateInitBattleSetting(int userId)
    {
        _context.BattleSetting.Add(new BattleSetting
        {
            UserId = userId, SheepId = 901, EnchantId = 1001, CharacterId = 2001
        });
    }
}
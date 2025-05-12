using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Security.Claims;
using ApiServer.DB;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
    
    public async Task<bool> CreateAccount(string userAccount, AuthProvider provider, string? password = null)
    {
        var account = _context.UserAuth.AsNoTracking()
            .FirstOrDefault(u => u.UserAccount == userAccount);
        if (account != null)
        {
            return false;
        }
        
        var newUser = new User
        {
            UserName = string.Empty,
            Role = UserRole.User,
            State = UserState.Deactivate,
        };

        var newUserAuth = new UserAuth
        {
            UserAccount = userAccount,
            PasswordHash = password ?? string.Empty,
            LinkedAt = DateTime.UtcNow,
            Provider = provider,
            User = newUser,  
        };
        
        _context.UserAuth.Add(newUserAuth);
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
        CreateInitDeckAndCollection(newUser.UserId);
        
        // Create Initial Sheep and Enchant
        CreateInitSheepAndEnchant(newUser.UserId, new [] { SheepId.PrimeSheepWhite }, new [] { EnchantId.Wind });
        CreateInitCharacter(newUser.UserId, new [] { CharacterId.PlayerCharacterBasic });
        CreateInitBattleSetting(newUser.UserId);
        CreateInitStageInfo(newUser.UserId);
        CreateInitTutorialInfo(newUser.UserId);

        await _context.SaveChangesExtendedAsync();
        return true;
    }
    
    private void CreateInitDeckAndCollection(int userId)
    {
        var sheepUnitIds = new [] { UnitId.Hare, UnitId.Toadstool, UnitId.FlowerPot };
        var wolfUnitIds = new [] { UnitId.DogBowwow, UnitId.MoleRatKing, UnitId.MosquitoStinger };
        var sheepKnightUnitIdsAll = new [] { UnitId.Blossom, UnitId.TrainingDummy, UnitId.Hermit };
        var wolfKnightUnitIdsAll = new [] { UnitId.PoisonBomb, UnitId.CactusBoss, UnitId.SnakeNaga };
        var sheepNobleKnightUnitIdsAll = new [] { UnitId.SunfloraPixie, UnitId.MothCelestial };
        var wolfNobleKnightUnitIdsAll = new [] { UnitId.Werewolf, UnitId.Horror };
        var sheepKnightUnitIds = Util.Util.ShuffleArray(sheepKnightUnitIdsAll).Take(2).ToArray();
        var wolfKnightUnitIds = Util.Util.ShuffleArray(wolfKnightUnitIdsAll).Take(2).ToArray();
        var sheepNobleKnightUnitIds = Util.Util.ShuffleArray(sheepNobleKnightUnitIdsAll).Take(1).ToArray();
        var wolfNobleKnightUnitIds = Util.Util.ShuffleArray(wolfNobleKnightUnitIdsAll).Take(1).ToArray();
        var sheepUnits = sheepUnitIds.Concat(sheepNobleKnightUnitIds.Concat(sheepKnightUnitIds)).ToArray();
        var wolfUnits = wolfUnitIds.Concat(wolfNobleKnightUnitIds.Concat(wolfKnightUnitIds)).ToArray();

        foreach (var sheepUnit in sheepUnits)
        {
            _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = sheepUnit, Count = 1});
        }
        
        foreach (var wolfUnit in wolfUnits)
        {
            _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = wolfUnit, Count = 1});
        }
        
        for (int i = 0; i < 3; i++)
        {
            var deck = new Deck { UserId = userId, Faction = Faction.Sheep, DeckNumber = i + 1};
            _context.Deck.Add(deck);
            _context.SaveChangesExtended();

            foreach (var sheepUnit in sheepUnits)
            {
                _context.DeckUnit.Add(new DeckUnit { DeckId = deck.DeckId, UnitId = sheepUnit });
            }
        }
        
        for (int i = 0; i < 3; i++)
        {
            var deck = new Deck { UserId = userId, Faction = Faction.Wolf, DeckNumber = i + 1};
            if (i == 0)
            {
                deck.LastPicked = true;
            }
            
            _context.Deck.Add(deck);
            _context.SaveChangesExtended();
            
            foreach (var wolfUnit in wolfUnits)
            {
                _context.DeckUnit.Add(new DeckUnit { DeckId = deck.DeckId, UnitId = wolfUnit });
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

    private void CreateInitStageInfo(int userId)
    {
        var initStages = new[]
        {
            new UserStage
            {
                StageId = 1001, UserId = userId, IsAvailable = true, IsCleared = false, StageLevel = 1, StageStar = 0
            },
            new UserStage
            {
                StageId = 5001, UserId = userId, IsAvailable = false, IsCleared = false, StageLevel = 1, StageStar = 0
            }
        };
        
        _context.UserStage.AddRange(initStages);
    }

    private void CreateInitTutorialInfo(int userId)
    {
        var initTutorials = new[]
        {
            new UserTutorial
            {
                UserId = userId, TutorialType = TutorialType.BattleWolf, Done = false
            },
            new UserTutorial
            {
                UserId = userId, TutorialType = TutorialType.BattleSheep, Done = false
            },
            new UserTutorial
            {
                UserId = userId, TutorialType = TutorialType.Collection, Done = false
            },
            new UserTutorial
            {
                UserId = userId, TutorialType = TutorialType.Reinforce, Done = false
            }
        };
        
        _context.UserTutorial.AddRange(initTutorials);
    }
}
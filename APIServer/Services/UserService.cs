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
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;
    private readonly Random _random = new();
    private readonly char[] _tagChars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    
    public UserService(IConfiguration configuration, AppDbContext context)
    {
        _config = configuration;
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
                Host = _config["Email:Smtp:Server"] ?? "smtp.gmail.com",
                Port = int.Parse(_config["Google:MailPort"] ?? "587"),
                Credentials = new NetworkCredential(
                    _config["Email:Smtp:Username"],
                    _config["Google:AppPassword"]),
                EnableSsl = true,
            };
        
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_config["Email:Smtp:Username"] ?? string.Empty),
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
        if (account != null) return false;

        for (var i = 0; i < 3; i++)
        {
            var uniqueTag = await GenerateUniqueUserTag();
            var newUser = InitUser(uniqueTag);
            var newUserAuth = InitUserAuth(newUser, userAccount, provider, password);
        
            _context.UserAuth.Add(newUserAuth);

            try
            {
                await _context.SaveChangesExtendedAsync(); // 이 때 UserId, UserTag가 생성
            }
            catch (DbUpdateException e) when (e.InnerException?.Message.Contains("IX_User_UserTag") == true)
            {
                _context.Entry(newUser).State = EntityState.Detached;
                _context.Entry(newUserAuth).State = EntityState.Detached;
                continue;            
            }
        
            var newUserStat = InitUserStats(newUser);
            var newUserMatch = InitUserMatch(newUser);
            
            _context.UserStats.Add(newUserStat);
            _context.UserMatch.Add(newUserMatch);
            newUser.UserName = $"Player{newUser.UserId}";
            InitPlayerDeck(newUser);

            await _context.SaveChangesExtendedAsync();
            return true;   
        }

        return false;
    }

    private User InitUser(string uniqueTag) => new()
    {
        UserName = string.Empty,
        UserTag = uniqueTag,
        Role = UserRole.User,
        State = UserState.Deactivate,
    };
    
    private UserAuth InitUserAuth(User user, string userAccount, AuthProvider provider, string? password = null) =>
        new()
        {
            UserAccount = userAccount,
            PasswordHash = password ?? string.Empty,
            LinkedAt = DateTime.UtcNow,
            Provider = provider,
            User = user,
        };
    
    private UserStats InitUserStats(User user) => new()
    {
        UserId = user.UserId,
        UserLevel = 1,
        Exp = 0,
        Gold = 0,
        Spinel = 0,
        RankPoint = 500,
    };

    private UserMatch InitUserMatch(User user) => new()
    {
        UserId = user.UserId,
        WinRankMatch = 0,
        LoseRankMatch = 0,
        WinFriendlyMatch = 0,
        LoseFriendlyMatch = 0,
    };
    
    private async Task<string> GenerateUniqueUserTag()
    {
        string? uniqueTag = null;
        const int maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            var tag = GenerateTag();
            var exists = await _context.User
                .AsNoTracking()
                .AnyAsync(u => u.UserTag == tag);

            if (!exists)
            {
                uniqueTag = tag;
                break;
            }
        }
        
        if (uniqueTag == null) throw new Exception("Failed to generate unique user tag.");

        return uniqueTag;
    }
    
    private string GenerateTag()
    {
        var chars = new char[5];
        for (int i = 0; i < chars.Length; i++)
        {
            var idx = _random.Next(0, _tagChars.Length);
            chars[i] = _tagChars[idx];
        }

        return new string(chars);
    }

    private void InitPlayerDeck(User newUser)
    {
        // Create Initial Deck and Collection
        CreateInitDeckAndCollection(newUser.UserId);
        
        // Create Initial Sheep and Enchant
        CreateInitSheepAndEnchant(newUser.UserId, new [] { SheepId.PrimeSheepWhite }, new [] { EnchantId.Wind });
        CreateInitCharacter(newUser.UserId, new [] { CharacterId.PlayerCharacterBasic });
        CreateInitBattleSetting(newUser.UserId);
        CreateInitStageInfo(newUser.UserId);
        CreateInitTutorialInfo(newUser.UserId);
    }
    
    public void SendProductMail(int userId, MailType type, string? message = null, int? productId = null)
    {
        var mail = new Mail
        {
            UserId = userId,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ProductId = productId,
            ProductCode = productId.HasValue 
                ? _context.Product.AsNoTracking()
                    .FirstOrDefault(p => p.ProductId == productId)?.ProductCode : string.Empty,
            Message = message ?? "Product purchased",
            Sender = "cry wolf"
        };
        
        _context.Mail.Add(mail);
    }
    
    private void SendMailClosedTest(int userId)
    {
        var mail1 = new Mail
        {
            UserId = userId,
            Type = MailType.Notice,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ProductId = null,
            ProductCode = string.Empty,
            Message = "게임을 이용해주셔서 감사합니다. 현재 Cry Wolf는 알파테스트 진행중으로 상점 등 몇몇 기능이 작동하지 않을 수 있습니다.",
            Sender = "cry wolf"
        };

        var mail2 = new Mail
        {
            UserId = userId,
            Type = MailType.Notice,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ProductId = null,
            ProductCode = string.Empty,
            Message = "꾸준한 업데이트로 찾아뵙도록 하겠습니다. 기타 문의사항은 hamonstd@gmail.com으로 보내주시면 감사하겠습니다.",
            Sender = "cry wolf"
        };
        
        var mailList = new List<Mail> { mail1, mail2 };

        _context.Mail.AddRange(mailList);
        _context.SaveChangesExtended();
    }
    
    private async Task CreateAssetsClosedTest(int userId)
    {
        var sheepUnitIds = new [] { 101, 104, 107, 110, 113, 116, 119, 122, 125 };
        var wolfUnitIds = new [] { 201, 204, 207, 210, 213, 216, 219, 222, 225 };
        
        foreach (var sheepUnit in sheepUnitIds)
        {
            _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = (UnitId)sheepUnit, Count = 1 });
        }
        
        foreach (var wolfUnit in wolfUnitIds)
        {
            _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = (UnitId)wolfUnit, Count = 1 });
        }

        for (int i = 1; i <= 35; i++)
        {
            _context.UserMaterial.Add( new UserMaterial { UserId = userId, MaterialId = (MaterialId)i, Count = 5});
        }
        
        await _context.SaveChangesExtendedAsync();
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
        
        for (int i = 0; i < 5; i++)
        {
            var deck = new Deck { UserId = userId, Faction = Faction.Sheep, DeckNumber = i + 1, LastPicked = i == 0};
            _context.Deck.Add(deck);

            foreach (var sheepUnit in sheepUnits)
            {
                _context.DeckUnit.Add(new DeckUnit { DeckId = deck.DeckId, UnitId = sheepUnit });
            }
        }
        
        for (int i = 0; i < 5; i++)
        {
            var deck = new Deck { UserId = userId, Faction = Faction.Wolf, DeckNumber = i + 1, LastPicked = i == 0};
            if (i == 0)
            {
                deck.LastPicked = true;
            }
            
            _context.Deck.Add(deck);
            
            foreach (var wolfUnit in wolfUnits)
            {
                _context.DeckUnit.Add(new DeckUnit { DeckId = deck.DeckId, UnitId = wolfUnit });
            }
        }
        
        _context.SaveChangesExtended();
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
            UserId = userId, SheepId = 901, EnchantId = 1001, CharacterId = 1101
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
                StageId = 5001, UserId = userId, IsAvailable = true, IsCleared = false, StageLevel = 1, StageStar = 0
            }
        };
        
        _context.UserStage.AddRange(initStages);
    }

    private void CreateInitTutorialInfo(int userId)
    {
        var initTutorials = new UserTutorial[5];
        for (var i = 1; i < Enum.GetValues(typeof(TutorialType)).Length; i++)
        {
            initTutorials[i - 1] = new UserTutorial
            {
                UserId = userId, TutorialType = (TutorialType)i, Done = false
            };
        }
        _context.UserTutorial.AddRange(initTutorials);
    }
}
using AccountServer.DB;
using AccountServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AccountServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserAccountController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApiService _apiService;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public UserAccountController(AppDbContext context, ApiService apiService, TokenService tokenService, TokenValidator validator)
    {
        _context = context;
        _apiService = apiService;
        _tokenService = tokenService;
        _tokenValidator = validator;
    }
    
    [HttpPost]
    [Route("CreateAccount")]
    public CreateUserAccountPacketResponse CreateAccount([FromBody] CreateUserAccountPacketRequired required)
    {
        var res = new CreateUserAccountPacketResponse();
        var account = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserAccount == required.UserAccount);

        if (account == null)
        {
            var newUser = new User
            {
                UserAccount = required.UserAccount,
                UserName = "",
                Password = required.Password,
                Role = UserRole.User,
                State = UserState.Deactivate,
                CreatedAt = DateTime.UtcNow,
                RankPoint = 0,
                Gold = 500,
                Gem = 0
            };
            
            _context.User.Add(newUser);
            var success = _context.SaveChangesExtended(); // 이 때 UserId가 생성
            newUser.UserName = $"Player{newUser.UserId}";
            res.CreateOk = success;
            
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
            
            _context.SaveChangesExtended();
        }
        else
        {
            res.CreateOk = false;
            res.Message = "Duplicate ID";
        }
        
        return res;
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
    
    [HttpPost]
    [Route("Login")]
    public LoginUserAccountPacketResponse Login([FromBody] LoginUserAccountPacketRequired required)
    {
        var res = new LoginUserAccountPacketResponse();
        var account = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserAccount == required.UserAccount && user.Password == required.Password);

        if (account == null)
        {
            res.LoginOk = false;
        }
        else
        {
            var tokens = _tokenService.GenerateTokens(account.UserId);
            res.AccessToken = tokens.AccessToken;
            res.RefreshToken = tokens.RefreshToken;
            res.LoginOk = true;
            account.State = UserState.Activate;
            account.Act = UserAct.InLobby;
            _context.SaveChangesExtended();
        }
        
        return res;
    }

    [HttpPost]
    [Route("GetUserIdByAccount")]
    public GetUserIdPacketResponse GetUserIdByAccount([FromBody] GetUserIdPacketRequired required)
    {
        var res = new GetUserIdPacketResponse();
        var account = _context.User
            .AsNoTracking()
            .FirstOrDefault(user => user.UserAccount == required.UserAccount);

        if (account == null)
        {
            res.UserId = -1;
        }
        else
        {
            res.UserId = account.UserId;
        }
        
        return res;
    }
    
    [HttpPost]
    [Route("RefreshToken")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequired request)
    {
        try
        {
            var tokens = _tokenValidator.RefreshAccessToken(request.RefreshToken);
            var response = new RefreshTokenResponse()
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken
            };
            return Ok(response);
        }
        catch (SecurityTokenException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }
    
    [HttpPut]
    [Route("ChangeActByMatchMaking")]
    public async Task<IActionResult> ChangeActByMatchMaking([FromBody] ChangeActPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new ChangeActPackerResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var user = _context.User
            .FirstOrDefault(user => user.UserId == userId);
        if (user == null) return NotFound();

        user.Act = required.Act;
        res.ChangeOk = true;
        _context.SaveChangesExtended();

        if (required.Act == UserAct.MatchMaking)
        {   
            // MatchMakingServer에 유저 정보 전달
            var matchPacket = new MatchMakingPacketRequired
            {
                UserId = user.UserId,
                Faction = required.Faction,
                RankPoint = user.RankPoint,
                RequestTime = DateTime.Now,
                MapId = required.MapId
            };
            await _apiService.SendRequestAsync<MatchMakingPacketRequired>(
                "MatchMaking/Match", matchPacket, HttpMethod.Post);
        }
        else
        {   
            // 매치 큐에서 해당 유저 제거
            var cancelPacket = new MatchCancelPacketRequired { UserId = user.UserId };
            await _apiService.SendRequestAsync<MatchCancelPacketRequired>(
                "MatchMaking/CancelMatch", cancelPacket, HttpMethod.Post);
        }
        
        return Ok(res);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AccountServer.DB;
using AccountServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
#pragma warning disable CS0472 // 이 형식의 값은 'null'과 같을 수 없으므로 식의 결과가 항상 동일합니다.

namespace AccountServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CollectionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    
    public CollectionController(AppDbContext context, TokenService tokenService, TokenValidator tokenValidator)
    {
        _context = context;
        _tokenService = tokenService;
        _tokenValidator = tokenValidator;
    }
    
    [HttpPost]
    [Route("InitCards")]
    public IActionResult InitCards([FromBody] InitCardsPacketRequired required)
    {
        if (required.Environment == Env.Local)
        {
            return InitCardsResponse();
        }
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitCardsResponse(principal);
    }

    private IActionResult InitCardsResponse(ClaimsPrincipal? principal = null)
    {
        var res = new InitCardsPacketResponse();
        
        // Assume the user ID is 1 if the environment is local
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);
        if (principal == null)
        {
            var tokens = _tokenService.GenerateTokens((int)userId!);
            res.AccessToken = tokens.AccessToken;
            res.RefreshToken = tokens.RefreshToken;
        }

        if (userId != null)
        {
            var units = _context.Unit.AsNoTracking().ToList();
            var userUnitIds = _context.UserUnit.AsNoTracking()
                .Where(userUnit => userUnit.UserId == userId && userUnit.Count > 0)
                .Select(userUnit => userUnit.UnitId)
                .ToList();
            var ownedCardList = new List<UnitInfo>();
            var notOwnedCardList = new List<UnitInfo>();
            
            foreach (var unit in units)
            {
                if (userUnitIds.Contains(unit.UnitId))
                {
                    ownedCardList.Add(new UnitInfo
                    {
                        Id = (int)unit.UnitId,
                        Class = unit.Class,
                        Level = unit.Level,
                        Species = (int)unit.Species,
                        Role = unit.Role,
                        Faction = unit.Faction,
                        Region = unit.Region
                    });
                }
                else if (ownedCardList.All(unitInfo => unitInfo.Species != (int)unit.Species) && unit.Level == 3)
                {
                    notOwnedCardList.Add(new UnitInfo
                    {
                        Id = (int)unit.UnitId,
                        Class = unit.Class,
                        Level = unit.Level,
                        Species = (int)unit.Species,
                        Role = unit.Role,
                        Faction = unit.Faction,
                        Region = unit.Region
                    });
                }
            }
            
            res.OwnedCardList = ownedCardList;
            res.NotOwnedCardList = notOwnedCardList;
            res.GetCardsOk = true;
        }
        else
        {
            res.GetCardsOk = false;
        }

        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitSheep")]
    public IActionResult InitSheep([FromBody] InitSheepPacketRequired required)
    {
        if (required.Environment == Env.Local)
        {
            return InitSheepResponse();
        }
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitSheepResponse(principal);
    }
    
    public IActionResult InitSheepResponse(ClaimsPrincipal? principal = null)
    {
        var res = new InitSheepPacketResponse();
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var manySheep = _context.Sheep.AsNoTracking().ToList();
            var userSheepIds = _context.UserSheep.AsNoTracking()
                .Where(userSheep => userSheep.UserId == userId)
                .Select(userSheep => userSheep.SheepId)
                .ToList();
            var ownedSheepList = new List<SheepInfo>();
            var notOwnedSheepList = new List<SheepInfo>();
            
            foreach (var sheep in manySheep)
            {
                if (userSheepIds.Contains(sheep.SheepId))
                {
                    ownedSheepList.Add(new SheepInfo
                    {
                        Id = (int)sheep.SheepId,
                        Class = sheep.Class
                    });
                }
                else
                {
                    notOwnedSheepList.Add(new SheepInfo
                    {
                        Id = (int)sheep.SheepId,
                        Class = sheep.Class
                    });
                }
            }
            
            res.OwnedSheepList = ownedSheepList;
            res.NotOwnedSheepList = notOwnedSheepList;
            res.GetSheepOk = true;
        }
        else
        {
            res.GetSheepOk = false;
        }
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitEnchants")]
    public IActionResult InitEnchants([FromBody] InitEnchantPacketRequired required)
    {
        if (required.Environment == Env.Local)
        {
            return InitEnchantsResponse();
        }
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitEnchantsResponse(principal);
    }

    public IActionResult InitEnchantsResponse(ClaimsPrincipal? principal = null)
    {
        var res = new InitEnchantPacketResponse();
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var manyEnchant = _context.Enchant.AsNoTracking().ToList();
            var userEnchantIds = _context.UserEnchants.AsNoTracking()
                .Where(userEnchant => userEnchant.UserId == userId)
                .Select(userEnchant => userEnchant.EnchantId)
                .ToList();
            var ownedEnchantList = new List<EnchantInfo>();
            var notOwnedEnchantList = new List<EnchantInfo>();
            
            foreach (var enchant in manyEnchant)
            {
                if (userEnchantIds.Contains(enchant.EnchantId))
                {
                    ownedEnchantList.Add(new EnchantInfo
                    {
                        Id = (int)enchant.EnchantId,
                        Class = enchant.Class
                    });
                }
                else
                {
                    notOwnedEnchantList.Add(new EnchantInfo
                    {
                        Id = (int)enchant.EnchantId,
                        Class = enchant.Class
                    });
                }
            }
            
            res.OwnedEnchantList = ownedEnchantList;
            res.NotOwnedEnchantList = notOwnedEnchantList;
            res.GetEnchantOk = true;
        }
        else
        {
            res.GetEnchantOk = false;
        }

        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitCharacters")]
    public IActionResult InitCharacters([FromBody] InitCharacterPacketRequired required)
    {
        if (required.Environment == Env.Local)
        {
            return InitCharactersResponse();
        }
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitCharactersResponse(principal);   
    }

    public IActionResult InitCharactersResponse(ClaimsPrincipal? principal = null)
    {
        var res = new InitCharacterPacketResponse();
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var manyCharacter = _context.Character.AsNoTracking().ToList();
            var userCharacterIds = _context.UserCharacter.AsNoTracking()
                .Where(userCharacter => userCharacter.UserId == userId)
                .Select(userCharacter => userCharacter.CharacterId)
                .ToList();
            var ownedCharacterList = new List<CharacterInfo>();
            var notOwnedCharacterList = new List<CharacterInfo>();
            
            foreach (var character in manyCharacter)
            {
                if (userCharacterIds.Contains(character.CharacterId))
                {
                    ownedCharacterList.Add(new CharacterInfo
                    {
                        Id = (int)character.CharacterId,
                        Class = character.Class
                    });
                }
                else
                {
                    notOwnedCharacterList.Add(new CharacterInfo
                    {
                        Id = (int)character.CharacterId,
                        Class = character.Class
                    });
                }
            }
            
            res.OwnedCharacterList = ownedCharacterList;
            res.NotOwnedCharacterList = notOwnedCharacterList;
            res.GetCharacterOk = true;
        }
        else
        {
            res.GetCharacterOk = false;
        }

        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitMaterials")]
    public IActionResult InitMaterials([FromBody] InitMaterialPacketRequired required)
    {
        if (required.Environment == Env.Local)
        {
            return InitMaterialsResponse();
        }
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitMaterialsResponse(principal);
    }

    public IActionResult InitMaterialsResponse(ClaimsPrincipal? principal = null)
    {
        var res = new InitMaterialPacketResponse();
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            res.OwnedMaterialList = _context.UserMaterial.AsNoTracking()
                .Where(userMaterial => userMaterial.UserId == userId)
                .Join(_context.Material.AsNoTracking(),
                    userMaterial => userMaterial.MaterialId,
                    material => material.MaterialId,
                    (userMaterial, material) => new MaterialInfo
                    {
                        Id = (int)userMaterial.MaterialId,
                        Class = material.Class,
                        Count = userMaterial.Count
                    }).ToList();

            res.GetMaterialOk = true;
        }
        else
        {
            res.GetMaterialOk = false;
        }

        return Ok(res);
    }
    
    [HttpPost]
    [Route("GetDecks")]
    public IActionResult GetDeck([FromBody] GetInitDeckPacketRequired required)
    {
        if (required.Environment == Env.Local) return GetDeckResponse();
        
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        return principal == null ? Unauthorized() : InitCardsResponse(principal);
    }
    
    private IActionResult GetDeckResponse(ClaimsPrincipal? principal = null)
    {
        var res = new GetInitDeckPacketResponse();
        var userId = principal == null ? 1 : _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var deckInfoList = _context.Deck
                .AsNoTracking()
                .Where(deck => deck.UserId == userId)
                .Select(deck => new DeckInfo
                {
                    DeckId = deck.DeckId,
                    UnitInfo = _context.DeckUnit.AsNoTracking()
                        .Where(deckUnit => deckUnit.DeckId == deck.DeckId)
                        .Select(deckUnit => _context.Unit.AsNoTracking()
                            .FirstOrDefault(unit => unit.UnitId == deckUnit.UnitId))
                        .Where(unit => unit != null)
                        .Select(unit => new UnitInfo
                        {
                            Id = (int)unit!.UnitId,
                            Class = unit.Class,
                            Level = unit.Level,
                            Species = (int)unit.Species,
                            Role = unit.Role,
                            Faction = unit.Faction,
                            Region = unit.Region
                        }).ToArray(),
                    DeckNumber = deck.DeckNumber,
                    Faction = (int)deck.Faction,
                    LastPicked = deck.LastPicked
                }).ToList();
            
            var battleSetting = _context.BattleSetting.AsNoTracking()
                .Where(b => b.UserId == userId)
                .Join(_context.Sheep.AsNoTracking(),
                    b => b.SheepId,
                    s => (int)s.SheepId,
                    (b, sheep) => new { b, sheep })
                .Join(_context.Enchant.AsNoTracking(),
                    bs => bs.b.EnchantId,
                    e => (int)e.EnchantId,
                    (bs, enchant) => new { bs.b, bs.sheep, enchant })
                .Join(_context.Character.AsNoTracking(),
                    bse => bse.b.CharacterId,
                    c => (int)c.CharacterId,
                    (bse, character) => new BattleSettingInfo
                    {
                        SheepInfo = new SheepInfo
                        {
                            Id = (int)bse.sheep.SheepId,
                            Class = bse.sheep.Class
                        },
                        EnchantInfo = new EnchantInfo
                        {
                            Id = (int)bse.enchant.EnchantId,
                            Class = bse.enchant.Class
                        },
                        CharacterInfo = new CharacterInfo
                        {
                            Id = (int)character.CharacterId,
                            Class = character.Class
                        }
                    }
                )
                .FirstOrDefault();
            
            if (battleSetting == null)
            {
                res.GetDeckOk = false;
                return Ok(res);
            }
            
            res.DeckList = deckInfoList;
            res.BattleSetting = battleSetting;
            res.GetDeckOk = true;
        }
        else
        {
            res.GetDeckOk = false;
        }

        return Ok(res);
    }

    [HttpPost]
    [Route("GetSelectedDeck")]
    public IActionResult GetSelectedDeck([FromBody] GetSelectedDeckRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new GetSelectedDeckResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var deck = _context.Deck
                .AsNoTracking()
                .Where(deck =>
                    deck.UserId == userId && deck.Faction == (Faction)required.Faction && deck.DeckNumber == required.DeckNumber)
                .Select(deck => new DeckInfo
                {
                    DeckId = deck.DeckId,
                    UnitInfo = _context.DeckUnit.AsNoTracking()
                        .Where(deckUnit => deckUnit.DeckId == deck.DeckId)
                        .Select(deckUnit => _context.Unit.AsNoTracking()
                            .FirstOrDefault(unit => unit.UnitId == deckUnit.UnitId))
                        .Where(unit => unit != null)
                        .Select(unit => new UnitInfo
                        {
                            Id = (int)unit!.UnitId,
                            Class = unit.Class,
                            Level = unit.Level,
                            Species = (int)unit.Species,
                            Role = unit.Role,
                            Faction = unit.Faction,
                            Region = unit.Region
                        }).ToArray(),
                    DeckNumber = deck.DeckNumber,
                    Faction = (int)deck.Faction,
                    LastPicked = deck.LastPicked
                }).FirstOrDefault();
            
            if (deck != null)
            {
                res.GetSelectedDeckOk = true;
                res.Deck = deck;
            }
            else
            {
                res.GetSelectedDeckOk = false;
            }
        }
        else
        {
            res.GetSelectedDeckOk = false;
        }

        return Ok(res);
    }
        
    [HttpPut]
    [Route("UpdateDeck")]
    public IActionResult UpdateDeck([FromBody] UpdateDeckPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();

        var res = new UpdateDeckPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {   
            // Check if the user has the unit to be updated
            var targetDeckId = required.DeckId;
            var unitToBeDeleted = required.UnitIdToBeDeleted;
            var unitToBeUpdated = required.UnitIdToBeUpdated;
            var deckUnit = _context.DeckUnit
                .FirstOrDefault(deckUnit => 
                    deckUnit.DeckId == targetDeckId && 
                    deckUnit.UnitId == unitToBeDeleted && 
                    _context.UserUnit.Any(userUnit => userUnit.UnitId == unitToBeUpdated && userUnit.UserId == userId));
            
            if (deckUnit != null)
            {
                _context.DeckUnit.Remove(deckUnit);
                _context.SaveChangesExtended();
                
                var newDeckUnit = new DeckUnit { DeckId = targetDeckId, UnitId = unitToBeUpdated };
                _context.DeckUnit.Add(newDeckUnit);
                _context.SaveChangesExtended();
                
                res.UpdateDeckOk = 0;
            }
            else
            {
                res.UpdateDeckOk = 1;
            }
        }
        else
        {
            res.UpdateDeckOk = 2;
        }

        return Ok(res);
    }
    
    [HttpPut]
    [Route("UpdateLastDeck")]
    public IActionResult UpdateLastDeck([FromBody] UpdateLastDeckPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new UpdateLastDeckPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var targetDeck = required.LastPickedInfo;
            var targetDeckIds = targetDeck.Keys.ToList();
            var decks = _context.Deck
                .Where(deck => targetDeckIds.Contains(deck.DeckId)).ToList();
            foreach (var deck in decks) deck.LastPicked = targetDeck[deck.DeckId];
            _context.SaveChangesExtended();
            res.UpdateLastDeckOk = true;
        }
        else
        {
            res.UpdateLastDeckOk = false;
        }

        return Ok(res);
    }

    [HttpPut]
    [Route("UpdateBattleSetting")]
    public IActionResult UpdateBattleSetting([FromBody] UpdateBattleSettingPacketRequired required)
    {
        var principal = _tokenValidator.ValidateAccessToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new UpdateBattleSettingPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        
        if (userId != null)
        {
            var oldBattleSetting = _context.BattleSetting
                .FirstOrDefault(battleSetting => battleSetting.UserId == userId);
            var newBattleSetting = required.BattleSettingInfo;
            if (oldBattleSetting != null)
            {
                oldBattleSetting.SheepId = newBattleSetting.SheepInfo.Id;
                oldBattleSetting.EnchantId = newBattleSetting.EnchantInfo.Id;
                oldBattleSetting.CharacterId = newBattleSetting.CharacterInfo.Id;
                _context.SaveChangesExtended();
                res.UpdateBattleSettingOk = true;
            }
            else
            {
                res.UpdateBattleSettingOk = false;
            }
        }
        
        return Ok();
    }
}
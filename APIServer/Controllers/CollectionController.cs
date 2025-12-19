using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.Services;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS0472 // 이 형식의 값은 'null'과 같을 수 없으므로 식의 결과가 항상 동일합니다.

namespace ApiServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CollectionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly TokenValidator _tokenValidator;
    private readonly CachedDataProvider _cachedDataProvider;
    
    public CollectionController(
        AppDbContext context, 
        TokenService tokenService,
        TokenValidator tokenValidator,
        CachedDataProvider cachedDataProvider)
    {
        _context = context;
        _tokenService = tokenService;
        _tokenValidator = tokenValidator;
        _cachedDataProvider = cachedDataProvider;
    }

    [HttpPost]
    [Route("LoadInfo")]
    public IActionResult LoadInfo([FromBody] LoadInfoPacketRequired required)
    {
        var res = new LoadInfoPacketResponse
        {
            UnitInfos = _cachedDataProvider.GetUnitLookup().Values
                .Select(unit => new UnitInfo
                {
                    Id = unit.Id,
                    Class = unit.Class,
                    Level = unit.Level,
                    Species = unit.Species,
                    Role = unit.Role,
                    Faction = unit.Faction,
                    Region = unit.Region
                }).ToList(),
            
            SheepInfos = _cachedDataProvider.GetSheepLookup().Values
                .Select(sheep => new SheepInfo
                {
                    Id = sheep.Id,
                    Class = sheep.Class
                }).ToList(),
            
            EnchantInfos = _cachedDataProvider.GetEnchantLookup().Values
                .Select(enchant => new EnchantInfo
                {
                    Id = enchant.Id,
                    Class = enchant.Class
                }).ToList(),
            
            CharacterInfos = _cachedDataProvider.GetCharacterLookup().Values
                .Select(character => new CharacterInfo
                {
                    Id = character.Id,
                    Class = character.Class
                }).ToList(),
            
            MaterialInfos = _cachedDataProvider.GetMaterialLookup().Values
                .Select(material => new MaterialInfo
                {
                    Id = material.Id,
                    Class = material.Class,
                })
                .ToList(),
            
            ReinforcePoints = _cachedDataProvider.GetReinforcePoints().Select(kv => new ReinforcePointInfo
            {
                Class = kv.Key.Item1,
                Level = kv.Key.Item2,
                Point = kv.Value
            }).ToList(),
            
            CraftingMaterials = _cachedDataProvider.GetUnitMaterialLookup().Select(kv => new UnitMaterialInfo
            {
                UnitId = kv.Key,
                Materials = kv.Value.Select(um => new OwnedMaterialInfo
                {
                    MaterialInfo = um.MaterialInfo,
                    Count = um.Count
                }).ToList()
            }).ToList(),
            
            LoadInfoOk = true
        };

        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitCards")]
    public async Task<IActionResult> InitCards([FromBody] InitCardsPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new InitCardsPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();
        
        var unitById = _cachedDataProvider.GetUnitLookup(); 
        var rows = await _context.UserUnit.AsNoTracking()
            .Where(uu => uu.UserId == userId && uu.Count > 0)
            .OrderBy(uu => uu.UnitId)
            .Select(uu => new { uu.UnitId, uu.Count })
            .ToListAsync();

        var ownedCardList = new List<OwnedUnitInfo>(rows.Count);

        foreach (var row in rows)
        {
            var unitId = (int)row.UnitId;
            if (!unitById.TryGetValue(unitId, out var unitInfo)) continue;

            ownedCardList.Add(new OwnedUnitInfo
            {
                UnitInfo = unitInfo,
                Count = row.Count
            });
        }

        res.OwnedCardList = ownedCardList;
        res.GetCardsOk = true;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitSheep")]
    public async Task<IActionResult> InitSheep([FromBody] InitSheepPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new InitSheepPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var sheepById = _cachedDataProvider.GetSheepLookup();
        var rows = await _context.UserSheep.AsNoTracking()
            .Where(us => us.UserId == userId && us.Count > 0)
            .OrderBy(us => us.SheepId)
            .Select(us => new { us.SheepId, us.Count })
            .ToListAsync();

        var owned = new List<OwnedSheepInfo>(rows.Count);

        foreach (var row in rows)
        {
            var sheepId = (int)row.SheepId;
            if (!sheepById.TryGetValue(sheepId, out var sheepInfo)) continue;

            owned.Add(new OwnedSheepInfo
            {
                SheepInfo = sheepInfo,
                Count = row.Count
            });
        }

        res.OwnedSheepList = owned;
        res.GetSheepOk = true;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitEnchants")]
    public async Task<IActionResult> InitEnchants([FromBody] InitEnchantPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new InitEnchantPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var enchantById = _cachedDataProvider.GetEnchantLookup();
        var rows = await _context.UserEnchant.AsNoTracking()
            .Where(ue => ue.UserId == userId && ue.Count > 0)
            .OrderBy(ue => ue.EnchantId)
            .Select(ue => new { ue.EnchantId, ue.Count })
            .ToListAsync();

        var owned = new List<OwnedEnchantInfo>(rows.Count);

        foreach (var row in rows)
        {
            var enchantId = (int)row.EnchantId;
            if (!enchantById.TryGetValue(enchantId, out var enchantInfo)) continue;
            
            owned.Add(new OwnedEnchantInfo
            {
                EnchantInfo = enchantInfo,
                Count = row.Count
            });
        }

        res.OwnedEnchantList = owned;
        res.GetEnchantOk = true;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitCharacters")]
    public async Task<IActionResult> InitCharacters([FromBody] InitCharacterPacketRequired required)
    { 
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new InitCharacterPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var characterById = _cachedDataProvider.GetCharacterLookup();
        var rows = await _context.UserCharacter.AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.Count > 0)
            .OrderBy(uc => uc.CharacterId)
            .Select(uc => new { uc.CharacterId, uc.Count })
            .ToListAsync();

        var owned = new List<OwnedCharacterInfo>(rows.Count);

        foreach (var row in rows)
        {
            var characterId = (int)row.CharacterId;
            if (!characterById.TryGetValue(characterId, out var characterInfo)) continue;

            owned.Add(new OwnedCharacterInfo
            {
                CharacterInfo = characterInfo,
                Count = row.Count
            });
        }
        
        res.OwnedCharacterList = owned;
        res.GetCharacterOk = true;
        
        return Ok(res);
    }
    
    [HttpPost]
    [Route("InitMaterials")]
    public async Task<IActionResult> InitMaterials([FromBody] InitMaterialPacketRequired required)
    { 
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new InitMaterialPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var materialById = _cachedDataProvider.GetMaterialLookup();
        var rows = await _context.UserMaterial.AsNoTracking()
            .Where(um => um.UserId == userId && um.Count > 0)
            .OrderBy(um => um.MaterialId)
            .Select(um => new { um.MaterialId, um.Count })
            .ToListAsync();

        var owned = new List<OwnedMaterialInfo>(rows.Count);
        
        foreach (var row in rows)
        {
            var materialId = (int)row.MaterialId;
            if (!materialById.TryGetValue(materialId, out var materialInfo)) continue;

            owned.Add(new OwnedMaterialInfo
            {
                MaterialInfo = materialInfo,
                Count = row.Count
            });
        }

        res.OwnedMaterialList = owned;
        res.GetMaterialOk = true;
        
        return Ok(res);
    }

    [HttpPost]
    [Route("GetDecks")]
    public async Task<IActionResult> GetDeck([FromBody] GetInitDeckPacketRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new GetInitDeckPacketResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);
        if (userId == null) return Unauthorized();

        var unitById = _cachedDataProvider.GetUnitLookup();
        var sheepById = _cachedDataProvider.GetSheepLookup();
        var enchantById = _cachedDataProvider.GetEnchantLookup();
        var characterById = _cachedDataProvider.GetCharacterLookup();
        var decks = await _context.Deck.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                d.DeckId,
                d.DeckNumber,
                d.Faction,
                d.LastPicked
            }).ToListAsync();

        if (decks.Count == 0)
        {
            res.GetDeckOk = false;
            return Ok(res);
        }
        
        var deckIds = decks.Select(d => d.DeckId).ToList();
        var deckUnits = await _context.DeckUnit.AsNoTracking()
            .Where(du => deckIds.Contains(du.DeckId))
            .OrderBy(du => deckIds.Contains(du.DeckId))
            .Select(du => new { du.DeckId, du.UnitId })
            .ToListAsync();
        
        var unitInfosByDeck = deckUnits
            .GroupBy(x => x.DeckId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x =>
                {
                    var id = (int)x.UnitId;
                    return unitById.GetValueOrDefault(id) ?? new UnitInfo();
                }).ToArray()
            );    
        
        var deckInfoList = new List<DeckInfo>(decks.Count);
        foreach (var d in decks)
        {
            unitInfosByDeck.TryGetValue(d.DeckId, out var arr);
            deckInfoList.Add(new DeckInfo
            {
                DeckId = d.DeckId,
                UnitInfo = arr ?? Array.Empty<UnitInfo>(),
                DeckNumber = d.DeckNumber,
                Faction = (int)d.Faction,
                LastPicked = d.LastPicked
            });
        }
        
        var battleSettingRow = await _context.BattleSetting.AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => new { b.SheepId, b.EnchantId, b.CharacterId })
            .FirstOrDefaultAsync();

        if (battleSettingRow == null)
        {
            res.GetDeckOk = false;
            return Ok(res);
        }
        
        if (!sheepById.TryGetValue(battleSettingRow.SheepId, out var sheepInfo) ||
            !enchantById.TryGetValue(battleSettingRow.EnchantId, out var enchantInfo) ||
            !characterById.TryGetValue(battleSettingRow.CharacterId, out var characterInfo))
        {
            res.GetDeckOk = false;
            return Ok(res);
        }

        res.DeckList = deckInfoList;
        res.BattleSetting = new BattleSettingInfo
        {
            SheepInfo = sheepInfo,
            EnchantInfo = enchantInfo,
            CharacterInfo = characterInfo
        };
        res.GetDeckOk = true;
        return Ok(res);
    }

    [HttpPost]
    [Route("GetSelectedDeck")]
    public IActionResult GetSelectedDeck([FromBody] GetSelectedDeckRequired required)
    {
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
        if (principal == null) return Unauthorized();
        
        var res = new GetSelectedDeckResponse();
        var userId = _tokenValidator.GetUserIdFromAccessToken(principal);

        if (userId != null)
        {
            var deck = _context.Deck.AsNoTracking()
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
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
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
                // User does not have the unit to be updated, suspecting hacking
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
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
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
        var principal = _tokenValidator.ValidateToken(required.AccessToken);
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
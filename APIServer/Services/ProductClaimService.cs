using System.Data;
using System.Data.Common;
using System.Text;
using ApiServer.DB;
using ApiServer.Providers;
using Google.Api.Gax.Rest;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ApiServer.Services;

public class ProductClaimService
{
    private readonly AppDbContext _context;
    private readonly CachedDataProvider _cachedDataProvider;
    private readonly ILogger<RewardService> _logger;    
    
    private readonly Random _random = new();
    
    public ProductClaimService(AppDbContext context, CachedDataProvider cachedDataProvider, ILogger<RewardService> logger)
    {
        _context = context;
        _cachedDataProvider = cachedDataProvider;
        _logger = logger;
    }

    public sealed record ConsumeResult(int OpenCount, ProductType ProductType);
    public sealed class ResolveOneLevelResult
    {
        // UI에 보여줄 결과(컨테이너 포함)
        public Dictionary<(int compId, ProductType type), int> Resolved { get; } = new();
        
        // 메일로 보낼 상품들(컨테이너)
        public Dictionary<int, int> MailBackContainer { get; } = new();
    }
    
    /// <summary>
    /// Unpack products in mailbox to user product table.
    /// </summary>
    public async Task UnpackPackages(int userId, List<Mail> mails)
    {
        var mailCounts = mails
            .Where(m => m.ProductId != null)
            .GroupBy(m => m.ProductId!.Value)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Count());

        if (!mailCounts.Any()) return;

        var compLookup = _cachedDataProvider.GetCompLookup();
        var staged = new Dictionary<(int productId, AcquisitionPath path), (int deltaCount, ProductType type)>();
        
        void AddStage(int productId, AcquisitionPath path, int deltaCount, ProductType type)
        {
            if (deltaCount <= 0) return;

            var key = (productId, path);
            if (staged.TryGetValue(key, out var cur))
                staged[key] = (cur.deltaCount + deltaCount, type);
            else staged[key] = (deltaCount, type);
        }

        foreach (var (productId, count) in mailCounts)
        {
            if (!compLookup.TryGetValue(productId, out var comps) || comps.Count == 0)
            {
                _logger.LogError("ProductComposition not found. UserId={UserId}, ProductId={ProductId}, Count={Count}",
                    userId, productId, count);

                // 이 메일들 claimed 처리하지 말고 실패로 반환/예외
                throw new InvalidOperationException($"Missing ProductComposition for ProductId={productId}");
            }
            
            if (comps.All(pc => pc is { Guaranteed: true, IsSelectable: false }))
            {
                foreach (var comp in comps)
                {
                    AddStage(comp.CompositionId,
                        comp.ProductType == ProductType.Container ? AcquisitionPath.None : AcquisitionPath.Open,
                        comp.Count * count, 
                        comp.ProductType);
                }
            }
            else
            {
                AddStage(productId, AcquisitionPath.Open, count, ProductType.Container);
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                await UpsertUserProductStageAsync(userId, staged);
                // 메일 클레임
                foreach (var mail in mails) mail.Claimed = true;

                await _context.SaveChangesExtendedAsync();
                await tx.CommitAsync();
            }
            catch (Exception e)
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    private async Task UpsertUserProductStageAsync(int userId,
        Dictionary<(int productId, AcquisitionPath path), (int deltaCount, ProductType type)> staged)
    {
        if (staged.Count == 0) return;

        const int batchSize = 200;
        
        var items = staged.Where(kv => kv.Value.deltaCount > 0)
            .Select(kv => (kv.Key.productId, kv.Key.path, kv.Value.deltaCount, kv.Value.type))
            .ToList();

        for (int offset = 0; offset < items.Count; offset += batchSize)
        {
            var batch = items.Skip(offset).Take(batchSize).ToList();
            var sb = new StringBuilder();
            var parameters = new List<MySqlParameter>(batch.Count * 5);

            sb.Append(
                "INSERT INTO `User_Product` (`UserId`,`ProductId`,`AcquisitionPath`,`Count`,`ProductType`) VALUES ");

            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(',');
                
                sb.Append($"(@u{i},@p{i},@a{i},@c{i},@t{i})");

                parameters.Add(new MySqlParameter($"@u{i}", userId));
                parameters.Add(new MySqlParameter($"@p{i}", batch[i].productId));
                parameters.Add(new MySqlParameter($"@a{i}", (int)batch[i].path));
                parameters.Add(new MySqlParameter($"@c{i}", batch[i].deltaCount));
                parameters.Add(new MySqlParameter($"@t{i}", (int)batch[i].type));
                
            }
            
            sb.Append(@"
ON DUPLICATE KEY UPDATE
  `Count` = `Count` + VALUES(`Count`)
  -- ProductType은 원칙적으로 고정이므로 업데이트하지 않는 게 안전
  -- , `ProductType` = VALUES(`ProductType`)
;");

            await _context.Database.ExecuteSqlRawAsync(sb.ToString(), parameters.ToArray());
        }
    }
    
    public async Task<ClaimData> GetNextPopupAsync(int userId)
    {
        var data = new ClaimData();

        var compLookup = _cachedDataProvider.GetCompLookup();

        static bool IsSelectBox(List<ProductComposition> comps)
            => comps.Count > 0 && comps.All(pc => pc.Guaranteed == false && pc.IsSelectable == true);

        // 1) Open 큐 스캔(Select/Open 우선 판단용)
        var openList = await _context.UserProduct
            .AsNoTracking()
            .Where(up => up.UserId == userId &&
                         up.AcquisitionPath == AcquisitionPath.Open &&
                         up.Count > 0)
            .ToListAsync();

        // 2) Select 우선
        foreach (var up in openList)
        {
            if (!compLookup.TryGetValue(up.ProductId, out var comps) || comps.Count == 0)
                continue;

            if (IsSelectBox(comps))
            {
                data.RewardPopupType = RewardPopupType.Select;
                data.ProductInfos.Add(MapProductInfo(new UserProduct
                {
                    UserId = userId,
                    ProductId = up.ProductId,
                    Count = up.Count,
                    ProductType = up.ProductType,
                    AcquisitionPath = up.AcquisitionPath
                }));
                data.CompositionInfos.AddRange(comps.Select(MapCompositionInfo));
                return data;
            }
        }

        // 3) Random(Open) 우선: Container 1종만
        var randomUp = openList.FirstOrDefault(up => up.ProductType == ProductType.Container);
        if (randomUp != null)
        {
            data.RewardPopupType = RewardPopupType.Open;
            data.RandomProductInfos.Add(new RandomProductInfo
            {
                ProductInfo = MapProductInfo(new UserProduct
                {
                    UserId = userId,
                    ProductId = randomUp.ProductId,
                    Count = randomUp.Count,
                    ProductType = randomUp.ProductType,
                    AcquisitionPath = randomUp.AcquisitionPath
                }),
                Count = randomUp.Count
            });
            return data;
        }

        // 4) 여기부터는 "정리 단계": 원자(Open atomic) 지급 + None(mailback) 처리 후 Item 반환
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            // (A) Open 큐 원자 지급(컨테이너 제외)
            var atomicRows = await _context.UserProduct
                .Where(up => up.UserId == userId &&
                             up.AcquisitionPath == AcquisitionPath.Open &&
                             up.Count > 0 &&
                             up.ProductType != ProductType.Container)
                .ToListAsync();

            if (atomicRows.Count > 0)
            {
                var agg = atomicRows
                    .GroupBy(r => (r.ProductId, r.ProductType))
                    .Select(g => new { g.Key.ProductId, g.Key.ProductType, Count = g.Sum(x => x.Count) })
                    .Where(x => x.Count > 0)
                    .ToList();

                foreach (var row in agg)
                {
                    var pc = new ProductComposition
                    {
                        ProductId = row.ProductId,
                        CompositionId = row.ProductId,
                        ProductType = row.ProductType,
                        Count = row.Count
                    };

                    await StoreProductAsync(userId, pc);

                    var info = MapCompositionInfo(pc);
                    AddDisplayingComposition(userId, info);
                }

                _context.UserProduct.RemoveRange(atomicRows);
            }

            // (B) None 큐(mailback 컨테이너) → UI 표시 + 메일 생성 + 큐 제거
            var noneRows = await _context.UserProduct
                .Where(up => up.UserId == userId &&
                             up.AcquisitionPath == AcquisitionPath.None &&
                             up.Count > 0)
                .ToListAsync();

            if (noneRows.Count > 0)
            {
                foreach (var row in noneRows)
                {
                    // 컨테이너 자체를 "획득했다"는 표시(정리팝업에 포함)
                    AddDisplayingComposition(userId, new CompositionInfo
                    {
                        ProductId = row.ProductId,
                        CompositionId = row.ProductId,
                        ProductType = ProductType.Container,
                        Count = row.Count
                    });

                    // 메일은 count가 없으니 row.Count만큼 row 생성
                    for (int i = 0; i < row.Count; i++)
                    {
                        _context.Mail.Add(new Mail
                        {
                            UserId = userId,
                            Type = MailType.Product,
                            ProductId = row.ProductId,
                            Claimed = false
                        });
                    }
                }

                _context.UserProduct.RemoveRange(noneRows);
            }

            await _context.SaveChangesExtendedAsync();
            await tx.CommitAsync();
        });

        // (C) 최종 정리 팝업 구성: 누적 로그 Drain
        List<CompositionInfo> summary;
        if (_cachedDataProvider.DisplayingCompositions.TryGetValue(userId, out var disp))
        {
            summary = disp.Drain();
            _cachedDataProvider.DisplayingCompositions.TryRemove(userId, out _); // 권장
        }
        else summary = new List<CompositionInfo>();

        if (summary.Count == 0)
        {
            data.RewardPopupType = RewardPopupType.None;
            return data;
        }

        data.RewardPopupType = RewardPopupType.Item;
        data.CompositionInfos = summary;
        return data;
    }
    
    public ResolveOneLevelResult ResolveRandomOpenOneLevel(int productId, int openCount)
    {
        if (openCount <= 0) return new ResolveOneLevelResult();

        var compLookup = _cachedDataProvider.GetCompLookup();
        var probLookup = _cachedDataProvider.GetProbLookup();

        if (!compLookup.TryGetValue(productId, out var comps) || comps.Count == 0)
            throw new InvalidOperationException($"Missing compositions. pid={productId}");

        var result = new ResolveOneLevelResult();
        bool hasProb = probLookup.TryGetValue(productId, out var probList) && probList.Count > 0;
        bool isPureRandomBox = hasProb && comps.All(pc => pc is { Guaranteed: false, IsSelectable: false });

        for (int i = 0; i < openCount; i++)
        {
            if (isPureRandomBox)
            {
                var drawn = DrawOne(productId, comps, probList!);
                AddResolved(result, drawn.CompositionId, drawn.ProductType, drawn.Count);
                continue;
            }

            // mixed/guaranteed 계열: "각 컴포지션의 수량을 1레벨에서 확정" (재귀 없음)
            foreach (var c in comps)
            {
                // 선택형은 여기 오면 안 됨(SelectProduct로 처리)
                if (c.IsSelectable) continue;
                if (c is { Guaranteed: false, Count: > 0 })
                {
                    _logger.LogWarning("Non-guaranteed fixed count composition detected. pid={Pid}, compId={CompId}, cnt={Cnt}",
                        productId, c.CompositionId, c.Count);
                }

                int cnt = c.Count;

                // Count==0이면 count확률표로 수량 확정 (예: 42가 0~2)
                if (cnt == 0)
                {
                    // 확률표가 없으면 0으로 처리(또는 예외)
                    cnt = TryDrawCount(productId, c.CompositionId, probLookup, out var drawnCnt) ? drawnCnt : 0;
                }

                if (cnt > 0)
                    AddResolved(result, c.CompositionId, c.ProductType, cnt);
            }
        }

        return result;
    }

    private void AddResolved(ResolveOneLevelResult res, int compId, ProductType type, int count)
    {
        if (count <= 0) return;

        var key = (compId, type);   
        res.Resolved[key] = res.Resolved.TryGetValue(key, out var cur) ? cur + count : count;

        if (type == ProductType.Container)
        {
            res.MailBackContainer[compId] = res.MailBackContainer.TryGetValue(compId, out var c) ? c + count : count;
        }
    }

    private bool TryDrawCount(int productId, int compositionId,
        IReadOnlyDictionary<int, List<CompositionProbability>> probLookup, out int count)
    {
        count = 0;
        if (!probLookup.TryGetValue(productId, out var list) || list.Count == 0) return false;

        var rows = list.Where(cp => cp.CompositionId == compositionId).ToList();
        if (rows.Count == 0) return false;

        double r = _random.NextDouble();
        double cum = 0;
        foreach (var row in rows)
        {
            cum += row.Probability;
            if (r <= cum) { count = row.Count; return true; }
        }
        count = rows[^1].Count;
        return true;
    }
    
    private ProductComposition DrawOne(int productId, List<ProductComposition> compsForPid,
        List<CompositionProbability> probList)
    {
        double random = _random.NextDouble();
        double cum = 0;

        foreach (var prob in probList)
        {
            cum += prob.Probability;
            if (random <= cum)
            {
                var comp = compsForPid.FirstOrDefault(pc => pc.CompositionId == prob.CompositionId);
                if (comp == null)
                    throw new InvalidOperationException($"Probability points to missing composition. pid={productId}, compId={prob.CompositionId}");

                return new ProductComposition
                {
                    ProductId = productId,
                    CompositionId = comp.CompositionId,
                    ProductType = comp.ProductType,
                    Count = prob.Count,
                    Guaranteed = comp.Guaranteed,
                    IsSelectable = comp.IsSelectable
                };
            }
        }
        
        // fallback
        var last = probList[^1];
        var lastComp = compsForPid.First(c => c.CompositionId == last.CompositionId);
        return new ProductComposition
        {
            ProductId = productId,
            CompositionId = lastComp.CompositionId,
            ProductType = lastComp.ProductType,
            Count = last.Count,
            Guaranteed = lastComp.Guaranteed,
            IsSelectable = lastComp.IsSelectable
        };    
    }
    
    /// <summary>
    /// User_Product의 특정 (UserId, ProductId, AcquisitionPath) 행을 “소비(consume)”하는 원자적 메서드입니다.
    /// <para>
    /// 목적
    /// - 같은 랜덤박스를 유저가 연타(동시 클릭)하거나, 네트워크 재시도/중복 요청이 발생해도
    ///   “한 번만” 차감되도록 DB 레벨에서 동시성을 제어합니다.
    /// </para>
    /// <para>
    /// 핵심 원리 (트랜잭션 + Row Lock)
    /// </para>
    /// <para>
    /// 1) SELECT ... FOR UPDATE 로 대상 행을 조회합니다.
    ///    - InnoDB 기준: 해당 행에 배타 잠금(X lock)이 걸려 같은 행을 다른 트랜잭션이 동시에 수정/삭제/동일 FOR UPDATE 조회를 못합니다.
    ///    - 따라서 여기서 읽은 Count 값은 현재 트랜잭션이 끝날 때까지 “고정된 판단 근거”가 됩니다.
    /// </para>
    /// <para>
    /// 2) 같은 트랜잭션(dbTx)에서 Count를 차감하거나(Delete/Update) 삭제합니다.
    ///    - consumeAll=false => 1개만 차감
    ///    - consumeAll=true  => 현재 Count 전체 차감(=행 삭제)
    ///    - UPDATE 시 WHERE Count >= @d 조건을 추가하여, 혹시라도 예상 밖 상태에서 음수 차감이 되지 않도록 방어합니다.
    /// </para>
    /// <para>
    /// 반환값
    /// - OpenCount: 실제로 소비된 개수(0이면 소비 실패: 이미 없음/이미 열림/동시 요청에 의해 먼저 소비됨)
    /// - ProductType: User_Product에 저장된 ProductType(호출부에서 UI/분기 판단에 활용 가능)
    /// </para>
    /// <para>
    /// 전제 조건
    /// - 호출부에서 EF의 BeginTransactionAsync로 만든 트랜잭션의 DbTransaction(tx.GetDbTransaction())을 전달해야 합니다.
    /// - 호출부에서 _context.Database.OpenConnectionAsync()로 같은 커넥션을 열어둔 상태여야 합니다.
    ///   (본 메서드는 커넥션을 열지 않습니다.)
    /// </para>
    /// </summary>
    public async Task<ConsumeResult> ConsumeUserProductAsync(
        int userId,
        int productId,
        AcquisitionPath path,
        bool consumeAll,
        DbTransaction dbTx)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            throw new InvalidOperationException("DB connection must be opened by caller before ConsumeUserProductAsync.");

        // 1) Row 잠금(FOR UPDATE)으로 Count 고정
        int count;
        ProductType pType;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = dbTx;
            cmd.CommandText = @"
    SELECT `Count`, `ProductType`
    FROM `User_Product`
    WHERE `UserId`=@u AND `ProductId`=@p AND `AcquisitionPath`=@a
    FOR UPDATE;";

            var pu = cmd.CreateParameter(); pu.ParameterName = "@u"; pu.Value = userId;
            var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = productId;
            var pa = cmd.CreateParameter(); pa.ParameterName = "@a"; pa.Value = (int)path;
            cmd.Parameters.Add(pu); cmd.Parameters.Add(pp); cmd.Parameters.Add(pa);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new ConsumeResult(0, ProductType.Container);

            count = reader.GetInt32(0);
            pType = (ProductType)reader.GetInt32(1);
        }

        if (count <= 0) return new ConsumeResult(0, pType);

        var openCount = consumeAll ? count : 1;
        if (openCount <= 0 || count < openCount) return new ConsumeResult(0, pType);

        // 2) 같은 tx에서 차감(또는 삭제) → 동시 클릭/중복 요청 방지
        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.Transaction = dbTx;

            if (count == openCount)
            {
                cmd2.CommandText = @"
    DELETE FROM `User_Product`
    WHERE `UserId`=@u AND `ProductId`=@p AND `AcquisitionPath`=@a;";
            }
            else
            {
                cmd2.CommandText = @"
    UPDATE `User_Product`
    SET `Count` = `Count` - @d
    WHERE `UserId`=@u AND `ProductId`=@p AND `AcquisitionPath`=@a AND `Count` >= @d;";

                var pd = cmd2.CreateParameter(); pd.ParameterName = "@d"; pd.Value = openCount;
                cmd2.Parameters.Add(pd);
            }

            var pu2 = cmd2.CreateParameter(); pu2.ParameterName = "@u"; pu2.Value = userId;
            var pp2 = cmd2.CreateParameter(); pp2.ParameterName = "@p"; pp2.Value = productId;
            var pa2 = cmd2.CreateParameter(); pa2.ParameterName = "@a"; pa2.Value = (int)path;
            cmd2.Parameters.Add(pu2); cmd2.Parameters.Add(pp2); cmd2.Parameters.Add(pa2);

            var affected = await cmd2.ExecuteNonQueryAsync();
            if (affected == 0) return new ConsumeResult(0, pType);
        }

        return new ConsumeResult(openCount, pType);
    }
    
    public async Task StoreProductAsync(int userId, ProductComposition pc)
    {
        switch (pc.ProductType)
        {
            case ProductType.Unit:
            {
                var unitId = (UnitId)pc.CompositionId;
                var row = await _context.UserUnit.FindAsync(userId, unitId);
                if (row == null)
                {
                    _context.UserUnit.Add(new UserUnit { UserId = userId, UnitId = unitId, Count = pc.Count });
                }
                else
                {
                    row.Count += pc.Count;
                }
                break;
            }

            case ProductType.Material:
            {
                var materialId = (MaterialId)pc.CompositionId;
                var row = await _context.UserMaterial.FindAsync(userId, materialId);
                if (row == null)
                    _context.UserMaterial.Add(new UserMaterial { UserId = userId, MaterialId = materialId, Count = pc.Count });
                else
                    row.Count += pc.Count;
                break;
            }

            case ProductType.Enchant:
            {
                var enchantId = (EnchantId)pc.CompositionId;
                var row = await _context.UserEnchant.FindAsync(userId, enchantId);
                if (row == null)
                    _context.UserEnchant.Add(new UserEnchant { UserId = userId, EnchantId = enchantId, Count = pc.Count });
                else
                    row.Count += pc.Count;
                break;
            }

            case ProductType.Sheep:
            {
                var sheepId = (SheepId)pc.CompositionId;
                var row = await _context.UserSheep.FindAsync(userId, sheepId);
                if (row == null)
                    _context.UserSheep.Add(new UserSheep { UserId = userId, SheepId = sheepId, Count = pc.Count });
                else
                    row.Count += pc.Count;
                break;
            }

            case ProductType.Character:
            {
                var characterId = (CharacterId)pc.CompositionId;
                var row = await _context.UserCharacter.FindAsync(userId, characterId);
                if (row == null)
                    _context.UserCharacter.Add(new UserCharacter { UserId = userId, CharacterId = characterId, Count = pc.Count });
                else
                    row.Count += pc.Count;
                break;
            }

            case ProductType.Gold:
            {
                var stats = await _context.UserStats.FindAsync(userId);
                if (stats != null) stats.Gold += pc.Count;
                break;
            }

            case ProductType.Spinel:
            {
                var stats = await _context.UserStats.FindAsync(userId);
                if (stats != null) stats.Spinel += pc.Count;
                break;
            }

            case ProductType.Exp:
            {
                var stats = await _context.UserStats.FindAsync(userId);
                if (stats != null)
                {
                    var level = stats.UserLevel;
                    stats.Exp += pc.Count;
                    if (stats.Exp >= _cachedDataProvider.GetExpSnapshots()[level])
                    {
                        stats.UserLevel++;
                        stats.Exp -= _cachedDataProvider.GetExpSnapshots()[level];
                    }
                }
                break;
            }

            case ProductType.Subscription:
            {
                // 이 케이스는 “유일 키”가 (UserId, SubscriptionType) 같은 형태일 가능성이 높음.
                // 해당 엔티티 PK/유니크 설정에 맞춰 FindAsync로 바꾸거나,
                // 지금처럼 조건 조회를 하되 Async로 바꾸는 정도가 최소 수정.
                var nowUtc = DateTime.UtcNow;
                var lifetimeUtc = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                var subType = SubscriptionType.AdsRemover;

                var existingActiveSub = await _context.UserSubscription
                    .FirstOrDefaultAsync(us =>
                        us.UserId == userId &&
                        us.SubscriptionType == subType &&
                        us.IsCanceled == false &&
                        us.ExpiresAtUtc > nowUtc);

                if (existingActiveSub == null)
                {
                    _context.UserSubscription.Add(new UserSubscription
                    {
                        UserId = userId,
                        SubscriptionType = subType,
                        CreatedAtUtc = nowUtc,
                        ExpiresAtUtc = lifetimeUtc,
                        IsCanceled = false,
                        IsTrial = false,
                    });

                    _context.UserSubscriptionHistory.Add(new UserSubscriptionHistory
                    {
                        UserId = userId,
                        SubscriptionType = subType,
                        FromUtc = nowUtc,
                        ToUtc = lifetimeUtc,
                        EventType = SubscriptionEvent.Started
                    });
                }
                break;
            }

            case ProductType.Container:
                break;
        }
    }
    
    public ProductInfo MapProductInfo(UserProduct userProduct)
    {
        var product = _cachedDataProvider.GetProducts().FirstOrDefault(p => p.ProductId == userProduct.ProductId);
        return new ProductInfo
        {
            ProductId = userProduct.ProductId,
            Compositions = _cachedDataProvider.GetProductCompositions()
                .Where(pc => pc.ProductId == userProduct.ProductId)
                .Select(MapCompositionInfo)
                .ToList(),
            ProductType = userProduct.ProductType,
            Price = product?.Price ?? 0,
            Category = product?.Category ?? ProductCategory.None,
            CurrencyType = product?.Currency ?? CurrencyType.None,
            ProductCode = product?.ProductCode ?? string.Empty,
        };
    }
    
    public CompositionInfo MapCompositionInfo(ProductComposition pc) => new()
    {
        ProductId = pc.ProductId,
        CompositionId = pc.CompositionId,
        ProductType = pc.ProductType,
        Count = pc.Count,
        MinCount = _cachedDataProvider.GetProbabilityLookup()
            .GetValueOrDefault((pc.ProductId, pc.CompositionId), (0, 0)).Item1,
        MaxCount = _cachedDataProvider.GetProbabilityLookup()
            .GetValueOrDefault((pc.ProductId, pc.CompositionId), (0, 0)).Item2,
        Guaranteed = pc.Guaranteed,
        IsSelectable = pc.IsSelectable
    };
    
    public void AddDisplayingComposition(int userId, CompositionInfo compositionInfo)
    {
        var dispCompositions = _cachedDataProvider.DisplayingCompositions.GetOrAdd(userId, 
            _ => new DisplayingCompositions());
        dispCompositions.AddOrIncrement(compositionInfo);
    }
}
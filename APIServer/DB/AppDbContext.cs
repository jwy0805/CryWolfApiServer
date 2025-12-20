using Microsoft.EntityFrameworkCore;

namespace ApiServer.DB;

public class AppDbContext : DbContext
{
    public DbSet<TempUser> TempUser { get; set; }
    public DbSet<User> User { get; set; }
    public DbSet<UserAuth> UserAuth { get; set; }
    public DbSet<UserStats> UserStats { get; set; }
    public DbSet<UserMatch> UserMatch { get; set; }
    public DbSet<UserTutorial> UserTutorial { get; set; }
    public DbSet<Friend> Friend { get; set; }
    public DbSet<Mail> Mail { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<EventNotice> EventNotice { get; set; }
    public DbSet<EventNoticeLocalization> EventNoticeLocalization { get; set; }
    public DbSet<Unit> Unit { get; set; }
    public DbSet<UserUnit> UserUnit { get; set; }
    public DbSet<Deck> Deck { get;set; }
    public DbSet<DeckUnit> DeckUnit { get; set; }
    public DbSet<Sheep> Sheep { get; set; }
    public DbSet<Enchant> Enchant { get; set; }
    public DbSet<Character> Character { get; set; }
    public DbSet<Material> Material { get; set; }
    public DbSet<Product> Product { get; set; }
    public DbSet<DailyProduct> DailyProduct { get; set; }
    public DbSet<DailyFreeProduct> DailyFreeProduct { get; set; }
    public DbSet<UserDailyProduct> UserDailyProduct { get; set; }
    public DbSet<Transaction> Transaction { get; set; }
    public DbSet<TransactionReceiptFailure> TransactionReceiptFailure { get; set; }
    public DbSet<ProductComposition> ProductComposition { get; set; }
    public DbSet<CompositionProbability> CompositionProbability { get; set; }
    public DbSet<Stage> Stage { get; set; }
    public DbSet<StageEnemy> StageEnemy { get; set; }
    public DbSet<StageReward> StageReward { get; set; }
    public DbSet<UserStage> UserStage { get; set; }
    public DbSet<UserProduct> UserProduct { get; set; }
    public DbSet<UserSubscription> UserSubscription { get; set; }
    public DbSet<UserSubscriptionHistory> UserSubscriptionHistory { get; set; }
    public DbSet<UserSheep> UserSheep { get; set; }
    public DbSet<UserEnchant> UserEnchant { get; set; }
    public DbSet<UserCharacter> UserCharacter { get; set; }
    public DbSet<UnitMaterial> UnitMaterial { get; set; }
    public DbSet<UserMaterial> UserMaterial { get; set; }
    public DbSet<BattleSetting> BattleSetting { get; set; }
    public DbSet<ReinforcePoint> ReinforcePoint { get; set; }
    public DbSet<ExpTable> Exp { get; set; }
    public DbSet<ExpReward> ExpReward { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<TempUser>().HasKey(user => new { user.TempUserAccount, user.CreatedAt });
        
        builder.Entity<User>().Property(u => u.LastPingTime).IsRequired(false);
        builder.Entity<User>().HasIndex(u => u.UserTag).IsUnique();
        
        builder.Entity<UserAuth>().Property(u => u.LinkedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Entity<UserAuth>().HasIndex(ua => new { ua.Provider, ua.UserAccount }).IsUnique();
        builder.Entity<UserAuth>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAuths)
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserStats>().HasKey(t => new { t.UserId });
        builder.Entity<UserStats>()
            .HasOne(us => us.User)
            .WithOne(u => u.UserStats)
            .HasForeignKey<UserStats>(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserMatch>().HasOne<User>().WithOne().HasForeignKey<UserMatch>(um => um.UserId);
        builder.Entity<UserMatch>()
            .HasOne(um => um.User)
            .WithMany(u => u.UserMatches)
            .HasForeignKey(um => um.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserTutorial>().HasIndex(ut => new { ut.UserId, ut.TutorialType }).IsUnique();
        builder.Entity<UserTutorial>().Property(ut => ut.TutorialType)
            .HasConversion(v => (int)v, v => (TutorialType)v);
        
        builder.Entity<UserTutorial>()
            .HasOne(ut => ut.User)
            .WithMany(u => u.UserTutorials)
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Friend>().HasKey(t => new { t.UserId, t.FriendId });
        builder.Entity<Friend>()
            .ToTable(t => t.HasCheckConstraint("CK_Friend_Order", "`UserId` < `FriendId`"));
        builder.Entity<Friend>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);    
        builder.Entity<Friend>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.FriendId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventNotice>(entity =>
        {
            entity.HasKey(e => e.EventNoticeId);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.HasIndex(e => new { e.IsActive, e.NoticeType, e.CreatedAt });
            entity.HasMany(e => e.Localizations)
                .WithOne(l => l.EventNotice)
                .HasForeignKey(l => l.EventNoticeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventNoticeLocalization>(entity =>
        {
            entity.HasKey(enl => enl.EventNoticeLocalizationId);
            entity.Property(enl => enl.LanguageCode).HasMaxLength(5).IsRequired();
            entity.Property(enl => enl.Title).HasMaxLength(100).IsRequired();
            entity.Property(enl => enl.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(enl => new { enl.EventNoticeId, enl.LanguageCode });
        });
        
        builder.Entity<Unit>(entity =>
        {
            entity.Property(unit => unit.UnitId).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Class).HasConversion(v => (int)v, v => (UnitClass)v);
            entity.Property(unit => unit.Species).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Role).HasConversion(v => (int)v, v => (UnitRole)v);
            entity.Property(unit => unit.Faction).HasConversion(v => (int)v, v => (Faction)v);
            entity.Property(unit => unit.Region).HasConversion(v => (int)v, v => (UnitRegion)v);
        });

        builder.Entity<BattleSetting>()
            .HasOne(bs => bs.User)
            .WithMany(u => u.BattleSettings)
            .HasForeignKey(bs => bs.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Deck>().HasIndex(d => new { d.UserId, d.Faction, d.DeckNumber }).IsUnique();
        builder.Entity<Deck>()
            .Property(d => d.DeckId)
            .ValueGeneratedOnAdd();
        builder.Entity<Deck>()
            .HasOne(d => d.User)
            .WithMany(u => u.Decks)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Sheep>(entity =>
        {
            entity.Property(sheep => sheep.SheepId).HasConversion(v => (int)v, v => (SheepId)v);
            entity.Property(sheep => sheep.Class).HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Enchant>(entity =>
        {
            entity.Property(enchant => enchant.EnchantId).HasConversion(v => (int)v, v => (EnchantId)v);
            entity.Property(enchant => enchant.Class).HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Character>(entity =>
        {
            entity.Property(character => character.CharacterId)
                .HasConversion(v => (int)v, v => (CharacterId)v);
            entity.Property(character => character.Class)
                .HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Material>(entity =>
        {
            entity.Property(material => material.MaterialId)
                .HasConversion(v => (int)v, v => (MaterialId)v);
            entity.Property(material => material.Class)
                .HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Product>()
            .HasOne(p => p.DailyProduct)
            .WithOne(dp => dp.Product)
            .HasForeignKey<DailyProduct>(dp => dp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Product>().Property(product => product.Currency)
            .HasConversion(v => (int)v, v => (CurrencyType)v);
        builder.Entity<Product>().Property(p => p.ProductType)
            .HasConversion((v => (int)v), v => (ProductType)v);
        
        builder.Entity<Mail>()
            .HasOne(m => m.User)
            .WithMany(u => u.Mails)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.TransactionId);

            entity.Property(t => t.TransactionId)
                .ValueGeneratedOnAdd();

            entity.Property(t => t.StoreTransactionId)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasIndex(t => new { t.StoreType, t.StoreTransactionId })
                .IsUnique();

            entity.HasIndex(t => new { t.UserId, t.PurchaseAt });

            entity.Property(t => t.Currency).HasConversion<int>();
            entity.Property(t => t.Status).HasConversion<int>();
            entity.Property(t => t.CashCurrency).HasConversion<int>();
            entity.Property(t => t.StoreType).HasConversion<int>();
        });
        
        builder.Entity<Transaction>()
            .HasOne(t => t.Failure)
            .WithOne(f => f.Transaction)
            .HasForeignKey<TransactionReceiptFailure>(f => f.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TransactionReceiptFailure>()
            .Property(x => x.ReceiptHash)
            .HasColumnType("BINARY(32)");

        builder.Entity<TransactionReceiptFailure>()
            .Property(x => x.ReceiptRawGzip)
            .HasColumnType("LONGBLOB");

        builder.Entity<TransactionReceiptFailure>()
            .Property(x => x.ResponseRawGzip)
            .HasColumnType("LONGBLOB");

        builder.Entity<DailyProduct>().HasKey(dp => dp.ProductId);
        builder.Entity<DailyProduct>().Property(dp => dp.Class)
            .HasConversion(v => (int)v, v => (UnitClass)v);

        builder.Entity<DailyFreeProduct>().HasKey(dp => dp.ProductId);
        builder.Entity<DailyFreeProduct>().Property(dp => dp.Class)
            .HasConversion(v => (int)v, v => (UnitClass)v);
        
        builder.Entity<UserDailyProduct>()
            .HasOne(udp => udp.Product)
            .WithMany()
            .HasForeignKey(udp => udp.ProductId);
        
        builder.Entity<UserDailyProduct>()
            .HasOne(udp => udp.User)
            .WithMany()                        
            .HasForeignKey(udp => udp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserDailyProduct>().HasKey(udp => new { udp.UserId, udp.Slot });
        
        builder.Entity<ProductComposition>().HasKey(pc => new { pc.ProductId, pc.CompositionId });
        builder.Entity<ProductComposition>().Property(pc => pc.ProductType)
            .HasConversion(v => (int)v, v => (ProductType)v);
        
        builder.Entity<CompositionProbability>().HasKey(cp => new { cp.ProductId, cp.CompositionId, cp.Count });
        
        builder.Entity<StageEnemy>().HasKey(se => new { se.StageId, se.UnitId });
        
        builder.Entity<StageReward>().HasKey(sr => new { sr.StageId, sr.ProductId, sr.ProductType });
        builder.Entity<StageReward>().Property(sr => sr.ProductType)
            .HasConversion(v => (int)v, v => (ProductType)v);
        
        builder.Entity<UserStage>().HasKey(us => new { us.UserId, us.StageId });
        builder.Entity<UserStage>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserStages)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserProduct>().HasKey(up => new { up.UserId, up.ProductId, up.AcquisitionPath });
        builder.Entity<UserProduct>().Property(up => up.ProductType)
            .HasConversion(v => (int)v, v => (ProductType)v);
        builder.Entity<UserProduct>()
            .HasOne(up => up.User)
            .WithMany(u => u.UserProducts)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserSubscription>().HasKey(us => new { us.UserId, us.SubscriptionType });
        builder.Entity<UserSubscription>().Property(us => us.SubscriptionType)
            .HasConversion(v => (int)v, v => (SubscriptionType)v);
        builder.Entity<UserSubscription>().HasIndex(u => u.ExpiresAtUtc);
        builder.Entity<UserSubscription>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserSubscriptions)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserSubscriptionHistory>().HasKey(ush => ush.HistoryId);               
        builder.Entity<UserSubscriptionHistory>().HasIndex(ush => ush.UserId);                
        builder.Entity<UserSubscriptionHistory>().HasIndex(ush => ush.EventType);
        builder.Entity<UserSubscriptionHistory>().Property(ush => ush.SubscriptionType)
            .HasConversion(v => (byte)v, v => (SubscriptionType)v);
        builder.Entity<UserSubscriptionHistory>().Property(ush => ush.EventType)
            .HasConversion(v => (byte)v, v => (SubscriptionEvent)v);
        
        builder.Entity<DeckUnit>().HasKey(deckUnit => new { deckUnit.DeckId, deckUnit.UnitId });
        builder.Entity<DeckUnit>()
            .HasOne(du => du.Deck)
            .WithMany(d => d.DeckUnits)
            .HasForeignKey(d => d.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DeckUnit>()
            .HasOne<Unit>()
            .WithMany()
            .HasForeignKey(du => du.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<DeckUnit>().Property(unit => unit.UnitId)
            .HasConversion(v => (int)v, v => (UnitId)v);
        
        builder.Entity<UserUnit>().HasKey(userUnit => new { userUnit.UserId, userUnit.UnitId });
        builder.Entity<UserUnit>().Property(unit => unit.UnitId)
            .HasConversion(v => (int)v, v => (UnitId)v);
        builder.Entity<UserUnit>()
            .HasOne(uu => uu.User)
            .WithMany(u => u.UserUnits)
            .HasForeignKey(uu => uu.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserSheep>().HasKey(userSheep => new { userSheep.UserId, userSheep.SheepId });
        builder.Entity<UserSheep>().Property(sheep => sheep.SheepId)
            .HasConversion(v => (int)v, v => (SheepId)v);
        builder.Entity<UserSheep>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserSheep)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserEnchant>().HasKey(userEnchant => new { userEnchant.UserId, userEnchant.EnchantId });
        builder.Entity<UserEnchant>().Property(enchant => enchant.EnchantId)
            .HasConversion(v => (int)v, v => (EnchantId)v);
        builder.Entity<UserEnchant>()
            .HasOne(ue => ue.User)
            .WithMany(u => u.UserEnchants)
            .HasForeignKey(ue => ue.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserCharacter>().HasKey(userCharacter => new { userCharacter.UserId, userCharacter.CharacterId });
        builder.Entity<UserCharacter>().Property(character => character.CharacterId)
            .HasConversion(v => (int)v, v => (CharacterId)v);
        builder.Entity<UserCharacter>()
            .HasOne(uc => uc.User)
            .WithMany(u => u.UserCharacters)
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UnitMaterial>().HasKey(unitMaterial => new { unitMaterial.UnitId, unitMaterial.MaterialId });
        
        builder.Entity<UserMaterial>().HasKey(userMaterial => new { userMaterial.UserId, userMaterial.MaterialId });
        builder.Entity<UserMaterial>()
            .HasOne(um => um.User)
            .WithMany(u => u.UserMaterials)
            .HasForeignKey(um => um.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<BattleSetting>().HasKey(b => new { b.UserId, b.SheepId, b.EnchantId, b.CharacterId });
        
        builder.Entity<ExpTable>().HasKey(et => et.Level);
        builder.Entity<ExpTable>().Property(et => et.Level).ValueGeneratedNever();
        
        builder.Entity<ReinforcePoint>().HasKey(rr => new { rr.Class, rr.Level });

        builder.Entity<ExpReward>().HasKey(er => new { er.Level, er.ProductId });
        builder.Entity<ExpReward>().Property(er => er.ProductType)
            .HasConversion(v => (int)v, v => (ProductType)v);
    }
}
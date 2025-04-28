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
    public DbSet<UserDailyProduct> UserDailyProduct { get; set; }
    public DbSet<Transaction> Transaction { get; set; }
    public DbSet<ProductComposition> ProductComposition { get; set; }
    public DbSet<CompositionProbability> CompositionProbability { get; set; }
    public DbSet<Stage> Stage { get; set; }
    public DbSet<StageEnemy> StageEnemy { get; set; }
    public DbSet<StageReward> StageReward { get; set; }
    public DbSet<UserStage> UserStage { get; set; }
    public DbSet<UserProduct> UserProduct { get; set; }
    public DbSet<UserSheep> UserSheep { get; set; }
    public DbSet<UserEnchant> UserEnchant { get; set; }
    public DbSet<UserCharacter> UserCharacter { get; set; }
    public DbSet<UnitMaterial> UnitMaterial { get; set; }
    public DbSet<UserMaterial> UserMaterial { get; set; }
    public DbSet<BattleSetting> BattleSetting { get; set; }
    public DbSet<ReinforcePoint> ReinforcePoint { get; set; }
    public DbSet<ExpTable> Exp { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<TempUser>().HasKey(user => new { user.TempUserAccount, user.CreatedAt });
        builder.Entity<User>().Property(u => u.LastPingTime).IsRequired(false);
        builder.Entity<UserAuth>().Property(u => u.LinkedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Entity<UserAuth>().HasIndex(ua => new { ua.Provider, ua.UserAccount }).IsUnique();
        builder.Entity<UserStats>().HasKey(t => new { t.UserId });
        builder.Entity<UserMatch>().HasOne<User>().WithOne().HasForeignKey<UserMatch>(um => um.UserId);
        builder.Entity<UserTutorial>().HasKey(ut => new { ut.UserId, ut.TutorialType });
        builder.Entity<UserTutorial>(entity =>
        {
            entity.Property(ut => ut.TutorialType).HasConversion(v => (int)v, v => (TutorialType)v);
        });
        
        builder.Entity<Friend>().HasKey(t => new { t.UserId, t.FriendId });
        builder.Entity<Friend>()
            .ToTable(t => t.HasCheckConstraint("CK_Friend_Order", "`UserId` < `FriendId`"));
        
        builder.Entity<Unit>(entity =>
        {
            entity.Property(unit => unit.UnitId).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Class).HasConversion(v => (int)v, v => (UnitClass)v);
            entity.Property(unit => unit.Species).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Role).HasConversion(v => (int)v, v => (UnitRole)v);
            entity.Property(unit => unit.Faction).HasConversion(v => (int)v, v => (Faction)v);
            entity.Property(unit => unit.Region).HasConversion(v => (int)v, v => (UnitRegion)v);
        });

        builder.Entity<Deck>().HasIndex(d => new { d.UserId, d.Faction, d.DeckNumber }).IsUnique();
        
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
            entity.Property(character => character.Class).HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Material>(entity =>
        {
            entity.Property(material => material.MaterialId).HasConversion(v => (int)v, v => (MaterialId)v);
            entity.Property(material => material.Class).HasConversion(v => (int)v, v => (UnitClass)v);
        });

        builder.Entity<Product>()
            .HasOne(p => p.DailyProduct)
            .WithOne(dp => dp.Product)
            .HasForeignKey<DailyProduct>(dp => dp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Product>(entity =>
        {
            entity.Property(product => product.Currency).HasConversion(v => (int)v, v => (CurrencyType)v);
        });
        
        builder.Entity<Transaction>().HasKey(t => new { t.TransactionTimestamp, t.UserId });
        builder.Entity<Transaction>(entity =>
        {
            entity.Property(t => t.Currency)
                .HasConversion(v => (int)v, v => (CurrencyType)v);
            entity.Property(t => t.Status)
                .HasConversion(v => (int)v, v => (TransactionStatus)v);
            entity.Property(t => t.CashCurrency)
                .HasConversion(v => (int)v, v => (CashCurrencyType)v);
        });

        builder.Entity<DailyProduct>().HasKey(dp => dp.ProductId);
        
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
        
        builder.Entity<CompositionProbability>().HasKey(cp => new { cp.ProductId, cp.CompositionId, cp.Count });
        
        builder.Entity<StageEnemy>().HasKey(se => new { se.StageId, se.UnitId });
        builder.Entity<StageReward>().HasKey(sr => new { sr.StageId, sr.ProductId, sr.ProductType });
        builder.Entity<UserStage>().HasKey(us => new { us.UserId, us.StageId });
        
        builder.Entity<UserProduct>().HasKey(up => new { up.UserId, up.ProductId });
        
        builder.Entity<DeckUnit>().HasKey(deckUnit => new { deckUnit.DeckId, deckUnit.UnitId });
        builder.Entity<DeckUnit>()
            .HasOne<Unit>()
            .WithMany()
            .HasForeignKey(du => du.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DeckUnit>(entity =>
        {
            entity.Property(unit => unit.UnitId)
                .HasConversion(v => (int)v, v => (UnitId)v);
        });
        
        builder.Entity<UserUnit>().HasKey(userUnit => new { userUnit.UserId, userUnit.UnitId });
        builder.Entity<UserUnit>(entity =>
        {
            entity.Property(unit => unit.UnitId)
                .HasConversion(v => (int)v, v => (UnitId)v);
        });
        
        builder.Entity<UserSheep>().HasKey(userSheep => new { userSheep.UserId, userSheep.SheepId });
        builder.Entity<UserSheep>(entity =>
        {
            entity.Property(sheep => sheep.SheepId)
                .HasConversion(v => (int)v, v => (SheepId)v);
        });
        
        builder.Entity<UserEnchant>().HasKey(userEnchant => new { userEnchant.UserId, userEnchant.EnchantId });
        builder.Entity<UserEnchant>(entity =>
        {
            entity.Property(enchant => enchant.EnchantId)
                .HasConversion(v => (int)v, v => (EnchantId)v);
        });
        
        builder.Entity<UserCharacter>().HasKey(userCharacter => new { userCharacter.UserId, userCharacter.CharacterId });
        builder.Entity<UserCharacter>(entity =>
        {
            entity.Property(character => character.CharacterId)
                .HasConversion(v => (int)v, v => (CharacterId)v);
        });

        builder.Entity<UnitMaterial>().HasKey(unitMaterial => new { unitMaterial.UnitId, unitMaterial.MaterialId });
        
        builder.Entity<UserMaterial>().HasKey(userMaterial => new { userMaterial.UserId, userMaterial.MaterialId });
        
        builder.Entity<BattleSetting>().HasKey(b => new { b.UserId, b.SheepId, b.EnchantId, b.CharacterId });
        
        builder.Entity<ExpTable>().HasKey(e => e.Level);
        builder.Entity<ExpTable>().Property(e => e.Level).ValueGeneratedNever();
        
        builder.Entity<ReinforcePoint>().HasKey(rr => new { rr.Class, rr.Level });

        builder.Entity<ExpTable>().HasKey(et => new { et.Level });
        
        // Delete
        builder.Entity<BattleSetting>()
            .HasOne(bs => bs.User)
            .WithMany(u => u.BattleSettings)
            .HasForeignKey(bs => bs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Deck>()
            .HasOne(d => d.User)
            .WithMany(u => u.Decks)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<DeckUnit>()
            .HasOne(du => du.Deck)
            .WithMany(d => d.DeckUnits)
            .HasForeignKey(d => d.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

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
        
        builder.Entity<Mail>()
            .HasOne(m => m.User)
            .WithMany(u => u.Mails)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserAuth>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAuths)
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserCharacter>()
            .HasOne(uc => uc.User)
            .WithMany(u => u.UserCharacters)
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserEnchant>()
            .HasOne(ue => ue.User)
            .WithMany(u => u.UserEnchants)
            .HasForeignKey(ue => ue.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserMaterial>()
            .HasOne(um => um.User)
            .WithMany(u => u.UserMaterials)
            .HasForeignKey(um => um.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserMatch>()
            .HasOne(um => um.User)
            .WithMany(u => u.UserMatches)
            .HasForeignKey(um => um.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserProduct>()
            .HasOne(up => up.User)
            .WithMany(u => u.UserProducts)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserSheep>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserSheep)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserStage>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserStages)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserUnit>()
            .HasOne(uu => uu.User)
            .WithMany(u => u.UserUnits)
            .HasForeignKey(uu => uu.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserStats>()
            .HasOne(us => us.User)
            .WithOne(u => u.UserStats)
            .HasForeignKey<UserStats>(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<UserTutorial>()
            .HasOne(ut => ut.User)
            .WithMany(u => u.UserTutorials)
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
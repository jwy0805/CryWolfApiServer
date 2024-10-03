using Microsoft.EntityFrameworkCore;

namespace AccountServer.DB;

public class AppDbContext : DbContext
{
    public DbSet<User> User { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Unit> Unit { get; set; }
    public DbSet<UserUnit> UserUnit { get; set; }
    public DbSet<Deck> Deck { get;set; }
    public DbSet<DeckUnit> DeckUnit { get; set; }
    public DbSet<Sheep> Sheep { get; set; }
    public DbSet<Enchant> Enchant { get; set; }
    public DbSet<Character> Character { get; set; }
    public DbSet<Material> Material { get; set; }
    public DbSet<UserSheep> UserSheep { get; set; }
    public DbSet<UserEnchant> UserEnchants { get; set; }
    public DbSet<UserCharacter> UserCharacter { get; set; }
    public DbSet<UnitMaterial> UnitMaterial { get; set; }
    public DbSet<UserMaterial> UserMaterial { get; set; }
    public DbSet<BattleSetting> BattleSetting { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>().HasIndex(user => user.UserAccount).IsUnique();
        
        builder.Entity<Unit>(entity =>
        {
            entity.Property(unit => unit.UnitId).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Class).HasConversion(v => (int)v, v => (UnitClass)v);
            entity.Property(unit => unit.Species).HasConversion(v => (int)v, v => (UnitId)v);
            entity.Property(unit => unit.Role).HasConversion(v => (int)v, v => (UnitRole)v);
            entity.Property(unit => unit.Faction).HasConversion(v => (int)v, v => (Faction)v);
            entity.Property(unit => unit.Region).HasConversion(v => (int)v, v => (UnitRegion)v);
        });

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
        
        builder.Entity<DeckUnit>().HasKey(deckUnit => new { deckUnit.DeckId, deckUnit.UnitId });
        builder.Entity<DeckUnit>(entity =>
        {
            entity.Property(unit => unit.UnitId).HasConversion(
                v => (int)v, v => (UnitId)v);
        });
        
        builder.Entity<UserUnit>().HasKey(userUnit => new { userUnit.UserId, userUnit.UnitId });
        builder.Entity<UserUnit>(entity =>
        {
            entity.Property(unit => unit.UnitId).HasConversion(
                v => (int)v, v => (UnitId)v);
        });
        
        builder.Entity<UserSheep>().HasKey(userSheep => new { userSheep.UserId, userSheep.SheepId });
        builder.Entity<UserSheep>(entity =>
        {
            entity.Property(sheep => sheep.SheepId).HasConversion(
                v => (int)v, v => (SheepId)v);
        });
        
        builder.Entity<UserEnchant>().HasKey(userEnchant => new { userEnchant.UserId, userEnchant.EnchantId });
        builder.Entity<UserEnchant>(entity =>
        {
            entity.Property(enchant => enchant.EnchantId).HasConversion(
                v => (int)v, v => (EnchantId)v);
        });
        
        builder.Entity<UserCharacter>().HasKey(userCharacter => new { userCharacter.UserId, userCharacter.CharacterId });
        builder.Entity<UserCharacter>(entity =>
        {
            entity.Property(character => character.CharacterId).HasConversion(
                v => (int)v, v => (CharacterId)v);
        });

        builder.Entity<UnitMaterial>().HasKey(unitMaterial => new { unitMaterial.UnitId, unitMaterial.MaterialId });
        
        builder.Entity<UserMaterial>().HasKey(userMaterial => new { userMaterial.UserId, userMaterial.MaterialId });
        
        builder.Entity<BattleSetting>().HasNoKey();
        builder.Entity<BattleSetting>()
            .HasIndex(b => new { b.UserId, b.SheepId, b.EnchantId, b.CharacterId }).IsUnique();
        
        builder.Entity<ExpTable>().HasKey(e => e.Level);
        builder.Entity<ExpTable>().Property(e => e.Level).ValueGeneratedNever();
    }
}
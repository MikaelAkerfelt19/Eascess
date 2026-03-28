using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Infrastructure.Persistence;

public partial class EaccessDbContext : IdentityDbContext<AppUser>
{
    public EaccessDbContext(DbContextOptions<EaccessDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiProcessingQueue> AiProcessingQueues { get; set; }
    public virtual DbSet<AiUsageLog> AiUsageLogs { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<Domain> Domains { get; set; }
    public virtual DbSet<Invoice> Invoices { get; set; }
    public virtual DbSet<Page> Pages { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<Plan> Plans { get; set; }
    public virtual DbSet<PlanFeature> PlanFeatures { get; set; }
    public virtual DbSet<ScanReport> ScanReports { get; set; }
    public virtual DbSet<ScanReportDetail> ScanReportDetails { get; set; }
    public virtual DbSet<UserSubscription> UserSubscriptions { get; set; }
    public virtual DbSet<WidgetSetting> WidgetSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tablolarını kurar

        modelBuilder.Entity<AiProcessingQueue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AiProces__3214EC07EFFFF379");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.HasOne(d => d.Domain).WithMany(p => p.AiProcessingQueues).HasConstraintName("FK_AiQueue_Domains");
        });

        modelBuilder.Entity<AiUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AiUsageL__3214EC07312D1AD3");
            entity.Property(e => e.RequestDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.TokensUsed).HasDefaultValue(0);
            entity.HasOne(d => d.Domain).WithMany(p => p.AiUsageLogs).HasConstraintName("FK_AiUsageLogs_Domains");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC07BDE314C3");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Domains__3214EC07EA700A98");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsVerified).HasDefaultValue(false);
            entity.Property(e => e.LicenseKey).HasDefaultValueSql("(newid())");
            entity.HasOne(d => d.User).WithMany(p => p.Domains)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Domains_Users");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invoices__3214EC0761B9CBEB");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Currency).HasDefaultValue("TRY");
            entity.HasOne(d => d.Payment).WithMany(p => p.Invoices).HasConstraintName("FK_Invoices_Payments");
            entity.HasOne(d => d.User).WithMany(p => p.Invoices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_Users");
        });

        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Pages__3214EC0715ED37E5");
            entity.HasOne(d => d.Domain).WithMany(p => p.Pages).HasConstraintName("FK_Pages_Domains");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC076095837F");
            entity.Property(e => e.Currency).HasDefaultValue("TRY");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(getutcdate())");
            entity.HasOne(d => d.Subscription).WithMany(p => p.Payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Subscriptions");
            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Users");
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Plans__3214EC07DD689DB1");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<PlanFeature>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PlanFeat__3214EC07672AF378");
            entity.HasOne(d => d.Plan).WithMany(p => p.PlanFeatures).HasConstraintName("FK_PlanFeatures_Plans");
        });

        modelBuilder.Entity<ScanReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ScanRepo__3214EC07D9E1BDF4");
            entity.Property(e => e.ScanDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ScanType).HasDefaultValue("Manual");
            entity.HasOne(d => d.Domain).WithMany(p => p.ScanReports).HasConstraintName("FK_ScanReports_Domains");
        });

        modelBuilder.Entity<ScanReportDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ScanRepo__3214EC078F61CE4F");
            entity.HasOne(d => d.Page).WithMany(p => p.ScanReportDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScanReportDetails_Pages");
            entity.HasOne(d => d.ScanReport).WithMany(p => p.ScanReportDetails).HasConstraintName("FK_ScanReportDetails_Reports");
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSubs__3214EC071C267DEF");
            entity.Property(e => e.AutoRenew).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(d => d.Plan).WithMany(p => p.UserSubscriptions).HasConstraintName("FK_UserSubscriptions_Plans");
            entity.HasOne(d => d.User).WithMany(p => p.UserSubscriptions).HasConstraintName("FK_UserSubscriptions_AspNetUsers");
        });

        modelBuilder.Entity<WidgetSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WidgetSe__3214EC07D5A978C5");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAiEnabled).HasDefaultValue(true);
            entity.Property(e => e.Language).HasDefaultValue("tr");
            entity.Property(e => e.Position).HasDefaultValue("bottom-right");
            entity.Property(e => e.ThemeColor).HasDefaultValue("#0056b3");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.VersionNumber).HasDefaultValue(1);
            entity.HasOne(d => d.Domain).WithMany(p => p.WidgetSettings).HasConstraintName("FK_WidgetSettings_Domains");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
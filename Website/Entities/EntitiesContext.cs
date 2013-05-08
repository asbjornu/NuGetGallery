﻿using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Ninject;

namespace NuGetGallery
{
    /// <summary>
    /// Used by EF Migrations to load the Entity Context for migrations and such like.
    /// Don't use it for anything else because it doesn't respect read-only mode.
    /// </summary>
    public class EntitiesContextFactory : IDbContextFactory<EntitiesContext>
    {
        // Used by GalleryGateway
        internal static string OverrideConnectionString { get; set; }

        public EntitiesContext Create()
        {
            // readOnly: false - without read access, database migrations will fail and 
            // the whole site will be down (even when migrations are a no-op apparently).
            return new EntitiesContext(
                OverrideConnectionString ?? Container.Kernel.Get<IConfiguration>().SqlConnectionString,
                readOnly: false);
        }
    }

    public class EntitiesContext : DbContext, IEntitiesContext
    {
        public EntitiesContext(string connectionString, bool readOnly)
            : base(connectionString)
        {
            ReadOnly = readOnly;
        }

        public bool ReadOnly { get; private set; }
        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<User> Users { get; set; }

        public override int SaveChanges()
        {
            if (ReadOnly)
            {
                throw new ReadOnlyModeException("Save changes unavailable: the gallery is currently in read only mode, with limited service. Please try again later.");
            }

            return base.SaveChanges();
        }

        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "We use this only in very controlled scenarios, all of which are parameterless.")]
        public TResult Sql<TResult>(string query, Func<IDataReader, TResult> loader, int? commandTimeout = null, CommandBehavior behavior = CommandBehavior.Default)
        {
            using (var command = Database.Connection.CreateCommand())
            {
                command.CommandText = query;
                if (commandTimeout != null)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                Database.Connection.Open();
                using (var reader = command.ExecuteReader(behavior))
                {
                    return loader(reader);
                }
            }
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<User>()
                .HasMany<EmailMessage>(u => u.Messages)
                .WithRequired(em => em.ToUser)
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<User>()
                .HasMany<Role>(u => u.Roles)
                .WithMany(r => r.Users)
                .Map(c => c.ToTable("UserRoles")
                           .MapLeftKey("UserKey")
                           .MapRightKey("RoleKey"));

            modelBuilder.Entity<Role>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasKey(em => em.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasOptional<User>(em => em.FromUser)
                .WithMany()
                .HasForeignKey(em => em.FromUserKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<Package>(pr => pr.Packages)
                .WithRequired(p => p.PackageRegistration)
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany()
                .Map(c => c.ToTable("PackageRegistrationOwners")
                           .MapLeftKey("PackageRegistrationKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<Package>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<Package>()
                .HasMany<PackageAuthor>(p => p.Authors)
                .WithRequired(pa => pa.Package)
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageStatistics>(p => p.DownloadStatistics)
                .WithRequired(ps => ps.Package)
                .HasForeignKey(ps => ps.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageDependency>(p => p.Dependencies)
                .WithRequired(pd => pd.Package)
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<PackageAuthor>()
                .HasKey(pa => pa.Key);

            modelBuilder.Entity<PackageStatistics>()
                .HasKey(ps => ps.Key);

            modelBuilder.Entity<PackageDependency>()
                .HasKey(pd => pd.Key);

            modelBuilder.Entity<GallerySetting>()
                .HasKey(gs => gs.Key);

            modelBuilder.Entity<PackageOwnerRequest>()
                .HasKey(por => por.Key);

            modelBuilder.Entity<PackageFramework>()
                .HasKey(pf => pf.Key);
            modelBuilder.Entity<CuratedFeed>()
                .HasKey(cf => cf.Key);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<CuratedPackage>(cf => cf.Packages)
                .WithRequired(cp => cp.CuratedFeed)
                .HasForeignKey(cp => cp.CuratedFeedKey);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<User>(cf => cf.Managers)
                .WithMany()
                .Map(c => c.ToTable("CuratedFeedManagers")
                           .MapLeftKey("CuratedFeedKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<CuratedPackage>()
                .HasKey(cp => cp.Key);

            modelBuilder.Entity<CuratedPackage>()
                .HasRequired(cp => cp.PackageRegistration);
        }
    }
}

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
//
//     Produced by Entity Framework Visual Editor v4.2.1.3
//     Source:                    https://github.com/msawczyn/EFDesigner
//     Visual Studio Marketplace: https://marketplace.visualstudio.com/items?itemName=michaelsawczyn.EFDesigner
//     Documentation:             https://msawczyn.github.io/EFDesigner/
//     License (MIT):             https://github.com/msawczyn/EFDesigner/blob/master/LICENSE
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ex2_ModelOne2One
{
   /// <inheritdoc/>
   public partial class EFModelOne2One : DbContext
   {
      #region DbSets
      public virtual Microsoft.EntityFrameworkCore.DbSet<global::Ex2_ModelOne2One.Address> Addresses { get; set; }
      public virtual Microsoft.EntityFrameworkCore.DbSet<global::Ex2_ModelOne2One.Person> People { get; set; }

      #endregion DbSets

      /// <summary>
      /// Default connection string
      /// </summary>
      public static string ConnectionString { get; set; } = @"Data Source=(localdb)\mssqllocaldb;Initial Catalog=EFLocalDb;Integrated Security=True";

      /// <summary>
      ///     <para>
      ///         Initializes a new instance of the <see cref="T:Microsoft.EntityFrameworkCore.DbContext" /> class using the specified options.
      ///         The <see cref="M:Microsoft.EntityFrameworkCore.DbContext.OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" /> method will still be called to allow further
      ///         configuration of the options.
      ///     </para>
      /// </summary>
      /// <param name="options">The options for this context.</param>
      public EFModelOne2One(DbContextOptions<EFModelOne2One> options) : base(options)
      {
      }

      partial void CustomInit(DbContextOptionsBuilder optionsBuilder);

      /// <inheritdoc />
      protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      {
         CustomInit(optionsBuilder);
      }

      partial void OnModelCreatingImpl(ModelBuilder modelBuilder);
      partial void OnModelCreatedImpl(ModelBuilder modelBuilder);

      /// <summary>
      ///     Override this method to further configure the model that was discovered by convention from the entity types
      ///     exposed in <see cref="T:Microsoft.EntityFrameworkCore.DbSet`1" /> properties on your derived context. The resulting model may be cached
      ///     and re-used for subsequent instances of your derived context.
      /// </summary>
      /// <remarks>
      ///     If a model is explicitly set on the options for this context (via <see cref="M:Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseModel(Microsoft.EntityFrameworkCore.Metadata.IModel)" />)
      ///     then this method will not be run.
      /// </remarks>
      /// <param name="modelBuilder">
      ///     The builder being used to construct the model for this context. Databases (and other extensions) typically
      ///     define extension methods on this object that allow you to configure aspects of the model that are specific
      ///     to a given database.
      /// </param>
      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
         base.OnModelCreating(modelBuilder);
         OnModelCreatingImpl(modelBuilder);

         modelBuilder.HasDefaultSchema("dbo");

         modelBuilder.Entity<global::Ex2_ModelOne2One.Address>()
                     .ToTable("Addresses")
                     .HasKey(t => t.AddressId);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Address>()
                     .Property(t => t.AddressId)
                     .ValueGeneratedOnAdd()
                     .IsRequired();

         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .ToTable("People")
                     .HasKey(t => t.PersonId);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.PersonId)
                     .ValueGeneratedOnAdd()
                     .IsRequired();
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.FirstName)
                     .HasMaxLength(35);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.MiddleName)
                     .HasMaxLength(35);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.LastName)
                     .HasMaxLength(35);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.PreferredName)
                     .HasMaxLength(75);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.Email)
                     .HasMaxLength(45);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .Property(t => t.Phone)
                     .HasMaxLength(15);
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>()
                     .HasOne<global::Ex2_ModelOne2One.Address>(p => p.Address)
                     .WithOne(p => p.Person)
                     .HasForeignKey("Address", "PersonPersonId");
         modelBuilder.Entity<global::Ex2_ModelOne2One.Person>().Navigation(e => e.Address).AutoInclude();

         OnModelCreatedImpl(modelBuilder);
      }
   }
}
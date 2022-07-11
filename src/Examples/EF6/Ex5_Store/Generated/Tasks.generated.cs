//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
//
//     Produced by Entity Framework Visual Editor v3.0.7.2
//     Source:                    https://github.com/msawczyn/EFDesigner
//     Visual Studio Marketplace: https://marketplace.visualstudio.com/items?itemName=michaelsawczyn.EFDesigner
//     Documentation:             https://msawczyn.github.io/EFDesigner/
//     License (MIT):             https://github.com/msawczyn/EFDesigner/blob/master/LICENSE
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ex5_Store
{
   public partial class Tasks
   {
      partial void Init();

      /// <summary>
      /// Default constructor. Protected due to required properties, but present because EF needs it.
      /// </summary>
      protected Tasks()
      {
         Audits = new System.Collections.Generic.HashSet<global::Ex5_Store.Audit>();

         Init();
      }

      /// <summary>
      /// Replaces default constructor, since it's protected. Caller assumes responsibility for setting all required values before saving.
      /// </summary>
      public static Tasks CreateTasksUnsafe()
      {
         return new Tasks();
      }

      /// <summary>
      /// Public constructor with required data
      /// </summary>
      /// <param name="person"></param>
      /// <param name="address"></param>
      public Tasks(global::Ex5_Store.Person person, global::Ex5_Store.Address address)
      {
         if (person == null) throw new ArgumentNullException(nameof(person));
         this.Person = person;
         person.Tasks.Add(this);

         if (address == null) throw new ArgumentNullException(nameof(address));
         this.Address = address;
         address.Tasks.Add(this);

         Audits = new System.Collections.Generic.HashSet<global::Ex5_Store.Audit>();
         Init();
      }

      /// <summary>
      /// Static create function (for use in LINQ queries, etc.)
      /// </summary>
      /// <param name="person"></param>
      /// <param name="address"></param>
      public static Tasks Create(global::Ex5_Store.Person person, global::Ex5_Store.Address address)
      {
         return new Tasks(person, address);
      }

      /*************************************************************************
       * Properties
       *************************************************************************/

      /// <summary>
      /// Identity, Indexed, Required
      /// Unique identifier
      /// </summary>
      [Key]
      [Required]
      [System.ComponentModel.Description("Unique identifier")]
      public long Id { get; set; }

      /*************************************************************************
       * Navigation properties
       *************************************************************************/

      public virtual ICollection<global::Ex5_Store.Audit> Audits { get; private set; }

      /// <summary>
      /// Required
      /// </summary>
      public virtual global::Ex5_Store.Person Person { get; set; }

      /// <summary>
      /// Required
      /// </summary>
      public virtual global::Ex5_Store.Address Address { get; set; }

   }
}

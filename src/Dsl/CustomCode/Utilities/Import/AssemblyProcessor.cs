﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.Modeling;

using Newtonsoft.Json;

using ParsingModels;

using Sawczyn.EFDesigner.EFModel.Extensions;

// ReSharper disable UseObjectOrCollectionInitializer

namespace Sawczyn.EFDesigner.EFModel
{
   /// <summary>
   /// Processes assembly files
   /// </summary>
   public class AssemblyProcessor : IFileProcessor
   {
      private readonly Store Store;

      /// <summary>
      /// Constructs a new instance of the AssemblyProcessor class with the given store.
      /// </summary>
      /// <param name="store">The store used for processing assembly information.</param>
      public AssemblyProcessor(Store store)
      {
         Store = store;
      }

      /// <summary>
      /// Processes the input file and returns a list of new model elements.
      /// </summary>
      /// <param name="inputFile">The path of the input file to be processed.</param>
      /// <param name="newElements">The list of new model elements created during the processing.</param>
      /// <returns>Returns true if the processing was successful; false otherwise.</returns>
      public bool Process(string inputFile, out List<ModelElement> newElements)
      {
         try
         {
            if (inputFile == null)
               throw new ArgumentNullException(nameof(inputFile));

            newElements = new List<ModelElement>();

            string outputFilename = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            string logFilename = Path.ChangeExtension(outputFilename, "log");
            StatusDisplay.Show("Detecting .NET and EF versions");

            KeyValuePair<string, string>[] parsers =
            {
               new KeyValuePair<string, string>("EF 6", @"Parsers\EF6Parser.exe"),
               new KeyValuePair<string, string>("EF Core 5", @"Parsers\EFCore5Parser.exe"),
               new KeyValuePair<string, string>("EF Core 6", @"Parsers\EFCore6Parser.exe"),
               new KeyValuePair<string, string>("EF Core 7", @"Parsers\EFCore7Parser.exe"),
               new KeyValuePair<string, string>("EF Core 8", @"Parsers\EFCore8Parser.exe")
            };

            Dictionary<string, bool> contexts = new Dictionary<string, bool>();

            foreach (KeyValuePair<string, string> parser in parsers)
            {
               StatusDisplay.Show($"Detecting .NET and EF versions ... Trying {parser.Key} ...");

               if (TryProcess(inputFile, ref newElements, parser.Value, outputFilename, logFilename, contexts))
                  return true;
            }

            foreach (string logEntry in File.ReadAllLines(logFilename))
               WarningDisplay.Show(logEntry);

            ErrorDisplay.Show(Store, $"Error processing assembly. See Output window or {logFilename} for further information");
            System.Diagnostics.Process.Start(logFilename);

            return false;
         }
         finally
         {
            StatusDisplay.Show("Ready");
         }
      }

      private Multiplicity ConvertMultiplicity(ParsingModels.Multiplicity data)
      {
         switch (data)
         {
            case ParsingModels.Multiplicity.ZeroMany:
               return Multiplicity.ZeroMany;

            case ParsingModels.Multiplicity.One:
               return Multiplicity.One;

            case ParsingModels.Multiplicity.ZeroOne:
               return Multiplicity.ZeroOne;
         }

         return Multiplicity.ZeroOne;
      }

      private EndpointRole ConvertRole(AssociationRole data)
      {
         switch (data)
         {
            case AssociationRole.Dependent:
               return EndpointRole.Dependent;

            case AssociationRole.Principal:
               return EndpointRole.Principal;
         }

         return EndpointRole.NotSet;
      }

      private bool DoProcessing(string outputFilename, out List<ModelElement> newElements)
      {
         newElements = new List<ModelElement>();

         try
         {
            using (StreamReader sr = new StreamReader(outputFilename))
            {
               string json = sr.ReadToEnd();
               ParsingModels.ModelRoot rootData = JsonConvert.DeserializeObject<ParsingModels.ModelRoot>(json);

               newElements = ProcessRootData(rootData);

               return true;
            }
         }
         catch (Exception e)
         {
            ErrorDisplay.Show(Store, $"Error processing assembly: {e.Message}");
         }

         return false;
      }

      #region ModelRoot

      private List<ModelElement> ProcessRootData(ParsingModels.ModelRoot rootData)
      {
         List<ModelElement> result = new List<ModelElement>();
         ModelRoot modelRoot = Store.ModelRoot();

         modelRoot.EntityContainerName = rootData.EntityContainerName;
         modelRoot.Namespace = rootData.Namespace;

         result.AddRange(ProcessClasses(modelRoot, rootData.Classes));
         result.AddRange(ProcessEnumerations(modelRoot, rootData.Enumerations));

         foreach (Association association in modelRoot.Store.GetAll<Association>())
         {
            // should happen at parsing time now
            // association.SetEndpointRoles();
            association.FixupForeignKeys();
         }

         // still a work-in-progress
         //FixupAssociationClasses(modelRoot);
         return result;
      }

      #endregion

      private int TryParseAssembly(string filename, string parserAssembly, string outputFilename, string contextName)
      {
         try
         {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), parserAssembly);
            string arguments = $@"""{filename.Trim('\"')}"" ""{outputFilename}""{(string.IsNullOrEmpty(contextName) ? string.Empty : $@" ""{contextName}""")}";
            Debug.WriteLine($"Trying parser: {path} {arguments}");

            ProcessStartInfo processStartInfo = new ProcessStartInfo(path)
                                                {
                                                   Arguments = arguments,
                                                   CreateNoWindow = true,
                                                   ErrorDialog = false,
                                                   WindowStyle = ProcessWindowStyle.Hidden,
                                                   UseShellExecute = true
                                                };

            using (Process process = System.Diagnostics.Process.Start(processStartInfo))
            {
               process.WaitForExit();

               return process.ExitCode;
            }
         }
         catch 
         {
            return 4;
         }
      }

      private bool TryProcess(string assemblyPath, ref List<ModelElement> newElements, string parserPath, string outputFilename, string logFilename, Dictionary<string, bool> contexts)
      {
         try
         {
            string contextName = contexts.Any(kv => contexts[kv.Key])
                                    ? contexts.First(kv => contexts[kv.Key]).Key
                                    : null;

            if (contexts.Any() && string.IsNullOrEmpty(contextName))
               return false;

            int parseResult = TryParseAssembly(assemblyPath, parserPath, outputFilename, contextName);

            if (parseResult == 0)
               return DoProcessing(outputFilename, out newElements);

            if (!contexts.Any())
            {
               string dupeContextTag = "Found more than one class derived from DbContext:";
               string dupeContextLogEntry = File.ReadAllLines(logFilename).FirstOrDefault(logEntry => logEntry.Contains(dupeContextTag));

               if (dupeContextLogEntry != null)
               {
                  IEnumerable<string> contextNames = dupeContextLogEntry.Substring(dupeContextLogEntry.IndexOf(dupeContextTag, StringComparison.InvariantCulture) + dupeContextTag.Length).Split(',')
                                                                        .Select(s => s.Trim().Split('.').Last());

                  foreach (string context in contextNames)
                     contexts.Add(context, BooleanQuestionDisplay.Show(Store, $"Found multiple DbContext classes. Process {context}?") == true);

                  return TryProcess(assemblyPath, ref newElements, parserPath, outputFilename, logFilename, contexts);
               }
            }
         }
         catch (Exception ex)
         {
            Debug.WriteLine(ex.ToString());
         }

         return false;
      }

      internal bool TryProcessIntermediateFile(string inputFile, List<ModelElement> newElements)
      {
         string json;

         using (StreamReader sr = new StreamReader(inputFile))
         {
            try
            {
               json = sr.ReadToEnd();
               sr.Close();
            }
            catch
            {
               json = null;
            }
         }

         if (json?.StartsWith("{\"EntityContainerName\":") == true)
         {
            if (DoProcessing(inputFile, out List<ModelElement> processedElements))
            {
               newElements.AddRange(processedElements);

               return true;
            }
         }

         return false;
      }

      #region Classes

      private List<ModelElement> ProcessClasses(ModelRoot modelRoot, List<ParsingModels.ModelClass> classDataList)
      {
         List<ModelElement> result = new List<ModelElement>();
         RemoveDuplicateBidirectionalAssociations(classDataList);
         Dictionary<string, List<ModelClass>> baseClasses = new Dictionary<string, List<ModelClass>>();

         // on the odd chance that a generic class passed through the parser, make sure we're not adding that to the model, since EF doesn't support that
         foreach (ParsingModels.ModelClass data in classDataList.Where(x => !x.FullName.Contains("<")))
         {
            StatusDisplay.Show($"Processing {data.FullName}");

            ModelClass element = modelRoot.Classes.FirstOrDefault(x => x.FullName == data.FullName);

            if (element == null)
            {
               if (!string.IsNullOrEmpty(data.ViewName) || !data.IsPersistent)
               {
                  element = new ModelClass(Store,
                                           new PropertyAssignment(ModelClass.NameDomainPropertyId, data.Name),
                                           new PropertyAssignment(ModelClass.NamespaceDomainPropertyId, data.Namespace),
                                           new PropertyAssignment(ModelClass.CustomAttributesDomainPropertyId, data.CustomAttributes),
                                           new PropertyAssignment(ModelClass.CustomInterfacesDomainPropertyId, data.CustomInterfaces),
                                           new PropertyAssignment(ModelClass.IsAbstractDomainPropertyId, data.IsAbstract),
                                           new PropertyAssignment(ModelClass.IsDatabaseViewDomainPropertyId, true),
                                           new PropertyAssignment(ModelClass.ViewNameDomainPropertyId, data.ViewName),
                                           new PropertyAssignment(ModelClass.PersistentDomainPropertyId, data.IsPersistent),
                                           new PropertyAssignment(ModelClass.SummaryDomainPropertyId, data.Summary),
                                           new PropertyAssignment(ModelClass.IsDependentTypeDomainPropertyId, data.IsDependentType));
               }
               else
               {
                  element = new ModelClass(Store,
                                           new PropertyAssignment(ModelClass.NameDomainPropertyId, data.Name),
                                           new PropertyAssignment(ModelClass.NamespaceDomainPropertyId, data.Namespace),
                                           new PropertyAssignment(ModelClass.CustomAttributesDomainPropertyId, data.CustomAttributes),
                                           new PropertyAssignment(ModelClass.CustomInterfacesDomainPropertyId, data.CustomInterfaces),
                                           new PropertyAssignment(ModelClass.IsAbstractDomainPropertyId, data.IsAbstract),
                                           new PropertyAssignment(ModelClass.TableNameDomainPropertyId, data.TableName),
                                           new PropertyAssignment(ModelClass.PersistentDomainPropertyId, data.IsPersistent),
                                           new PropertyAssignment(ModelClass.SummaryDomainPropertyId, data.Summary),
                                           new PropertyAssignment(ModelClass.IsDependentTypeDomainPropertyId, data.IsDependentType));
               }

               modelRoot.Classes.Add(element);
               result.Add(element);
            }
            else
            {
               element.Name = data.Name;
               element.Namespace = data.Namespace;
               element.CustomAttributes = data.CustomAttributes;
               element.CustomInterfaces = data.CustomInterfaces;
               element.IsAbstract = data.IsAbstract;
               element.Persistent = data.IsPersistent;
               element.Summary = element.Summary ?? data.Summary;
               element.IsDatabaseView = !data.IsPersistent || !string.IsNullOrEmpty(data.ViewName);

               if (element.IsDatabaseView)
                  element.ViewName = data.ViewName;
               else
                  element.TableName = data.TableName;


               element.IsDependentType = data.IsDependentType;
            }

            // if base class exists and isn't in the list yet, we can't hook it up to this class
            // so we'll defer base class linkage for all classes until we're sure they're all in the model.
            // Note that we don't support generic base classes or System.Object, so we'll tell the user that they'll have to add that to the partial
            if (!string.IsNullOrEmpty(data.BaseClass) && (data.BaseClass != "System.Object"))
            {
               if (data.BaseClass.Contains("<"))
               {
                  string message =
                     $"Found base class {data.BaseClass} for {element.FullName}. The designer doesn't support generic base classes. You will have to manually add this to a partial class for {element.FullName}.";

                  MessageDisplay.Show(message);
               }
               else
               {
                  if (!baseClasses.ContainsKey(data.BaseClass))
                     baseClasses.Add(data.BaseClass, new List<ModelClass>());

                  baseClasses[data.BaseClass].Add(element);
               }
            }

            ProcessProperties(element, data.Properties);
         }

         // now we can fixup the generalization links
         foreach (string baseClassKey in baseClasses.Keys.Where(x => x != "System.Object"))
         {
            string baseClassName = baseClassKey.StartsWith("global::")
                                      ? baseClassKey.Substring(8)
                                      : baseClassKey;

            foreach (ModelClass subClass in baseClasses[baseClassKey])
            {
               ModelClass superClass = modelRoot.Classes.FirstOrDefault(s => s.Name == baseClassName);

               // The Generalization connection tool specifies that source and target should be reversed. 
               if (superClass != null)
                  GeneralizationBuilder.Connect(superClass, subClass);
               else
               {
                  string message =
                     $"Found base class {baseClassName} for {subClass.FullName}, but it's not a persistent entity. You will have to manually add this to a partial class for {subClass.FullName}.";

                  MessageDisplay.Show(message);
               }
            }
         }

         // classes are all created, so we can work the associations
         List<string> allModelClassNames = modelRoot.Classes.Select(c => c.FullName).ToList();

         foreach (ParsingModels.ModelClass data in classDataList.Where(cd => allModelClassNames.Contains(cd.FullName)))
         {
            ProcessUnidirectionalAssociations(data);
            ProcessBidirectionalAssociations(data);
         }

         return result;
      }

      private static void RemoveDuplicateBidirectionalAssociations(List<ParsingModels.ModelClass> classDataList)
      {
         // Bidirectional associations get duplicates, and it's better to clean them here than to rely on each parser version
         List<ModelBidirectionalAssociation> allBidirectionalAssociations = classDataList.SelectMany(cls => cls.BidirectionalAssociations).ToList();

         for (int index = 0; index < allBidirectionalAssociations.Count; index++)
         {
            ModelBidirectionalAssociation keeper = allBidirectionalAssociations[index];

            ModelBidirectionalAssociation duplicate =
               allBidirectionalAssociations.Skip(index)
                                           .FirstOrDefault(a => (a.SourcePropertyTypeName == keeper.TargetPropertyTypeName)
                                                             && (a.SourcePropertyName == keeper.TargetPropertyName)
                                                             && (a.TargetPropertyTypeName == keeper.SourcePropertyTypeName)
                                                             && (a.TargetPropertyName == keeper.SourcePropertyName));

            if (duplicate != null)
            {
               // discard the one on the target
               ParsingModels.ModelClass duplicateOwner = classDataList.Single(c => c.FullName == duplicate.TargetClassFullName);
               duplicateOwner.BidirectionalAssociations.Remove(duplicate);
               allBidirectionalAssociations.Remove(duplicate);
            }
         }
      }

      private void ProcessProperties(ModelClass modelClass, List<ModelProperty> properties)
      {
         foreach (ModelProperty data in properties)
         {
            ModelAttribute element = modelClass.Attributes.FirstOrDefault(x => x.Name == data.Name);

            if (element == null)
            {
               // we've never seen this one before. Add it.
               element = new ModelAttribute(Store,
                                            new PropertyAssignment(ModelAttribute.TypeDomainPropertyId, data.TypeName),
                                            new PropertyAssignment(ModelAttribute.NameDomainPropertyId, data.Name),
                                            new PropertyAssignment(ModelAttribute.CustomAttributesDomainPropertyId, data.CustomAttributes),
                                            new PropertyAssignment(ModelAttribute.IndexedDomainPropertyId, data.Indexed),
                                            new PropertyAssignment(ModelAttribute.RequiredDomainPropertyId, data.Required),
                                            new PropertyAssignment(ModelAttribute.MaxLengthDomainPropertyId, data.MaxStringLength),
                                            new PropertyAssignment(ModelAttribute.MinLengthDomainPropertyId, data.MinStringLength),
                                            new PropertyAssignment(ModelAttribute.IsIdentityDomainPropertyId, data.IsIdentity),
                                            new PropertyAssignment(ModelAttribute.SummaryDomainPropertyId, data.Summary),
                                            new PropertyAssignment(ModelAttribute.IdentityTypeDomainPropertyId,
                                                                   data.IsIdentity
                                                                      ? data.IsIdentityGenerated
                                                                           ? IdentityType.AutoGenerated
                                                                           : IdentityType.Manual
                                                                      : IdentityType.None));

               modelClass.Attributes.Add(element);
            }
            else
            {
               // somehow, we have seen this before. Update it.
               element.Type = data.TypeName;
               element.Name = data.Name;
               element.CustomAttributes = data.CustomAttributes;
               element.Indexed = data.Indexed;
               element.IndexedUnique = data.IndexedUnique;
               element.IndexName = data.IndexName;
               element.Required = data.Required;
               element.MaxLength = data.MaxStringLength;
               element.MinLength = data.MinStringLength;
               element.IsIdentity = data.IsIdentity;
               element.Summary = element.Summary ?? data.Summary;

               element.IdentityType = data.IsIdentity
                                         ? data.IsIdentityGenerated
                                              ? IdentityType.AutoGenerated
                                              : IdentityType.Manual
                                         : IdentityType.None;
            }
         }
      }

      private void ProcessUnidirectionalAssociations(ParsingModels.ModelClass modelClass)
      {
         List<ModelUnidirectionalAssociation> unidirectionalAssociations = modelClass.UnidirectionalAssociations;

         foreach (ModelUnidirectionalAssociation data in unidirectionalAssociations)
         {
            if ((Store.ModelRoot().EntityFrameworkVersion == EFVersion.EF6)
             && (data.SourceMultiplicity != ParsingModels.Multiplicity.ZeroMany)
             && (data.TargetMultiplicity != ParsingModels.Multiplicity.ZeroMany))
               data.ForeignKey = null;

            UnidirectionalAssociation existing = Store.GetAll<UnidirectionalAssociation>()
                                                      .FirstOrDefault(x => (x.Target.FullName == data.TargetClassFullName)
                                                                        && (x.Source.FullName == data.SourceClassFullName)
                                                                        && (x.Source.FullName == modelClass.FullName) // just to be sure
                                                                        && (x.TargetPropertyName == data.TargetPropertyName));

            if (existing != null)
            {
               if (string.IsNullOrWhiteSpace(existing.FKPropertyName) && !string.IsNullOrWhiteSpace(data.ForeignKey))
               {
                  existing.FKPropertyName = data.ForeignKey;
                  existing.Source.ModelRoot.ExposeForeignKeys = true;
               }

               continue;
            }

            ModelClass source = Store.GetAll<ModelClass>().FirstOrDefault(c => c.FullName == data.SourceClassFullName);
            ModelClass target = Store.GetAll<ModelClass>().FirstOrDefault(c => c.FullName == data.TargetClassFullName);

            if ((source == null) || (target == null) || (source.FullName != modelClass.FullName))
               continue;

            UnidirectionalAssociation elementLink = (UnidirectionalAssociation)UnidirectionalAssociationBuilder.Connect(source, target);
            elementLink.SourceMultiplicity = ConvertMultiplicity(data.SourceMultiplicity);
            elementLink.TargetMultiplicity = ConvertMultiplicity(data.TargetMultiplicity);
            elementLink.TargetPropertyName = data.TargetPropertyName;
            elementLink.TargetSummary = data.TargetSummary;
            elementLink.TargetDescription = data.TargetDescription;
            elementLink.FKPropertyName = data.ForeignKey;
            elementLink.SourceRole = ConvertRole(data.SourceRole);
            elementLink.TargetRole = ConvertRole(data.TargetRole);

            if (elementLink.SourceRole == EndpointRole.NotSet || elementLink.TargetRole == EndpointRole.NotSet)
               elementLink.SetEndpointRoles();

            elementLink.FixupForeignKeys();

            // we could have a situation where there are no roles assigned (if 0/1-0/1 or 1-1). If we have exposed foreign keys, though, we can figure those out.
            if (((elementLink.SourceMultiplicity != Multiplicity.ZeroMany) || (elementLink.TargetMultiplicity != Multiplicity.ZeroMany))
             && ((elementLink.SourceRole == EndpointRole.NotSet) || (elementLink.TargetRole == EndpointRole.NotSet))
             && !string.IsNullOrEmpty(elementLink.FKPropertyName))
            {
               // which, if any, end has the foreign key properties in it?
               string firstFKPropertyName = elementLink.FKPropertyName.Split(',').First();

               if (elementLink.Source.AllPropertyNames.Contains(firstFKPropertyName))
               {
                  elementLink.SourceRole = EndpointRole.Dependent;
                  elementLink.TargetRole = EndpointRole.Principal;
               }
               else if (elementLink.Target.AllPropertyNames.Contains(firstFKPropertyName))
               {
                  elementLink.TargetRole = EndpointRole.Dependent;
                  elementLink.SourceRole = EndpointRole.Principal;
               }
            }
         }
      }

      private void ProcessBidirectionalAssociations(ParsingModels.ModelClass modelClass)
      {
         List<ModelBidirectionalAssociation> bidirectionalAssociations = modelClass.BidirectionalAssociations;

         foreach (ModelBidirectionalAssociation data in bidirectionalAssociations)
         {
            if ((Store.ModelRoot().EntityFrameworkVersion == EFVersion.EF6)
             && (data.SourceMultiplicity != ParsingModels.Multiplicity.ZeroMany)
             && (data.TargetMultiplicity != ParsingModels.Multiplicity.ZeroMany))
               data.ForeignKey = null;

            BidirectionalAssociation existing = Store.GetAll<BidirectionalAssociation>()
                                                     .FirstOrDefault(x => (x.Target.Name == data.TargetClassName)
                                                                       && (x.Source.Name == data.SourceClassName)
                                                                       && (x.Source.Name == modelClass.Name) // just to be sure
                                                                       && (x.TargetPropertyName == data.TargetPropertyName)
                                                                       && (x.SourcePropertyName == data.SourcePropertyName))
                                             ?? Store.GetAll<BidirectionalAssociation>()
                                                     .FirstOrDefault(x => (x.Source.Name == data.TargetClassName)
                                                                       && (x.Target.Name == data.SourceClassName)
                                                                       && (x.Target.Name == modelClass.Name) // just to be sure
                                                                       && (x.SourcePropertyName == data.TargetPropertyName)
                                                                       && (x.TargetPropertyName == data.SourcePropertyName));

            if (existing != null)
            {
               if (string.IsNullOrWhiteSpace(existing.FKPropertyName) && !string.IsNullOrWhiteSpace(data.ForeignKey))
               {
                  existing.FKPropertyName = string.Join(",", data.ForeignKey.Split(',').ToList().Select(p => p.Split('/').Last().Split(' ').Last()));
                  existing.Source.ModelRoot.ExposeForeignKeys = true;
               }

               continue;
            }

            ModelClass source = Store.GetAll<ModelClass>().FirstOrDefault(c => c.Name == data.SourceClassName);
            ModelClass target = Store.GetAll<ModelClass>().FirstOrDefault(c => c.Name == data.TargetClassName);

            if ((source == null) || (target == null) || (source.FullName != modelClass.FullName))
               continue;

            BidirectionalAssociation elementLink = (BidirectionalAssociation)BidirectionalAssociationBuilder.Connect(source, target);
            elementLink.SourceMultiplicity = ConvertMultiplicity(data.SourceMultiplicity);
            elementLink.TargetMultiplicity = ConvertMultiplicity(data.TargetMultiplicity);
            elementLink.TargetPropertyName = data.TargetPropertyName;
            elementLink.TargetSummary = data.TargetSummary;
            elementLink.TargetDescription = data.TargetDescription;
            elementLink.FKPropertyName = data.ForeignKey;
            elementLink.SourceRole = ConvertRole(data.SourceRole);
            elementLink.TargetRole = ConvertRole(data.TargetRole);
            elementLink.SourcePropertyName = data.SourcePropertyName;
            elementLink.SourceSummary = data.SourceSummary;
            elementLink.SourceDescription = data.SourceDescription;
            elementLink.JoinTableName = data.JoinTableName;

            elementLink.SetEndpointRoles();
            elementLink.FixupForeignKeys();

            // we could have a situation where there are no roles assigned (if 0/1-0/1 or 1-1). If we have exposed foreign keys, though, we can figure those out.
            if (((elementLink.SourceMultiplicity != Multiplicity.ZeroMany) || (elementLink.TargetMultiplicity != Multiplicity.ZeroMany))
             && ((elementLink.SourceRole == EndpointRole.NotSet) || (elementLink.TargetRole == EndpointRole.NotSet))
             && !string.IsNullOrEmpty(elementLink.FKPropertyName))
            {
               // which, if any, end has the foreign key properties in it?
               string firstFKPropertyName = elementLink.FKPropertyName.Split(',').First();

               if (elementLink.Source.AllPropertyNames.Contains(firstFKPropertyName))
               {
                  elementLink.SourceRole = EndpointRole.Dependent;
                  elementLink.TargetRole = EndpointRole.Principal;
               }
               else if (elementLink.Target.AllPropertyNames.Contains(firstFKPropertyName))
               {
                  elementLink.TargetRole = EndpointRole.Dependent;
                  elementLink.SourceRole = EndpointRole.Principal;
               }
            }
         }
      }

      //private void FixupAssociationClasses(ModelRoot modelRoot)
      //{
      //   // find classes that look like N-N association classes. 
      //   // turn them into association classes and add the bidirectional association

      //   using (Transaction tx = Store.TransactionManager.BeginTransaction("Convert to association class"))
      //   {
      //      // simple case - only foreign key fields (ignoring identity fields)
      //      foreach (ModelClass associationClass in modelRoot.Classes
      //                                                 .Where(c => c.Persistent
      //                                                          && c.Attributes.Count(a => (!a.IsIdentity || a.IsForeignKeyProperty) && a.Required) == 2)
      //                                                 .ToArray())
      //      {
      //         //// remove standalone identities
      //         //foreach (ModelAttribute attribute in modelClass.Attributes.Where(a => a.IsIdentity && !a.IsForeignKeyProperty))
      //         //   associationClass.Attributes.Remove(attribute);

      //         //// add fk properties as identities (should only be 2 properties left)
      //         //foreach (ModelAttribute attribute in associationClass.Attributes)
      //         //   attribute.IsIdentity = true;

      //         // get the entities we're serving


      //         ModelClass[] entities = associationClass.UnidirectionalSources.Where(x => x != associationClass)
      //                                                 .Union(associationClass.UnidirectionalTargets.Where(x => x != associationClass))
      //                                                 .Union(associationClass.BidirectionalSources.Where(x => x != associationClass))
      //                                                 .Union(associationClass.BidirectionalTargets.Where(x => x != associationClass))
      //                                                 .ToArray();

      //         if (entities.Length != 2)
      //            continue;


      //         {
      //            // remove any association between those entities and the association class that might be there
      //            modelRoot.Store
      //                     .GetAll<Association>()
      //                     .Where(a => (entities.Contains(a.Source) && a.Target == associationClass)
      //                              || (entities.Contains(a.Target) && a.Source == associationClass))
      //                     .ToList()
      //                     .ForEach(a => a.Delete());

      //            // add the new bidirectional association
      //            BidirectionalAssociation bidirectional =
      //               new BidirectionalAssociation(Store,
      //                                            new[]
      //                                            {
      //                                               new RoleAssignment(Association.SourceDomainRoleId, entities[0]), 
      //                                               new RoleAssignment(Association.TargetDomainRoleId, entities[1])
      //                                            },
      //                                            new[]
      //                                            {
      //                                               new PropertyAssignment(Association.SourceMultiplicityDomainPropertyId, Multiplicity.ZeroMany),
      //                                               new PropertyAssignment(Association.TargetMultiplicityDomainPropertyId, Multiplicity.ZeroMany)
      //                                            });

      //            // turn the class of interest into a proper association class for that new bidirectional association
      //            associationClass.ConvertToAssociationClass(bidirectional);
      //         }
      //      }

      //      tx.Commit();
      //   }
      //}

      #endregion

      #region Enumerations

      private List<ModelElement> ProcessEnumerations(ModelRoot modelRoot, List<ParsingModels.ModelEnum> enumDataList)
      {
         List<ModelElement> result = new List<ModelElement>();

         foreach (ParsingModels.ModelEnum data in enumDataList)
         {
            StatusDisplay.Show($"Processing {data.FullName}");
            ModelEnum element = modelRoot.Enums.FirstOrDefault(e => e.FullName == data.FullName);

            if (element == null)
            {
               element = new ModelEnum(Store,
                                       new PropertyAssignment(ModelEnum.NameDomainPropertyId, data.Name),
                                       new PropertyAssignment(ModelEnum.NamespaceDomainPropertyId, data.Namespace),
                                       new PropertyAssignment(ModelEnum.CustomAttributesDomainPropertyId, data.CustomAttributes),
                                       new PropertyAssignment(ModelEnum.IsFlagsDomainPropertyId, data.IsFlags));

               modelRoot.Enums.Add(element);
               result.Add(element);
            }
            else
            {
               element.Name = data.Name;
               element.Namespace = data.Namespace;
               element.CustomAttributes = data.CustomAttributes;

               // TODO - deal with ValueType
               //element.ValueType = data.ValueType;
               element.IsFlags = data.IsFlags;
            }

            ProcessEnumerationValues(element, data.Values);
         }

         return result;
      }

      private void ProcessEnumerationValues(ModelEnum modelEnum, List<ParsingModels.ModelEnumValue> enumValueList)
      {
         foreach (ParsingModels.ModelEnumValue data in enumValueList)
         {
            ModelEnumValue element = modelEnum.Values.FirstOrDefault(x => x.Name == data.Name);

            if (element == null)
            {
               element = new ModelEnumValue(Store,
                                            new PropertyAssignment(ModelEnumValue.NameDomainPropertyId, data.Name),
                                            new PropertyAssignment(ModelEnumValue.ValueDomainPropertyId, data.Value),
                                            new PropertyAssignment(ModelEnumValue.CustomAttributesDomainPropertyId, data.CustomAttributes),
                                            new PropertyAssignment(ModelEnumValue.DisplayTextDomainPropertyId, data.DisplayText));

               modelEnum.Values.Add(element);
            }
            else
            {
               element.Name = data.Name;
               element.Value = data.Value;
               element.CustomAttributes = data.CustomAttributes;
               element.DisplayText = data.DisplayText;
            }
         }
      }

      #endregion
   }
}
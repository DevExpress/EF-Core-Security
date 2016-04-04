﻿using DevExpress.EntityFramework.SecurityDataStore.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevExpress.EntityFramework.SecurityDataStore {
    public static class ChangeTrackerExtensions {
        public static EntityEntry GetEntity(this ChangeTracker changeTracker, object targetObject) {
           return changeTracker.Entries().FirstOrDefault(p => p.Entity == targetObject);
        }
        public static EntityEntry GetPrincipaEntityEntryCurrentValue(this ChangeTracker changeTracker, EntityEntry targetEntity, IForeignKey foreignKey) {
            IEnumerable<EntityEntry> targetEntities = changeTracker.Entries().Where(p => Equals(p.Metadata.ClrType, foreignKey.PrincipalEntityType.ClrType));
            EntityEntry principalEntityEntry = null;
            foreach(EntityEntry entityEntry in targetEntities) {
                bool result = true;
                for(int i = 0; i < foreignKey.Properties.Count; i++) {
                    PropertyEntry propertyEntryForeign = targetEntity.Property(foreignKey.Properties[i].Name);
                    PropertyEntry propertyEntryPrincipal = entityEntry.Property(foreignKey.PrincipalKey.Properties[i].Name);
                    if(!Equals(propertyEntryForeign.CurrentValue, propertyEntryPrincipal.CurrentValue)) {
                        result = false;
                        break;
                    }
                }
                if(result) {
                    principalEntityEntry = entityEntry;
                }
            }
            return principalEntityEntry;
        }
        public static EntityEntry GetPrincipaEntityEntryOriginalValue(this ChangeTracker changeTracker, EntityEntry targetEntity, IForeignKey foreignKey) {
            IEnumerable<EntityEntry> targetEntities = changeTracker.Entries().Where(p => Equals(p.Metadata.ClrType, foreignKey.PrincipalEntityType.ClrType));
            EntityEntry principalEntityEntry = null;
            foreach(EntityEntry entityEntry in targetEntities) {
                bool result = true;
                for(int i = 0; i < foreignKey.Properties.Count; i++) {
                    PropertyEntry propertyEntryForeign = targetEntity.Property(foreignKey.Properties[i].Name);
                    PropertyEntry propertyEntryPrincipal = entityEntry.Property(foreignKey.PrincipalKey.Properties[i].Name);
                    if(!Equals(propertyEntryForeign.OriginalValue, propertyEntryPrincipal.OriginalValue)) {
                        result = false;
                        break;
                    }
                }
                if(result) {
                    principalEntityEntry = entityEntry;
                }
            }
            return principalEntityEntry;
        }
        public static IEnumerable<ModifyObjectMetada> GetModifyObjectMetada(this ChangeTracker changeTracker) {
            IEnumerable<EntityEntry> entities = changeTracker.Entries().Where(p => p.State == EntityState.Modified);
            return GetModifyObjectMetada(entities, changeTracker);
        }
        public static IEnumerable<ModifyObjectMetada> GetModifyObjectMetadaForAddedObjects(this ChangeTracker changeTracker) {
            IEnumerable<EntityEntry> entities = changeTracker.Entries().Where(p => p.State == EntityState.Added);
            return GetModifyObjectMetada(entities, changeTracker);
        }
        private static IEnumerable<ModifyObjectMetada> GetModifyObjectMetada(IEnumerable<EntityEntry> entitiesEntry, ChangeTracker changeTracker) {
            List<ModifyObjectMetada> modifyObjectsMetada = new List<ModifyObjectMetada>();
            foreach(EntityEntry entityEntry in entitiesEntry) {
                switch(entityEntry.State) {

                    case EntityState.Modified:
                        ProcessModifiedEntity(modifyObjectsMetada, entityEntry, changeTracker);
                        break;
                    case EntityState.Added:
                        ProcessAddedEntity(modifyObjectsMetada, entityEntry, changeTracker);
                        break;
                }

            }
            return modifyObjectsMetada;
        }
        private static void ProcessAddedEntity(List<ModifyObjectMetada> modifyObjectsMetada, EntityEntry entityEntry, ChangeTracker changeTracker) {
            IEnumerable<IForeignKey> foreignKeys = entityEntry.Metadata.GetForeignKeys();
            ModifyObjectMetada modifyObjectMetada = GetOrCreateMetaData(modifyObjectsMetada, entityEntry.Entity);
            IEnumerable<PropertyEntry> properties = entityEntry.GetProperties();
            foreach(IForeignKey foreignKey in foreignKeys) {
                for(int i = 0; i < foreignKey.Properties.Count(); i++) {
                    PropertyEntry propertyEntry = properties.First(p => p.Metadata.Name == foreignKey.Properties[0].Name);
                    if(propertyEntry.CurrentValue != null && propertyEntry.CurrentValue.Equals(null)) {
                        continue;
                    }
                    EntityEntry principaEntityEntryCurrentValue = changeTracker.GetPrincipaEntityEntryCurrentValue(entityEntry, foreignKey);
                    if(principaEntityEntryCurrentValue != null && principaEntityEntryCurrentValue.State != EntityState.Added) {
                        ProcessPrincipalEntity(entityEntry, modifyObjectsMetada, foreignKey, principaEntityEntryCurrentValue);
                    }
                }
            }
        }
        private static void ProcessModifiedEntity(List<ModifyObjectMetada> modifyObjectsMetada, EntityEntry entityEntry, ChangeTracker changeTracker) {
            ModifyObjectMetada modifyObjectMetada = GetOrCreateMetaData(modifyObjectsMetada, entityEntry.Entity);
            IEnumerable<PropertyEntry> properties = entityEntry.GetProperties();
            ProcessProperties(entityEntry.Entity, modifyObjectMetada, properties);
            ProcessNavigations(entityEntry, modifyObjectsMetada, modifyObjectMetada, properties, changeTracker);
        }
        private static void ProcessNavigations(EntityEntry entityEntry, List<ModifyObjectMetada> modifyObjectsMetada, ModifyObjectMetada modifyObjectMetada, IEnumerable<PropertyEntry> properties, ChangeTracker changeTracker) {
            IEnumerable<IForeignKey> foreignKeys = entityEntry.Metadata.GetForeignKeys();
            foreach(IForeignKey foreignKey in foreignKeys) {
                for(int i = 0; i < foreignKey.Properties.Count(); i++) {
                    PropertyEntry propertyEntry = properties.First(p => p.Metadata.Name == foreignKey.Properties[0].Name);
                    if(!propertyEntry.IsModified && entityEntry.State != EntityState.Added) {
                        continue;
                    }
                    modifyObjectMetada.ModifiedForeignKey.Add(propertyEntry.Metadata.Name, propertyEntry.CurrentValue);
                    IEnumerable<INavigation> findNavigationsToForeign = foreignKey.FindNavigationsFrom(entityEntry.Metadata);
                    foreach(INavigation nav in findNavigationsToForeign) {
                        modifyObjectMetada.NavigationProperty.Add(nav.Name);
                    }

                    EntityEntry principaEntityEntryOriginalValue = changeTracker.GetPrincipaEntityEntryOriginalValue(entityEntry, foreignKey);
                    if(principaEntityEntryOriginalValue != null) {
                        ProcessPrincipalEntity(entityEntry, modifyObjectsMetada, foreignKey, principaEntityEntryOriginalValue);
                    }
                    EntityEntry principaEntityEntryCurrentValue = changeTracker.GetPrincipaEntityEntryCurrentValue(entityEntry, foreignKey);
                    if(principaEntityEntryCurrentValue != null) {
                        ProcessPrincipalEntity(entityEntry, modifyObjectsMetada, foreignKey, principaEntityEntryCurrentValue);
                    }
                }
            }
        }
        private static void ProcessPrincipalEntity(EntityEntry entityEntry, List<ModifyObjectMetada> modifyObjectsMetada, IForeignKey foreignKey, EntityEntry principaEntityEntry) {
            ModifyObjectMetada modifyObjectMetadaNavigation = GetOrCreateMetaData(modifyObjectsMetada, principaEntityEntry.Entity);
            IEnumerable<INavigation> findNavigationsTo = foreignKey.FindNavigationsFrom(principaEntityEntry.Metadata);
            foreach(var entityNavigation in findNavigationsTo) {
                if(!modifyObjectMetadaNavigation.NavigationProperty.Contains(entityNavigation.Name)) {
                    modifyObjectMetadaNavigation.NavigationProperty.Add(entityNavigation.Name);

                }
            }
        }
        private static void ProcessProperties(object targetObject, ModifyObjectMetada modifyObjectMetada, IEnumerable<PropertyEntry> properties) {
            foreach(PropertyEntry propertyEntry in properties) {
                if(propertyEntry.IsModified) {
                    if(propertyEntry.Metadata.IsKeyOrForeignKey()) {
                        continue;
                    }
                    modifyObjectMetada.ModifiedProperties.Add(propertyEntry.Metadata.Name, propertyEntry.CurrentValue);
                }
            }
        }
        private static ModifyObjectMetada GetOrCreateMetaData(List<ModifyObjectMetada> ModifyObjectsMetada, object targetObject) {
            ModifyObjectMetada modifyObjectMetada = ModifyObjectsMetada.FirstOrDefault(p => Equals(p.Object, targetObject));
            if(modifyObjectMetada == null) {
                modifyObjectMetada = new ModifyObjectMetada(targetObject);
                ModifyObjectsMetada.Add(modifyObjectMetada);
            }
            return modifyObjectMetada;
        }
    }
}

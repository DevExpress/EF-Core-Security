﻿using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore;

namespace DevExpress.EntityFramework.SecurityDataStore.Security {
    public class TrackPrimaryKeyService {
        private SecurityDbContext securityDbContext;
        private SecurityObjectRepository securityObjectRepository;
        public void ApplyChanges(IEnumerable<EntityEntry> updateEntities) {
            foreach(EntityEntry securityEntityEntry in updateEntities) {
                object realObject =  securityObjectRepository.GetRealObject(securityEntityEntry.Entity);
                EntityEntry realEntityEntry = securityDbContext.realDbContext.ChangeTracker.GetEntity(realObject);
                if(realEntityEntry == null) {
                    continue;
                }
                ApplyChanges(securityEntityEntry, realEntityEntry);
            }
        }

        private void ApplyChanges(EntityEntry securityEntityEntry, EntityEntry realEntityEntry) {
            IEnumerable<PropertyEntry> securityProperties = securityEntityEntry.GetProperties();
            IEnumerable<PropertyEntry> realProperties = realEntityEntry.GetProperties();
            foreach(PropertyEntry propertyEntry in securityProperties) {
                if(!propertyEntry.Metadata.IsKey()) {
                    continue;
                }
                PropertyEntry securityPropertyEntry = propertyEntry;
                PropertyEntry realPropertyEntry = realProperties.First(p=>p.Metadata.Name == securityPropertyEntry.Metadata.Name);
                ApplyChanges(securityPropertyEntry, realPropertyEntry);
            }
        }

        private void ApplyChanges(PropertyEntry securityPropertyEntry, PropertyEntry realPropertyEntry) {
            if(!Equals(securityPropertyEntry.CurrentValue, realPropertyEntry.CurrentValue)) {
                securityPropertyEntry.CurrentValue = realPropertyEntry.CurrentValue;
                securityPropertyEntry.OriginalValue = realPropertyEntry.CurrentValue;
                securityPropertyEntry.IsModified = false;
            }
        }

        public TrackPrimaryKeyService(SecurityDbContext securityDbContext, SecurityObjectRepository securityObjectRepository) {
            this.securityDbContext = securityDbContext;
            this.securityObjectRepository = securityObjectRepository;
        }
    }
}

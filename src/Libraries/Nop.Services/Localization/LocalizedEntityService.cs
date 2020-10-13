﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using LinqToDB;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Localization;
using Nop.Data;


namespace Nop.Services.Localization
{
    /// <summary>
    /// Provides information about localizable entities
    /// </summary>
    public partial class LocalizedEntityService : ILocalizedEntityService
    {
        #region Fields

        private readonly IRepository<LocalizedProperty> _localizedPropertyRepository;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly LocalizationSettings _localizationSettings;

        #endregion

        #region Ctor

        public LocalizedEntityService(IRepository<LocalizedProperty> localizedPropertyRepository,
            IStaticCacheManager staticCacheManager,
            LocalizationSettings localizationSettings)
        {
            _localizedPropertyRepository = localizedPropertyRepository;
            _staticCacheManager = staticCacheManager;
            _localizationSettings = localizationSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets localized properties
        /// </summary>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="localeKeyGroup">Locale key group</param>
        /// <returns>Localized properties</returns>
        protected virtual async Task<IList<LocalizedProperty>> GetLocalizedProperties(int entityId, string localeKeyGroup)
        {
            if (entityId == 0 || string.IsNullOrEmpty(localeKeyGroup))
                return new List<LocalizedProperty>();

            var query = from lp in _localizedPropertyRepository.Table
                        orderby lp.Id
                        where lp.EntityId == entityId &&
                              lp.LocaleKeyGroup == localeKeyGroup
                        select lp;

            var props = await query.ToListAsync();

            return props;
        }

        /// <summary>
        /// Gets all cached localized properties
        /// </summary>
        /// <returns>Cached localized properties</returns>
        protected virtual async Task<IList<LocalizedProperty>> GetAllLocalizedProperties()
        {
            return await _localizedPropertyRepository.GetAll(query =>
            {
                return from lp in query
                    select lp;
            }, cache => default);
        }

        #endregion
        
        #region Methods

        /// <summary>
        /// Deletes a localized property
        /// </summary>
        /// <param name="localizedProperty">Localized property</param>
        public virtual async Task DeleteLocalizedProperty(LocalizedProperty localizedProperty)
        {
            await _localizedPropertyRepository.Delete(localizedProperty);
        }

        /// <summary>
        /// Gets a localized property
        /// </summary>
        /// <param name="localizedPropertyId">Localized property identifier</param>
        /// <returns>Localized property</returns>
        public virtual async Task<LocalizedProperty> GetLocalizedPropertyById(int localizedPropertyId)
        {
            return await _localizedPropertyRepository.GetById(localizedPropertyId);
        }

        /// <summary>
        /// Find localized value
        /// </summary>
        /// <param name="languageId">Language identifier</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="localeKeyGroup">Locale key group</param>
        /// <param name="localeKey">Locale key</param>
        /// <returns>Found localized value</returns>
        public virtual async Task<string> GetLocalizedValue(int languageId, int entityId, string localeKeyGroup, string localeKey)
        {
            var key = _staticCacheManager.PrepareKeyForDefaultCache(NopLocalizationDefaults.LocalizedPropertyCacheKey
                , languageId, entityId, localeKeyGroup, localeKey);

            return await _staticCacheManager.Get(key, async () =>
            {
                var source = _localizationSettings.LoadAllLocalizedPropertiesOnStartup
                    //load all records (we know they are cached)
                    ? (await GetAllLocalizedProperties()).AsQueryable()
                    //gradual loading
                    : _localizedPropertyRepository.Table;

                var query = from lp in source
                    where lp.LanguageId == languageId &&
                          lp.EntityId == entityId &&
                          lp.LocaleKeyGroup == localeKeyGroup &&
                          lp.LocaleKey == localeKey
                    select lp.LocaleValue;

                //little hack here. nulls aren't cacheable so set it to ""
                var localeValue = query.FirstOrDefault() ?? string.Empty;

                return localeValue;
            });
        }

        /// <summary>
        /// Inserts a localized property
        /// </summary>
        /// <param name="localizedProperty">Localized property</param>
        public virtual async Task InsertLocalizedProperty(LocalizedProperty localizedProperty)
        {
            await _localizedPropertyRepository.Insert(localizedProperty);
        }

        /// <summary>
        /// Updates the localized property
        /// </summary>
        /// <param name="localizedProperty">Localized property</param>
        public virtual async Task UpdateLocalizedProperty(LocalizedProperty localizedProperty)
        {
            await _localizedPropertyRepository.Update(localizedProperty);
        }

        /// <summary>
        /// Save localized value
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="entity">Entity</param>
        /// <param name="keySelector">Key selector</param>
        /// <param name="localeValue">Locale value</param>
        /// <param name="languageId">Language ID</param>
        public virtual async Task SaveLocalizedValue<T>(T entity,
            Expression<Func<T, string>> keySelector,
            string localeValue,
            int languageId) where T : BaseEntity, ILocalizedEntity
        {
            await SaveLocalizedValue<T, string>(entity, keySelector, localeValue, languageId);
        }

        /// <summary>
        /// Save localized value
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <typeparam name="TPropType">Property type</typeparam>
        /// <param name="entity">Entity</param>
        /// <param name="keySelector">Key selector</param>
        /// <param name="localeValue">Locale value</param>
        /// <param name="languageId">Language ID</param>
        public virtual async Task SaveLocalizedValue<T, TPropType>(T entity,
            Expression<Func<T, TPropType>> keySelector,
            TPropType localeValue,
            int languageId) where T : BaseEntity, ILocalizedEntity
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (languageId == 0)
                throw new ArgumentOutOfRangeException(nameof(languageId), "Language ID should not be 0");

            if (!(keySelector.Body is MemberExpression member))
            {
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a method, not a property.",
                    keySelector));
            }

            var propInfo = member.Member as PropertyInfo;
            if (propInfo == null)
            {
                throw new ArgumentException(string.Format(
                       "Expression '{0}' refers to a field, not a property.",
                       keySelector));
            }

            //load localized value (check whether it's a cacheable entity. In such cases we load its original entity type)
            var localeKeyGroup = entity.GetType().Name;
            var localeKey = propInfo.Name;

            var props = await GetLocalizedProperties(entity.Id, localeKeyGroup);
            var prop = props.FirstOrDefault(lp => lp.LanguageId == languageId &&
                lp.LocaleKey.Equals(localeKey, StringComparison.InvariantCultureIgnoreCase)); //should be culture invariant

            var localeValueStr = CommonHelper.To<string>(localeValue);

            if (prop != null)
            {
                if (string.IsNullOrWhiteSpace(localeValueStr))
                {
                    //delete
                    await DeleteLocalizedProperty(prop);
                }
                else
                {
                    //update
                    prop.LocaleValue = localeValueStr;
                    await UpdateLocalizedProperty(prop);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(localeValueStr))
                    return;

                //insert
                prop = new LocalizedProperty
                {
                    EntityId = entity.Id,
                    LanguageId = languageId,
                    LocaleKey = localeKey,
                    LocaleKeyGroup = localeKeyGroup,
                    LocaleValue = localeValueStr
                };
                await InsertLocalizedProperty(prop);
            }
        }

        #endregion
    }
}
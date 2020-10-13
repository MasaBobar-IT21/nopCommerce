﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Services.Localization;

namespace Nop.Services.Directory
{
    /// <summary>
    /// Country service
    /// </summary>
    public partial class CountryService : ICountryService
    {
        #region Fields

        private readonly CatalogSettings _catalogSettings;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly ILocalizationService _localizationService;
        private readonly IRepository<Country> _countryRepository;
        private readonly IRepository<StoreMapping> _storeMappingRepository;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Ctor

        public CountryService(CatalogSettings catalogSettings,
            IStaticCacheManager staticCacheManager,
            ILocalizationService localizationService,
            IRepository<Country> countryRepository,
            IRepository<StoreMapping> storeMappingRepository,
            IStoreContext storeContext)
        {
            _catalogSettings = catalogSettings;
            _staticCacheManager = staticCacheManager;
            _localizationService = localizationService;
            _countryRepository = countryRepository;
            _storeMappingRepository = storeMappingRepository;
            _storeContext = storeContext;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Deletes a country
        /// </summary>
        /// <param name="country">Country</param>
        public virtual async Task DeleteCountry(Country country)
        {
            await _countryRepository.Delete(country);
        }

        /// <summary>
        /// Gets all countries
        /// </summary>
        /// <param name="languageId">Language identifier. It's used to sort countries by localized names (if specified); pass 0 to skip it</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Countries</returns>
        public virtual async Task<IList<Country>> GetAllCountries(int languageId = 0, bool showHidden = false)
        {
            var key = _staticCacheManager.PrepareKeyForDefaultCache(NopDirectoryDefaults.CountriesAllCacheKey, languageId,
                showHidden, await _storeContext.GetCurrentStore());

            return await _staticCacheManager.Get(key, async () =>
            {
                var countries = await _countryRepository.GetAll(query =>
                {
                    if (!showHidden)
                        query = query.Where(c => c.Published);

                    if (!showHidden && !_catalogSettings.IgnoreStoreLimitations)
                    {
                        //Store mapping
                        var currentStoreId = _storeContext.GetCurrentStore().Result.Id;
                        query = from c in query
                            join sc in _storeMappingRepository.Table
                                on new {c1 = c.Id, c2 = nameof(Country)} equals new
                                {
                                    c1 = sc.EntityId, c2 = sc.EntityName
                                } into c_sc
                            from sc in c_sc.DefaultIfEmpty()
                            where !c.LimitedToStores || currentStoreId == sc.StoreId
                            select c;

                        query = query.Distinct();
                    }

                    return query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name);
                });

                if (languageId > 0)
                {
                    //we should sort countries by localized names when they have the same display order
                    countries = countries
                        .OrderBy(c => c.DisplayOrder)
                        .ThenBy(c => _localizationService.GetLocalized(c, x => x.Name, languageId).Result)
                        .ToList();
                }

                return countries;
            });
        }

        /// <summary>
        /// Gets all countries that allow billing
        /// </summary>
        /// <param name="languageId">Language identifier. It's used to sort countries by localized names (if specified); pass 0 to skip it</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Countries</returns>
        public virtual async Task<IList<Country>> GetAllCountriesForBilling(int languageId = 0, bool showHidden = false)
        {
            return (await GetAllCountries(languageId, showHidden)).Where(c => c.AllowsBilling).ToList();
        }

        /// <summary>
        /// Gets all countries that allow shipping
        /// </summary>
        /// <param name="languageId">Language identifier. It's used to sort countries by localized names (if specified); pass 0 to skip it</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Countries</returns>
        public virtual async Task<IList<Country>> GetAllCountriesForShipping(int languageId = 0, bool showHidden = false)
        {
            return (await GetAllCountries(languageId, showHidden)).Where(c => c.AllowsShipping).ToList();
        }

        /// <summary>
        /// Gets a country by address 
        /// </summary>
        /// <param name="address">Address</param>
        /// <returns>Country</returns>
        public virtual async Task<Country> GetCountryByAddress(Address address)
        {
            return await GetCountryById(address?.CountryId ?? 0);
        }

        /// <summary>
        /// Gets a country 
        /// </summary>
        /// <param name="countryId">Country identifier</param>
        /// <returns>Country</returns>
        public virtual async Task<Country> GetCountryById(int countryId)
        {
            return await _countryRepository.GetById(countryId, cache => default);
        }

        /// <summary>
        /// Get countries by identifiers
        /// </summary>
        /// <param name="countryIds">Country identifiers</param>
        /// <returns>Countries</returns>
        public virtual async Task<IList<Country>> GetCountriesByIds(int[] countryIds)
        {
            return await _countryRepository.GetByIds(countryIds);
        }

        /// <summary>
        /// Gets a country by two letter ISO code
        /// </summary>
        /// <param name="twoLetterIsoCode">Country two letter ISO code</param>
        /// <returns>Country</returns>
        public virtual async Task<Country> GetCountryByTwoLetterIsoCode(string twoLetterIsoCode)
        {
            if (string.IsNullOrEmpty(twoLetterIsoCode))
                return null;

            var key = _staticCacheManager.PrepareKeyForDefaultCache(NopDirectoryDefaults.CountriesByTwoLetterCodeCacheKey, twoLetterIsoCode);

            var query = from c in _countryRepository.Table
                where c.TwoLetterIsoCode == twoLetterIsoCode
                select c;

            return await _staticCacheManager.Get(key, async () => await query.ToAsyncEnumerable().FirstOrDefaultAsync());
        }

        /// <summary>
        /// Gets a country by three letter ISO code
        /// </summary>
        /// <param name="threeLetterIsoCode">Country three letter ISO code</param>
        /// <returns>Country</returns>
        public virtual async Task<Country> GetCountryByThreeLetterIsoCode(string threeLetterIsoCode)
        {
            if (string.IsNullOrEmpty(threeLetterIsoCode))
                return null;

            var key = _staticCacheManager.PrepareKeyForDefaultCache(NopDirectoryDefaults.CountriesByThreeLetterCodeCacheKey, threeLetterIsoCode);

            var query = from c in _countryRepository.Table
                where c.ThreeLetterIsoCode == threeLetterIsoCode
                select c;

            return await _staticCacheManager.Get(key, async () => await query.ToAsyncEnumerable().FirstOrDefaultAsync());
        }

        /// <summary>
        /// Inserts a country
        /// </summary>
        /// <param name="country">Country</param>
        public virtual async Task InsertCountry(Country country)
        {
            await _countryRepository.Insert(country);
        }

        /// <summary>
        /// Updates the country
        /// </summary>
        /// <param name="country">Country</param>
        public virtual async Task UpdateCountry(Country country)
        {
            await _countryRepository.Update(country);
        }

        #endregion
    }
}
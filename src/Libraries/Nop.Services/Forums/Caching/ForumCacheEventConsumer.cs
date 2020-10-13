﻿using System.Threading.Tasks;
using Nop.Core.Domain.Forums;
using Nop.Services.Caching;

namespace Nop.Services.Forums.Caching
{
    /// <summary>
    /// Represents a forum cache event consumer
    /// </summary>
    public partial class ForumCacheEventConsumer : CacheEventConsumer<Forum>
    {
        /// <summary>
        /// Clear cache data
        /// </summary>
        /// <param name="entity">Entity</param>
        protected override async Task ClearCache(Forum entity)
        {
            await Remove(NopForumDefaults.ForumByForumGroupCacheKey, entity.ForumGroupId);
        }
    }
}

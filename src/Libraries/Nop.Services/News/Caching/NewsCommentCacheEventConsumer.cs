﻿using Nop.Core.Domain.News;
using Nop.Services.Caching;
using System.Threading.Tasks;

namespace Nop.Services.News.Caching
{
    /// <summary>
    /// Represents a news comment cache event consumer
    /// </summary>
    public partial class NewsCommentCacheEventConsumer : CacheEventConsumer<NewsComment>
    {
        /// <summary>
        /// entity
        /// </summary>
        /// <param name="entity">Entity</param>
        /// <param name="entityEventType">Entity event type</param>
        protected override async Task ClearCache(NewsComment entity, EntityEventType entityEventType)
        {
            if (entityEventType == EntityEventType.Delete)
                await RemoveByPrefix(NopNewsDefaults.NewsCommentsNumberPrefix, entity.NewsItemId);

            await base.ClearCache(entity, entityEventType);
        }
    }
}